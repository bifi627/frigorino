import { useMemo, type ReactNode } from "react";
import type {
    DuplicateConfig,
    DuplicateResult,
    Suggestion,
    SuggestionsConfig,
} from "../components/composer";

/** Minimal item shape the add-item footers rely on. */
export interface ComposerItem {
    id: number;
    text: string;
}

interface UseItemComposerArgs<TItem extends ComposerItem> {
    editingItem: TItem | null;
    existingItems: TItem[];
    /** Optional trailing badge for a suggestion row. */
    getBadge?: (item: TItem) => ReactNode;
    /** Optional secondary label shown in suggestion rows (e.g. formatted quantity). */
    getSecondaryLabel?: (item: TItem) => string | undefined;
    /** Builds the inline message/action when an exact-name match is found. */
    onDuplicate: (match: TItem) => DuplicateResult;
}

/**
 * Shared wiring for the list/inventory add-item footers: type-ahead suggestions
 * and exact-name duplicate detection. Both exclude the item being edited; the
 * duplicate check ignores 1–2 char input to match the autocomplete minChars.
 */
export function useItemComposer<TItem extends ComposerItem>({
    editingItem,
    existingItems,
    getBadge,
    getSecondaryLabel,
    onDuplicate,
}: UseItemComposerArgs<TItem>): {
    suggestions: SuggestionsConfig;
    duplicate: DuplicateConfig;
} {
    const editingId = editingItem?.id;

    const suggestions = useMemo<SuggestionsConfig>(
        () => ({
            getItems: (query) => {
                const q = query.trim().toLowerCase();
                return existingItems
                    .filter(
                        (item) =>
                            item.id !== editingId &&
                            item.text.toLowerCase().includes(q),
                    )
                    .map(
                        (item): Suggestion => ({
                            id: item.id,
                            label: item.text,
                            secondaryLabel: getSecondaryLabel?.(item),
                            badge: getBadge?.(item),
                        }),
                    );
            },
        }),
        [existingItems, editingId, getBadge, getSecondaryLabel],
    );

    const duplicate = useMemo<DuplicateConfig>(
        () => ({
            check: (text) => {
                const needle = text.trim().toLowerCase();
                if (needle.length < 3) {
                    return null;
                }
                const match = existingItems.find(
                    (item) =>
                        item.text.toLowerCase() === needle &&
                        item.id !== editingId,
                );
                return match ? onDuplicate(match) : null;
            },
        }),
        [existingItems, editingId, onDuplicate],
    );

    return { suggestions, duplicate };
}
