using System.Collections.Concurrent;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Web.Middlewares
{
    public sealed class InitialConnectionMiddleware : IMiddleware
    {
        // Per-process cache: once a user has been upserted in this server lifetime, subsequent
        // requests skip the DB round trip. ConcurrentDictionary is required — InvokeAsync runs
        // concurrently across threads, and the previous HashSet was getting corrupted.
        private static readonly ConcurrentDictionary<string, byte> _checkedConnections = new();

        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _applicationDbContext;

        public InitialConnectionMiddleware(ICurrentUserService currentUserService, ApplicationDbContext applicationDbContext)
        {
            _currentUserService = currentUserService;
            _applicationDbContext = applicationDbContext;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var userId = _currentUserService.UserId;
            if (userId != "0" && !_checkedConnections.ContainsKey(userId))
            {
                var nameOnInsert = _currentUserService.UserName
                    ?? _currentUserService.Email?.Split("@")[0]
                    ?? $"User_{Guid.NewGuid()}";
                var nameOnUpdate = _currentUserService.Email?.Split("@")[0];
                var email = _currentUserService.Email;
                var now = DateTime.UtcNow;

                // Race-free first-write: parallel requests for the same new user converge on one row.
                // On conflict we refresh LastLoginAt + Email; Name is only swapped when we have a
                // fresh email-prefix, otherwise the existing Name is preserved.
                await _applicationDbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Users" ("ExternalId", "Name", "Email", "CreatedAt", "LastLoginAt", "IsActive")
                    VALUES ({userId}, {nameOnInsert}, {email}, {now}, {now}, true)
                    ON CONFLICT ("ExternalId") DO UPDATE SET
                        "LastLoginAt" = EXCLUDED."LastLoginAt",
                        "Email" = EXCLUDED."Email",
                        "Name" = COALESCE({nameOnUpdate}, "Users"."Name")
                    """, context.RequestAborted);

                _checkedConnections.TryAdd(userId, 0);
            }

            await next(context);
        }
    }
}
