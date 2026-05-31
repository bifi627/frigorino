import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import type { HouseholdRole } from "../../lib/api";

export const HouseholdRoleValue = {
    Member: "Member",
    Admin: "Admin",
    Owner: "Owner",
} as const satisfies Record<string, HouseholdRole>;

// Role hierarchy for ordinal comparisons (higher = more privileged). The wire format
// is now the string name, so numeric `>=` comparisons are no longer possible — use this
// rank when an ordering check is needed (see members/memberPermissions.ts).
export const roleRank: Record<HouseholdRole, number> = {
    Member: 0,
    Admin: 1,
    Owner: 2,
};

// Canonical English role names. Used for stable test attributes (data-role) and
// other non-user-facing identifiers. Don't use these for UI text — use
// `useRoleLabels` so users see translated strings.
export const roleNames: Record<HouseholdRole, string> = {
    [HouseholdRoleValue.Member]: "Member",
    [HouseholdRoleValue.Admin]: "Admin",
    [HouseholdRoleValue.Owner]: "Owner",
};

export const roleColors: Record<
    HouseholdRole,
    | "default"
    | "primary"
    | "secondary"
    | "error"
    | "info"
    | "success"
    | "warning"
> = {
    [HouseholdRoleValue.Member]: "default",
    [HouseholdRoleValue.Admin]: "primary",
    [HouseholdRoleValue.Owner]: "warning",
};

export const useRoleLabels = (): Record<HouseholdRole, string> => {
    const { t } = useTranslation();
    return useMemo(
        () => ({
            [HouseholdRoleValue.Member]: t("household.member"),
            [HouseholdRoleValue.Admin]: t("household.admin"),
            [HouseholdRoleValue.Owner]: t("household.owner"),
        }),
        [t],
    );
};
