export interface ExpiryInfo {
    humanReadable: string;
    color: "success" | "warning" | "error" | "info";
    isOverdue: boolean;
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

    // Past dates
    if (diffDays < 0) {
        const overdueDays = Math.abs(diffDays);
        if (overdueDays === 1) {
            return {
                humanReadable: `${t("common.expired")} ${t("common.yesterday")}`,
                color: "error",
                isOverdue: true,
            };
        } else if (overdueDays < 7) {
            return {
                humanReadable: `${t("common.expired")} ${t("common.ago")} ${overdueDays} ${overdueDays === 1 ? t("common.day") : t("common.days")}`,
                color: "error",
                isOverdue: true,
            };
        } else {
            const overdueWeeks = Math.round(overdueDays / 7);
            return {
                humanReadable: `${t("common.expired")} ${t("common.ago")} ${overdueWeeks} ${overdueWeeks > 1 ? t("common.weeks") : t("common.week")}`,
                color: "error",
                isOverdue: true,
            };
        }
    }

    // Future dates
    if (diffDays === 0) {
        return {
            humanReadable: `${t("common.expires")} ${t("common.today")}`,
            color: "error",
            isOverdue: false,
        };
    } else if (diffDays === 1) {
        return {
            humanReadable: `${t("common.expires")} ${t("common.tomorrow")}`,
            color: "error",
            isOverdue: false,
        };
    } else if (diffDays < 2) {
        return {
            humanReadable: `${t("common.expires")} ${t("common.in")} 1 ${t("common.day")}`,
            color: "error",
            isOverdue: false,
        };
    } else if (diffDays < 7) {
        return {
            humanReadable: `${t("common.expires")} ${t("common.in")} ${diffDays} ${t("common.days")}`,
            color: "error",
            isOverdue: false,
        };
    } else if (diffDays < 14) {
        const weeks = Math.round(diffDays / 7);
        return {
            humanReadable: `${t("common.expires")} ${t("common.in")} ${weeks} ${weeks > 1 ? t("common.weeks") : t("common.week")}`,
            color: "warning",
            isOverdue: false,
        };
    } else if (diffDays < 30) {
        const weeks = Math.round(diffDays / 7);
        return {
            humanReadable: `${t("common.expires")} ${t("common.in")} ${weeks} ${t("common.weeks")}`,
            color: "info",
            isOverdue: false,
        };
    }

    // More than 30 days - don't show relative time
    return { humanReadable: "", color: "success", isOverdue: false };
}

export function getExpiryColor(expiryDate: string) {
    const diffDays = diffInDays(expiryDate);

    if (diffDays < 2) {
        return "error.main"; // Red: < 2 days
    } else if (diffDays < 7) {
        return "warning.main"; // Orange: < 1 week
    } else if (diffDays < 14) {
        return "#FFD700"; // Yellow: < 2 weeks
    } else {
        return "success.main"; // Green: > 2 weeks
    }
}
