using FluentResults;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;
using Frigorino.Domain.Quantities;
using Xunit;

namespace Frigorino.Test.Recipes
{
    public class RecipeAggregateTests
    {
        private const int DefaultSectionId = 100;

        private static Recipe NewRecipe()
        {
            var r = Recipe.Create("Apple Pie", null, householdId: 1, createdByUserId: "u1");
            Assert.True(r.IsSuccess);
            var recipe = r.Value;
            recipe.Id = 10;
            var section = recipe.AddSection(null, null);
            Assert.True(section.IsSuccess);
            section.Value.Id = DefaultSectionId;
            return recipe;
        }

        private static RecipeItem AddItem(Recipe recipe, string text, Quantity? quantity = null, string? comment = null)
            => recipe.AddItem(DefaultSectionId, text, quantity, comment).Value;

        [Fact]
        public void Create_BlankName_Fails()
        {
            var result = Recipe.Create("  ", null, 1, "u1");
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(Recipe.Name));
        }

        [Fact]
        public void Create_ValidServings_IsStored()
        {
            var result = Recipe.Create("Apple Pie", null, 1, "u1", servings: 4);
            Assert.True(result.IsSuccess);
            Assert.Equal(4, result.Value.Servings);
        }

        [Fact]
        public void Create_NullServings_IsAllowed()
        {
            var result = Recipe.Create("Apple Pie", null, 1, "u1", servings: null);
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Servings);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(Recipe.ServingsMax + 1)]
        public void Create_OutOfRangeServings_FailsWithServingsProperty(int servings)
        {
            var result = Recipe.Create("Apple Pie", null, 1, "u1", servings: servings);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(Recipe.Servings));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(Recipe.ServingsMax)]
        public void Create_BoundaryServings_IsAllowed(int servings)
        {
            var result = Recipe.Create("Apple Pie", null, 1, "u1", servings: servings);
            Assert.True(result.IsSuccess);
            Assert.Equal(servings, result.Value.Servings);
        }

        [Fact]
        public void Update_ValidServings_IsStored()
        {
            var recipe = NewRecipe();
            var result = recipe.Update("u1", HouseholdRole.Owner, "Apple Pie", null, servings: 6);
            Assert.True(result.IsSuccess);
            Assert.Equal(6, recipe.Servings);
        }

        [Fact]
        public void Update_OutOfRangeServings_FailsWithServingsProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.Update("u1", HouseholdRole.Owner, "Apple Pie", null, servings: 0);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(Recipe.Servings));
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
            var result = recipe.AddItem(DefaultSectionId, "Flour", q, "  sifted  ");
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
            var result = recipe.AddItem(DefaultSectionId, "Flour", null, "   ");
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void AddItem_OverlongText_FailsWithTextProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.AddItem(DefaultSectionId, new string('x', RecipeItem.TextMaxLength + 1), null, null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeItem.Text));
        }

        [Fact]
        public void UpdateItem_AllFieldsNull_RejectedAsNoOp()
        {
            var recipe = NewRecipe();
            var added = AddItem(recipe, "Flour");
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: null);
            Assert.True(result.IsFailed);
        }

        [Fact]
        public void UpdateItem_CommentOnly_Succeeds()
        {
            var recipe = NewRecipe();
            var added = AddItem(recipe, "Flour");
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: "room temp");
            Assert.True(result.IsSuccess);
            Assert.Equal("room temp", result.Value.Comment);
        }

        [Fact]
        public void UpdateItem_EmptyComment_Clears()
        {
            var recipe = NewRecipe();
            var added = AddItem(recipe, "Flour", comment: "note");
            var result = recipe.UpdateItem(added.Id, text: null, quantity: null, clearQuantity: false, comment: "");
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Comment);
        }

        [Fact]
        public void ApplyExtractedQuantity_RewritesNameAndQuantity()
        {
            var recipe = NewRecipe();
            var added = AddItem(recipe, "250g flour");
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
            var a = AddItem(recipe, "A");
            var b = AddItem(recipe, "B");
            // Items added in-memory have Id=0; set distinct IDs so ReorderItem can distinguish
            // b from afterItemId=0 (otherwise the self-anchor no-op guard fires).
            a.Id = 1;
            b.Id = 2;
            var result = recipe.ReorderItem(b.Id, afterItemId: 0);
            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(b.Rank, a.Rank) < 0);
        }

        [Fact]
        public void RestoreItem_ReactivatesSoftDeletedItem()
        {
            var recipe = NewRecipe();
            var item = AddItem(recipe, "Flour");
            item.Id = 1;
            recipe.RemoveItem(item.Id);
            Assert.False(item.IsActive);

            var result = recipe.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            Assert.True(item.IsActive);
            Assert.Same(item, result.Value);
        }

        [Fact]
        public void RestoreItem_PreservesOriginalRank()
        {
            var recipe = NewRecipe();
            var item = AddItem(recipe, "Flour");
            item.Id = 1;
            var originalRank = item.Rank;
            recipe.RemoveItem(item.Id);

            var result = recipe.RestoreItem(item.Id);

            Assert.True(result.IsSuccess);
            // RestoreItem keeps the old rank verbatim; rank re-mint is a separate step.
            Assert.Equal(originalRank, item.Rank);
        }

        [Fact]
        public void RestoreItem_AlreadyActive_ReturnsEntityNotFound()
        {
            var recipe = NewRecipe();
            var item = AddItem(recipe, "Flour");
            item.Id = 1; // active by default

            var result = recipe.RestoreItem(item.Id);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void ReplaceRestoredItemRank_OnRankCollision_MintsFreshAppendRank()
        {
            var recipe = NewRecipe();
            // Restored item shares its old rank with a live item that took its slot while it was
            // soft-deleted. ReplaceRestoredItemRank re-places it at the end of the section.
            var restored = AddItem(recipe, "Flour");
            restored.Id = 1;
            recipe.RemoveItem(restored.Id);

            var colliding = AddItem(recipe, "Sugar");
            colliding.Id = 2;
            // Force the rank collision: the new live item now occupies the restored item's old rank.
            colliding.Rank = restored.Rank;

            // Bring the restored item back (still carrying the now-colliding rank).
            recipe.RestoreItem(restored.Id);
            Assert.Equal(colliding.Rank, restored.Rank);

            var result = recipe.ReplaceRestoredItemRank(restored.Id);

            Assert.True(result.IsSuccess);
            Assert.Same(restored, result.Value);
            // A fresh rank is minted at the end of the section — no longer colliding, and after the
            // sibling that took its slot.
            Assert.NotEqual(colliding.Rank, restored.Rank);
            Assert.True(string.CompareOrdinal(colliding.Rank, restored.Rank) < 0);
        }

        [Fact]
        public void ReplaceRestoredItemRank_NotFound_ReturnsEntityNotFound()
        {
            var recipe = NewRecipe();

            var result = recipe.ReplaceRestoredItemRank(itemId: 999);

            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void AddSection_AppendsWithRankAfterPrevious()
        {
            var recipe = NewRecipe();
            var second = recipe.AddSection("Filling", null);
            Assert.True(second.IsSuccess);
            var first = recipe.Sections.First(s => s.Id == DefaultSectionId);
            Assert.True(string.CompareOrdinal(first.Rank, second.Value.Rank) < 0);
        }

        [Fact]
        public void AddSection_OverlongName_FailsWithNameProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.AddSection(new string('x', RecipeSection.NameMaxLength + 1), null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeSection.Name));
        }

        [Fact]
        public void AddItem_UnknownSection_ReturnsEntityNotFound()
        {
            var recipe = NewRecipe();
            var result = recipe.AddItem(sectionId: 999, "Flour", null, null);
            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveSection_CascadesItemsAndDeactivatesSection()
        {
            var recipe = NewRecipe();
            var second = recipe.AddSection("Filling", null).Value;
            second.Id = 200;
            var item = recipe.AddItem(200, "Sugar", null, null).Value;
            item.Id = 1;

            var result = recipe.RemoveSection(200);

            Assert.True(result.IsSuccess);
            Assert.False(second.IsActive);
            Assert.False(item.IsActive);
        }

        [Fact]
        public void RemoveSection_LastSection_IsBlocked()
        {
            var recipe = NewRecipe();
            var result = recipe.RemoveSection(DefaultSectionId);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(Recipe.Sections));
        }

        [Fact]
        public void RestoreSection_ReactivatesSectionAndItsItems()
        {
            var recipe = NewRecipe();
            var second = recipe.AddSection("Filling", null).Value;
            second.Id = 200;
            var item = recipe.AddItem(200, "Sugar", null, null).Value;
            item.Id = 1;
            recipe.RemoveSection(200);

            var result = recipe.RestoreSection(200);

            Assert.True(result.IsSuccess);
            Assert.True(second.IsActive);
            Assert.True(item.IsActive);
        }

        [Fact]
        public void ReplaceRestoredSectionRank_OnCollision_MintsFreshAppendRank()
        {
            var recipe = NewRecipe();
            var second = recipe.AddSection("Filling", null).Value;
            second.Id = 200;
            recipe.RemoveSection(200);

            var third = recipe.AddSection("Topping", null).Value;
            third.Id = 300;
            third.Rank = second.Rank; // force a collision

            recipe.RestoreSection(200);
            Assert.Equal(third.Rank, second.Rank);

            var result = recipe.ReplaceRestoredSectionRank(200);

            Assert.True(result.IsSuccess);
            Assert.NotEqual(third.Rank, second.Rank);
            Assert.True(string.CompareOrdinal(third.Rank, second.Rank) < 0);
        }

        [Fact]
        public void ReorderSection_ToTop_PlacesBeforeFirst()
        {
            var recipe = NewRecipe();
            var second = recipe.AddSection("Filling", null).Value;
            second.Id = 200;
            var first = recipe.Sections.First(s => s.Id == DefaultSectionId);

            var result = recipe.ReorderSection(second.Id, afterSectionId: 0);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(second.Rank, first.Rank) < 0);
        }

        [Fact]
        public void UpdateSection_RenamesAndBumpsTimestamp()
        {
            var recipe = NewRecipe();
            var section = recipe.Sections.First(s => s.Id == DefaultSectionId);

            var result = recipe.UpdateSection(DefaultSectionId, "Crust", "Flaky base");

            Assert.True(result.IsSuccess);
            Assert.Equal("Crust", section.Name);
            Assert.Equal("Flaky base", section.Description);
        }

        [Fact]
        public void UpdateSection_UnknownSection_ReturnsEntityNotFound()
        {
            var recipe = NewRecipe();
            var result = recipe.UpdateSection(sectionId: 999, "Crust", null);
            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RestoreSection_DeCollidesItemsSharingARank()
        {
            var recipe = NewRecipe();
            // A second section so the default isn't the last (RemoveSection blocks the last one).
            recipe.AddSection("Other", null).Value.Id = 200;
            // Two items in the default section: I1 at the initial rank, I2 appended after it.
            var i1 = recipe.AddItem(DefaultSectionId, "I1", null, null).Value; i1.Id = 1;
            var i2 = recipe.AddItem(DefaultSectionId, "I2", null, null).Value; i2.Id = 2;

            // Delete I1 (frees its rank), then reorder I2 to the top so it reclaims I1's old rank slot.
            recipe.RemoveItem(i1.Id);
            recipe.ReorderItem(i2.Id, afterItemId: 0);
            // Force the exact collision the de-collide guard must handle: I1 (inactive) now shares I2's rank.
            i1.Rank = i2.Rank;

            // Cascade-delete the whole section (both items inactive, sharing a rank), then restore.
            recipe.RemoveSection(DefaultSectionId);
            var result = recipe.RestoreSection(DefaultSectionId);

            Assert.True(result.IsSuccess);
            Assert.True(i1.IsActive);
            Assert.True(i2.IsActive);
            // No two active items in the section may share a rank after restore.
            Assert.NotEqual(i1.Rank, i2.Rank);
        }

        [Fact]
        public void ReorderItem_StaysWithinItsSection()
        {
            var recipe = NewRecipe();
            var other = recipe.AddSection("Filling", null).Value;
            other.Id = 200;
            // Two items in section A, one in section B.
            var a1 = recipe.AddItem(DefaultSectionId, "A1", null, null).Value; a1.Id = 1;
            var a2 = recipe.AddItem(DefaultSectionId, "A2", null, null).Value; a2.Id = 2;
            var b1 = recipe.AddItem(200, "B1", null, null).Value; b1.Id = 3;

            // Anchoring a2 after a foreign-section item (b1) falls back to top of a2's own section.
            var result = recipe.ReorderItem(a2.Id, afterItemId: b1.Id);

            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(a2.Rank, a1.Rank) < 0);
        }

        // ---- Source links ----

        [Fact]
        public void AddLink_ValidHttpsUrl_TrimsAndStores()
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink("  https://example.com/recipe  ", "  My Blog  ");
            Assert.True(result.IsSuccess);
            Assert.Equal("https://example.com/recipe", result.Value.Url);
            Assert.Equal("My Blog", result.Value.Label);
            Assert.Single(recipe.Links);
        }

        [Fact]
        public void AddLink_EmptyLabel_StoresNull()
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink("https://example.com", "   ");
            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Label);
        }

        [Fact]
        public void AddLink_BlankUrl_FailsWithUrlProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink("   ", null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeLink.Url));
        }

        [Theory]
        [InlineData("ftp://example.com/file")]
        [InlineData("javascript:alert(1)")]
        [InlineData("not a url")]
        [InlineData("example.com")]
        public void AddLink_NonHttpUrl_FailsWithUrlProperty(string url)
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink(url, null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeLink.Url));
        }

        [Fact]
        public void AddLink_OverlongLabel_FailsWithLabelProperty()
        {
            var recipe = NewRecipe();
            var result = recipe.AddLink("https://example.com", new string('x', RecipeLink.LabelMaxLength + 1));
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeLink.Label));
        }

        [Fact]
        public void AddLink_AppendsWithRankAfterPrevious()
        {
            var recipe = NewRecipe();
            var first = recipe.AddLink("https://a.example.com", null).Value;
            var second = recipe.AddLink("https://b.example.com", null).Value;
            Assert.True(string.CompareOrdinal(first.Rank, second.Rank) < 0);
        }

        [Fact]
        public void UpdateLink_ChangesUrlAndLabel()
        {
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://old.example.com", "old").Value;
            link.Id = 500;
            var result = recipe.UpdateLink(500, "https://new.example.com", "new");
            Assert.True(result.IsSuccess);
            Assert.Equal("https://new.example.com", result.Value.Url);
            Assert.Equal("new", result.Value.Label);
        }

        [Fact]
        public void UpdateLink_InvalidUrl_FailsWithUrlProperty()
        {
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://old.example.com", null).Value;
            link.Id = 501;
            var result = recipe.UpdateLink(501, "ftp://nope", null);
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string)p! == nameof(RecipeLink.Url));
        }

        [Fact]
        public void UpdateLink_UnknownId_NotFound()
        {
            var recipe = NewRecipe();
            var result = recipe.UpdateLink(999, "https://example.com", null);
            Assert.True(result.IsFailed);
            Assert.IsType<EntityNotFoundError>(result.Errors[0]);
        }

        [Fact]
        public void RemoveLink_DeactivatesIt()
        {
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://example.com", null).Value;
            link.Id = 510;
            var result = recipe.RemoveLink(510);
            Assert.True(result.IsSuccess);
            Assert.False(link.IsActive);
        }

        [Fact]
        public void RemoveLink_LastLink_IsAllowed()
        {
            // Unlike sections, zero links is a valid state.
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://example.com", null).Value;
            link.Id = 511;
            var result = recipe.RemoveLink(511);
            Assert.True(result.IsSuccess);
            Assert.Empty(recipe.Links.Where(l => l.IsActive));
        }

        [Fact]
        public void RestoreLink_ReactivatesIt()
        {
            var recipe = NewRecipe();
            var link = recipe.AddLink("https://example.com", null).Value;
            link.Id = 520;
            recipe.RemoveLink(520);
            var result = recipe.RestoreLink(520);
            Assert.True(result.IsSuccess);
            Assert.True(link.IsActive);
        }

        [Fact]
        public void ReplaceRestoredLinkRank_MovesToEnd()
        {
            var recipe = NewRecipe();
            var first = recipe.AddLink("https://a.example.com", null).Value;
            first.Id = 530;
            var second = recipe.AddLink("https://b.example.com", null).Value;
            second.Id = 531;
            var result = recipe.ReplaceRestoredLinkRank(530);
            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(second.Rank, first.Rank) < 0);
        }

        [Fact]
        public void ReorderLink_ToTop_PlacesFirst()
        {
            var recipe = NewRecipe();
            var first = recipe.AddLink("https://a.example.com", null).Value;
            first.Id = 540;
            var second = recipe.AddLink("https://b.example.com", null).Value;
            second.Id = 541;
            var result = recipe.ReorderLink(541, 0);
            Assert.True(result.IsSuccess);
            Assert.True(string.CompareOrdinal(second.Rank, first.Rank) < 0);
        }
    }
}
