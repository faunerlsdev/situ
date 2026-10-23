using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NetArchTest.Rules;
using Situ.Api.Endpoints;
using Situ.Application;
using Situ.Domain.Common;
using Situ.Infrastructure.Persistence;

namespace Situ.Architecture.Tests;

public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Entity).Assembly;
    private static readonly Assembly Application = typeof(DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(IEndpoint).Assembly;

    [Fact]
    public void Domain_has_no_outward_dependencies()
    {
        var references = Domain.GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .Where(name => !IsRuntimeAssembly(name))
            .ToArray();

        Assert.Empty(references);

        AssertSuccess(
            Types.InAssembly(Domain)
                .Should()
                .OnlyHaveDependenciesOn("System", "Situ.Domain")
                .GetResult());
    }

    [Fact]
    public void Application_depends_only_on_domain()
    {
        Assert.Contains(Application.GetReferencedAssemblies(), assembly => assembly.Name == "Situ.Domain");
        Assert.DoesNotContain(Application.GetReferencedAssemblies(), assembly => assembly.Name is "Situ.Infrastructure" or "Situ.Api");

        AssertSuccess(
            Types.InAssembly(Application)
                .Should()
                .NotHaveDependencyOnAny("Situ.Infrastructure", "Situ.Api")
                .GetResult());
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        Assert.DoesNotContain(Infrastructure.GetReferencedAssemblies(), assembly => assembly.Name == "Situ.Api");

        AssertSuccess(
            Types.InAssembly(Infrastructure)
                .Should()
                .NotHaveDependencyOn("Situ.Api")
                .GetResult());
    }

    [Fact]
    public void Api_does_not_use_domain_types_except_enums_and_ids()
    {
        var domainUsers = Types.InAssembly(Api)
            .That()
            .HaveDependencyOn("Situ.Domain")
            .GetTypes()
            .Select(type => type.FullName)
            .ToArray();

        var illegal = DomainTypeReferences(Api)
            .Where(reference => reference.Type is not { IsEnum: true } && !reference.Name.EndsWith("Id", StringComparison.Ordinal))
            .Select(reference => reference.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(illegal);
        Assert.All(domainUsers, name => Assert.False(string.IsNullOrWhiteSpace(name)));
    }

    private static IEnumerable<(string Name, Type? Type)> DomainTypeReferences(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        foreach (var handle in reader.TypeReferences)
        {
            var typeRef = reader.GetTypeReference(handle);
            if (!ResolvesToAssembly(reader, typeRef.ResolutionScope, "Situ.Domain"))
            {
                continue;
            }

            var typeNamespace = reader.GetString(typeRef.Namespace);
            var typeName = reader.GetString(typeRef.Name).Replace('/', '+');
            var fullName = string.IsNullOrEmpty(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
            yield return (fullName, Domain.GetType(fullName, throwOnError: false));
        }
    }

    private static bool ResolvesToAssembly(MetadataReader reader, EntityHandle scope, string assemblyName)
    {
        switch (scope.Kind)
        {
            case HandleKind.AssemblyReference:
                var reference = reader.GetAssemblyReference((AssemblyReferenceHandle)scope);
                return reader.GetString(reference.Name) == assemblyName;
            case HandleKind.TypeReference:
                var parent = reader.GetTypeReference((TypeReferenceHandle)scope);
                return ResolvesToAssembly(reader, parent.ResolutionScope, assemblyName);
            default:
                return false;
        }
    }

    private static bool IsRuntimeAssembly(string name) =>
        name is "System" or "netstandard" or "mscorlib"
        || name.StartsWith("System.", StringComparison.Ordinal)
        || name.StartsWith("Microsoft.CSharp", StringComparison.Ordinal);

    private static void AssertSuccess(TestResult result)
    {
        var failing = result.FailingTypeNames ?? [];
        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, failing));
    }
}
