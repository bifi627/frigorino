import {
    MoreVert as MoreVertIcon,
    PersonAdd as PersonAddIcon,
} from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Card,
    CardContent,
    Chip,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    IconButton,
    List,
    ListItem,
    ListItemSecondaryAction,
    ListItemText,
    Menu,
    MenuItem,
    Typography,
} from "@mui/material";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import React, { useState } from "react";
import { ClientApi } from "../../common/apiClient";
import { type HouseholdMemberDto, type HouseholdRole } from "../../lib/api";
import { AddMemberDialog } from "./AddMemberDialog";

interface HouseholdMembersProps {
    householdId: number;
    currentUserRole?: HouseholdRole;
}

const roleLabels: Record<number, string> = {
    0: "Member",
    1: "Admin",
    2: "Owner",
};

const roleColors: Record<
    number,
    | "default"
    | "primary"
    | "secondary"
    | "error"
    | "info"
    | "success"
    | "warning"
> = {
    0: "default",
    1: "primary",
    2: "warning",
};

export const HouseholdMembers: React.FC<HouseholdMembersProps> = ({
    householdId,
    currentUserRole = 0,
}) => {
    const [addDialogOpen, setAddDialogOpen] = useState(false);
    const [confirmRemoveOpen, setConfirmRemoveOpen] = useState(false);
    const [memberToRemove, setMemberToRemove] =
        useState<HouseholdMemberDto | null>(null);
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    const [selectedMember, setSelectedMember] =
        useState<HouseholdMemberDto | null>(null);

    const queryClient = useQueryClient();

    // Fetch household members
    const {
        data: members,
        isLoading,
        error,
    } = useQuery({
        queryKey: ["household-members", householdId],
        queryFn: () => ClientApi.members.getApiHouseholdMembers(householdId),
    });

    // Remove member mutation
    const removeMemberMutation = useMutation({
        mutationFn: async (userId: string) => {
            return ClientApi.members.deleteApiHouseholdMembers(
                householdId,
                userId,
            );
        },
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: ["household-members", householdId],
            });
            setConfirmRemoveOpen(false);
            setMemberToRemove(null);
        },
    });

    // Update member role mutation
    const updateRoleMutation = useMutation({
        mutationFn: async ({
            userId,
            role,
        }: {
            userId: string;
            role: HouseholdRole;
        }) => {
            return ClientApi.members.putApiHouseholdMembersRole(
                householdId,
                userId,
                {
                    role,
                },
            );
        },
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: ["household-members", householdId],
            });
            handleMenuClose();
        },
    });

    const handleMenuClick = (
        event: React.MouseEvent<HTMLElement>,
        member: HouseholdMemberDto,
    ) => {
        setMenuAnchor(event.currentTarget);
        setSelectedMember(member);
    };

    const handleMenuClose = () => {
        setMenuAnchor(null);
        setSelectedMember(null);
    };

    const handleRemoveClick = (member: HouseholdMemberDto) => {
        setMemberToRemove(member);
        setConfirmRemoveOpen(true);
        handleMenuClose();
    };

    const handleRoleChange = (role: HouseholdRole) => {
        if (selectedMember?.user?.externalId) {
            updateRoleMutation.mutate({
                userId: selectedMember.user.externalId,
                role,
            });
        }
    };

    const canManageMembers = currentUserRole >= 1; // Admin or Owner
    const canRemoveMember = (member: HouseholdMemberDto): boolean => {
        if (!canManageMembers) return false;
        if (member.role === 2) return false; // Cannot remove owner
        if (currentUserRole === 1 && member.role === 1) return false; // Admin cannot remove another admin
        return true;
    };

    const canChangeRole = (member: HouseholdMemberDto): boolean => {
        if (!canManageMembers) return false;
        if (member.role === 2) return false; // Cannot change owner role
        if (currentUserRole === 1 && member.role === 1) return false; // Admin cannot change another admin's role
        return true;
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
            <Card
                sx={{ borderRadius: 2, boxShadow: "0 1px 3px rgba(0,0,0,0.1)" }}
            >
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
                            sx={{
                                fontSize: { xs: "1.1rem", sm: "1.25rem" },
                                fontWeight: 600,
                                textAlign: { xs: "center", sm: "left" },
                            }}
                        >
                            Members
                        </Typography>
                        {canManageMembers && (
                            <Button
                                variant="contained"
                                startIcon={
                                    <PersonAddIcon
                                        sx={{ fontSize: { xs: 18, sm: 20 } }}
                                    />
                                }
                                onClick={() => setAddDialogOpen(true)}
                                size={
                                    window.innerWidth < 600 ? "small" : "medium"
                                }
                                sx={{
                                    textTransform: "none",
                                    fontWeight: 500,
                                    px: { xs: 2, sm: 3 },
                                    py: { xs: 1, sm: 1.25 },
                                    fontSize: { xs: "0.875rem", sm: "1rem" },
                                    borderRadius: 2,
                                }}
                            >
                                Add Member
                            </Button>
                        )}
                    </Box>

                    {members && members.length > 0 ? (
                        <List sx={{ px: 0 }}>
                            {members.map(
                                (member: HouseholdMemberDto, index: number) => (
                                    <ListItem
                                        key={member.user?.externalId || index}
                                        divider
                                        sx={{
                                            px: { xs: 1, sm: 2 },
                                            py: { xs: 1.5, sm: 2 },
                                            "&:last-child": {
                                                borderBottom: "none",
                                            },
                                        }}
                                    >
                                        <ListItemText
                                            primary={
                                                <Typography
                                                    variant="body1"
                                                    sx={{
                                                        fontWeight: 500,
                                                        fontSize: {
                                                            xs: "0.9rem",
                                                            sm: "1rem",
                                                        },
                                                        mb: 0.5,
                                                    }}
                                                >
                                                    {member.user?.name ||
                                                        "Unknown User"}
                                                </Typography>
                                            }
                                            secondary={
                                                <Typography
                                                    variant="body2"
                                                    color="text.secondary"
                                                    sx={{
                                                        fontSize: {
                                                            xs: "0.8rem",
                                                            sm: "0.875rem",
                                                        },
                                                        overflow: "hidden",
                                                        textOverflow:
                                                            "ellipsis",
                                                        whiteSpace: "nowrap",
                                                    }}
                                                >
                                                    {member.user?.email ||
                                                        "No email"}
                                                </Typography>
                                            }
                                        />
                                        <Box
                                            display="flex"
                                            alignItems="center"
                                            gap={1}
                                            sx={{
                                                ml: { xs: 1, sm: 2 },
                                                flexShrink: 0,
                                            }}
                                        >
                                            <Chip
                                                label={roleLabels[member.role!]}
                                                color={roleColors[member.role!]}
                                                size="small"
                                                sx={{
                                                    height: { xs: 24, sm: 28 },
                                                    fontSize: {
                                                        xs: "0.7rem",
                                                        sm: "0.75rem",
                                                    },
                                                    fontWeight: 500,
                                                    "& .MuiChip-label": {
                                                        px: { xs: 0.75, sm: 1 },
                                                    },
                                                }}
                                            />
                                            {(canRemoveMember(member) ||
                                                canChangeRole(member)) && (
                                                <ListItemSecondaryAction
                                                    sx={{
                                                        position: "static",
                                                        transform: "none",
                                                    }}
                                                >
                                                    <IconButton
                                                        edge="end"
                                                        onClick={(e) =>
                                                            handleMenuClick(
                                                                e,
                                                                member,
                                                            )
                                                        }
                                                        size="small"
                                                        sx={{
                                                            bgcolor:
                                                                "background.paper",
                                                            border: 1,
                                                            borderColor:
                                                                "divider",
                                                            "&:hover": {
                                                                bgcolor:
                                                                    "action.hover",
                                                            },
                                                        }}
                                                    >
                                                        <MoreVertIcon fontSize="small" />
                                                    </IconButton>
                                                </ListItemSecondaryAction>
                                            )}
                                        </Box>
                                    </ListItem>
                                ),
                            )}
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

            {/* Action Menu */}
            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={handleMenuClose}
            >
                {selectedMember && canChangeRole(selectedMember) && (
                    <>
                        <MenuItem onClick={() => handleRoleChange(0)}>
                            Make Member
                        </MenuItem>
                        <MenuItem onClick={() => handleRoleChange(1)}>
                            Make Admin
                        </MenuItem>
                    </>
                )}
                {selectedMember && canRemoveMember(selectedMember) && (
                    <MenuItem onClick={() => handleRemoveClick(selectedMember)}>
                        Remove from Household
                    </MenuItem>
                )}
            </Menu>

            {/* Add Member Dialog */}
            <AddMemberDialog
                open={addDialogOpen}
                onClose={() => setAddDialogOpen(false)}
                householdId={householdId}
            />

            {/* Remove Confirmation Dialog */}
            <Dialog
                open={confirmRemoveOpen}
                onClose={() => setConfirmRemoveOpen(false)}
            >
                <DialogTitle>Remove Member</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        Are you sure you want to remove{" "}
                        {memberToRemove?.user?.name || "this user"} from this
                        household? This action cannot be undone.
                    </DialogContentText>
                </DialogContent>
                <DialogActions>
                    <Button onClick={() => setConfirmRemoveOpen(false)}>
                        Cancel
                    </Button>
                    <Button
                        onClick={() =>
                            memberToRemove?.user?.externalId &&
                            removeMemberMutation.mutate(
                                memberToRemove.user.externalId,
                            )
                        }
                        color="error"
                        disabled={removeMemberMutation.isPending}
                    >
                        {removeMemberMutation.isPending
                            ? "Removing..."
                            : "Remove"}
                    </Button>
                </DialogActions>
            </Dialog>
        </Box>
    );
};
