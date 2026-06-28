using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Domain.Quantities;
using Frigorino.Features.Households;
using Frigorino.Features.Results;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Frigorino.Features.Recipes
{
    public sealed record ImportRecipeRequest(string Url);

    public static class ImportRecipeEndpoint
    {
        public static IEndpointRouteBuilder MapImportRecipe(this IEndpointRouteBuilder app)
        {
            app.MapPost("import", Handle)
               .WithName("ImportRecipe")
               .Produces<RecipeResponse>(StatusCodes.Status201Created)
               .Produces(StatusCodes.Status404NotFound)
               .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
               .ProducesValidationProblem();
            return app;
        }

        private static async Task<Results<Created<RecipeResponse>, NotFound, ValidationProblem, ProblemHttpResult>> Handle(
            int householdId,
            ImportRecipeRequest request,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            RecipeImportService importService,
            IRecipeQuantityExtractionTrigger quantityTrigger,
            CancellationToken ct)
        {
            var membership = await db.FindActiveMembershipWithUserAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }
            var creator = membership.User;

            var import = await importService.ImportAsync(request.Url, ct);
            if (import.IsFailed)
            {
                var code = import.Errors[0].Metadata.TryGetValue("code", out var c) ? c?.ToString() : null;
                if (code == "invalid_url")
                {
                    return new Error("Enter a valid http(s) URL.").WithProperty("Url").ToValidationProblemResult();
                }
                return TypedResults.Problem(
                    detail: import.Errors[0].Message,
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { ["code"] = code });
            }

            var imported = import.Value;
            var creation = Recipe.Create(imported.Name, imported.Description, householdId, currentUser.UserId, imported.Servings);
            if (creation.IsFailed)
            {
                return creation.ToValidationProblem();
            }

            var recipe = creation.Value;
            recipe.CreatedByUser = creator;
            recipe.AddSection(null, null); // every recipe starts with one unnamed default section
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync(ct); // recipe + section now have real ids so AddItem can link the FK

            var section = recipe.Sections.First(s => s.IsActive);
            var routed = new List<(RecipeItem Item, ItemTextAnalysis Analysis)>();
            foreach (var ingredient in imported.Ingredients)
            {
                var analysis = ItemTextRouter.Analyze(ingredient);
                var add = recipe.AddItem(section.Id, analysis.CleanName, quantity: null, comment: null);
                if (add.IsSuccess)
                {
                    routed.Add((add.Value, analysis));
                }
            }
            recipe.AddLink(request.Url, imported.SourceName);
            await db.SaveChangesAsync(ct);

            // Bulk add carries the same obligation as the item-create slice: route each new item so
            // quantity extraction runs. Recipe items never chain product classification (MVP).
            foreach (var (item, analysis) in routed)
            {
                quantityTrigger.OnItemRouted(householdId, recipe.Id, item.Id, analysis);
            }

            return TypedResults.Created(
                $"/api/household/{householdId}/recipes/{recipe.Id}",
                RecipeResponse.From(recipe, creator, routed.Count));
        }
    }
}
