import { PersonAdd as PersonAddIcon } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    CircularProgress,
    List,
    Typography,
} from "@mui/material";
import { useState } from "react";
import type { HouseholdRole, MemberResponse } from "../../../../lib/api";
import { HouseholdRoleValue } from "../../householdRole";
import { canManageMember, canManageMembers } from "../memberPermissions";
import { useHouseholdMembers } from "../useHouseholdMembers";
import { AddMemberDialog } from "./AddMemberDialog";
import { MemberActionsMenu } from "./MemberActionsMenu";
import { MemberListItem } from "./MemberListItem";
import { RemoveMemberConfirmDialog } from "./RemoveMemberConfirmDialog";

interface MembersPanelProps {
    householdId: number;
    currentUserRole?: HouseholdRole;
}

interface ActionsTarget {
    anchor: HTMLElement;
    member: MemberResponse;
}

export const MembersPanel = ({
    householdId,
    currentUserRole = HouseholdRoleValue.Member,
}: MembersPanelProps) => {
    const [addDialogOpen, setAddDialogOpen] = useState(false);
    const [memberToRemove, setMemberToRemove] = useState<MemberResponse | null>(
        null,
    );
    const [actionsTarget, setActionsTarget] = useState<ActionsTarget | null>(
        null,
    );

    const { data: members, isLoading, error } = useHouseholdMembers(householdId);

    const handleRemoveClick = (member: MemberResponse) => {
        setMemberToRemove(member);
        setActionsTarget(null);
    };

    if (isLoading) {
        return (
            <Box display="flex" justifyContent="center" p={3}>
                <CircularProgress />
            </Box>
        );
    }

    if (error) {
        return <Alert severity="error">Failed to load household members</Alert>;
    }

    return (
        <Box>
            <Card elevation={1}>
                <CardContent sx={{ p: { xs: 2, sm: 3 } }}>
                    <Box
                        display="flex"
                        justifyContent="space-between"
                        alignItems="center"
                        mb={2}
                        sx={{
                            flexDirection: { xs: "column", sm: "row" },
                            gap: { xs: 2, sm: 0 },
                            alignItems: { xs: "stretch", sm: "center" },
                        }}
                    >
                        <Typography
                            variant="h6"
                            component="h2"
                            sx={{ fontWeight: 600 }}
                        >
                            Members
                        </Typography>
                        {canManageMembers(currentUserRole) && (
                            <Button
                                data-testid="household-add-member-button"
                                variant="contained"
                                startIcon={<PersonAddIcon fontSize="small" />}
                                onClick={() => setAddDialogOpen(true)}
                                sx={{
                                    fontWeight: 500,
                                    px: { xs: 2, sm: 3 },
                                    py: { xs: 1, sm: 1.25 },
                                }}
                            >
                                Add Member
                            </Button>
                        )}
                    </Box>

                    {members && members.length > 0 ? (
                        <List
                            data-testid="household-members-list"
                            sx={{ px: 0 }}
                        >
                            {members.map((member) => (
                                <MemberListItem
                                    key={member.externalId}
                                    member={member}
                                    showActions={canManageMember(
                                        member,
                                        currentUserRole,
                                    )}
                                    onActionsClick={(event, m) =>
                                        setActionsTarget({
                                            anchor: event.currentTarget,
                                            member: m,
                                        })
                                    }
                                />
                            ))}
                        </List>
                    ) : (
                        <Typography
                            color="text.secondary"
                            textAlign="center"
                            py={3}
                        >
                            No members found
                        </Typography>
                    )}
                </CardContent>
            </Card>

            <MemberActionsMenu
                anchorEl={actionsTarget?.anchor ?? null}
                member={actionsTarget?.member ?? null}
                householdId={householdId}
                currentUserRole={currentUserRole}
                onClose={() => setActionsTarget(null)}
                onRemoveClick={handleRemoveClick}
            />

            <AddMemberDialog
                open={addDialogOpen}
                onClose={() => setAddDialogOpen(false)}
                householdId={householdId}
            />

            <RemoveMemberConfirmDialog
                open={memberToRemove !== null}
                onClose={() => setMemberToRemove(null)}
                member={memberToRemove}
                householdId={householdId}
            />
        </Box>
    );
};
