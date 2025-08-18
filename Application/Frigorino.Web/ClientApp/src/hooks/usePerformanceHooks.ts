import { useCallback, useRef } from "react";

/**
 * Custom hook for throttling function calls
 * Useful for performance-critical operations like drag handlers
 */
export const useThrottle = <T extends (...args: unknown[]) => unknown>(
    func: T,
    delay: number,
): T => {
    const timeoutRef = useRef<NodeJS.Timeout | null>(null);
    const lastExecRef = useRef<number>(0);

    return useCallback(
        ((...args: Parameters<T>) => {
            const now = Date.now();

            if (now - lastExecRef.current >= delay) {
                lastExecRef.current = now;
                return func(...args);
            } else {
                if (timeoutRef.current) {
                    clearTimeout(timeoutRef.current);
                }
                timeoutRef.current = setTimeout(
                    () => {
                        lastExecRef.current = Date.now();
                        func(...args);
                    },
                    delay - (now - lastExecRef.current),
                );
            }
        }) as T,
        [func, delay],
    );
};

/**
 * Custom hook for debouncing function calls
 * Useful for search inputs or expensive operations
 */
export const useDebounce = <T extends (...args: unknown[]) => unknown>(
    func: T,
    delay: number,
): T => {
    const timeoutRef = useRef<NodeJS.Timeout | null>(null);

    return useCallback(
        ((...args: Parameters<T>) => {
            if (timeoutRef.current) {
                clearTimeout(timeoutRef.current);
            }
            timeoutRef.current = setTimeout(() => func(...args), delay);
        }) as T,
        [func, delay],
    );
};

/**
 * Custom hook for optimizing drag operations
 * Combines throttling with state management for smooth drag experience
 */
export const useOptimizedDrag = () => {
    const dragStateRef = useRef({
        isDragging: false,
        activeId: null as string | null,
    });

    const startDrag = useCallback((activeId: string) => {
        dragStateRef.current = { isDragging: true, activeId };
    }, []);

    const endDrag = useCallback(() => {
        dragStateRef.current = { isDragging: false, activeId: null };
    }, []);

    const getDragState = useCallback(() => dragStateRef.current, []);

    return { startDrag, endDrag, getDragState };
};
