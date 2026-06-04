import { Box, Chip, Drawer, Stack, Typography, useTheme } from "@mui/material";
import { useTranslation } from "react-i18next";
import {
    formatLocalDate,
    getExpiryInfo,
    getExpiryLevel,
    parseLocalDate,
    todayIsoDate,
} from "../../../../utils/dateUtils";
import { calendarLevelColor } from "../calendarColors";

export interface CalendarItemDetail {
    title: string;
    inventoryName: string;
    expiryDate: string;
}

interface CalendarItemDetailsSheetProps {
    detail: CalendarItemDetail | null;
    onClose: () => void;
}

const MS_PER_DAY = 1000 * 60 * 60 * 24;

// Bottom sheet shown when a narrow (compact) calendar bar is tapped — those bars can't render
// readable inline text, so the full item details live here. Matches the app's other bottom sheets.
export const CalendarItemDetailsSheet = ({
    detail,
    onClose,
}: CalendarItemDetailsSheetProps) => {
    const theme = useTheme();
    const { t } = useTranslation();
    const translateKey = (key: string): string => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return t(key as any);
    };

    const info = detail ? getExpiryInfo(detail.expiryDate, translateKey) : null;
    let statusColor = "";
    if (detail) {
        const diffDays = Math.round(
            (parseLocalDate(detail.expiryDate).getTime() -
                parseLocalDate(todayIsoDate()).getTime()) /
                MS_PER_DAY,
        );
        statusColor = calendarLevelColor(theme, getExpiryLevel(diffDays));
    }

    return (
        <Drawer
            anchor="bottom"
            open={!!detail}
            onClose={onClose}
            data-testid="calendar-item-details-sheet"
            slotProps={{
                paper: {
                    sx: { borderTopLeftRadius: 16, borderTopRightRadius: 16 },
                },
            }}
        >
            {detail && (
                <Box sx={{ p: 2, maxWidth: 600, mx: "auto", width: "100%" }}>
                    <Typography
                        variant="h6"
                        data-testid="calendar-item-details-title"
                    >
                        {detail.title}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                        {detail.inventoryName}
                    </Typography>
                    <Stack
                        direction="row"
                        spacing={1}
                        sx={{ alignItems: "center", mt: 2 }}
                    >
                        <Typography variant="body1">
                            {formatLocalDate(detail.expiryDate)}
                        </Typography>
                        {info?.humanReadable && (
                            <Chip
                                size="small"
                                label={info.humanReadable}
                                sx={{
                                    bgcolor: statusColor,
                                    color: theme.palette.getContrastText(
                                        statusColor,
                                    ),
                                }}
                            />
                        )}
                    </Stack>
                </Box>
            )}
        </Drawer>
    );
};
