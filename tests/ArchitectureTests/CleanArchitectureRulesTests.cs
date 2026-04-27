using System.Reflection;
using FluentAssertions;
using MassTransit;
using NetArchTest.Rules;
using Xunit;

namespace PlatformWallet.ArchitectureTests;

/// <summary>
/// Encodes every hard rule from CLAUDE.md §2 as executable tests.
/// A failure here blocks CI — no exceptions beyond the documented SagaOrchestrator carve-out.
/// </summary>
[Trait("Category", "Architecture")]
public class CleanArchitectureRulesTests
{
    // ── Assembly lists ────────────────────────────────────────────────────────

    // Domain assemblies that must be completely framework-free.
    // SagaOrchestrator.Domain is excluded — the state machine IS the domain
    // and explicitly references MassTransit.Abstractions per CLAUDE.md §2.
    private static readonly string[] DomainAssemblies =
    [
        "PlatformWallet.Ledger.Domain",
        "PlatformWallet.TransactionIntake.Domain",
        "PlatformWallet.BalanceQuery.Domain",
        "PlatformWallet.WebhookDispatcher.Domain",
    ];

    private static readonly string[] ApplicationAssemblies =
    [
        "PlatformWallet.Ledger.Application",
        "PlatformWallet.TransactionIntake.Application",
        "PlatformWallet.SagaOrchestrator.Application",
        "PlatformWallet.BalanceQuery.Application",
        "PlatformWallet.WebhookDispatcher.Application",
    ];

    private static readonly string[] InfrastructureAssemblies =
    [
        "PlatformWallet.Ledger.Infrastructure",
        "PlatformWallet.TransactionIntake.Infrastructure",
        "PlatformWallet.SagaOrchestrator.Infrastructure",
        "PlatformWallet.BalanceQuery.Infrastructure",
        "PlatformWallet.WebhookDispatcher.Infrastructure",
    ];

    private static readonly string[] ApiAssemblies =
    [
        "PlatformWallet.Ledger.Api",
        "PlatformWallet.TransactionIntake.Api",
        "PlatformWallet.BalanceQuery.Api",
        "PlatformWallet.ApiGateway.Yarp",
        "PlatformWallet.SagaOrchestrator.Worker",
        "PlatformWallet.WebhookDispatcher.Worker",
    ];

    private static readonly string[] InfrastructureFrameworks =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "StackExchange.Redis",
        "Dapper",
        "Yarp.ReverseProxy",
        "MassTransit",
    ];

    // ── 1. Domain purity ──────────────────────────────────────────────────────

    [Fact]
    public void Domain_must_not_depend_on_infrastructure_or_web_frameworks()
    {
        var forbidden = InfrastructureFrameworks
            .Concat(["Microsoft.AspNetCore", "MediatR"])
            .ToArray();

        AssertAssemblies(DomainAssemblies, asm =>
            Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny(forbidden)
                .GetResult(),
            "Domain must have zero external dependencies beyond System.*");
    }

    [Fact]
    public void Domain_must_not_use_HttpClient()
    {
        // HttpClient lives in System.Net.Http (BCL), so it won't be caught by the
        // namespace-prefix check above. We enforce it with a type-level scan instead.
        foreach (var asmName in DomainAssemblies)
        {
            var asm = Assembly.Load(asmName);
            var violations = asm.GetTypes()
                .Where(t => t.GetFields(System.Reflection.BindingFlags.Instance
                                        | System.Reflection.BindingFlags.Static
                                        | System.Reflection.BindingFlags.NonPublic
                                        | System.Reflection.BindingFlags.Public)
                             .Any(f => f.FieldType == typeof(System.Net.Http.HttpClient))
                         || t.GetProperties()
                             .Any(p => p.PropertyType == typeof(System.Net.Http.HttpClient))
                         || t.GetConstructors()
                             .SelectMany(c => c.GetParameters())
                             .Any(p => p.ParameterType == typeof(System.Net.Http.HttpClient)))
                .Select(t => t.FullName)
                .ToList();

            violations.Should().BeEmpty(
                $"[{asmName}] Domain must not declare or inject HttpClient — HTTP calls belong in Infrastructure.");
        }
    }

    [Fact]
    public void Domain_must_not_depend_on_Application_or_Infrastructure_layers()
    {
        var forbidden = ApplicationAssemblies
            .Concat(InfrastructureAssemblies)
            .ToArray();

        AssertAssemblies(DomainAssemblies, asm =>
            Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny(forbidden)
                .GetResult(),
            "Domain must not reference Application or Infrastructure layers");
    }

    [Fact]
    public void Domain_must_not_reference_MassTransit_except_SagaOrchestrator()
    {
        // Explicit test so the carve-out is visible and intentional.
        AssertAssemblies(DomainAssemblies, asm =>
            Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOn("MassTransit")
                .GetResult(),
            "MassTransit must not appear in Domain assemblies (except SagaOrchestrator.Domain, which is excluded from this list)");
    }

    // ── 2. Application layer ──────────────────────────────────────────────────

    [Fact]
    public void Application_must_not_depend_on_Infrastructure_adapters()
    {
        var forbidden = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "StackExchange.Redis",
            "Yarp.ReverseProxy",
        };

        AssertAssemblies(ApplicationAssemblies, asm =>
            Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny(forbidden)
                .GetResult(),
            "Application must not reference Infrastructure adapters directly");
    }

    [Fact]
    public void Application_must_not_depend_on_Infrastructure_assemblies()
    {
        AssertAssemblies(ApplicationAssemblies, asm =>
            Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny(InfrastructureAssemblies)
                .GetResult(),
            "Application must not reference Infrastructure assemblies (dependency inversion)");
    }

    // ── 3. Infrastructure layer ───────────────────────────────────────────────

    [Fact]
    public void Infrastructure_must_not_depend_on_Api_or_Worker_assemblies()
    {
        AssertAssemblies(InfrastructureAssemblies, asm =>
            Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny(ApiAssemblies)
                .GetResult(),
            "Infrastructure must not reference Api/Worker assemblies — dependency arrow points inward only");
    }

    // ── 4. Api / Worker layer ─────────────────────────────────────────────────

    [Fact]
    public void Api_and_Worker_must_not_reference_EFCore_directly()
    {
        // EF Core in Api/Worker was a historic bug caught during Milestone 3.
        // Migrations run via DatabaseMigratorService in Infrastructure.
        string[] entryPoints =
        [
            "PlatformWallet.Ledger.Api",
            "PlatformWallet.TransactionIntake.Api",
            "PlatformWallet.BalanceQuery.Api",
            "PlatformWallet.SagaOrchestrator.Worker",
            "PlatformWallet.WebhookDispatcher.Worker",
        ];

        AssertAssemblies(entryPoints, asm =>
            Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql.EntityFrameworkCore")
                .GetResult(),
            "Api and Worker entry points must not reference EF Core — migrations belong in Infrastructure");
    }

    // ── 5. BuildingBlocks/Contracts purity ────────────────────────────────────

    [Fact]
    public void Contracts_must_only_reference_MassTransit_Abstractions()
    {
        // Positive whitelist: the only non-BCL assembly Contracts may reference
        // is MassTransit.Abstractions. We inspect the actual referenced assemblies
        // rather than a blacklist, so any new transitive dependency is caught
        // automatically — not just the ones we thought to name.
        var contracts = Assembly.Load("PlatformWallet.Contracts");

        var illegalRefs = contracts
            .GetReferencedAssemblies()
            .Where(r => !r.Name!.StartsWith("System",      StringComparison.Ordinal)
                     && !r.Name!.StartsWith("Microsoft.NETCore", StringComparison.Ordinal)
                     && !r.Name!.StartsWith("netstandard", StringComparison.Ordinal)
                     && !r.Name!.StartsWith("mscorlib",    StringComparison.Ordinal)
                     && r.Name != "MassTransit.Abstractions")
            .Select(r => r.Name)
            .ToList();

        illegalRefs.Should().BeEmpty(
            "BuildingBlocks/Contracts must depend on exactly one non-BCL assembly: " +
            "MassTransit.Abstractions. All other non-System references are forbidden.");
    }

    // ── 6. Consumer naming convention ─────────────────────────────────────────

    [Fact]
    public void All_MassTransit_consumers_must_end_with_Consumer()
    {
        var allAssemblies = ApplicationAssemblies
            .Concat(ApiAssemblies)
            .Select(Assembly.Load);

        foreach (var asm in allAssemblies)
        {
            // Open generic types (e.g. WebhookFaultConsumer`1) are definitions, not
            // concrete implementations. NetArchTest sees them when scanning; we skip
            // them here because the convention applies to instantiable types only.
            var consumers = asm.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition
                            && t.GetInterfaces().Any(i =>
                                i.IsGenericType &&
                                i.GetGenericTypeDefinition() == typeof(IConsumer<>)))
                .ToList();

            var violations = consumers
                .Where(t => !t.Name.EndsWith("Consumer", StringComparison.Ordinal))
                .Select(t => t.FullName)
                .ToList();

            violations.Should().BeEmpty(
                $"All IConsumer<T> implementations in {asm.GetName().Name} must be named *Consumer. Failing: " +
                string.Join(", ", violations));
        }
    }

    // ── 7. Auth — no Microsoft.Identity or Duende anywhere ───────────────────

    [Fact]
    public void No_Microsoft_Identity_or_Duende_IdentityServer_anywhere()
    {
        var allAssemblies = DomainAssemblies
            .Concat(ApplicationAssemblies)
            .Concat(InfrastructureAssemblies)
            .Concat(ApiAssemblies);

        AssertAssemblies(allAssemblies, asm =>
            Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny(
                    "Microsoft.Identity",
                    "Microsoft.AspNetCore.Identity",
                    "Duende.IdentityServer")
                .GetResult(),
            "Keycloak + JwtBearer is the only auth path. Microsoft.Identity/AspNetCore.Identity/Duende are forbidden.");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static void AssertAssemblies(
        IEnumerable<string> assemblyNames,
        Func<Assembly, TestResult> check,
        string ruleDescription)
    {
        foreach (var name in assemblyNames)
        {
            var asm    = Assembly.Load(name);
            var result = check(asm);

            result.IsSuccessful.Should().BeTrue(
                $"[{asm.GetName().Name}] violated rule: {ruleDescription}. " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }
}
