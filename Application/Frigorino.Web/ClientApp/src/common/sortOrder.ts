// Client-side mirrors of the server's sort-order math. Kept in lockstep with
// SortOrderCalculator + List.ComputeAppendSortOrder + List.ReorderItem / Inventory.ReorderItem
// so optimistic updates land items in the same slot the server will.

export const DEFAULT_GAP = 10_000;
export const UNCHECKED_MIN = 1_000_000;
export const CHECKED_MIN = 10_000_000;

// Sorts a copy of `section`. Optimistic updates mutate `sortOrder` in place without
// reordering the cache array, so we can't trust array index as a sortOrder proxy.
const sortBySortOrder = <T extends { sortOrder: number }>(section: T[]): T[] =>
    [...section].sort((a, b) => a.sortOrder - b.sortOrder);

// Mirror of List.ComputeAppendSortOrder. Caller passes the section pre-filtered by
// status; this returns the slot the server would assign for an "append" into that
// section (or empty-section seed when nothing is there yet).
export function computeAppendSortOrder<T extends { sortOrder: number }>(
    section: T[],
    targetStatus: boolean,
): number {
    if (section.length === 0) {
        return (targetStatus ? CHECKED_MIN : UNCHECKED_MIN) + DEFAULT_GAP;
    }
    const sorted = sortBySortOrder(section);
    return targetStatus
        ? sorted[0].sortOrder - DEFAULT_GAP
        : sorted[sorted.length - 1].sortOrder + DEFAULT_GAP;
}

// Mirror of List.ReorderItem / Inventory.ReorderItem midpoint math.
//   afterId falsy → top: section[0].sortOrder - DEFAULT_GAP (or emptyDefault if empty)
//   no `before`   → bottom: afterItem.sortOrder + DEFAULT_GAP
//   otherwise     → midpoint between after and before
// `emptyDefault` is consulted ONLY on the empty-section branch (no afterItem AND no
// existing items). Other branches ignore it.
export function computeReorderSortOrder<
    T extends { id: number; sortOrder: number },
>({
    section,
    afterId,
    emptyDefault,
}: {
    section: T[];
    afterId: number | undefined;
    emptyDefault: number;
}): number {
    const sorted = sortBySortOrder(section);
    const afterItem = afterId
        ? sorted.find((i) => i.id === afterId)
        : undefined;
    const beforeItem = afterItem
        ? sorted.find((i) => i.sortOrder > afterItem.sortOrder)
        : undefined;

    if (!afterItem) {
        return sorted.length
            ? sorted[0].sortOrder - DEFAULT_GAP
            : emptyDefault;
    }
    if (!beforeItem) {
        return afterItem.sortOrder + DEFAULT_GAP;
    }
    return Math.floor(
        afterItem.sortOrder +
            (beforeItem.sortOrder - afterItem.sortOrder) / 2,
    );
}
