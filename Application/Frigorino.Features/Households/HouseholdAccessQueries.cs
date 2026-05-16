using Frigorino.Domain.Entities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Features.Households
{
    // Auth-boundary primitive used by slice handlers across sibling-aggregate features
    // (Lists, Inventories, future). Returns the caller's active UserHousehold row — null when
    // the caller isn't an active member of the household (or the household itself is inactive).
    //
    // This is a *query*, not an aggregate-load: the Household aggregate stays focused on its
    // membership/tenancy invariants instead of becoming a god-aggregate that every sibling
    // feature has to route through. Sibling aggregates (List, Inventory, ...) reference
    // Household by id and rely on this query for the membership check; the returned row also
    // exposes `Role`, which sibling-aggregate methods consume for creator-OR-Admin+ policy.
    //
    // Maps to NotFound on the wire (auth boundary convention — "you're not a member" is
    // indistinguishable from "the household doesn't exist"). Use `Forbid` only after a member
    // has been confirmed AND the role-policy on the sibling aggregate denies the action.
    public static class HouseholdAccessQueries
    {
        public static Task<UserHousehold?> FindActiveMembershipAsync(
            this ApplicationDbContext db,
            int householdId,
            string userId,
            CancellationToken ct)
        {
            return db.UserHouseholds.FirstOrDefaultAsync(
                uh => uh.UserId == userId
                   && uh.HouseholdId == householdId
                   && uh.IsActive
                   && uh.Household.IsActive,
                ct);
        }

        // Sibling of FindActiveMembershipAsync for the rare slices that also need the User
        // entity (Create slices that wire it onto a new aggregate's CreatedByUser navigation).
        // One round-trip via JOIN instead of an extra Users query — the FK relationship is
        // already configured (UserHousehold.UserId → User.ExternalId).
        public static Task<UserHousehold?> FindActiveMembershipWithUserAsync(
            this ApplicationDbContext db,
            int householdId,
            string userId,
            CancellationToken ct)
        {
            return db.UserHouseholds
                .Include(uh => uh.User)
                .FirstOrDefaultAsync(
                    uh => uh.UserId == userId
                       && uh.HouseholdId == householdId
                       && uh.IsActive
                       && uh.Household.IsActive,
                    ct);
        }
    }
}
