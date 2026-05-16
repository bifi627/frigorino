using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;

namespace Frigorino.Test.Domain
{
    // Pure unit tests for the Inventory aggregate. No DbContext, no DI — builds entities
    // in-memory and calls methods directly. Locks down the validation + role-policy matrix
    // (creator-OR-Admin+) that integration tests only cover on happy paths. Mirrors
    // ListAggregateTests.cs — Inventory and List share the same CRUD shape.
    public class InventoryAggregateTests
    {
        private const string CreatorId = "user-creator";
        private const string AdminId = "user-admin";
        private const string MemberId = "user-member";
        private const string OwnerId = "user-owner";
        private const int HouseholdId = 42;

        // ------- Create -------

        [Fact]
        public void Create_EmptyName_Fails()
        {
            var result = Inventory.Create("", "desc", HouseholdId, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Inventory.Name));
        }

        [Fact]
        public void Create_WhitespaceName_Fails()
        {
            var result = Inventory.Create("   ", "desc", HouseholdId, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Inventory.Name));
        }

        [Fact]
        public void Create_NameLongerThanMax_Fails()
        {
            var tooLong = new string('x', Inventory.NameMaxLength + 1);

            var result = Inventory.Create(tooLong, null, HouseholdId, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Inventory.Name));
        }

        [Fact]
        public void Create_DescriptionLongerThanMax_Fails()
        {
            var tooLong = new string('x', Inventory.DescriptionMaxLength + 1);

            var result = Inventory.Create("Pantry", tooLong, HouseholdId, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Inventory.Description));
        }

        [Fact]
        public void Create_EmptyCreatorId_Fails()
        {
            var result = Inventory.Create("Pantry", null, HouseholdId, "");

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Inventory.CreatedByUserId));
        }

        [Fact]
        public void Create_InvalidHouseholdId_Fails()
        {
            var result = Inventory.Create("Pantry", null, 0, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(Inventory.HouseholdId));
        }

        [Fact]
        public void Create_Valid_StampsTimestampsAndTrims()
        {
            var result = Inventory.Create("  Pantry  ", "  perishables  ", HouseholdId, CreatorId);

            Assert.True(result.IsSuccess);
            var inventory = result.Value;
            Assert.Equal("Pantry", inventory.Name);
            Assert.Equal("perishables", inventory.Description);
            Assert.Equal(HouseholdId, inventory.HouseholdId);
            Assert.Equal(CreatorId, inventory.CreatedByUserId);
            Assert.True(inventory.IsActive);
            Assert.True(inventory.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
            Assert.Equal(inventory.CreatedAt, inventory.UpdatedAt);
        }

        [Fact]
        public void Create_WhitespaceDescription_TrimsToNull()
        {
            var result = Inventory.Create("Pantry", "   ", HouseholdId, CreatorId);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Description);
        }

        [Fact]
        public void Create_AtMaxLengths_Succeeds()
        {
            var maxName = new string('x', Inventory.NameMaxLength);
            var maxDescription = new string('y', Inventory.DescriptionMaxLength);

            var result = Inventory.Create(maxName, maxDescription, HouseholdId, CreatorId);

            Assert.True(result.IsSuccess);
        }

        // ------- Update -------

        [Fact]
        public void Update_CreatorAsMember_Succeeds()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.Update(CreatorId, HouseholdRole.Member, "Renamed", "new desc");

            Assert.True(result.IsSuccess);
            Assert.Equal("Renamed", inventory.Name);
            Assert.Equal("new desc", inventory.Description);
        }

        [Fact]
        public void Update_NonCreatorMember_ReturnsAccessDenied()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.Update(MemberId, HouseholdRole.Member, "Renamed", null);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void Update_NonCreatorAdmin_Succeeds()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.Update(AdminId, HouseholdRole.Admin, "Renamed", null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Renamed", inventory.Name);
        }

        [Fact]
        public void Update_NonCreatorOwner_Succeeds()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.Update(OwnerId, HouseholdRole.Owner, "Renamed", null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Renamed", inventory.Name);
        }

        [Fact]
        public void Update_EmptyName_ReturnsValidationKeyedOnName()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.Update(CreatorId, HouseholdRole.Member, "", null);

            Assert.True(result.IsFailed);
            var error = result.Errors[0];
            Assert.False(error is AccessDeniedError);
            Assert.Equal(nameof(Inventory.Name), error.Metadata["Property"]);
        }

        [Fact]
        public void Update_NameLongerThanMax_ReturnsValidationKeyedOnName()
        {
            var inventory = InventoryBy(CreatorId);
            var tooLong = new string('x', Inventory.NameMaxLength + 1);

            var result = inventory.Update(CreatorId, HouseholdRole.Member, tooLong, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(Inventory.Name), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void Update_Succeeds_StampsUpdatedAt()
        {
            var inventory = InventoryBy(CreatorId);
            var before = inventory.UpdatedAt;

            var result = inventory.Update(CreatorId, HouseholdRole.Member, "Renamed", null);

            Assert.True(result.IsSuccess);
            Assert.True(inventory.UpdatedAt > before);
        }

        [Fact]
        public void Update_Succeeds_TrimsWhitespaceDescriptionToNull()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.Update(CreatorId, HouseholdRole.Member, "Renamed", "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(inventory.Description);
        }

        // ------- SoftDelete -------

        [Fact]
        public void SoftDelete_CreatorAsMember_Succeeds()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.SoftDelete(CreatorId, HouseholdRole.Member);

            Assert.True(result.IsSuccess);
            Assert.False(inventory.IsActive);
        }

        [Fact]
        public void SoftDelete_NonCreatorMember_ReturnsAccessDenied()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.SoftDelete(MemberId, HouseholdRole.Member);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
            Assert.True(inventory.IsActive); // not flipped
        }

        [Fact]
        public void SoftDelete_NonCreatorAdmin_Succeeds()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.SoftDelete(AdminId, HouseholdRole.Admin);

            Assert.True(result.IsSuccess);
            Assert.False(inventory.IsActive);
        }

        [Fact]
        public void SoftDelete_NonCreatorOwner_Succeeds()
        {
            var inventory = InventoryBy(CreatorId);

            var result = inventory.SoftDelete(OwnerId, HouseholdRole.Owner);

            Assert.True(result.IsSuccess);
            Assert.False(inventory.IsActive);
        }

        [Fact]
        public void SoftDelete_StampsUpdatedAt()
        {
            var inventory = InventoryBy(CreatorId);
            var before = inventory.UpdatedAt;

            var result = inventory.SoftDelete(CreatorId, HouseholdRole.Member);

            Assert.True(result.IsSuccess);
            Assert.True(inventory.UpdatedAt > before);
        }

        // ------- Helpers -------

        private static Inventory InventoryBy(string creatorUserId)
        {
            return new Inventory
            {
                Id = 1,
                Name = "Original",
                Description = "original desc",
                HouseholdId = HouseholdId,
                CreatedByUserId = creatorUserId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
                IsActive = true,
            };
        }
    }
}
