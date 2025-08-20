import { Delete } from "@mui/icons-material";
import { Box, IconButton, Paper } from "@mui/material";
import { memo } from "react";
import { ActionButton } from "./components/ActionButton";
import { AutocompleteInput } from "./components/AutocompleteInput";
import { EditingHeader } from "./components/EditingHeader";
import { AddInputProvider } from "./context/AddInputContext";
import { useAddInputStyles } from "./hooks/useAddInputStyles";
import { useDuplicateDetection } from "./hooks/useDuplicateDetection";
import { useInputState } from "./hooks/useInputState";
import { useSubmitHandler } from "./hooks/useSubmitHandler";
import type { AddInputItem, AddInputProps } from "./types";

export const AddInput = memo(
    ({
        onAdd,
        onUpdate,
        onCancelEdit,
        onUncheckExisting,
        editingItem = undefined,
        existingItems = [],
        isLoading = false,
        topPanels = [],
        bottomPanels = [],
        rightControls = [],
    }: AddInputProps) => {
        const isEditing = Boolean(editingItem);

        const { text, setText, clearText, inputRef, focusInput } = useInputState({
            editingItem,
            isLoading,
        });

        const { existingItem, hasDuplicate, checkForDuplicate } = useDuplicateDetection({
            text,
            existingItems,
            isEditing,
            editingItem,
        });

        const { handleSubmit, handleKeyDown } = useSubmitHandler({
            text,
            isEditing,
            onAdd,
            onUpdate,
            onUncheckExisting,
            clearText,
            focusInput,
            checkForDuplicate,
        });

        const styles = useAddInputStyles({
            isEditing,
            hasDuplicate,
            hasText: Boolean(text.trim()),
            existingItemStatus: existingItem?.status,
        });

        const handleCancel = () => {
            clearText();
            if (onCancelEdit) {
                onCancelEdit();
            }
            focusInput();
        };

        const handleDiscard = () => {
            clearText();
            focusInput();
        };

        const handleContainerClick = (event: React.MouseEvent) => {
            const target = event.target as HTMLElement;
            if (target.closest(".panel-section")) {
                return;
            }
            focusInput();
        };

        const handleSelectOption = (option: AddInputItem) => {
            setText(option.text || "");
        };

        const contextValue = {
            text,
            editingItem,
            existingItems,
            isLoading,
            isEditing,
            hasDuplicate,
            hasText: Boolean(text.trim()),
            existingItem,
            existingItemStatus: existingItem?.status,
        };

        return (
            <AddInputProvider value={contextValue}>
                <Paper
                    elevation={3}
                    onClick={handleContainerClick}
                    sx={styles.containerStyles}
                >
                {isEditing && editingItem && (
                    <EditingHeader
                        editingItem={editingItem}
                        onCancel={handleCancel}
                    />
                )}

                {/* Top Panels */}
                {topPanels.length > 0 && (
                    <Box className="panel-section" sx={{ mb: 1 }}>
                        {topPanels.map((panel, index) => (
                            <Box
                                key={index}
                                sx={{
                                    mb: index < topPanels.length - 1 ? 1 : 0,
                                }}
                            >
                                {panel}
                            </Box>
                        ))}
                    </Box>
                )}

                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    {text.trim() && !isEditing && (
                        <IconButton
                            onClick={handleDiscard}
                            size="small"
                            sx={styles.discardButtonStyles}
                            title="Discard input"
                        >
                            <Delete fontSize="small" />
                        </IconButton>
                    )}

                    <AutocompleteInput
                        onTextChange={setText}
                        onSelectOption={handleSelectOption}
                        onKeyDown={handleKeyDown}
                        inputRef={inputRef}
                    />

                    {rightControls.map((control, index) => (
                        <Box key={index}>{control}</Box>
                    ))}

                    <ActionButton onSubmit={handleSubmit} />
                </Box>

                {/* Bottom Panels */}
                {bottomPanels.length > 0 && (
                    <Box className="panel-section" sx={{ mt: 1 }}>
                        {bottomPanels.map((panel, index) => (
                            <Box key={index} sx={{ mt: index === 0 ? 0 : 1 }}>
                                {panel}
                            </Box>
                        ))}
                    </Box>
                )}
                </Paper>
            </AddInputProvider>
        );
    },
);

// Add display name for debugging
AddInput.displayName = "AddInput";
