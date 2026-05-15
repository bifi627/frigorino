import type { HouseholdRole, MemberResponse } from "../../../lib/api";
import { HouseholdRoleValue } from "../householdRole";

export const canManageMembers = (currentUserRole: HouseholdRole): boolean =>
    currentUserRole >= HouseholdRoleValue.Admin;

export const canManageMember = (
    member: MemberResponse,
    currentUserRole: HouseholdRole,
): boolean => {
    if (!canManageMembers(currentUserRole)) return false;
    if (member.role === HouseholdRoleValue.Owner) return false;
    if (
        currentUserRole === HouseholdRoleValue.Admin &&
        member.role === HouseholdRoleValue.Admin
    ) {
        return false;
    }
    return true;
};
