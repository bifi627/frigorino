using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Frigorino.Application.Services;
using Frigorino.Domain.Entities;
using Frigorino.Features.Households;
using Frigorino.Infrastructure.EntityFramework;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

// Pin Frigorino.Application's assembly via any remaining service (ListItemService) — picked
// arbitrarily; swap when the last legacy service migrates.

namespace Frigorino.Test.Architecture
{
    // Locks down dependency direction so the layering doesn't drift as the slice count grows.
    // Rules picked for high signal + zero false positives. Add slice-isolation rules when
    // sibling slice folders multiply (Lists/Inventories migration).
    public class ArchitectureTests
    {
        private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
            .LoadAssemblies(
                typeof(Household).Assembly,
                typeof(ListItemService).Assembly,
                typeof(ApplicationDbContext).Assembly,
                typeof(CreateHouseholdEndpoint).Assembly)
            .Build();

        private static readonly IObjectProvider<IType> DomainLayer =
            Types().That().ResideInAssembly("Frigorino.Domain").As("Domain");

        private static readonly IObjectProvider<IType> ApplicationLayer =
            Types().That().ResideInAssembly("Frigorino.Application").As("Application");

        private static readonly IObjectProvider<IType> InfrastructureLayer =
            Types().That().ResideInAssembly("Frigorino.Infrastructure").As("Infrastructure");

        private static readonly IObjectProvider<IType> FeaturesLayer =
            Types().That().ResideInAssembly("Frigorino.Features").As("Features");

        [Fact]
        public void Domain_Should_Not_Depend_On_Infrastructure_Frameworks()
        {
            Types().That().Are(DomainLayer)
                .Should().NotDependOnAny(Types().That().ResideInNamespace(@"Microsoft\.EntityFrameworkCore.*"))
                .AndShould().NotDependOnAny(Types().That().ResideInNamespace(@"Microsoft\.AspNetCore.*"))
                .AndShould().NotDependOnAny(Types().That().ResideInNamespace(@"Hangfire.*"))
                .AndShould().NotDependOnAny(Types().That().ResideInNamespace(@"FirebaseAdmin.*"))
                .AndShould().NotDependOnAny(Types().That().ResideInNamespace(@"OpenAI.*"))
                .Because("Domain stays free of infrastructure concerns; entities and value objects are persistence-ignorant.")
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }

        [Fact]
        public void Application_Should_Not_Depend_On_Infrastructure()
        {
            Types().That().Are(ApplicationLayer)
                .Should().NotDependOnAny(InfrastructureLayer)
                .Because("Application contains domain services and use-case orchestration; Infrastructure types are wired in via DI from the host.")
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }

        [Fact]
        public void Infrastructure_Should_Not_Depend_On_Web()
        {
            Types().That().Are(InfrastructureLayer)
                .Should().NotDependOnAny(Types().That().ResideInAssembly("Frigorino.Web"))
                .Because("Infrastructure is consumable by any host; coupling it to Frigorino.Web inverts the dependency direction.")
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }

        [Fact]
        public void Features_Should_Not_Depend_On_Web()
        {
            Types().That().Are(FeaturesLayer)
                .Should().NotDependOnAny(Types().That().ResideInAssembly("Frigorino.Web"))
                .Because("Slice handlers compose ASP.NET Core abstractions (IEndpointRouteBuilder); they shouldn't reach back into the host project.")
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }
    }
}
