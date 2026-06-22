import { useCallback, useEffect, useState } from "react";

// localStorage-backed number, used to remember a single-select choice per device
// (e.g. the recipe-edit composer's target section). The key is global (shared across recipes).
export const usePersistedNumber = (
    key: string,
    defaultValue: number,
): [number, (value: number) => void] => {
    const [value, setValue] = useState<number>(() => {
        try {
            const stored = localStorage.getItem(key);
            if (stored === null) return defaultValue;
            const parsed = Number(stored);
            return Number.isFinite(parsed) ? parsed : defaultValue;
        } catch {
            // Private mode / storage disabled — fall back to the default in memory.
            return defaultValue;
        }
    });

    useEffect(() => {
        try {
            localStorage.setItem(key, String(value));
        } catch {
            // Ignore persistence failures; the in-memory state still works for the session.
        }
    }, [key, value]);

    const set = useCallback((next: number) => setValue(next), []);

    return [value, set];
};
