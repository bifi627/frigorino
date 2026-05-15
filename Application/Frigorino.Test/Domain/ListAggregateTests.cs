using Frigorino.Domain.Entities;
using Frigorino.Domain.Errors;

namespace Frigorino.Test.Domain
{
    // Pure unit tests for the List aggregate. No DbContext, no DI — builds entities in-memory and
    // calls methods directly. Locks down the validation + role-policy matrix (creator-OR-Admin+)
    // that integration tests only cover on happy paths.
    public class ListAggregateTests
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
            var result = List.Create("", "desc", HouseholdId, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(List.Name));
        }

        [Fact]
        public void Create_WhitespaceName_Fails()
        {
            var result = List.Create("   ", "desc", HouseholdId, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(List.Name));
        }

        [Fact]
        public void Create_NameLongerThanMax_Fails()
        {
            var tooLong = new string('x', List.NameMaxLength + 1);

            var result = List.Create(tooLong, null, HouseholdId, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(List.Name));
        }

        [Fact]
        public void Create_DescriptionLongerThanMax_Fails()
        {
            var tooLong = new string('x', List.DescriptionMaxLength + 1);

            var result = List.Create("Groceries", tooLong, HouseholdId, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(List.Description));
        }

        [Fact]
        public void Create_EmptyCreatorId_Fails()
        {
            var result = List.Create("Groceries", null, HouseholdId, "");

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(List.CreatedByUserId));
        }

        [Fact]
        public void Create_InvalidHouseholdId_Fails()
        {
            var result = List.Create("Groceries", null, 0, CreatorId);

            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, e => e.Metadata.TryGetValue("Property", out var p) && (string?)p == nameof(List.HouseholdId));
        }

        [Fact]
        public void Create_Valid_StampsTimestampsAndTrims()
        {
            var result = List.Create("  Groceries  ", "  weekly  ", HouseholdId, CreatorId);

            Assert.True(result.IsSuccess);
            var list = result.Value;
            Assert.Equal("Groceries", list.Name);
            Assert.Equal("weekly", list.Description);
            Assert.Equal(HouseholdId, list.HouseholdId);
            Assert.Equal(CreatorId, list.CreatedByUserId);
            Assert.True(list.IsActive);
            Assert.True(list.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
            Assert.Equal(list.CreatedAt, list.UpdatedAt);
        }

        [Fact]
        public void Create_WhitespaceDescription_TrimsToNull()
        {
            var result = List.Create("Groceries", "   ", HouseholdId, CreatorId);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Description);
        }

        [Fact]
        public void Create_AtMaxLengths_Succeeds()
        {
            var maxName = new string('x', List.NameMaxLength);
            var maxDescription = new string('y', List.DescriptionMaxLength);

            var result = List.Create(maxName, maxDescription, HouseholdId, CreatorId);

            Assert.True(result.IsSuccess);
        }

        // ------- Update -------

        [Fact]
        public void Update_CreatorAsMember_Succeeds()
        {
            var list = ListBy(CreatorId);

            var result = list.Update(CreatorId, HouseholdRole.Member, "Renamed", "new desc");

            Assert.True(result.IsSuccess);
            Assert.Equal("Renamed", list.Name);
            Assert.Equal("new desc", list.Description);
        }

        [Fact]
        public void Update_NonCreatorMember_ReturnsAccessDenied()
        {
            var list = ListBy(CreatorId);

            var result = list.Update(MemberId, HouseholdRole.Member, "Renamed", null);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
        }

        [Fact]
        public void Update_NonCreatorAdmin_Succeeds()
        {
            var list = ListBy(CreatorId);

            var result = list.Update(AdminId, HouseholdRole.Admin, "Renamed", null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Renamed", list.Name);
        }

        [Fact]
        public void Update_NonCreatorOwner_Succeeds()
        {
            var list = ListBy(CreatorId);

            var result = list.Update(OwnerId, HouseholdRole.Owner, "Renamed", null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Renamed", list.Name);
        }

        [Fact]
        public void Update_EmptyName_ReturnsValidationKeyedOnName()
        {
            var list = ListBy(CreatorId);

            var result = list.Update(CreatorId, HouseholdRole.Member, "", null);

            Assert.True(result.IsFailed);
            var error = result.Errors[0];
            Assert.False(error is AccessDeniedError);
            Assert.Equal(nameof(List.Name), error.Metadata["Property"]);
        }

        [Fact]
        public void Update_NameLongerThanMax_ReturnsValidationKeyedOnName()
        {
            var list = ListBy(CreatorId);
            var tooLong = new string('x', List.NameMaxLength + 1);

            var result = list.Update(CreatorId, HouseholdRole.Member, tooLong, null);

            Assert.True(result.IsFailed);
            Assert.Equal(nameof(List.Name), result.Errors[0].Metadata["Property"]);
        }

        [Fact]
        public void Update_Succeeds_StampsUpdatedAt()
        {
            var list = ListBy(CreatorId);
            var before = list.UpdatedAt;

            var result = list.Update(CreatorId, HouseholdRole.Member, "Renamed", null);

            Assert.True(result.IsSuccess);
            Assert.True(list.UpdatedAt > before);
        }

        [Fact]
        public void Update_Succeeds_TrimsWhitespaceDescriptionToNull()
        {
            var list = ListBy(CreatorId);

            var result = list.Update(CreatorId, HouseholdRole.Member, "Renamed", "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(list.Description);
        }

        // ------- SoftDelete -------

        [Fact]
        public void SoftDelete_CreatorAsMember_Succeeds()
        {
            var list = ListBy(CreatorId);

            var result = list.SoftDelete(CreatorId, HouseholdRole.Member);

            Assert.True(result.IsSuccess);
            Assert.False(list.IsActive);
        }

        [Fact]
        public void SoftDelete_NonCreatorMember_ReturnsAccessDenied()
        {
            var list = ListBy(CreatorId);

            var result = list.SoftDelete(MemberId, HouseholdRole.Member);

            Assert.True(result.IsFailed);
            Assert.IsType<AccessDeniedError>(result.Errors[0]);
            Assert.True(list.IsActive); // not flipped
        }

        [Fact]
        public void SoftDelete_NonCreatorAdmin_Succeeds()
        {
            var list = ListBy(CreatorId);

            var result = list.SoftDelete(AdminId, HouseholdRole.Admin);

            Assert.True(result.IsSuccess);
            Assert.False(list.IsActive);
        }

        [Fact]
        public void SoftDelete_NonCreatorOwner_Succeeds()
        {
            var list = ListBy(CreatorId);

            var result = list.SoftDelete(OwnerId, HouseholdRole.Owner);

            Assert.True(result.IsSuccess);
            Assert.False(list.IsActive);
        }

        [Fact]
        public void SoftDelete_StampsUpdatedAt()
        {
            var list = ListBy(CreatorId);
            var before = list.UpdatedAt;

            var result = list.SoftDelete(CreatorId, HouseholdRole.Member);

            Assert.True(result.IsSuccess);
            Assert.True(list.UpdatedAt > before);
        }

        // ------- Helpers -------

        private static List ListBy(string creatorUserId)
        {
            return new List
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
