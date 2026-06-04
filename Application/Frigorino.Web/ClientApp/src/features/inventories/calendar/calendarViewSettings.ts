import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import type { ExpiryLevel } from "../../../utils/dateUtils";

// Per-level visibility. Keyed by ExpiryLevel so the four keys always match the bands in dateUtils.
export type CalendarLevelFilters = Record<ExpiryLevel, boolean>;

export interface CalendarViewSettings {
    windowDays: number; // cook-by bar length in days
    fullRunway: boolean; // true ⇒ bars span [today … expiry], ignoring windowDays
    levels: CalendarLevelFilters;
}

export const CALENDAR_WINDOW_MIN = 1;
export const CALENDAR_WINDOW_MAX = 180;

export const DEFAULT_CALENDAR_VIEW_SETTINGS: CalendarViewSettings = {
    windowDays: 30,
    fullRunway: false,
    levels: { expired: true, critical: true, soon: true, fresh: true },
};

// Keep windowDays inside [MIN, MAX] so a stale/corrupt persisted or typed value can't break the render.
const clampWindow = (n: number): number => {
    if (!Number.isFinite(n)) {
        return DEFAULT_CALENDAR_VIEW_SETTINGS.windowDays;
    }
    return Math.min(
        CALENDAR_WINDOW_MAX,
        Math.max(CALENDAR_WINDOW_MIN, Math.round(n)),
    );
};

interface CalendarViewState extends CalendarViewSettings {
    setWindowDays: (n: number) => void;
    setFullRunway: (b: boolean) => void;
    toggleLevel: (level: ExpiryLevel) => void;
    reset: () => void;
}

export const useCalendarViewSettings = create<CalendarViewState>()(
    persist(
        (set) => ({
            ...DEFAULT_CALENDAR_VIEW_SETTINGS,
            setWindowDays: (n) => set({ windowDays: clampWindow(n) }),
            setFullRunway: (b) => set({ fullRunway: b }),
            toggleLevel: (level) =>
                set((state) => ({
                    levels: { ...state.levels, [level]: !state.levels[level] },
                })),
            reset: () => set({ ...DEFAULT_CALENDAR_VIEW_SETTINGS }),
        }),
        {
            name: "frigorino-calendar-view",
            version: 1,
            storage: createJSONStorage(() => localStorage),
            // Defensive merge: an older/partial stored object falls back to defaults for missing keys,
            // and windowDays is re-clamped on hydration.
            merge: (persisted, current) => {
                const p = (persisted ?? {}) as Partial<CalendarViewSettings>;
                return {
                    ...current,
                    ...p,
                    windowDays: clampWindow(p.windowDays ?? current.windowDays),
                    levels: { ...current.levels, ...(p.levels ?? {}) },
                };
            },
        },
    ),
);
