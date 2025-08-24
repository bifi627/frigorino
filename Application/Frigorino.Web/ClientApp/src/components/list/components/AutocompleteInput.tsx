import {
    Autocomplete,
    Box,
    Chip,
    createFilterOptions,
    TextField,
    Typography,
} from "@mui/material";
import { memo, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { useAddInputContext } from "../hooks/useAddInputContext";
import { useAddInputStyles } from "../hooks/useAddInputStyles";
import type { ListItem } from "../types";

interface AutocompleteInputProps {
    onTextChange: (value: string) => void;
    onSelectOption: (option: ListItem) => void;
    onKeyDown: (event: React.KeyboardEvent) => void;
    inputRef: React.RefObject<HTMLInputElement | null>;
}

export const AutocompleteInput = memo(
    ({
        onTextChange,
        onSelectOption,
        onKeyDown,
        inputRef,
    }: AutocompleteInputProps) => {
        const { t } = useTranslation();
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
                createFilterOptions<ListItem>({
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
                    options={text.trim().length >= 3 ? autocompleteOptions : []}
                    getOptionLabel={(option) =>
                        typeof option === "string" ? option : option.text || ""
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
                            ? t("common.noMatchingItems")
                            : t("common.typeAtLeastCharacters")
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
                                    ? t("common.editItem")
                                    : t("common.addItemPlaceholder")
                            }
                            disabled={isLoading}
                            inputRef={inputRef}
                            error={hasDuplicate}
                            helperText={
                                hasDuplicate && existingItem
                                    ? `"${existingItem.text}" ${t("common.alreadyExists")}${existingItem.status ? ` (${t("common.completed")})` : ""}`
                                    : undefined
                            }
                            onKeyDown={(event) => {
                                // Handle Enter key specially to avoid Autocomplete interference
                                if (event.key === "Enter" && !event.shiftKey) {
                                    event.preventDefault();
                                    event.stopPropagation();
                                    onKeyDown(event);
                                    return;
                                }
                                onKeyDown(event);
                            }}
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
    },
);

AutocompleteInput.displayName = "AutocompleteInput";
