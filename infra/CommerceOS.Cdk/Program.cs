using Amazon.CDK;
using CommerceOS.Cdk;

var app = new App();
var environmentName = app.Node.TryGetContext("environment")?.ToString() ?? "dev";
var instanceId = app.Node.TryGetContext("instance")?.ToString()
    ?? System.Environment.GetEnvironmentVariable("COMMERCEOS_INSTANCE")
    ?? "0000";
var profile = EnvironmentProfile.Create(environmentName, instanceId);

_ = new FoundationStack(
    app,
    $"{profile.ResourcePrefix}-foundation",
    profile,
    new StackProps
    {
        Env = new Amazon.CDK.Environment
        {
            Account = profile.AccountId,
            Region = profile.Region
        }
    });

app.Synth();

