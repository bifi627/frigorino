export interface ExpiryInfo {
    humanReadable: string;
    color: "success" | "warning" | "error" | "info";
    isOverdue: boolean;
}

export function getExpiryInfo(
    expiryDate: string,
    t: (key: string) => string,
): ExpiryInfo {
    const now = new Date();
    const expiry = new Date(expiryDate);
    const diffMs = expiry.getTime() - now.getTime();
    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));

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
    const now = new Date();
    const expiry = new Date(expiryDate);
    const diffMs = expiry.getTime() - now.getTime();
    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));

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
