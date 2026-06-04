import { Close } from "@mui/icons-material";
import {
    Box,
    Button,
    Chip,
    Drawer,
    FormControlLabel,
    IconButton,
    Slider,
    Stack,
    Switch,
    TextField,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ExpiryLevel } from "../../../../utils/dateUtils";
import {
    CALENDAR_WINDOW_MAX,
    CALENDAR_WINDOW_MIN,
    useCalendarViewSettings,
} from "../calendarViewSettings";

const LEVEL_KEYS: ExpiryLevel[] = ["expired", "critical", "soon", "fresh"];

// Chip color per level mirrors the urgency bands so the control reads as "what this shows/hides".
const LEVEL_CHIP_COLOR: Record<ExpiryLevel, "error" | "warning" | "success"> = {
    expired: "error",
    critical: "error",
    soon: "warning",
    fresh: "success",
};

interface CalendarSettingsSheetProps {
    open: boolean;
    onClose: () => void;
}

export const CalendarSettingsSheet = ({
    open,
    onClose,
}: CalendarSettingsSheetProps) => {
    const { t } = useTranslation();
    const windowDays = useCalendarViewSettings((s) => s.windowDays);
    const fullRunway = useCalendarViewSettings((s) => s.fullRunway);
    const levels = useCalendarViewSettings((s) => s.levels);
    const setWindowDays = useCalendarViewSettings((s) => s.setWindowDays);
    const setFullRunway = useCalendarViewSettings((s) => s.setFullRunway);
    const toggleLevel = useCalendarViewSettings((s) => s.toggleLevel);
    const reset = useCalendarViewSettings((s) => s.reset);

    return (
        <Drawer
            anchor="bottom"
            open={open}
            onClose={onClose}
            data-testid="calendar-settings-sheet"
            slotProps={{
                paper: {
                    sx: { borderTopLeftRadius: 16, borderTopRightRadius: 16 },
                },
            }}
        >
            <Box sx={{ p: 2, maxWidth: 600, mx: "auto", width: "100%" }}>
                <Stack
                    direction="row"
                    sx={{
                        alignItems: "center",
                        justifyContent: "space-between",
                        mb: 1,
                    }}
                >
                    <Typography variant="h6">
                        {t("inventory.calendar.settings.title")}
                    </Typography>
                    <IconButton
                        onClick={onClose}
                        size="small"
                        aria-label="close"
                        data-testid="calendar-settings-close"
                    >
                        <Close />
                    </IconButton>
                </Stack>

                <Typography variant="subtitle2" sx={{ mt: 1 }}>
                    {t("inventory.calendar.settings.windowLength")}
                </Typography>
                <Stack
                    direction="row"
                    spacing={2}
                    sx={{ alignItems: "center", mb: 1 }}
                >
                    <TextField
                        size="small"
                        type="number"
                        value={fullRunway ? "" : windowDays}
                        disabled={fullRunway}
                        onChange={(e) => setWindowDays(Number(e.target.value))}
                        slotProps={{
                            htmlInput: {
                                min: CALENDAR_WINDOW_MIN,
                                max: CALENDAR_WINDOW_MAX,
                                inputMode: "numeric",
                                "data-testid": "calendar-window-input",
                            },
                        }}
                        sx={{ width: 96 }}
                    />
                    <Typography variant="body2" color="text.secondary">
                        {t("inventory.calendar.settings.days")}
                    </Typography>
                    <FormControlLabel
                        sx={{ ml: "auto" }}
                        data-testid="calendar-fullrunway-toggle"
                        control={
                            <Switch
                                checked={fullRunway}
                                onChange={(e) =>
                                    setFullRunway(e.target.checked)
                                }
                            />
                        }
                        label={t("inventory.calendar.settings.fullRunway")}
                    />
                </Stack>
                <Slider
                    value={fullRunway ? CALENDAR_WINDOW_MAX : windowDays}
                    disabled={fullRunway}
                    min={CALENDAR_WINDOW_MIN}
                    max={CALENDAR_WINDOW_MAX}
                    onChange={(_, v) => setWindowDays(v as number)}
                    data-testid="calendar-window-slider"
                    sx={{ mb: 2 }}
                />

                <Typography variant="subtitle2">
                    {t("inventory.calendar.settings.levels")}
                </Typography>
                <Stack direction="row" sx={{ flexWrap: "wrap", gap: 1, my: 1 }}>
                    {LEVEL_KEYS.map((lvl) => (
                        <Chip
                            key={lvl}
                            label={t(
                                `inventory.calendar.settings.level.${lvl}`,
                            )}
                            color={
                                levels[lvl] ? LEVEL_CHIP_COLOR[lvl] : "default"
                            }
                            variant={levels[lvl] ? "filled" : "outlined"}
                            onClick={() => toggleLevel(lvl)}
                            data-testid={`calendar-level-${lvl}`}
                            data-active={levels[lvl] ? "true" : "false"}
                        />
                    ))}
                </Stack>

                <Button
                    fullWidth
                    onClick={reset}
                    sx={{ mt: 1 }}
                    data-testid="calendar-settings-reset"
                >
                    {t("inventory.calendar.settings.reset")}
                </Button>
            </Box>
        </Drawer>
    );
};
