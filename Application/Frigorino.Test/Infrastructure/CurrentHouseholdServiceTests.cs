using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Services;
using Frigorino.Test.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Infrastructure
{
    public class CurrentHouseholdServiceTests
    {
        private const string UserId = "user-1";

        [Fact]
        public async Task SetCurrentHouseholdAsync_Success_PersistsToUserRow()
        {
            var (service, _, _, dbName) = await CreateServiceWithSeededUserAsync(seedHouseholdIds: new[] { 10, 20 });

            var result = await service.SetCurrentHouseholdAsync(20);

            Assert.True(result.IsSuccess);

            // Re-read the user from a fresh context to confirm the write hit the DB, not just the change tracker.
            await using var verifyCtx = NewContext(dbName);
            var user = await verifyCtx.Users.SingleAsync(u => u.ExternalId == UserId);
            Assert.Equal(20, user.LastActiveHouseholdId);
        }

        [Fact]
        public async Task GetCurrentHouseholdIdAsync_NoSession_ReturnsStoredLastActive()
        {
            var (service, ctx, _, _) = await CreateServiceWithSeededUserAsync(seedHouseholdIds: new[] { 10, 20 });

            // Simulate "user previously picked 20, then session was lost" — set the column directly.
            var user = await ctx.Users.SingleAsync(u => u.ExternalId == UserId);
            user.LastActiveHouseholdId = 20;
            await ctx.SaveChangesAsync();

            var id = await service.GetCurrentHouseholdIdAsync();

            Assert.Equal(20, id);
        }

        [Fact]
        public async Task GetCurrentHouseholdIdAsync_StoredAndSessionEmpty_RehydratesSession()
        {
            var (service, _, session, _) = await CreateServiceWithSeededUserAsync(seedHouseholdIds: new[] { 10, 20 });

            // First set via the service to seed the column …
            await service.SetCurrentHouseholdAsync(20);

            // … then wipe the session to simulate browser restart and re-read.
            session.Clear();

            var id = await service.GetCurrentHouseholdIdAsync();

            Assert.Equal(20, id);
            // Subsequent call should hit session; value must still be 20, not flip to the role default (10).
            Assert.Equal(20, await service.GetCurrentHouseholdIdAsync());
        }

        // ----- helpers -----

        private static async Task<(CurrentHouseholdService service, TestApplicationDbContext ctx, FakeSession session, string dbName)>
            CreateServiceWithSeededUserAsync(int[] seedHouseholdIds)
        {
            var dbName = Guid.NewGuid().ToString();
            var ctx = NewContext(dbName);

            var user = new User { ExternalId = UserId, Name = "User One", Email = "u1@example.com" };
            ctx.Users.Add(user);
            foreach (var hid in seedHouseholdIds)
            {
                var h = new Household
                {
                    Id = hid,
                    Name = $"H{hid}",
                    CreatedByUserId = UserId,
                };
                ctx.Households.Add(h);
                ctx.UserHouseholds.Add(new UserHousehold
                {
                    UserId = UserId,
                    HouseholdId = hid,
                    Role = HouseholdRole.Owner,
                    IsActive = true,
                });
            }
            await ctx.SaveChangesAsync();

            var session = new FakeSession();
            var httpContext = new DefaultHttpContext { Session = session };
            var accessor = A.Fake<IHttpContextAccessor>();
            A.CallTo(() => accessor.HttpContext).Returns(httpContext);

            var currentUser = A.Fake<ICurrentUserService>();
            A.CallTo(() => currentUser.UserId).Returns(UserId);

            var service = new CurrentHouseholdService(ctx, currentUser, accessor);
            return (service, ctx, session, dbName);
        }

        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }
    }
}
