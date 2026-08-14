using CommerceOS.SubscriptionBilling.Application.PlatformCharges;
using CommerceOS.SubscriptionBilling.Domain;

namespace CommerceOS.SubscriptionBilling.Infrastructure.Provider;

public enum SimulatedSaasBillingScenario
{
    VerifiedSuccess,
    DefinitiveNoCommit,
    TimeoutAfterCommit,
    NetworkFailure,
    MissingCallback
}

/// <summary>
/// Deterministic external-like SaaS billing simulation. Its scenario state contains no payment instrument data
/// and is intentionally separate from the merchant-order Payments mock provider.
/// </summary>
public sealed class DeterministicSaasBillingProvider : IPlatformBillingProvider
{
    private readonly DeterministicSaasBillingProviderState _state;
    private readonly TimeProvider _clock;

    public DeterministicSaasBillingProvider(DeterministicSaasBillingProviderState state, TimeProvider? clock = null)
    {
        _state = state;
        _clock = clock ?? TimeProvider.System;
    }

    public Task<PlatformChargeEvidence?> SubmitAsync(PlatformBillingRequest request, CancellationToken cancellationToken)
    {
        var scenario = _state.GetScenario(request.IdempotencyKey);
        var evidence = scenario switch
        {
            SimulatedSaasBillingScenario.VerifiedSuccess or SimulatedSaasBillingScenario.TimeoutAfterCommit => Evidence(request, PlatformChargeEvidenceKind.VerifiedSuccess),
            SimulatedSaasBillingScenario.DefinitiveNoCommit => Evidence(request, PlatformChargeEvidenceKind.DefinitiveNoCommit),
            _ => null
        };
        if (evidence is not null)
        {
            _state.RecordEvidence(evidence);
        }

        if (scenario is SimulatedSaasBillingScenario.TimeoutAfterCommit or SimulatedSaasBillingScenario.NetworkFailure)
        {
            throw new TimeoutException("The simulated SaaS billing provider did not return a final response.");
        }

        return Task.FromResult(evidence);
    }

    public Task<PlatformChargeEvidence?> FindEvidenceAsync(string providerOperationId, CancellationToken cancellationToken) =>
        Task.FromResult(_state.FindEvidence(providerOperationId));

    private PlatformChargeEvidence Evidence(PlatformBillingRequest request, PlatformChargeEvidenceKind kind) => new(
        $"provider-evidence:{request.ProviderOperationId}",
        request.ChargeId,
        request.ProviderOperationId,
        kind,
        _clock.GetUtcNow());
}

public sealed class DeterministicSaasBillingProviderState
{
    private readonly Dictionary<string, SimulatedSaasBillingScenario> _scenarios = [];
    private readonly Dictionary<string, PlatformChargeEvidence> _evidence = [];

    public void Configure(string idempotencyKey, SimulatedSaasBillingScenario scenario) => _scenarios[idempotencyKey] = scenario;

    public SimulatedSaasBillingScenario GetScenario(string idempotencyKey) =>
        _scenarios.GetValueOrDefault(idempotencyKey, SimulatedSaasBillingScenario.VerifiedSuccess);

    public void RecordEvidence(PlatformChargeEvidence evidence) => _evidence.TryAdd(evidence.ProviderOperationId, evidence);

    public PlatformChargeEvidence? FindEvidence(string providerOperationId) => _evidence.GetValueOrDefault(providerOperationId);
}
