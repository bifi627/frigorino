namespace Frigorino.Domain.Entities
{
    // Role-policy matrix for household membership operations. Lifted out of the aggregate
    // methods so the predicate "who is allowed to manage members" lives in one file and is
    // testable in isolation. Add new role-gated predicates here as they emerge — keep the
    // file the canonical home for the household role policy.
    public static class HouseholdRoleExtensions
    {
        public static bool CanManageMembers(this HouseholdRole role)
        {
            return role >= HouseholdRole.Admin;
        }

        // Can the caller's role grant `roleToGrant` to another member? Anchored to enum order
        // (Member=0, Admin=1, Owner=2) — a caller can grant any role at or below their own.
        // The previous gate `CanManageMembers` blocks Members entirely; this gate then blocks
        // Admins from granting Owner (privilege escalation).
        public static bool CanGrantRole(this HouseholdRole caller, HouseholdRole roleToGrant)
        {
            return roleToGrant <= caller;
        }
    }
}
