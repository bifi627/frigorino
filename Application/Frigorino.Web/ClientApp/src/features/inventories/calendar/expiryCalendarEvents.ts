import type { ExpiryCalendarItemResponse } from "../../../lib/api";
import {
    CALENDAR_WINDOW_DAYS,
    getExpiryLevel,
    parseLocalDate,
    type ExpiryLevel,
} from "../../../utils/dateUtils";

export interface ExpiryEventProps {
    itemId: number;
    inventoryId: number;
    inventoryName: string;
    expiryDate: string;
    activeToday: boolean;
}

export interface ExpiryCalendarEvent {
    id: string;
    title: string;
    start: string; // YYYY-MM-DD, inclusive
    end: string; // YYYY-MM-DD, EXCLUSIVE (FullCalendar all-day convention)
    allDay: true;
    backgroundColor: string;
    borderColor: string;
    extendedProps: ExpiryEventProps;
}

const MS_PER_DAY = 1000 * 60 * 60 * 24;

function toIsoDate(date: Date): string {
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function addDays(date: Date, days: number): Date {
    const copy = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    copy.setDate(copy.getDate() + days);
    return copy;
}

function wholeDayDiff(from: Date, to: Date): number {
    return Math.round((to.getTime() - from.getTime()) / MS_PER_DAY);
}

// Maps inventory items (each with an expiry) to all-day cook-by bars spanning
// [expiry - CALENDAR_WINDOW_DAYS, expiry]. FullCalendar's all-day `end` is EXCLUSIVE, so
// end = expiry + 1 makes the bar cover the expiry day itself. `levelColor` is injected by the
// page so the bar color stays in sync with the MUI theme palette. `todayIso` is passed in
// (not read from the clock here) so the mapping is pure and deterministic.
export function buildExpiryEvents(
    items: ExpiryCalendarItemResponse[],
    todayIso: string,
    levelColor: (level: ExpiryLevel) => string,
): ExpiryCalendarEvent[] {
    const today = parseLocalDate(todayIso);
    return items.map((item) => {
        const expiry = parseLocalDate(item.expiryDate);
        const level = getExpiryLevel(wholeDayDiff(today, expiry));
        const color = levelColor(level);
        const windowStart = addDays(expiry, -CALENDAR_WINDOW_DAYS);
        const activeToday =
            wholeDayDiff(windowStart, today) >= 0 &&
            wholeDayDiff(today, expiry) >= 0;
        return {
            id: String(item.id),
            title: item.text,
            start: toIsoDate(windowStart),
            end: toIsoDate(addDays(expiry, 1)),
            allDay: true,
            backgroundColor: color,
            borderColor: color,
            extendedProps: {
                itemId: item.id,
                inventoryId: item.inventoryId,
                inventoryName: item.inventoryName,
                expiryDate: item.expiryDate,
                activeToday,
            },
        };
    });
}
