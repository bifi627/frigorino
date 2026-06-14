using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Quantities;
using Xunit;

namespace Frigorino.Test.Recipes
{
    public class RecipeAggregateTests
    {
        private static Recipe NewRecipe()
        {
            var r = Recipe.Create("Apple Pie", null, householdId: 1, createdByUserId: "u1");
            Assert.True(r.IsSuccess);
            var recipe = r.Value;
            recipe.Id = 10;
            return recipe;
        }

        [Fact]
        public void Create_BlankName_Fails()
        {
            var result = Recipe.Create("  ", null, 1, "u1");
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(Recipe.Name));
        }

        [Fact]
        public void Update_NonOwnerNonAdmin_Denied()
        {
            var recipe = NewRecipe();
            var result = recipe.Update("someone-else", HouseholdRole.Member, "New", null);
            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void AddItem_TrimsAndStoresCommentAndQuantity()
        {
            var recipe = NewRecipe();
            var q = Quantity.Create(250, QuantityUnit.Gram).Value;
            var result = recipe.AddItem("Flour", q, "  sifted  ");
            Assert.True(result.IsSuccess);
            Assert.Equal("Flour", result.Value.Text);
            Assert.Equal("sifted", result.Value.Comment);
            Assert.Equal(250m, result.Value.QuantityValue);
            Assert.Single(recipe.Items);
        }

        [Fact]
        public void AddItem_EmptyComment_StoresNull()
        {
            var recipe = NewRecipe();
            var result = recipe.AddItem("Flour", null, "   ");
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddItem_OverlongText_FailsWithTextProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.AddItem(new string('x', RecipeItem.TextMaxLength + 1), null, null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeItem.Text));
        }

        [Fact]
        public void UpdateItem_AllFieldsNull_RejectedAsNoOp()
        {
            var recipe = NewRecipe();
            var added = recipe.AddItem("Flour", null, null).Value;
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: null);
            Assert.True(result.IsFailed);
        }

        [Fact]
        public void UpdateItem_CommentOnly_Succeeds()
        {
            var recipe = NewRecipe();
            var added = recipe.AddItem("Flour", null, null).Value;
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: "room temp");
            Assert.True(result.IsSuccess);
            Assert.Equal("room temp", result.Value.Comment);
        }

        [Fact]
        public void UpdateItem_EmptyComment_Clears()
        {
            var recipe = NewRecipe();
            var added = recipe.AddItem("Flour", null, "note").Value;
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: "");
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void ApplyExtractedQuantity_RewritesNameAndQuantity()
        {
            var recipe = NewRecipe();
            var added = recipe.AddItem("250g flour", null, null).Value;
            var q = Quantity.Create(250, QuantityUnit.Gram).Value;
            var result = recipe.ApplyExtractedQuantity(added.Id, "Flour", q);
            Assert.True(result.IsSuccess);
            Assert.Equal("Flour", added.Text);
            Assert.Equal(250m, added.QuantityValue);
        }

        [Fact]
        public void ReorderItem_ToTop_PlacesBeforeFirst()
        {
            var recipe = NewRecipe();
            var a = recipe.AddItem("A", null, null).Value;
            var b = recipe.AddItem("B", null, null).Value;
            // Items added in-memory have Id=0; set distinct IDs so ReorderItem can distinguish
            // b from afterItemId=0 (otherwise the self-anchor no-op guard fires).
            a.Id = 1;
            b.Id = 2;
            var result = recipe.ReorderItem(b.Id, afterItemId: 0);
            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(b.Rank, a.Rank) < 0);
        }
    }
}
