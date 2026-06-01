using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Features.Notifications;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Test.Features
{
    public class FcmTokenSliceTests
    {
        private static TestApplicationDbContext NewContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TestApplicationDbContext(options);
        }

        private static ICurrentUserService UserNamed(string id)
        {
            var svc = A.Fake<ICurrentUserService>();
            A.CallTo(() => svc.UserId).Returns(id);
            return svc;
        }

        [Fact]
        public async Task Register_CreatesTokenForCurrentUser()
        {
            using var db = NewContext();
            db.Users.Add(new User { ExternalId = "u1", Name = "U", Email = "u1@example.com" });
            await db.SaveChangesAsync();

            await RegisterFcmTokenEndpoint.Handle(
                new RegisterFcmTokenRequest("tok-1"), UserNamed("u1"), db, CancellationToken.None);

            var token = await db.FcmTokens.SingleAsync();
            Assert.Equal("u1", token.UserId);
            Assert.Equal("tok-1", token.Token);
        }

        [Fact]
        public async Task Register_ReassignsExistingTokenToCurrentUser()
        {
            using var db = NewContext();
            db.Users.Add(new User { ExternalId = "u1", Name = "U1", Email = "u1@example.com" });
            db.Users.Add(new User { ExternalId = "u2", Name = "U2", Email = "u2@example.com" });
            db.FcmTokens.Add(FcmToken.Create("u1", "shared-tok"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await RegisterFcmTokenEndpoint.Handle(
                new RegisterFcmTokenRequest("shared-tok"), UserNamed("u2"), db, CancellationToken.None);

            var token = await db.FcmTokens.SingleAsync();
            Assert.Equal("u2", token.UserId);
        }

        [Fact]
        public async Task Unregister_DeletesOwnToken()
        {
            using var db = NewContext();
            db.Users.Add(new User { ExternalId = "u1", Name = "U", Email = "u1@example.com" });
            db.FcmTokens.Add(FcmToken.Create("u1", "tok-1"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await UnregisterFcmTokenEndpoint.Handle(
                new UnregisterFcmTokenRequest("tok-1"), UserNamed("u1"), db, CancellationToken.None);

            Assert.Empty(db.FcmTokens);
        }
    }
}
