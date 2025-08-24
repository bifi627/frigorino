import { DateRange, ShoppingBag } from "@mui/icons-material";
import { Box, ListItemText, Typography } from "@mui/material";
import type { InventoryItemDto } from "../../lib/api";

interface Props {
    item: InventoryItemDto;
}
export function InventoryItemContent({ item }: Props) {
    return (
        <>
            <ListItemText
                primary={
                    <Typography
                        variant="button"
                        sx={{
                            fontWeight: 500,
                            wordBreak: "break-word",
                        }}
                    >
                        {item.text}
                    </Typography>
                }
                secondary={
                    <Box
                        display="flex"
                        justifyContent="space-between"
                        alignItems="center"
                    >
                        {item.quantity && (
                            <Typography
                                variant="caption"
                                sx={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 0.5,
                                }}
                            >
                                <ShoppingBag fontSize="small" />
                                {item.quantity}
                            </Typography>
                        )}
                        {item.expiryDate && (
                            <Typography
                                variant="caption"
                                sx={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 0.5,
                                }}
                            >
                                <DateRange fontSize="small" />
                                {new Date(item.expiryDate).toLocaleDateString()}
                            </Typography>
                        )}
                    </Box>
                }
            />
        </>
    );
}
