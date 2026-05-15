import { MoreVert } from "@mui/icons-material";
import {
    Box,
    Card,
    CardContent,
    Chip,
    IconButton,
    ListItem,
    ListItemText,
    List as MuiList,
    Typography,
} from "@mui/material";
import type { ListResponse } from "../../../lib/api";

interface ListSummaryCardProps {
    list: ListResponse;
    onClick: (listId: number) => void;
    onMenuOpen: (event: React.MouseEvent<HTMLElement>, list: ListResponse) => void;
    menuDisabled?: boolean;
}

const formatDate = (dateString: string) =>
    new Date(dateString).toLocaleDateString("de-DE", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
    });

export const ListSummaryCard = ({
    list,
    onClick,
    onMenuOpen,
    menuDisabled = false,
}: ListSummaryCardProps) => {
    return (
        <Card elevation={1}>
            <CardContent sx={{ py: 2 }}>
                <MuiList disablePadding>
                    <ListItem
                        data-testid={`list-item-${list.name}`}
                        sx={{
                            px: 0,
                            cursor: "pointer",
                            "&:hover": { bgcolor: "action.hover" },
                        }}
                        onClick={() => list.id && onClick(list.id)}
                        secondaryAction={
                            <Box
                                sx={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 1,
                                }}
                            >
                                <Chip
                                    label={
                                        list.createdAt
                                            ? formatDate(list.createdAt)
                                            : ""
                                    }
                                    size="small"
                                    variant="outlined"
                                />
                                <IconButton
                                    size="small"
                                    data-testid={`list-item-menu-button-${list.name}`}
                                    onClick={(e) => {
                                        e.stopPropagation();
                                        onMenuOpen(e, list);
                                    }}
                                    disabled={menuDisabled}
                                >
                                    <MoreVert fontSize="small" />
                                </IconButton>
                            </Box>
                        }
                    >
                        <ListItemText
                            primary={
                                <Typography
                                    variant="body1"
                                    sx={{ fontWeight: 600 }}
                                >
                                    {list.name}
                                </Typography>
                            }
                            secondary={
                                list.description && (
                                    <Typography
                                        variant="body2"
                                        color="text.secondary"
                                        sx={{ mt: 0.5 }}
                                    >
                                        {list.description}
                                    </Typography>
                                )
                            }
                        />
                    </ListItem>
                </MuiList>
            </CardContent>
        </Card>
    );
};
