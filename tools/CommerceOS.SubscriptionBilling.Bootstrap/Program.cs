using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CommerceOS.SubscriptionBilling.Application.Catalog;
using CommerceOS.SubscriptionBilling.Infrastructure.Persistence;

var endpoint = Environment.GetEnvironmentVariable("COMMERCEOS_LOCALSTACK_ENDPOINT");
var tableName = Environment.GetEnvironmentVariable("COMMERCEOS_SUBSCRIPTION_BILLING_TABLE");
if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(tableName))
{
    Console.Error.WriteLine("SubscriptionBilling bootstrap requires LocalStack endpoint and table configuration.");
    return 1;
}

using var client = new AmazonDynamoDBClient(
    new BasicAWSCredentials("test", "test"),
    new AmazonDynamoDBConfig { ServiceURL = endpoint, AuthenticationRegion = "us-east-1" });
using var seedStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "initial-catalog.v1.json"));
var seed = CatalogSeedLoader.Load(seedStream);
var result = await new CatalogBootstrapService(
    new DynamoDbSubscriptionCatalogStore(client, new DynamoDbSubscriptionBillingOptions(tableName)))
    .BootstrapAsync(seed, CancellationToken.None);

Console.WriteLine(string.Join(
    ",",
    result.Entries.Select(entry => $"{entry.RecordId.Value}:{entry.Outcome}")));
return result.Succeeded ? 0 : 1;
