import { Close, Delete, Edit, Send } from "@mui/icons-material";
import {
    Autocomplete,
    Box,
    Chip,
    createFilterOptions,
    IconButton,
    Paper,
    TextField,
    Typography,
} from "@mui/material";
import { memo, useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ListItemDto } from "../../hooks/useListItemQueries";

interface InputItem {
    id?: number;
    text?: string | null;
    secondaryText?: string | null;
    status?: boolean;
}

interface AddInputProps {
    onAdd: (data: string) => void;
    onUpdate?: (data: string) => void;
    onCancelEdit?: () => void;
    onUncheckExisting?: (itemId: number) => void;
    editingItem?: InputItem;
    existingItems?: InputItem[];
    isLoading?: boolean;
    hasItems?: boolean;
    topPanels?: React.ReactNode[];
    bottomPanels?: React.ReactNode[];
    rightControls?: React.ReactNode[];
}

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
        const [text, setText] = useState("");
        const inputRef = useRef<HTMLInputElement>(null);

        const isEditing = Boolean(editingItem);

        // Check if current text matches an existing item (only check after 3 characters) - memoized
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

        // Create filter for autocomplete - memoized
        const filter = useMemo(
            () =>
                createFilterOptions<ListItemDto>({
                    stringify: (option) => option.text || "",
                    matchFrom: "start",
                    limit: 5,
                }),
            [],
        );

        // Get autocomplete options (only show items that don't match current editing item) - memoized
        const autocompleteOptions = useMemo(
            () =>
                existingItems.filter(
                    (item) => !isEditing || item.id !== editingItem?.id,
                ),
            [existingItems, isEditing, editingItem?.id],
        );

        useEffect(() => {
            if (editingItem) {
                setText(editingItem.text || "");
            } else {
                setText("");
            }
        }, [editingItem]);

        // Focus the input field
        const focusInput = useCallback(() => {
            if (inputRef.current && !isLoading) {
                inputRef.current.focus();
            }
        }, [isLoading]);

        // Auto-focus the input when component mounts
        useEffect(() => {
            focusInput();
        }, [focusInput]);

        // Re-focus when loading state changes (mutation completes)
        useEffect(() => {
            if (!isLoading) {
                // Small delay to ensure DOM is updated
                setTimeout(() => {
                    focusInput();
                }, 50);
            }
        }, [isLoading, focusInput]);

        const handleSubmit = () => {
            const trimmedText = text.trim();
            if (!trimmedText) return;

            // Check for duplicates
            const existingItem = existingItems.find(
                (item) =>
                    item.text?.toLowerCase() === trimmedText.toLowerCase() &&
                    (!isEditing || item.id !== editingItem?.id),
            );

            if (existingItem && existingItem.id) {
                if (!isEditing && existingItem.status && onUncheckExisting) {
                    // If we're adding a new item and it exists but is checked, uncheck it
                    onUncheckExisting(existingItem.id);
                    setText("");

                    // Re-focus the input
                    requestAnimationFrame(() => {
                        focusInput();
                    });
                    return;
                } else if (!isEditing || (isEditing && existingItem.status)) {
                    // Show a warning for duplicates (either adding new or editing to existing name)
                    alert(
                        `"${trimmedText}" already exists in your list${existingItem.status ? " and is checked" : ""}.`,
                    );
                    return;
                }
            }

            if (isEditing && onUpdate) {
                // Update existing item
                onUpdate(trimmedText);
            } else {
                // Add new item
                onAdd(trimmedText);
            }

            setText("");

            // Re-focus the input after adding an item - use multiple timeouts to ensure it works
            requestAnimationFrame(() => {
                focusInput();
            });

            // Backup focus attempt
            setTimeout(() => {
                focusInput();
            }, 10);
        };

        const handleCancel = () => {
            setText("");
            if (onCancelEdit) {
                onCancelEdit();
            }
            focusInput();
        };

        const handleDiscard = () => {
            setText("");
            focusInput();
        };

        const handleKeyPress = (event: React.KeyboardEvent) => {
            if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                handleSubmit();
            }
        };

        const handleContainerClick = (event: React.MouseEvent) => {
            // Don't auto-focus main input if clicking inside panels
            const target = event.target as HTMLElement;
            if (target.closest(".panel-section")) {
                return;
            }
            focusInput();
        };

        return (
            <Paper
                elevation={3}
                onClick={handleContainerClick}
                sx={{
                    width: "100%",
                    p: 2,
                    bgcolor: "background.paper",
                    borderRadius: 2,
                    border: "1px solid",
                    borderColor: isEditing ? "warning.main" : "primary.200",
                    cursor: "text",
                    transition: "all 0.3s ease",
                    "&:hover": {
                        borderColor: isEditing
                            ? "warning.dark"
                            : "primary.main",
                        boxShadow: 3,
                    },
                    "&:focus-within": {
                        borderColor: isEditing
                            ? "warning.dark"
                            : "primary.main",
                        boxShadow: 3,
                    },
                }}
            >
                {/* Editing Header */}
                {isEditing && (
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            p: 0.75,
                            backgroundColor: "warning.50",
                            borderRadius: 1,
                            mb: 0.75,
                        }}
                    >
                        <Box
                            sx={{
                                display: "flex",
                                alignItems: "center",
                                gap: 1,
                            }}
                        >
                            <Edit fontSize="small" color="warning" />
                            <Typography variant="body2" color="warning.dark">
                                Bearbeiten
                            </Typography>
                            {editingItem?.status && (
                                <Chip
                                    label="Completed"
                                    size="small"
                                    color="success"
                                    variant="outlined"
                                />
                            )}
                        </Box>
                        <IconButton
                            size="small"
                            onClick={handleCancel}
                            sx={{
                                color: "warning.dark",
                                "&:hover": { backgroundColor: "warning.100" },
                            }}
                        >
                            <Close fontSize="small" />
                        </IconButton>
                    </Box>
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

                {/* Main Input with Controls */}
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    {/* Discard Button - Top Left */}
                    {text.trim() && !isEditing && (
                        <IconButton
                            onClick={handleDiscard}
                            size="small"
                            sx={{
                                color: "text.secondary",
                                "&:hover": {
                                    color: "error.main",
                                    backgroundColor: "error.50",
                                },
                            }}
                            title="Discard input"
                        >
                            <Delete fontSize="small" />
                        </IconButton>
                    )}

                    {/* Main Input Field */}
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
                                setText(newInputValue);
                            }}
                            onChange={(_, newValue) => {
                                if (newValue && typeof newValue !== "string") {
                                    // User selected an existing item from dropdown
                                    setText(newValue.text || "");
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
                                    onKeyPress={handleKeyPress}
                                    InputProps={{
                                        ...params.InputProps,
                                        sx: {
                                            "& .MuiOutlinedInput-notchedOutline":
                                                {
                                                    border: "none",
                                                },
                                            "& .MuiInputBase-input": {
                                                py: 1,
                                            },
                                        },
                                    }}
                                    sx={{
                                        "& .MuiOutlinedInput-root": {
                                            borderRadius: 2,
                                        },
                                        mb: isEditing ? 1 : 0,
                                    }}
                                />
                            )}
                            sx={{
                                "& .MuiAutocomplete-popupIndicator": {
                                    display: "none",
                                },
                                "& .MuiAutocomplete-clearIndicator": {
                                    display: "none",
                                },
                            }}
                        />
                    </Box>

                    {/* Right Controls */}
                    {rightControls.map((control, index) => (
                        <Box key={index}>{control}</Box>
                    ))}

                    {/* Send Button */}
                    <IconButton
                        onClick={handleSubmit}
                        disabled={!text.trim() || isLoading}
                        color={
                            hasDuplicate && existingItem?.status
                                ? "success"
                                : hasDuplicate
                                  ? "error"
                                  : isEditing
                                    ? "warning"
                                    : "primary"
                        }
                        size="small"
                        sx={{
                            bgcolor: text.trim()
                                ? hasDuplicate && existingItem?.status
                                    ? "success.main"
                                    : hasDuplicate
                                      ? "error.main"
                                      : isEditing
                                        ? "warning.main"
                                        : "primary.main"
                                : "transparent",
                            color: text.trim() ? "white" : "action.disabled",
                            "&:hover": {
                                bgcolor: text.trim()
                                    ? hasDuplicate && existingItem?.status
                                        ? "success.dark"
                                        : hasDuplicate
                                          ? "error.dark"
                                          : isEditing
                                            ? "warning.dark"
                                            : "primary.dark"
                                    : "transparent",
                            },
                            "&:disabled": {
                                bgcolor: "transparent",
                                color: "action.disabled",
                            },
                            transition: "all 0.2s ease",
                        }}
                        title={
                            hasDuplicate && existingItem?.status
                                ? `Uncheck "${existingItem.text}"`
                                : hasDuplicate
                                  ? "Item already exists"
                                  : isEditing
                                    ? "Update item"
                                    : "Add item"
                        }
                    >
                        <Send fontSize="small" />
                    </IconButton>
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
        );
    },
);

// Add display name for debugging
AddInput.displayName = "AddInput";
