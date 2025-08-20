import { useMemo } from "react";
import type { AddInputItem } from "../types";

interface UseDuplicateDetectionProps {
    text: string;
    existingItems: AddInputItem[];
    isEditing: boolean;
    editingItem?: AddInputItem;
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

    const checkForDuplicate = (trimmedText: string): AddInputItem | undefined => {
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