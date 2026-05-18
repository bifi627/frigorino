using System.Collections.Concurrent;
using System.Security.Claims;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Infrastructure.Auth
{
    public static class UserSync
    {
        // sub → last-seen auth_time (seconds since epoch).
        //
        // Firebase sets auth_time when the user actually signs in and preserves it across
        // silent token refreshes, so this cache only invalidates on a real new login.
        // Process-static is intentional: the cache key is the auth event itself, not
        // process lifetime. Cold start costs one redundant idempotent upsert per active
        // user and recovers.
        //
        // Monotonic high-water mark: we trust Firebase's clock on auth_time. A forward-
        // dated token would suppress legitimate re-syncs until a token with a still-newer
        // auth_time arrives. Acceptable — auth_time only gates the upsert, not authorization.
        //
        // When auth_time is absent (e.g. non-JWT auth schemes used in integration tests)
        // we deliberately skip the cache and upsert every time — those callers don't have
        // a stable login event to key against.
        private static readonly ConcurrentDictionary<string, long> _lastSeenAuthTime = new();

        public static async Task EnsureAsync(
            ClaimsPrincipal principal,
            ApplicationDbContext db,
            CancellationToken ct)
        {
            var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(sub))
            {
                return;
            }

            var hasAuthTime = long.TryParse(principal.FindFirstValue("auth_time"), out var authTime);
            if (hasAuthTime
                && _lastSeenAuthTime.TryGetValue(sub, out var seen)
                && seen >= authTime)
            {
                return;
            }

            var email = principal.FindFirstValue(ClaimTypes.Email);
            var nameOnInsert = principal.FindFirstValue("name")
                ?? email?.Split("@")[0]
                ?? $"User_{Guid.NewGuid()}";
            var nameOnUpdate = email?.Split("@")[0];
            var now = DateTime.UtcNow;

            // Race-free first-write: parallel requests for the same new user converge on one row.
            // On conflict we refresh LastLoginAt + Email; Name is only swapped when we have a
            // fresh email-prefix, otherwise the existing Name is preserved.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("ExternalId", "Name", "Email", "CreatedAt", "LastLoginAt", "IsActive")
                VALUES ({sub}, {nameOnInsert}, {email}, {now}, {now}, true)
                ON CONFLICT ("ExternalId") DO UPDATE SET
                    "LastLoginAt" = EXCLUDED."LastLoginAt",
                    "Email" = EXCLUDED."Email",
                    "Name" = COALESCE({nameOnUpdate}, "Users"."Name")
                """, ct);

            // DB write first, cache update second: if the upsert throws (transient DB
            // failure, cancellation, etc.) the cache stays empty and the next request
            // retries instead of poisoning the cache with an unwritten auth_time.
            if (hasAuthTime)
            {
                _lastSeenAuthTime[sub] = authTime;
            }
        }
    }
}
