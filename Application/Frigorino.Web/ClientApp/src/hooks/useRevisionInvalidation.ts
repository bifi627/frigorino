import { useQueryClient, type QueryKey } from "@tanstack/react-query";
import { useEffect, useLayoutEffect, useRef } from "react";

// Shared focus-gating config for every /revision poll. Spread into the useQuery call alongside the
// generated getXRevisionOptions. 2s cadence (chosen with the user); polling stops entirely while the
// tab is backgrounded (keeps Railway's idle-sleep intact) and resumes instantly on focus.
export const REVISION_QUERY_OPTIONS = {
    refetchInterval: 2000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
    staleTime: 0,
} as const;

interface UseRevisionInvalidationParams {
    // The opaque token from the latest successful poll (undefined until the first poll resolves).
    rev: string | undefined;
    // The real data query to invalidate when the token moves (built via getXQueryKey).
    dataQueryKey: QueryKey;
    // True when a LOCAL mutation targeting this resource is in flight. Its own optimistic update plus
    // onSettled invalidation are authoritative, so we must NOT clobber it with a remote refetch.
    isLocalMutation: (variables: unknown) => boolean;
    // Whether to advance the baseline when an invalidation is suppressed by an in-flight local
    // mutation. Default true: safe when the suppressing mutation re-invalidates THIS SAME data query
    // on settle (list/inventory items), so a coincident remote change is picked up for free and
    // advancing avoids a redundant double-fetch. The calendar passes false: an inventory-item
    // mutation re-invalidates the inventory-items query, NOT the calendar query, so advancing here
    // would drop a coincident remote calendar change. Keeping the baseline lets the next tick (after
    // the local mutation settles) catch it, at the cost of at most one redundant calendar refetch.
    advanceBaselineWhenSuppressed?: boolean;
}

// Compares each incoming opaque revision token to the last one seen. On a change it invalidates the
// real data query (one full refetch) UNLESS a local mutation is in flight — in which case it skips the
// refetch but STILL advances the baseline, so the next tick doesn't re-detect the same change and fire
// a redundant fetch once the local mutation settles.
export const useRevisionInvalidation = ({
    rev,
    dataQueryKey,
    isLocalMutation,
    advanceBaselineWhenSuppressed = true,
}: UseRevisionInvalidationParams) => {
    const queryClient = useQueryClient();
    const lastRev = useRef<string | null>(null);
    // Keep the latest predicate without making it an effect dependency (it's recreated each render).
    const isLocalMutationRef = useRef(isLocalMutation);
    // Sync the ref before effects run. useLayoutEffect fires synchronously after DOM mutations but
    // before the useEffect below, guaranteeing the predicate is current when the effect reads it.
    useLayoutEffect(() => {
        isLocalMutationRef.current = isLocalMutation;
    });

    useEffect(() => {
        if (rev == null) {
            return;
        }
        // First successful poll: adopt as the baseline. The data query already holds fresh data from
        // its own mount fetch, so there is nothing to invalidate yet.
        if (lastRev.current === null) {
            lastRev.current = rev;
            return;
        }
        if (rev === lastRev.current) {
            return;
        }

        const localMutating =
            queryClient.isMutating({
                predicate: (m) => isLocalMutationRef.current(m.state.variables),
            }) > 0;
        if (!localMutating) {
            queryClient.invalidateQueries({ queryKey: dataQueryKey });
        }
        // Advance the baseline when we invalidated, or when the caller opts to advance even on
        // suppression (the default — prevents a redundant double-fetch once a local mutation settles
        // for queries whose own mutation re-invalidates them). When suppressed AND the caller opted
        // out (calendar), keep the baseline so the next tick re-detects the change and catches up.
        if (!localMutating || advanceBaselineWhenSuppressed) {
            lastRev.current = rev;
        }
        // dataQueryKey is a fresh array each render; when rev is unchanged the early-return above makes
        // the re-run a no-op, so it is safe to depend on.
    }, [rev, queryClient, dataQueryKey, advanceBaselineWhenSuppressed]);
};
