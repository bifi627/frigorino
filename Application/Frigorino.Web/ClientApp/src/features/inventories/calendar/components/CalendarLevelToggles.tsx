import { Chip, Stack, useTheme } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ExpiryLevel } from "../../../../utils/dateUtils";
import { calendarLevelColor } from "../calendarColors";
import { useCalendarViewSettings } from "../calendarViewSettings";

const LEVEL_KEYS: ExpiryLevel[] = ["expired", "critical", "soon", "fresh"];

// Always-visible per-level filter chips, shown between the page header and the calendar so the
// user can toggle urgency bands without opening the settings sheet. Chip color follows the
// calendar's level palette (expired = magenta) so the control reads as "what this shows/hides".
export const CalendarLevelToggles = () => {
    const theme = useTheme();
    const { t } = useTranslation();
    const levels = useCalendarViewSettings((s) => s.levels);
    const toggleLevel = useCalendarViewSettings((s) => s.toggleLevel);

    return (
        <Stack
            direction="row"
            data-testid="calendar-level-toggles"
            sx={{ flexWrap: "wrap", gap: 1, mb: 2 }}
        >
            {LEVEL_KEYS.map((lvl) => {
                const active = levels[lvl];
                const color = calendarLevelColor(theme, lvl);
                return (
                    <Chip
                        key={lvl}
                        label={t(`inventory.calendar.settings.level.${lvl}`)}
                        size="small"
                        variant={active ? "filled" : "outlined"}
                        onClick={() => toggleLevel(lvl)}
                        data-testid={`calendar-level-${lvl}`}
                        data-active={active ? "true" : "false"}
                        sx={
                            active
                                ? {
                                      bgcolor: color,
                                      color: theme.palette.getContrastText(
                                          color,
                                      ),
                                      "&:hover": { bgcolor: color },
                                  }
                                : { color, borderColor: color }
                        }
                    />
                );
            })}
        </Stack>
    );
};
