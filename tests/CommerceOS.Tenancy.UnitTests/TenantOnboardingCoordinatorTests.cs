using CommerceOS.SubscriptionBilling.Contracts;
using CommerceOS.Tenancy.Application.Onboarding;
using CommerceOS.Tenancy.Domain;

namespace CommerceOS.Tenancy.UnitTests;

public sealed class TenantOnboardingCoordinatorTests
{
    [Fact]
    public async Task EquivalentRetryCreatesOneTenantOwnerAndTrialThenReturnsTheSameCompletedOperation()
    {
        var store = new InMemoryOnboardingStore();
        var trial = new RecordingTrialStarter();
        var coordinator = new TenantOnboardingCoordinator(store, trial);
        var context = TrustedOnboardingContext.FromVerifiedIdentity(new SubjectId("subject-a"), "owner@example.test");

        var first = await coordinator.RegisterAsync(context, "registration-1", Profile(), "corr-1", CancellationToken.None);
        var replay = await coordinator.RegisterAsync(context, "registration-1", Profile(), "corr-2", CancellationToken.None);

        Assert.Equal(MerchantOnboardingOutcome.Completed, first.Outcome);
        Assert.Equal(first.OperationId, replay.OperationId);
        Assert.Equal(first.TenantId, replay.TenantId);
        Assert.Equal(1, store.CreatedOperations);
        Assert.Equal(1, trial.InvocationCount);
        Assert.Single(trial.AcceptedSources);
    }

    [Fact]
    public async Task IncompatibleIdempotencyReuseConflictsWithoutSecondTenantOrTrial()
    {
        var store = new InMemoryOnboardingStore();
        var trial = new RecordingTrialStarter();
        var coordinator = new TenantOnboardingCoordinator(store, trial);
        var context = TrustedOnboardingContext.FromVerifiedIdentity(new SubjectId("subject-a"), "owner@example.test");

        await coordinator.RegisterAsync(context, "registration-1", Profile(), "corr-1", CancellationToken.None);
        var conflict = await coordinator.RegisterAsync(
            context,
            "registration-1",
            new BusinessProfile("Different merchant", "Asia/Bangkok"),
            "corr-2",
            CancellationToken.None);

        Assert.Equal(MerchantOnboardingOutcome.Conflict, conflict.Outcome);
        Assert.Equal(1, store.CreatedOperations);
        Assert.Single(trial.AcceptedSources);
    }

    [Fact]
    public async Task InterruptedTrialCallReturnsDurablePendingOperationWithoutRollback()
    {
        var store = new InMemoryOnboardingStore();
        var trial = new RecordingTrialStarter { Throws = true };
        var coordinator = new TenantOnboardingCoordinator(store, trial);
        var context = TrustedOnboardingContext.FromVerifiedIdentity(new SubjectId("subject-a"), "owner@example.test");

        var pending = await coordinator.RegisterAsync(context, "registration-1", Profile(), "corr-1", CancellationToken.None);
        var status = await coordinator.GetStatusAsync(context, "registration-1", CancellationToken.None);

        Assert.Equal(MerchantOnboardingOutcome.PendingTrial, pending.Outcome);
        Assert.Equal(pending, status);
        Assert.Single(store.WorkItems);
        Assert.Equal(OnboardingStatus.PendingTrial, store.Operation!.Status);
        Assert.Equal(TenantStatus.Active, store.Operation.Tenant.Status);
        Assert.Equal(MerchantRole.Owner, store.Operation.InitialOwner.Role);
    }

    [Fact]
    public async Task RecoveryUsingTheSameWorkSourceCompletesWithoutCreatingAnotherTrial()
    {
        var store = new InMemoryOnboardingStore();
        var trial = new RecordingTrialStarter { Throws = true };
        var coordinator = new TenantOnboardingCoordinator(store, trial);
        var context = TrustedOnboardingContext.FromVerifiedIdentity(new SubjectId("subject-a"), "owner@example.test");
        var pending = await coordinator.RegisterAsync(context, "registration-1", Profile(), "corr-1", CancellationToken.None);
        trial.Throws = false;

        var recovered = await coordinator.RegisterAsync(context, "registration-1", Profile(), "corr-3", CancellationToken.None);

        Assert.Equal(MerchantOnboardingOutcome.Completed, recovered.Outcome);
        Assert.Equal(pending.OperationId, recovered.OperationId);
        Assert.Equal(1, store.CreatedOperations);
        Assert.Single(trial.AcceptedSources);
    }

    [Fact]
    public async Task DuplicateRecoveryDeliveryCompletesOnlyOnce()
    {
        var store = new InMemoryOnboardingStore();
        var trial = new RecordingTrialStarter { Throws = true };
        var coordinator = new TenantOnboardingCoordinator(store, trial);
        var context = TrustedOnboardingContext.FromVerifiedIdentity(new SubjectId("subject-a"), "owner@example.test");
        await coordinator.RegisterAsync(context, "registration-1", Profile(), "corr-1", CancellationToken.None);
        trial.Throws = false;
        var worker = new OnboardingTrialRecoveryWorker(store, trial);

        var first = await worker.ProcessAsync(store.WorkItems.Single(), CancellationToken.None);
        var duplicate = await worker.ProcessAsync(store.WorkItems.Single(), CancellationToken.None);

        Assert.True(first);
        Assert.False(duplicate);
        Assert.Single(trial.AcceptedSources);
    }

    private static BusinessProfile Profile() => new("Merchant one", "Asia/Bangkok");

    private sealed class RecordingTrialStarter : ITrialSubscriptionStarter
    {
        public bool Throws { get; set; }
        public int InvocationCount { get; private set; }
        public HashSet<string> AcceptedSources { get; } = [];

        public Task<TrialSubscriptionStartResult> StartTrialSubscriptionAsync(
            StartTrialSubscriptionCommand command,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            if (Throws)
            {
                throw new TimeoutException();
            }
            return Task.FromResult(new TrialSubscriptionStartResult(
                AcceptedSources.Add(command.SourceIdentity)
                    ? TrialSubscriptionStartOutcome.Accepted
                    : TrialSubscriptionStartOutcome.AlreadyApplied,
                "trial-v1"));
        }
    }

    private sealed class InMemoryOnboardingStore : ITenantOnboardingStore
    {
        private readonly Dictionary<(string SubjectId, string Key), OnboardingOperation> _operations = [];
        public int CreatedOperations { get; private set; }
        public OnboardingOperation? Operation => _operations.Values.SingleOrDefault();
        public List<TrialBootstrapWorkItem> WorkItems { get; } = [];

        public Task<LocalOnboardingRegistrationResult> RegisterAsync(
            OnboardingOperation operation,
            TrialBootstrapWorkItem workItem,
            CancellationToken cancellationToken)
        {
            var key = (operation.SubjectId.Value, operation.IdempotencyKey);
            if (_operations.TryGetValue(key, out var existing))
            {
                return Task.FromResult(existing.RequestFingerprint == operation.RequestFingerprint
                    ? new LocalOnboardingRegistrationResult(LocalOnboardingRegistrationOutcome.Replayed, existing, workItem)
                    : new LocalOnboardingRegistrationResult(LocalOnboardingRegistrationOutcome.Conflict, null, null));
            }
            _operations.Add(key, operation);
            WorkItems.Add(workItem);
            CreatedOperations++;
            return Task.FromResult(new LocalOnboardingRegistrationResult(LocalOnboardingRegistrationOutcome.Created, operation, workItem));
        }

        public Task<OnboardingOperation?> GetAsync(TrustedOnboardingContext context, string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(_operations.GetValueOrDefault((context.SubjectId.Value, idempotencyKey)));

        public Task<OnboardingOperation?> GetByOperationIdAsync(string operationId, CancellationToken cancellationToken) =>
            Task.FromResult(_operations.Values.SingleOrDefault(operation => operation.Id == operationId));

        public Task<bool> MarkCompletedAsync(string operationId, CancellationToken cancellationToken)
        {
            var pair = _operations.Single(item => item.Value.Id == operationId);
            _operations[pair.Key] = pair.Value with { Status = OnboardingStatus.Completed };
            return Task.FromResult(true);
        }
    }
}
