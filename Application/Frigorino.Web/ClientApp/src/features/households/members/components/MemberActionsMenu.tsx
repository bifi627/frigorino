import { Menu, MenuItem } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { HouseholdRole, MemberResponse } from "../../../../lib/api";
import { HouseholdRoleValue } from "../../householdRole";
import { canManageMember } from "../memberPermissions";
import { useUpdateMemberRole } from "../useUpdateMemberRole";

interface MemberActionsMenuProps {
    anchorEl: HTMLElement | null;
    member: MemberResponse | null;
    householdId: number;
    currentUserRole: HouseholdRole;
    onClose: () => void;
    onRemoveClick: (member: MemberResponse) => void;
}

export const MemberActionsMenu = ({
    anchorEl,
    member,
    householdId,
    currentUserRole,
    onClose,
    onRemoveClick,
}: MemberActionsMenuProps) => {
    const { t } = useTranslation();
    const { mutate: updateRole } = useUpdateMemberRole(householdId);

    const handleRoleChange = (role: HouseholdRole) => {
        if (!member?.externalId) return;
        updateRole(
            { userId: member.externalId, role },
            { onSuccess: () => onClose() },
        );
    };

    const canManage = member ? canManageMember(member, currentUserRole) : false;

    return (
        <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={onClose}>
            {canManage && member && (
                <MenuItem
                    data-testid="household-member-action-make-member"
                    onClick={() => handleRoleChange(HouseholdRoleValue.Member)}
                >
                    {t("household.makeMember")}
                </MenuItem>
            )}
            {canManage && member && (
                <MenuItem
                    data-testid="household-member-action-make-admin"
                    onClick={() => handleRoleChange(HouseholdRoleValue.Admin)}
                >
                    {t("household.makeAdmin")}
                </MenuItem>
            )}
            {canManage && member && (
                <MenuItem
                    data-testid="household-member-action-remove"
                    onClick={() => onRemoveClick(member)}
                >
                    {t("household.removeFromHousehold")}
                </MenuItem>
            )}
        </Menu>
    );
};
