using System.Xml.Linq;
using CommerceOS.Platform.Application.Readiness;
using CommerceOS.Platform.Domain;
using NetArchTest.Rules;

namespace CommerceOS.ArchitectureTests;

public sealed class DependencyRulesTests
{
    private static readonly string[] ForbiddenDomainDependencies =
    [
        "Amazon",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions",
        "System.Net.Http"
    ];

    private static readonly string[] ForbiddenApplicationDependencies =
    [
        "Amazon",
        "LocalStack",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Configuration",
        "Microsoft.Extensions.Options",
        "DynamoDB",
        "EntityFramework",
        "Dapper"
    ];

    [Fact]
    public void DomainTypesDoNotDependOnFrameworkOrAwsNamespaces()
    {
        var result = Types
            .InAssembly(typeof(PlatformDomainAssembly).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDomainDependencies)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void DomainProjectsHaveNoPackageOrProjectReferences()
    {
        var root = FindRepositoryRoot();
        var domainProjects = Directory.GetFiles(
            Path.Combine(root, "src", "Modules"),
            "*.Domain.csproj",
            SearchOption.AllDirectories);

        Assert.NotEmpty(domainProjects);

        foreach (var project in domainProjects)
        {
            var document = XDocument.Load(project);
            var references = document
                .Descendants()
                .Where(node => node.Name.LocalName is "PackageReference" or "ProjectReference")
                .Select(node => node.Attribute("Include")?.Value)
                .Where(value => value is not null)
                .ToArray();

            Assert.True(references.Length == 0, $"Domain project {project} has forbidden references: {string.Join(", ", references)}");
        }
    }

    [Fact]
    public void ApplicationTypesDoNotDependOnInfrastructureOrEndpointConfigurationNamespaces()
    {
        var result = Types
            .InAssembly(typeof(IPlatformReadiness).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenApplicationDependencies)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void ApplicationProjectsReferenceTheirOwnDomainAndApprovedContractsOnly()
    {
        var root = FindRepositoryRoot();
        var applicationProjects = Directory.GetFiles(
            Path.Combine(root, "src", "Modules"),
            "*.Application.csproj",
            SearchOption.AllDirectories);

        Assert.NotEmpty(applicationProjects);

        foreach (var project in applicationProjects)
        {
            var document = XDocument.Load(project);
            var violations = ValidateApplicationProject(root, project, document);

            Assert.Empty(violations);
        }
    }

    [Fact]
    public void ApplicationReferencePolicyAllowsOwnDomainAndProducerOwnedContracts()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "src", "Modules", "Consumer", "CommerceOS.Consumer.Application.csproj");
        var document = XDocument.Parse(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="../CommerceOS.Consumer.Domain/CommerceOS.Consumer.Domain.csproj" />
                <ProjectReference Include="../Producer/CommerceOS.Producer.Contracts/CommerceOS.Producer.Contracts.csproj" />
              </ItemGroup>
            </Project>
            """);

        var violations = ValidateApplicationProject(root, project, document);

        Assert.Empty(violations);
    }

    [Fact]
    public void ApplicationReferencePolicyRejectsForeignImplementationDependencies()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "src", "Modules", "Consumer", "CommerceOS.Consumer.Application.csproj");
        var document = XDocument.Parse(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="../CommerceOS.Consumer.Domain/CommerceOS.Consumer.Domain.csproj" />
                <ProjectReference Include="../Producer/CommerceOS.Producer.Domain/CommerceOS.Producer.Domain.csproj" />
                <ProjectReference Include="../Producer/CommerceOS.Producer.Application/CommerceOS.Producer.Application.csproj" />
                <ProjectReference Include="../Producer/CommerceOS.Producer.Infrastructure/CommerceOS.Producer.Infrastructure.csproj" />
              </ItemGroup>
            </Project>
            """);

        var violations = ValidateApplicationProject(root, project, document);

        Assert.Equal(3, violations.Count);
        Assert.All(violations, violation => Assert.Contains("may reference only its own Domain or an approved producer-owned Contracts project", violation));
    }

    [Fact]
    public void ApplicationReferencePolicyRejectsInfrastructureAndEndpointConfigurationPackages()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "src", "Modules", "Consumer", "CommerceOS.Consumer.Application.csproj");
        var document = XDocument.Parse(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="../CommerceOS.Consumer.Domain/CommerceOS.Consumer.Domain.csproj" />
                <PackageReference Include="AWSSDK.DynamoDBv2" />
                <PackageReference Include="LocalStack.Client" />
                <PackageReference Include="Microsoft.Extensions.Configuration" />
              </ItemGroup>
            </Project>
            """);

        var violations = ValidateApplicationProject(root, project, document);

        Assert.Equal(3, violations.Count);
        Assert.All(violations, violation => Assert.Contains("must not reference infrastructure or endpoint configuration package", violation));
    }

    [Fact]
    public void ProducerOwnedContractsAreRegistered()
    {
        var root = FindRepositoryRoot();
        var contractsProjects = FindContractsProjects(root);

        Assert.Contains(contractsProjects, contract => contract.EndsWith("CommerceOS.SubscriptionBilling.Contracts.csproj", StringComparison.Ordinal));
        Assert.Contains(contractsProjects, contract => contract.EndsWith("CommerceOS.Catalog.Contracts.csproj", StringComparison.Ordinal));
        Assert.Contains(contractsProjects, contract => contract.EndsWith("CommerceOS.FilesMedia.Contracts.csproj", StringComparison.Ordinal));
    }

    [Fact]
    public void ContractsProjectPolicyRejectsProjectAndForbiddenPackageReferences()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, "src", "Modules", "Producer", "CommerceOS.Producer.Contracts.csproj");
        var document = XDocument.Parse(
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="../CommerceOS.Producer.Domain/CommerceOS.Producer.Domain.csproj" />
                <PackageReference Include="AWSSDK.DynamoDBv2" />
                <PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" />
              </ItemGroup>
            </Project>
            """);

        var violations = ValidateContractsProject(project, document);

        Assert.Equal(3, violations.Count);
        Assert.Contains(violations, violation => violation.Contains("ProjectReference", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("AWSSDK.DynamoDBv2", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.Contains("Microsoft.AspNetCore.Http.Abstractions", StringComparison.Ordinal));
    }

    [Fact]
    public void ContractsProjectsHaveOnlyTransportNeutralDependencies()
    {
        var root = FindRepositoryRoot();

        foreach (var project in FindContractsProjects(root))
        {
            var violations = ValidateContractsProject(project, XDocument.Load(project));

            Assert.Empty(violations);
        }
    }

    [Fact]
    public void InfrastructureProjectsReferenceOnlyTheirOwnApplicationProject()
    {
        var root = FindRepositoryRoot();
        var infrastructureProjects = Directory.GetFiles(
            Path.Combine(root, "src", "Modules"),
            "*.Infrastructure.csproj",
            SearchOption.AllDirectories);

        Assert.NotEmpty(infrastructureProjects);

        foreach (var project in infrastructureProjects)
        {
            var document = XDocument.Load(project);
            var projectName = Path.GetFileNameWithoutExtension(project);
            var expectedApplicationProject = $"{projectName.Replace(".Infrastructure", ".Application", StringComparison.Ordinal)}.csproj";
            var projectReferences = document
                .Descendants()
                .Where(node => node.Name.LocalName == "ProjectReference")
                .Select(node => node.Attribute("Include")?.Value ?? string.Empty)
                .ToArray();

            Assert.NotEmpty(projectReferences);
            Assert.All(
                projectReferences,
                reference => Assert.Equal(expectedApplicationProject, Path.GetFileName(reference)));
        }
    }

    [Fact]
    public void InfrastructureAdaptersDoNotHardCodeLocalStackEndpointsOrCredentials()
    {
        var root = FindRepositoryRoot();
        var sources = Directory.GetFiles(Path.Combine(root, "src", "Modules"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains(".Infrastructure", StringComparison.OrdinalIgnoreCase));
        var forbidden = new[] { "http://localhost", "localhost:4566", "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY" };

        foreach (var source in sources)
        {
            var text = File.ReadAllText(source);
            Assert.DoesNotContain(forbidden, marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CommerceOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the CommerceOS repository root.");
    }

    private static List<string> ValidateApplicationProject(string root, string project, XDocument document)
    {
        var projectName = Path.GetFileNameWithoutExtension(project);
        var expectedDomainProject = $"{projectName.Replace(".Application", ".Domain", StringComparison.Ordinal)}.csproj";
        var references = ProjectReferences(document);
        var violations = new List<string>();

        if (!references.Any(reference => string.Equals(Path.GetFileName(reference), expectedDomainProject, StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add($"Application project {project} must reference its own Domain project {expectedDomainProject}.");
        }

        foreach (var reference in references)
        {
            var fileName = Path.GetFileName(reference);
            var isOwnDomain = string.Equals(fileName, expectedDomainProject, StringComparison.OrdinalIgnoreCase);
            var isApprovedContracts = IsApprovedContractsReference(root, project, reference);

            if (!isOwnDomain && !isApprovedContracts)
            {
                violations.Add(
                    $"Application project {project} may reference only its own Domain or an approved producer-owned Contracts project; forbidden reference: {reference}.");
            }
        }

        foreach (var package in PackageReferences(document))
        {
            if (IsForbiddenApplicationPackage(package))
            {
                violations.Add(
                    $"Application project {project} must not reference infrastructure or endpoint configuration package {package}.");
            }
        }

        return violations;
    }

    private static List<string> ValidateContractsProject(string project, XDocument document)
    {
        var violations = new List<string>();

        foreach (var reference in ProjectReferences(document))
        {
            violations.Add($"Contracts project {project} must not contain a ProjectReference to another project: {reference}.");
        }

        foreach (var package in document
                     .Descendants()
                     .Where(node => node.Name.LocalName == "PackageReference")
                     .Select(node => node.Attribute("Include")?.Value)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Cast<string>())
        {
            if (IsForbiddenContractsPackage(package))
            {
                violations.Add($"Contracts project {project} must not reference framework, AWS, or persistence package {package}.");
            }
        }

        return violations;
    }

    private static string[] ProjectReferences(XDocument document) => document
        .Descendants()
        .Where(node => node.Name.LocalName == "ProjectReference")
        .Select(node => node.Attribute("Include")?.Value ?? string.Empty)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToArray();

    private static string[] PackageReferences(XDocument document) => document
        .Descendants()
        .Where(node => node.Name.LocalName == "PackageReference")
        .Select(node => node.Attribute("Include")?.Value ?? string.Empty)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToArray();

    private static string[] FindContractsProjects(string root) => Directory.GetFiles(
        Path.Combine(root, "src", "Modules"),
        "*.Contracts.csproj",
        SearchOption.AllDirectories);

    private static bool IsApprovedContractsReference(string root, string consumingProject, string reference)
    {
        if (Path.IsPathRooted(reference)
            || !Path.GetFileName(reference).EndsWith(".Contracts.csproj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(consumingProject)!, reference));
        var modulesRoot = Path.GetFullPath(Path.Combine(root, "src", "Modules")) + Path.DirectorySeparatorChar;

        return resolved.StartsWith(modulesRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenContractsPackage(string package) =>
        package.Contains("Amazon", StringComparison.OrdinalIgnoreCase)
        || package.Contains("AWSSDK", StringComparison.OrdinalIgnoreCase)
        || package.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase)
        || package.Contains("Microsoft.Extensions", StringComparison.OrdinalIgnoreCase)
        || package.Contains("Dynamo", StringComparison.OrdinalIgnoreCase)
        || package.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)
        || package.Contains("Dapper", StringComparison.OrdinalIgnoreCase)
        || package.Contains("Http", StringComparison.OrdinalIgnoreCase);

    private static bool IsForbiddenApplicationPackage(string package) =>
        package.Contains("Amazon", StringComparison.OrdinalIgnoreCase)
        || package.Contains("LocalStack", StringComparison.OrdinalIgnoreCase)
        || package.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase)
        || package.Contains("Dynamo", StringComparison.OrdinalIgnoreCase)
        || package.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)
        || package.Contains("Dapper", StringComparison.OrdinalIgnoreCase)
        || package.Contains("Microsoft.Extensions.Configuration", StringComparison.OrdinalIgnoreCase)
        || package.Contains("Microsoft.Extensions.Options", StringComparison.OrdinalIgnoreCase);

    private static string FormatFailures(TestResult result) => result.FailingTypeNames is { Count: > 0 }
        ? $"Forbidden dependencies found in: {string.Join(", ", result.FailingTypeNames)}"
        : "Forbidden dependency rule failed.";
}
