import { useCallback } from "react";
import type { AddInputItem } from "../types";

interface UseSubmitHandlerProps {
    text: string;
    isEditing: boolean;
    onAdd: (data: string) => void;
    onUpdate?: (data: string) => void;
    onUncheckExisting?: (itemId: number) => void;
    clearText: () => void;
    focusInput: () => void;
    checkForDuplicate: (trimmedText: string) => AddInputItem | undefined;
}

export const useSubmitHandler = ({
    text,
    isEditing,
    onAdd,
    onUpdate,
    onUncheckExisting,
    clearText,
    focusInput,
    checkForDuplicate,
}: UseSubmitHandlerProps) => {
    const handleSubmit = useCallback(() => {
        const trimmedText = text.trim();
        if (!trimmedText) return;

        const existingItem = checkForDuplicate(trimmedText);

        if (existingItem && existingItem.id) {
            if (!isEditing && existingItem.status && onUncheckExisting) {
                onUncheckExisting(existingItem.id);
                clearText();
                requestAnimationFrame(() => {
                    focusInput();
                });
                return;
            } else if (!isEditing || (isEditing && existingItem.status)) {
                alert(
                    `"${trimmedText}" already exists in your list${existingItem.status ? " and is checked" : ""}.`,
                );
                return;
            }
        }

        if (isEditing && onUpdate) {
            onUpdate(trimmedText);
        } else {
            onAdd(trimmedText);
        }

        clearText();

        requestAnimationFrame(() => {
            focusInput();
        });

        setTimeout(() => {
            focusInput();
        }, 10);
    }, [
        text,
        isEditing,
        onAdd,
        onUpdate,
        onUncheckExisting,
        clearText,
        focusInput,
        checkForDuplicate,
    ]);

    const handleKeyDown = useCallback(
        (event: React.KeyboardEvent) => {
            if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                handleSubmit();
            }
        },
        [handleSubmit],
    );

    return {
        handleSubmit,
        handleKeyDown,
    };
};