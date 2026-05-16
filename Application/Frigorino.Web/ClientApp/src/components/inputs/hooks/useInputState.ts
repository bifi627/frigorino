import { useCallback, useEffect, useRef, useState } from "react";
import type { ListItem } from "../types";

interface UseInputStateProps {
    editingItem?: ListItem;
    isLoading: boolean;
    onClearText: () => void;
}

export const useInputState = ({
    editingItem,
    isLoading,
    onClearText,
}: UseInputStateProps) => {
    const [text, setText] = useState("");
    const inputRef = useRef<HTMLInputElement>(null);

    const focusInput = useCallback(() => {
        if (inputRef.current && !isLoading) {
            inputRef.current.focus();
        }
    }, [isLoading]);

    useEffect(() => {
        if (editingItem) {
            setText(editingItem.text || "");
        } else {
            setText("");
        }
    }, [editingItem]);

    const handleTextChange = useCallback((newText: string) => {
        setText(newText);
    }, []);

    const clearText = useCallback(() => {
        setText("");
        onClearText();
    }, [onClearText]);

    return {
        text,
        setText: handleTextChange,
        clearText,
        inputRef,
        focusInput,
    };
};
