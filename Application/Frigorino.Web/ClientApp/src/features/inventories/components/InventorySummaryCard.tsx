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
import type { InventoryResponse } from "../../../lib/api";

interface InventorySummaryCardProps {
    inventory: InventoryResponse;
    onClick: (inventoryId: number) => void;
    onMenuOpen: (
        event: React.MouseEvent<HTMLElement>,
        inventory: InventoryResponse,
    ) => void;
    menuDisabled?: boolean;
}

const formatDate = (dateString: string) =>
    new Date(dateString).toLocaleDateString("de-DE", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
    });

export const InventorySummaryCard = ({
    inventory,
    onClick,
    onMenuOpen,
    menuDisabled = false,
}: InventorySummaryCardProps) => {
    return (
        <Card elevation={1}>
            <CardContent sx={{ py: 2 }}>
                <MuiList disablePadding>
                    <ListItem
                        data-testid={`inventory-item-${inventory.name}`}
                        sx={{
                            px: 0,
                            cursor: "pointer",
                            "&:hover": { bgcolor: "action.hover" },
                        }}
                        onClick={() => inventory.id && onClick(inventory.id)}
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
                                        inventory.createdAt
                                            ? formatDate(inventory.createdAt)
                                            : ""
                                    }
                                    size="small"
                                    variant="outlined"
                                />
                                <IconButton
                                    size="small"
                                    data-testid={`inventory-item-menu-button-${inventory.name}`}
                                    onClick={(e) => {
                                        e.stopPropagation();
                                        onMenuOpen(e, inventory);
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
                                    {inventory.name}
                                </Typography>
                            }
                            secondary={
                                inventory.description && (
                                    <Typography
                                        variant="body2"
                                        color="text.secondary"
                                        sx={{ mt: 0.5 }}
                                    >
                                        {inventory.description}
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
