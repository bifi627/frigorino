import { useMemo } from "react";
import { useTranslation } from "react-i18next";

export const HouseholdRoleValue = {
    Member: 0,
    Admin: 1,
    Owner: 2,
} as const;

// Canonical English role names. Used for stable test attributes (data-role) and
// other non-user-facing identifiers. Don't use these for UI text — use
// `useRoleLabels` so users see translated strings.
export const roleNames: Record<number, string> = {
    [HouseholdRoleValue.Member]: "Member",
    [HouseholdRoleValue.Admin]: "Admin",
    [HouseholdRoleValue.Owner]: "Owner",
};

export const roleColors: Record<
    number,
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

export const useRoleLabels = (): Record<number, string> => {
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
