import { Box, Snackbar, Tooltip, Typography } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import { useLongPress } from "../../../../hooks/useLongPress";
import type { InventoryItemResponse } from "../../../../lib/api";
import {
    formatLocalDate,
    getExpiryColor,
    getExpiryInfo,
} from "../../../../utils/dateUtils";

interface Props {
    item: InventoryItemResponse;
}
export function InventoryItemContent({ item }: Props) {
    const { t } = useTranslation();
    const [showDateSnackbar, setShowDateSnackbar] = useState(false);

    const events = useLongPress({
        shouldPreventDefault: true,
        onLongPress: () => {
            navigator.clipboard.writeText(item.text ?? "");
            toast.success(t("common.textCopiedToClipboard"), {
                duration: 2000,
            });
        },
    });

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
                    data-testid={`inventory-item-expiry-${item.text}`}
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
            <Box
                {...events}
                sx={{
                    display: "flex",
                    width: "100%",
                    alignItems: "flex-start",
                    justifyContent: "space-between",
                    gap: 1,
                    pl: item.expiryDate ? 2 : 0, // Clear the highlight bar when present
                }}
            >
                <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography
                        variant="body2"
                        sx={{ fontWeight: 500, wordBreak: "break-word" }}
                    >
                        {item.text}
                    </Typography>
                    {item.expiryDate && (
                        <Tooltip
                            title={`${t("inventory.expiryDate")}: ${formatLocalDate(item.expiryDate)}`}
                            arrow
                        >
                            <Typography
                                variant="caption"
                                onClick={handleDateClick}
                                sx={{
                                    display: "block",
                                    color: "text.secondary",
                                    cursor: "pointer",
                                    userSelect: "none",
                                    "&:hover": { color: "text.primary" },
                                    "&:active": { color: "text.primary" },
                                }}
                            >
                                {getExpiryInfo(item.expiryDate, translateKey)
                                    .humanReadable ||
                                    formatLocalDate(item.expiryDate)}
                            </Typography>
                        </Tooltip>
                    )}
                </Box>
                {item.quantity && (
                    <ItemQuantityChip
                        quantity={item.quantity}
                        testId={`inventory-item-quantity-${item.text}`}
                    />
                )}
            </Box>
            {/* Snackbar for mobile date display */}
            <Snackbar
                open={showDateSnackbar}
                onClose={() => setShowDateSnackbar(false)}
                message={
                    item.expiryDate
                        ? `${t("inventory.expiryDate")}: ${formatLocalDate(item.expiryDate)}`
                        : ""
                }
                autoHideDuration={2000}
                anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
            />
        </Box>
    );
}
