import { Menu, MenuItem } from "@mui/material";
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
            {canManage && member && [
                <MenuItem
                    key="make-member"
                    data-testid="household-member-action-make-member"
                    onClick={() => handleRoleChange(HouseholdRoleValue.Member)}
                >
                    Make Member
                </MenuItem>,
                <MenuItem
                    key="make-admin"
                    data-testid="household-member-action-make-admin"
                    onClick={() => handleRoleChange(HouseholdRoleValue.Admin)}
                >
                    Make Admin
                </MenuItem>,
                <MenuItem
                    key="remove"
                    data-testid="household-member-action-remove"
                    onClick={() => onRemoveClick(member)}
                >
                    Remove from Household
                </MenuItem>,
            ]}
        </Menu>
    );
};
