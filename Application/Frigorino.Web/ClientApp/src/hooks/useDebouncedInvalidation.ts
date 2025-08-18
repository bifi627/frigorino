import { QueryClient, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect } from "react";

// Shared debouncer for all query invalidations
class QueryInvalidationDebouncer {
    private timeouts = new Map<string, NodeJS.Timeout>();

    debounce(
        queryClient: QueryClient,
        queryKey: readonly unknown[],
        delay = 500,
    ) {
        const keyString = JSON.stringify(queryKey);

        // Clear existing timeout for this query key
        const existingTimeout = this.timeouts.get(keyString);
        if (existingTimeout) {
            clearTimeout(existingTimeout);
        }

        // Set new timeout
        const timeout = setTimeout(() => {
            queryClient.invalidateQueries({ queryKey });
            this.timeouts.delete(keyString);
        }, delay);

        this.timeouts.set(keyString, timeout);
    }

    clear() {
        this.timeouts.forEach((timeout) => clearTimeout(timeout));
        this.timeouts.clear();
    }
}

// Global instance
const queryDebouncer = new QueryInvalidationDebouncer();

/**
 * Custom hook that provides a debounced query invalidation function.
 * This helps prevent excessive API calls when multiple mutations happen in quick succession.
 *
 * @returns A debounced invalidation function that accepts a query key and optional delay
 */
export const useDebouncedInvalidation = () => {
    const queryClient = useQueryClient();

    const debouncedInvalidate = useCallback(
        (queryKey: readonly unknown[], delay = 1000) => {
            queryDebouncer.debounce(queryClient, queryKey, delay);
        },
        [queryClient],
    );

    // Cleanup timeouts on unmount
    useEffect(() => {
        return () => {
            queryDebouncer.clear();
        };
    }, []);

    return debouncedInvalidate;
};
