using Amazon.CDK;
using CommerceOS.Cdk;

var app = new App();
var environmentName = app.Node.TryGetContext("environment")?.ToString() ?? "dev";
var profile = EnvironmentProfile.Create(environmentName);

_ = new FoundationStack(
    app,
    $"commerceos-{profile.Name}-foundation",
    profile,
    new StackProps());

app.Synth();

