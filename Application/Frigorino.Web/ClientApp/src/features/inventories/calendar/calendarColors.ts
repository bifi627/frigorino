import type { Theme } from "@mui/material/styles";
import type { ExpiryLevel } from "../../../utils/dateUtils";

// Calendar-only: expired gets a distinct magenta so it doesn't read as the same red as critical.
// (App-wide expiry coloring in dateUtils still maps expired → error red; this override is scoped
// to the calendar bars + level-filter chips, where the side-by-side comparison matters.)
export const CALENDAR_EXPIRED_COLOR = "#D81B9A";

export function calendarLevelColor(theme: Theme, level: ExpiryLevel): string {
    if (level === "expired") {
        return CALENDAR_EXPIRED_COLOR;
    }
    if (level === "critical") {
        return theme.palette.error.main;
    }
    if (level === "soon") {
        return theme.palette.warning.main;
    }
    return theme.palette.success.main;
}
