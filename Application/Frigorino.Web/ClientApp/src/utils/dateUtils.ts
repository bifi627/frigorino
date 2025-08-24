export interface ExpiryInfo {
    humanReadable: string;
    color: 'success' | 'warning' | 'error' | 'info';
    isOverdue: boolean;
}

export function getExpiryInfo(expiryDate: string): ExpiryInfo {
    const now = new Date();
    const expiry = new Date(expiryDate);
    const diffMs = expiry.getTime() - now.getTime();
    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));

    // Past dates
    if (diffDays < 0) {
        const overdueDays = Math.abs(diffDays);
        if (overdueDays === 1) {
            return { humanReadable: "expired yesterday", color: "error", isOverdue: true };
        } else if (overdueDays < 7) {
            return { humanReadable: `expired ${overdueDays} days ago`, color: "error", isOverdue: true };
        } else {
            const overdueWeeks = Math.round(overdueDays / 7);
            return { humanReadable: `expired ${overdueWeeks} week${overdueWeeks > 1 ? 's' : ''} ago`, color: "error", isOverdue: true };
        }
    }

    // Future dates
    if (diffDays === 0) {
        return { humanReadable: "expires today", color: "error", isOverdue: false };
    } else if (diffDays === 1) {
        return { humanReadable: "expires tomorrow", color: "error", isOverdue: false };
    } else if (diffDays < 2) {
        return { humanReadable: "expires in 1 day", color: "error", isOverdue: false };
    } else if (diffDays < 7) {
        return { humanReadable: `expires in ${diffDays} days`, color: "error", isOverdue: false };
    } else if (diffDays < 14) {
        const weeks = Math.round(diffDays / 7);
        return { humanReadable: `expires in ${weeks} week${weeks > 1 ? 's' : ''}`, color: "warning", isOverdue: false };
    } else if (diffDays < 30) {
        const weeks = Math.round(diffDays / 7);
        return { humanReadable: `expires in ${weeks} weeks`, color: "info", isOverdue: false };
    }

    // More than 30 days - don't show relative time
    return { humanReadable: "", color: "success", isOverdue: false };
}

export function getExpiryColor(expiryDate: string): 'success.main' | 'info.main' | 'warning.main' | 'error.main' {
    const now = new Date();
    const expiry = new Date(expiryDate);
    const diffMs = expiry.getTime() - now.getTime();
    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays < 2) {
        return 'error.main'; // Red: < 2 days
    } else if (diffDays < 7) {
        return 'warning.main'; // Orange: < 1 week
    } else if (diffDays < 14) {
        return '#FFD700'; // Yellow: < 2 weeks
    } else {
        return 'success.main'; // Green: > 2 weeks
    }
}