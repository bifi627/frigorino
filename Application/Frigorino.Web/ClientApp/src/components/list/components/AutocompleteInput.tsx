import {
    Autocomplete,
    Box,
    Chip,
    createFilterOptions,
    TextField,
    Typography,
} from "@mui/material";
import { memo, useMemo } from "react";
import { useAddInputContext } from "../hooks/useAddInputContext";
import { useAddInputStyles } from "../hooks/useAddInputStyles";
import type { AddInputItem } from "../types";

interface AutocompleteInputProps {
    onTextChange: (value: string) => void;
    onSelectOption: (option: AddInputItem) => void;
    onKeyDown: (event: React.KeyboardEvent) => void;
    inputRef: React.RefObject<HTMLInputElement | null>;
}

export const AutocompleteInput = memo(({
    onTextChange,
    onSelectOption,
    onKeyDown,
    inputRef,
}: AutocompleteInputProps) => {
    const {
        text,
        existingItems,
        editingItem,
        isEditing,
        isLoading,
        hasDuplicate,
        existingItem,
        hasText,
        existingItemStatus,
    } = useAddInputContext();

    const styles = useAddInputStyles({
        isEditing,
        hasDuplicate,
        hasText,
        existingItemStatus,
    });

    const filter = useMemo(
        () =>
            createFilterOptions<AddInputItem>({
                stringify: (option) => option.text || "",
                matchFrom: "start",
                limit: 5,
            }),
        [],
    );

    const autocompleteOptions = useMemo(
        () =>
            existingItems.filter(
                (item) => !isEditing || item.id !== editingItem?.id,
            ),
        [existingItems, isEditing, editingItem?.id],
    );

    return (
        <Box sx={{ flex: 1 }}>
            <Autocomplete
                freeSolo
                options={
                    text.trim().length >= 3
                        ? autocompleteOptions
                        : []
                }
                getOptionLabel={(option) =>
                    typeof option === "string"
                        ? option
                        : option.text || ""
                }
                filterOptions={(options, params) => {
                    if (params.inputValue.trim().length < 3) {
                        return [];
                    }
                    const filtered = filter(options, params);
                    return filtered;
                }}
                inputValue={text}
                onInputChange={(_, newInputValue) => {
                    onTextChange(newInputValue);
                }}
                onChange={(_, newValue) => {
                    if (newValue && typeof newValue !== "string") {
                        onSelectOption(newValue);
                    }
                }}
                noOptionsText={
                    text.trim().length >= 3
                        ? "No matching items"
                        : "Type at least 3 characters"
                }
                renderOption={(props, option) => (
                    <Box component="li" {...props} key={option.id}>
                        <Box
                            sx={{
                                display: "flex",
                                flexDirection: "column",
                                width: "100%",
                            }}
                        >
                            <Typography variant="body2">
                                {option.text}
                                {option.status && (
                                    <Chip
                                        label="✓"
                                        size="small"
                                        color="success"
                                        variant="outlined"
                                        sx={{
                                            ml: 1,
                                            height: 16,
                                            fontSize: "0.7rem",
                                        }}
                                    />
                                )}
                            </Typography>
                            {option.secondaryText && (
                                <Typography
                                    variant="caption"
                                    color="text.secondary"
                                >
                                    {option.secondaryText}
                                </Typography>
                            )}
                        </Box>
                    </Box>
                )}
                renderInput={(params) => (
                    <TextField
                        {...params}
                        fullWidth
                        variant="outlined"
                        placeholder={
                            isEditing
                                ? "Bearbeite Artikel..."
                                : "Füge Artikel hinzu..."
                        }
                        disabled={isLoading}
                        inputRef={inputRef}
                        error={hasDuplicate}
                        helperText={
                            hasDuplicate && existingItem
                                ? `"${existingItem.text}" already exists${existingItem.status ? " (completed)" : ""}`
                                : undefined
                        }
                        onKeyDown={onKeyDown}
                        slotProps={{
                            input: {
                                ...params.InputProps,
                                sx: styles.inputContainerStyles,
                            },
                        }}
                        sx={styles.textFieldStyles}
                    />
                )}
                sx={styles.autocompleteStyles}
            />
        </Box>
    );
});

AutocompleteInput.displayName = "AutocompleteInput";