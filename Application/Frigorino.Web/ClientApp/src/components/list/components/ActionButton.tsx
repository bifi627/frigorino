import { Send } from "@mui/icons-material";
import { IconButton } from "@mui/material";
import { memo } from "react";
import { useAddInputContext } from "../hooks/useAddInputContext";
import { useAddInputStyles } from "../hooks/useAddInputStyles";

interface ActionButtonProps {
    onSubmit: () => void;
}

export const ActionButton = memo(({ onSubmit }: ActionButtonProps) => {
    const {
        hasText,
        isLoading,
        isEditing,
        hasDuplicate,
        existingItem,
        existingItemStatus,
    } = useAddInputContext();

    const styles = useAddInputStyles({
        isEditing,
        hasDuplicate,
        hasText,
        existingItemStatus,
    });

    return (
        <IconButton
            onClick={onSubmit}
            disabled={!hasText || isLoading}
            color={styles.getActionButtonColor()}
            size="medium"
            sx={styles.actionButtonStyles}
            title={styles.getActionButtonTitle(existingItem?.text || undefined)}
        >
            <Send />
        </IconButton>
    );
});

ActionButton.displayName = "ActionButton";
