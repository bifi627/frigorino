import { useCallback, useEffect, useState } from "react";

// localStorage-backed boolean, used to remember a collapsible section's open/closed
// state per device. The key is global (shared across recipes), so collapsing a section
// once keeps it collapsed everywhere until reopened.
export const usePersistedExpanded = (
    key: string,
    defaultExpanded: boolean,
): [boolean, (expanded: boolean) => void] => {
    const [expanded, setExpanded] = useState<boolean>(() => {
        try {
            const stored = localStorage.getItem(key);
            if (stored === null) return defaultExpanded;
            return stored === "true";
        } catch {
            // Private mode / storage disabled — fall back to the default in memory.
            return defaultExpanded;
        }
    });

    useEffect(() => {
        try {
            localStorage.setItem(key, String(expanded));
        } catch {
            // Ignore persistence failures; the in-memory state still works for the session.
        }
    }, [key, expanded]);

    const set = useCallback((next: boolean) => setExpanded(next), []);

    return [expanded, set];
};
