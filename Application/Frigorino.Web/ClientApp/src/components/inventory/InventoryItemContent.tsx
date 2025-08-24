import { ShoppingBag } from "@mui/icons-material";
import {
    Box,
    ListItemText,
    Snackbar,
    Tooltip,
    Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { InventoryItemDto } from "../../lib/api";
import { getExpiryColor, getExpiryInfo } from "../../utils/dateUtils";

interface Props {
    item: InventoryItemDto;
}
export function InventoryItemContent({ item }: Props) {
    const { t } = useTranslation();
    const [showDateSnackbar, setShowDateSnackbar] = useState(false);

    const handleDateClick = () => {
        setShowDateSnackbar(true);
    };

    // Create a type-safe wrapper for the translation function
    const translateKey = (key: string): string => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return t(key as any);
    };

    return (
        <Box sx={{ position: "relative", width: "100%" }}>
            {/* Colored highlight bar on the very left */}
            {item.expiryDate && (
                <Box
                    sx={{
                        position: "absolute",
                        top: 0,
                        left: 0,
                        bottom: 0,
                        width: 4,
                        backgroundColor: getExpiryColor(item.expiryDate),
                        borderRadius: "0 2px 2px 0",
                    }}
                />
            )}

            <ListItemText
                sx={{ pl: item.expiryDate ? 2 : 0 }} // Add padding when highlight bar is present
                primary={
                    <Typography
                        variant="body2"
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
                        mt={0.5}
                        sx={{ minHeight: 20 }}
                    >
                        {/* Left side - Quantity */}
                        <Box>
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
                        </Box>

                        {/* Right side - Human readable date with tooltip and mobile support */}
                        <Box>
                            {item.expiryDate &&
                                getExpiryInfo(item.expiryDate, translateKey)
                                    .humanReadable && (
                                    <Tooltip
                                        title={`${t("inventory.expiryDate")}: ${new Date(item.expiryDate).toLocaleDateString()}`}
                                        arrow
                                    >
                                        <Typography
                                            variant="caption"
                                            onClick={handleDateClick}
                                            sx={{
                                                color: "text.secondary",
                                                cursor: "pointer",
                                                userSelect: "none",
                                                "&:hover": {
                                                    color: "text.primary",
                                                },
                                                "&:active": {
                                                    color: "text.primary",
                                                },
                                            }}
                                        >
                                            {
                                                getExpiryInfo(
                                                    item.expiryDate,
                                                    translateKey,
                                                ).humanReadable
                                            }
                                        </Typography>
                                    </Tooltip>
                                )}
                        </Box>
                    </Box>
                }
            />

            {/* Snackbar for mobile date display */}
            <Snackbar
                open={showDateSnackbar}
                onClose={() => setShowDateSnackbar(false)}
                message={
                    item.expiryDate
                        ? `${t("inventory.expiryDate")}: ${new Date(item.expiryDate).toLocaleDateString()}`
                        : ""
                }
                autoHideDuration={2000}
                anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
            />
        </Box>
    );
}
