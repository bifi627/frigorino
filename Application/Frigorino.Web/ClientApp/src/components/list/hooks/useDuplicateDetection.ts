import { useMemo } from "react";
import type { ListItem } from "../types";

interface UseDuplicateDetectionProps {
    text: string;
    existingItems: ListItem[];
    isEditing: boolean;
    editingItem?: ListItem;
}

export const useDuplicateDetection = ({
    text,
    existingItems,
    isEditing,
    editingItem,
}: UseDuplicateDetectionProps) => {
    const existingItem = useMemo(
        () =>
            text.trim().length >= 3
                ? existingItems.find(
                      (item) =>
                          item.text?.toLowerCase() ===
                              text.trim().toLowerCase() &&
                          (!isEditing || item.id !== editingItem?.id),
                  )
                : null,
        [text, existingItems, isEditing, editingItem?.id],
    );

    const hasDuplicate = Boolean(existingItem && text.trim().length >= 3);

    const checkForDuplicate = (trimmedText: string): ListItem | undefined => {
        return existingItems.find(
            (item) =>
                item.text?.toLowerCase() === trimmedText.toLowerCase() &&
                (!isEditing || item.id !== editingItem?.id),
        );
    };

    return {
        existingItem,
        hasDuplicate,
        checkForDuplicate,
    };
};
