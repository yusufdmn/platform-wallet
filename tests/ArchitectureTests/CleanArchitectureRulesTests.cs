using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace PlatformWallet.ArchitectureTests;

/// <summary>
/// Encodes the dependency rules from /CLAUDE.md §2: Domain depends on nothing,
/// Application/Infra/Api layer dependencies flow one-way, BuildingBlocks.Contracts
/// is contract-only. A violation fails CI — this is the non-negotiable layer gate.
/// </summary>
[Trait("Category", "Architecture")]
public class CleanArchitectureRulesTests
{
    private static readonly string[] DomainAssemblies =
    [
        "PlatformWallet.Ledger.Domain",
        "PlatformWallet.TransactionIntake.Domain",
        "PlatformWallet.BalanceQuery.Domain",
        "PlatformWallet.WebhookDispatcher.Domain",
        // NOTE: SagaOrchestrator.Domain is deliberately excluded — the state
        // machine IS the domain and references MassTransit.Abstractions (see
        // /src/SagaOrchestrator/CLAUDE.md and the plan's §2).
    ];

    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "MediatR",
        "Npgsql",
        "StackExchange.Redis",
        "Dapper",
        "Yarp",
    ];

    [Fact]
    public void Domain_assemblies_must_not_depend_on_infrastructure_or_web_frameworks()
    {
        foreach (var asmName in DomainAssemblies)
        {
            var asm = Assembly.Load(asmName);
            var result = Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny(ForbiddenInDomain)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{asmName} violated layering rule. Failing types: " +
                string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
        }
    }

    [Fact]
    public void Application_layers_must_not_depend_on_EF_Core_or_Npgsql()
    {
        string[] appAssemblies =
        [
            "PlatformWallet.Ledger.Application",
            "PlatformWallet.TransactionIntake.Application",
            "PlatformWallet.SagaOrchestrator.Application",
            "PlatformWallet.BalanceQuery.Application",
            "PlatformWallet.WebhookDispatcher.Application",
        ];

        foreach (var asmName in appAssemblies)
        {
            var asm = Assembly.Load(asmName);
            var result = Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "StackExchange.Redis", "Yarp")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{asmName} must not reference infrastructure adapters. Failing types: " +
                string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
        }
    }

    [Fact]
    public void Contracts_must_not_reference_EF_Core_MediatR_or_domain_assemblies()
    {
        var contracts = Assembly.Load("PlatformWallet.Contracts");
        var forbidden = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "MediatR",
            "MassTransit.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "PlatformWallet.Ledger.Domain",
            "PlatformWallet.TransactionIntake.Domain",
        };

        var result = Types.InAssembly(contracts)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "BuildingBlocks/Contracts must be contract-only (TheMainPlan.md §0.5). Failing: " +
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void No_Microsoft_Identity_or_AspNetCore_Identity_anywhere()
    {
        // Keycloak + JwtBearer only. See /CLAUDE.md Forbidden list.
        string[] allProductionAsms =
        [
            "PlatformWallet.ApiGateway.Yarp",
            "PlatformWallet.Ledger.Api",
            "PlatformWallet.TransactionIntake.Api",
            "PlatformWallet.BalanceQuery.Api",
            "PlatformWallet.SagaOrchestrator.Worker",
            "PlatformWallet.WebhookDispatcher.Worker",
        ];

        foreach (var asmName in allProductionAsms)
        {
            var asm = Assembly.Load(asmName);
            var result = Types.InAssembly(asm)
                .Should()
                .NotHaveDependencyOnAny("Microsoft.Identity", "Microsoft.AspNetCore.Identity", "Duende.IdentityServer")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{asmName} must not reference Microsoft.Identity / AspNetCore.Identity / Duende. Failing: " +
                string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
        }
    }
}
