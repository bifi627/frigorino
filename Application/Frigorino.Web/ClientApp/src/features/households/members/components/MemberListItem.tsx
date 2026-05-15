import { MoreVert as MoreVertIcon } from "@mui/icons-material";
import {
    Box,
    Chip,
    IconButton,
    ListItem,
    ListItemSecondaryAction,
    ListItemText,
    Typography,
} from "@mui/material";
import type { MemberResponse } from "../../../../lib/api";
import { roleColors, roleNames, useRoleLabels } from "../../householdRole";

interface MemberListItemProps {
    member: MemberResponse;
    showActions: boolean;
    onActionsClick: (
        event: React.MouseEvent<HTMLElement>,
        member: MemberResponse,
    ) => void;
}

export const MemberListItem = ({
    member,
    showActions,
    onActionsClick,
}: MemberListItemProps) => {
    const roleLabels = useRoleLabels();

    return (
        <ListItem
            data-testid={`household-member-${member.externalId}`}
            divider
            sx={{
                px: { xs: 1, sm: 2 },
                py: { xs: 1.5, sm: 2 },
                "&:last-child": { borderBottom: "none" },
            }}
        >
            <ListItemText
                primary={
                    <Typography variant="body1" sx={{ fontWeight: 500, mb: 0.5 }}>
                        {member.name || "Unknown User"}
                    </Typography>
                }
                secondary={
                    <Typography
                        variant="body2"
                        color="text.secondary"
                        sx={{
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                        }}
                    >
                        {member.email || "No email"}
                    </Typography>
                }
            />
            <Box
                display="flex"
                alignItems="center"
                gap={1}
                sx={{ ml: { xs: 1, sm: 2 }, flexShrink: 0 }}
            >
                <Chip
                    data-testid={`household-member-${member.externalId}-role`}
                    data-role={roleNames[member.role!]}
                    label={roleLabels[member.role!]}
                    color={roleColors[member.role!]}
                    size="small"
                />
                {showActions && (
                    <ListItemSecondaryAction
                        sx={{ position: "static", transform: "none" }}
                    >
                        <IconButton
                            data-testid={`household-member-${member.externalId}-menu-toggle`}
                            edge="end"
                            onClick={(e) => onActionsClick(e, member)}
                            size="small"
                            sx={{
                                bgcolor: "background.paper",
                                border: 1,
                                borderColor: "divider",
                                "&:hover": { bgcolor: "action.hover" },
                            }}
                        >
                            <MoreVertIcon fontSize="small" />
                        </IconButton>
                    </ListItemSecondaryAction>
                )}
            </Box>
        </ListItem>
    );
};
