export interface ExpiryInfo {
    humanReadable: string;
    color: "success" | "warning" | "error";
    isOverdue: boolean;
}

// Escalation levels for an expiry date. One source of truth for coloring across the app
// (chips, highlight bars) and for deciding what the overview surfaces.
export type ExpiryLevel = "expired" | "critical" | "soon" | "fresh";

// Band boundaries in whole days until expiry (negative diff = already expired).
//   diff < 0        → expired   (red)
//   0..critical     → critical  (red)
//   critical+1..soon→ soon      (amber)
//   > soon          → fresh     (green)
export const EXPIRY_THRESHOLDS = {
    critical: 3,
    soon: 7,
} as const;

// Level → MUI palette color name (used by ExpiryInfo.color / chip color props).
const LEVEL_COLOR: Record<ExpiryLevel, ExpiryInfo["color"]> = {
    expired: "error",
    critical: "error",
    soon: "warning",
    fresh: "success",
};

// Level → theme color path (used where a raw sx color string is needed).
const LEVEL_THEME_COLOR: Record<ExpiryLevel, string> = {
    expired: "error.main",
    critical: "error.main",
    soon: "warning.main",
    fresh: "success.main",
};

export function getExpiryLevel(diffDays: number): ExpiryLevel {
    if (diffDays < 0) {
        return "expired";
    }
    if (diffDays <= EXPIRY_THRESHOLDS.critical) {
        return "critical";
    }
    if (diffDays <= EXPIRY_THRESHOLDS.soon) {
        return "soon";
    }
    return "fresh";
}

const MS_PER_DAY = 1000 * 60 * 60 * 24;

// Expiry is a calendar date ("YYYY-MM-DD"), not an instant. Parse it into a LOCAL date so the
// day never shifts via UTC. `new Date("YYYY-MM-DD")` would parse as UTC midnight and render the
// wrong day in non-UTC timezones.
export function parseLocalDate(value: string): Date {
    const [y, m, d] = value.split("-").map(Number);
    return new Date(y, m - 1, d);
}

// Locale-formatted display of an expiry calendar date, parsed in local time.
export function formatLocalDate(value: string): string {
    return parseLocalDate(value).toLocaleDateString();
}

// Today as a "YYYY-MM-DD" string in LOCAL time (matches what an <input type="date"> emits).
export function todayIsoDate(): string {
    const now = new Date();
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

// Whole-day difference between an expiry calendar date and today, both anchored at local midnight.
// Math.round absorbs the ±1h DST wobble so boundaries land on exact day counts.
function diffInDays(expiryDate: string): number {
    const expiry = parseLocalDate(expiryDate);
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    return Math.round((expiry.getTime() - today.getTime()) / MS_PER_DAY);
}

export function getExpiryInfo(
    expiryDate: string,
    t: (key: string) => string,
): ExpiryInfo {
    const diffDays = diffInDays(expiryDate);
    const level = getExpiryLevel(diffDays);
    const color = LEVEL_COLOR[level];

    // Past dates
    if (diffDays < 0) {
        const overdueDays = Math.abs(diffDays);
        if (overdueDays === 1) {
            return {
                humanReadable: `${t("common.expired")} ${t("common.yesterday")}`,
                color,
                isOverdue: true,
            };
        } else if (overdueDays < 7) {
            return {
                humanReadable: `${t("common.expired")} ${t("common.ago")} ${overdueDays} ${t("common.days")}`,
                color,
                isOverdue: true,
            };
        } else {
            const overdueWeeks = Math.round(overdueDays / 7);
            return {
                humanReadable: `${t("common.expired")} ${t("common.ago")} ${overdueWeeks} ${overdueWeeks > 1 ? t("common.weeks") : t("common.week")}`,
                color,
                isOverdue: true,
            };
        }
    }

    // Future dates
    if (diffDays === 0) {
        return {
            humanReadable: `${t("common.expires")} ${t("common.today")}`,
            color,
            isOverdue: false,
        };
    } else if (diffDays === 1) {
        return {
            humanReadable: `${t("common.expires")} ${t("common.tomorrow")}`,
            color,
            isOverdue: false,
        };
    } else if (diffDays < 7) {
        return {
            humanReadable: `${t("common.expires")} ${t("common.in")} ${diffDays} ${t("common.days")}`,
            color,
            isOverdue: false,
        };
    } else if (diffDays < 30) {
        const weeks = Math.round(diffDays / 7);
        return {
            humanReadable: `${t("common.expires")} ${t("common.in")} ${weeks} ${weeks > 1 ? t("common.weeks") : t("common.week")}`,
            color,
            isOverdue: false,
        };
    }

    // More than 30 days - relative weeks get noisy; the caller falls back to the date.
    return { humanReadable: "", color, isOverdue: false };
}

export function getExpiryColor(expiryDate: string) {
    return LEVEL_THEME_COLOR[getExpiryLevel(diffInDays(expiryDate))];
}
