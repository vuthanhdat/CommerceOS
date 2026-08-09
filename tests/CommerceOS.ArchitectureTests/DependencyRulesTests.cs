using System.Xml.Linq;
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
    public void ApplicationProjectsReferenceOnlyTheirOwnDomainProject()
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
            var projectName = Path.GetFileNameWithoutExtension(project);
            var expectedDomainProject = $"{projectName.Replace(".Application", ".Domain", StringComparison.Ordinal)}.csproj";
            var projectReferences = document
                .Descendants()
                .Where(node => node.Name.LocalName == "ProjectReference")
                .Select(node => node.Attribute("Include")?.Value ?? string.Empty)
                .ToArray();

            Assert.NotEmpty(projectReferences);
            Assert.All(
                projectReferences,
                reference => Assert.Equal(expectedDomainProject, Path.GetFileName(reference)));
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

    private static string FormatFailures(TestResult result) => result.FailingTypeNames is { Count: > 0 }
        ? $"Forbidden dependencies found in: {string.Join(", ", result.FailingTypeNames)}"
        : "Forbidden dependency rule failed.";
}
