import { Add, Close, Delete, Edit, Remove, Send } from "@mui/icons-material";
import {
    Autocomplete,
    Box,
    Button,
    ButtonGroup,
    Chip,
    Collapse,
    createFilterOptions,
    IconButton,
    Paper,
    TextField,
    Typography,
} from "@mui/material";
import { memo, useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { BaseCreateRequest, BaseItem, BaseUpdateRequest } from "./SortableItemList";

interface AddItemInputProps<T extends BaseItem> {
    onAdd: (data: BaseCreateRequest) => void;
    onUpdate?: (data: BaseUpdateRequest) => void;
    onCancelEdit?: () => void;
    onUncheckExisting?: (itemId: number) => void;
    editingItem?: T | null;
    existingItems?: T[];
    isLoading?: boolean;
    hasItems?: boolean;
    placeholder?: string;
    editingPlaceholder?: string;
    showQuantity?: boolean;
    showDatePicker?: boolean;
    dateLabel?: string;
    quantityLabel?: string;
}

export const AddItemInput = memo(
    <T extends BaseItem>({
        onAdd,
        onUpdate,
        onCancelEdit,
        onUncheckExisting,
        editingItem = null,
        existingItems = [],
        isLoading = false,
        placeholder = "Add item...",
        editingPlaceholder = "Edit item...",
        showQuantity = true,
        showDatePicker = false,
        dateLabel = "Date",
        quantityLabel = "Quantity",
    }: AddItemInputProps<T>) => {
        const [text, setText] = useState("");
        const [quantity, setQuantity] = useState("");
        const [date, setDate] = useState("");
        const [showQuantityInput, setShowQuantityInput] = useState(false);
        const [showDateInput, setShowDateInput] = useState(false);
        const inputRef = useRef<HTMLInputElement>(null);
        const quantityInputRef = useRef<HTMLInputElement>(null);
        const dateInputRef = useRef<HTMLInputElement>(null);
        const parseTimeoutRef = useRef<NodeJS.Timeout | null>(null);

        const isEditing = Boolean(editingItem);

        // Check if current text matches an existing item
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

        // Create filter for autocomplete
        const filter = useMemo(
            () =>
                createFilterOptions<T>({
                    stringify: (option) => option.text || "",
                    matchFrom: "start",
                    limit: 5,
                }),
            [],
        );

        // Get autocomplete options
        const autocompleteOptions = useMemo(
            () =>
                existingItems.filter(
                    (item) => !isEditing || item.id !== editingItem?.id,
                ),
            [existingItems, isEditing, editingItem?.id],
        );

        // Auto-show inputs when editing or when values exist
        useEffect(() => {
            if (isEditing || quantity.trim()) {
                setShowQuantityInput(true);
            }
            if (isEditing || date.trim()) {
                setShowDateInput(true);
            }
        }, [isEditing, quantity, date]);

        // Enhanced text parsing for quantity
        const parseTextWithQuantity = (input: string) => {
            const trimmedInput = input.trim();

            // Pattern 1: Quantity at the beginning
            const quantityFirstMatch = trimmedInput.match(
                /^(\d+(?:\.\d+)?(?:\s*(?:kg|g|l|ml|lbs|oz|pcs?|pieces?|units?)?)?)\s+(.+)$/i,
            );
            if (quantityFirstMatch) {
                const [, qty, itemName] = quantityFirstMatch;
                return { text: itemName.trim(), quantity: qty.trim() };
            }

            // Pattern 2: Quantity at the end with units
            const quantityLastWithUnitsMatch = trimmedInput.match(
                /^(.+?)\s+(\d+(?:\.\d+)?\s*(?:kg|g|l|ml|lbs|oz|pcs?|pieces?|units?))\s*$/i,
            );
            if (quantityLastWithUnitsMatch) {
                const [, itemName, qty] = quantityLastWithUnitsMatch;
                return { text: itemName.trim(), quantity: qty.trim() };
            }

            // Pattern 3: Pure number at end
            const quantityLastPureNumberMatch = trimmedInput.match(
                /^(.+?)\s+(\d+(?:\.\d+)?)\s*$/i,
            );
            if (quantityLastPureNumberMatch) {
                const [, itemName, qty] = quantityLastPureNumberMatch;
                if (
                    itemName.length >= 4 &&
                    (parseInt(qty) >= 10 || trimmedInput.endsWith(" "))
                ) {
                    return { text: itemName.trim(), quantity: qty.trim() };
                }
            }

            return { text: trimmedInput, quantity: "" };
        };

        useEffect(() => {
            if (editingItem) {
                setText(editingItem.text || "");
                setQuantity(editingItem.quantity || "");
                // Handle date field if available
                if ('expiryDate' in editingItem) {
                    const expiryDate = (editingItem as any).expiryDate;
                    if (expiryDate) {
                        const dateObj = new Date(expiryDate);
                        setDate(dateObj.toISOString().slice(0, 10));
                    }
                }
            } else {
                setText("");
                setQuantity("");
                setDate("");
            }
        }, [editingItem]);

        // Focus management
        const focusInput = useCallback(() => {
            if (inputRef.current && !isLoading) {
                inputRef.current.focus();
                inputRef.current.scrollTop = 0;

                if (window.innerWidth <= 600) {
                    setTimeout(() => {
                        inputRef.current?.scrollIntoView({
                            behavior: "smooth",
                            block: "center",
                        });
                    }, 300);
                }
            }
        }, [isLoading]);

        useEffect(() => {
            focusInput();
        }, [focusInput]);

        useEffect(() => {
            if (!isLoading) {
                setTimeout(() => {
                    focusInput();
                }, 50);
            }
        }, [isLoading, focusInput]);

        useEffect(() => {
            return () => {
                if (parseTimeoutRef.current) {
                    clearTimeout(parseTimeoutRef.current);
                }
            };
        }, []);

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
                    onUncheckExisting(existingItem.id);
                    setText("");
                    setQuantity("");
                    setDate("");
                    requestAnimationFrame(() => {
                        focusInput();
                    });
                    return;
                } else if (!isEditing || (isEditing && existingItem.status)) {
                    alert(
                        `"${trimmedText}" already exists${existingItem.status ? " and is checked" : ""}.`,
                    );
                    return;
                }
            }

            if (isEditing && onUpdate) {
                // Update existing item
                const updateData: BaseUpdateRequest = {
                    text: trimmedText,
                    quantity: quantity.trim() || undefined,
                    status: editingItem?.status || false,
                };

                // Add date if supported
                if (showDatePicker && date) {
                    (updateData as any).expiryDate = new Date(date).toISOString();
                }

                onUpdate(updateData);
            } else {
                // Add new item
                const createData: BaseCreateRequest = {
                    text: trimmedText,
                    quantity: quantity.trim() || undefined,
                };

                // Add date if supported
                if (showDatePicker && date) {
                    (createData as any).expiryDate = new Date(date).toISOString();
                }

                onAdd(createData);
            }

            setText("");
            setQuantity("");
            setDate("");

            requestAnimationFrame(() => {
                focusInput();
            });

            setTimeout(() => {
                focusInput();
            }, 10);
        };

        const handleCancel = () => {
            setText("");
            setQuantity("");
            setDate("");
            if (onCancelEdit) {
                onCancelEdit();
            }
            focusInput();
        };

        const handleDiscard = () => {
            setText("");
            setQuantity("");
            setDate("");
            setShowQuantityInput(false);
            setShowDateInput(false);
            focusInput();
        };

        const handleKeyPress = (event: React.KeyboardEvent) => {
            if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                handleSubmit();
            }
        };

        const handleContainerClick = (event: React.MouseEvent) => {
            const target = event.target as HTMLElement;
            if (target.closest(".quantity-section") || target.closest(".date-section")) {
                return;
            }
            focusInput();
        };

        return (
            <Paper
                elevation={3}
                onClick={handleContainerClick}
                sx={{
                    position: "fixed",
                    bottom: 8,
                    left: "50%",
                    p: 0.75,
                    zIndex: 1000,
                    bgcolor: "background.paper",
                    borderRadius: 2,
                    border: "1px solid",
                    borderColor: isEditing ? "warning.main" : "primary.200",
                    cursor: "text",
                    transition: "all 0.3s ease",
                    "@supports (height: 100dvh)": {
                        bottom: 8,
                        transform: "translateX(-50%)",
                    },
                    "@supports (height: 100svh)": {
                        bottom: "max(8px, env(keyboard-inset-height, 8px))",
                    },
                    "@media (max-width: 600px)": {
                        width: "calc(100vw - 16px)",
                        left: "8px",
                        right: "8px",
                        transform: "none",
                        paddingLeft: "max(8px, env(safe-area-inset-left))",
                        paddingRight: "max(8px, env(safe-area-inset-right))",
                        bottom: "max(8px, env(keyboard-inset-height, 8px))",
                    },
                    "&:hover": {
                        borderColor: isEditing ? "warning.dark" : "primary.main",
                        boxShadow: 3,
                    },
                    "&:focus-within": {
                        borderColor: isEditing ? "warning.dark" : "primary.main",
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
                        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                            <Edit fontSize="small" color="warning" />
                            <Typography variant="body2" color="warning.dark">
                                Editing
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

                {/* Main Input with Controls */}
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    {/* Discard Button */}
                    {(text.trim() || quantity.trim() || date.trim()) && !isEditing && (
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
                                setText(newInputValue);

                                if (parseTimeoutRef.current) {
                                    clearTimeout(parseTimeoutRef.current);
                                }

                                if (!isEditing && newInputValue.trim() && showQuantity) {
                                    parseTimeoutRef.current = setTimeout(() => {
                                        const parsed = parseTextWithQuantity(newInputValue);

                                        if (
                                            parsed.quantity &&
                                            parsed.text &&
                                            parsed.text !== newInputValue.trim()
                                        ) {
                                            setText(parsed.text);
                                            setQuantity(parsed.quantity);
                                        }
                                    }, 1200);
                                }
                            }}
                            onChange={(_, newValue) => {
                                if (newValue && typeof newValue !== "string") {
                                    setText(newValue.text || "");
                                    setQuantity(newValue.quantity || "");
                                }
                            }}
                            noOptionsText={
                                text.trim().length >= 3
                                    ? "No matching items"
                                    : "Type at least 3 characters"
                            }
                            renderOption={(props, option) => (
                                <Box component="li" {...props} key={option.id}>
                                    <Box sx={{ display: "flex", flexDirection: "column", width: "100%" }}>
                                        <Typography variant="body2">
                                            {option.text}
                                            {option.status && (
                                                <Chip
                                                    label="✓"
                                                    size="small"
                                                    color="success"
                                                    variant="outlined"
                                                    sx={{ ml: 1, height: 16, fontSize: "0.7rem" }}
                                                />
                                            )}
                                        </Typography>
                                        {option.quantity && (
                                            <Typography variant="caption" color="text.secondary">
                                                {option.quantity}
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
                                    placeholder={isEditing ? editingPlaceholder : placeholder}
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
                                            "& .MuiOutlinedInput-notchedOutline": { border: "none" },
                                            "& .MuiInputBase-input": { py: 1 },
                                        },
                                    }}
                                    sx={{
                                        "& .MuiOutlinedInput-root": { borderRadius: 2 },
                                        mb: isEditing ? 1 : 0,
                                    }}
                                />
                            )}
                            sx={{
                                "& .MuiAutocomplete-popupIndicator": { display: "none" },
                                "& .MuiAutocomplete-clearIndicator": { display: "none" },
                            }}
                        />
                    </Box>

                    {/* Right Side Controls */}
                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        {/* Quantity Toggle Button */}
                        {showQuantity && (
                            <IconButton
                                onClick={() => {
                                    const newShowState = !showQuantityInput;
                                    setShowQuantityInput(newShowState);

                                    if (newShowState) {
                                        setTimeout(() => {
                                            quantityInputRef.current?.focus();
                                        }, 100);
                                    }
                                }}
                                size="small"
                            >
                                {quantity ? (
                                    <Typography
                                        variant="caption"
                                        sx={{ fontWeight: "bold", color: "primary.main", minWidth: "30px" }}
                                    >
                                        {quantity}
                                    </Typography>
                                ) : (
                                    <Typography variant="caption" sx={{ fontWeight: "bold", minWidth: "30px" }}>
                                        #
                                    </Typography>
                                )}
                            </IconButton>
                        )}

                        {/* Date Toggle Button */}
                        {showDatePicker && (
                            <IconButton
                                onClick={() => {
                                    const newShowState = !showDateInput;
                                    setShowDateInput(newShowState);

                                    if (newShowState) {
                                        setTimeout(() => {
                                            dateInputRef.current?.focus();
                                        }, 100);
                                    }
                                }}
                                size="small"
                            >
                                <Typography variant="caption" sx={{ fontWeight: "bold", minWidth: "30px" }}>
                                    📅
                                </Typography>
                            </IconButton>
                        )}

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
                </Box>

                {/* Collapsible Quantity Input */}
                {showQuantity && (
                    <Collapse in={showQuantityInput}>
                        <Box
                            className="quantity-section"
                            sx={{ display: "flex", gap: 0.75, mt: 0.75, alignItems: "center" }}
                        >
                            <TextField
                                fullWidth
                                variant="outlined"
                                placeholder={quantityLabel}
                                value={quantity}
                                onChange={(e) => setQuantity(e.target.value)}
                                onKeyPress={handleKeyPress}
                                onClick={(e) => e.stopPropagation()}
                                disabled={isLoading}
                                size="small"
                                inputRef={quantityInputRef}
                                sx={{ "& .MuiOutlinedInput-root": { borderRadius: 2 } }}
                                InputProps={{
                                    sx: {
                                        "& .MuiOutlinedInput-notchedOutline": {
                                            border: "1px solid",
                                            borderColor: "divider",
                                        },
                                        "& .MuiInputBase-input": { py: 0.75 },
                                        "&:hover .MuiOutlinedInput-notchedOutline": {
                                            borderColor: "primary.main",
                                        },
                                        "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
                                            borderColor: "primary.main",
                                            borderWidth: 2,
                                        },
                                    },
                                }}
                            />

                            {/* Quick Quantity Buttons */}
                            <ButtonGroup variant="outlined" size="small">
                                {[1, 2, 5].map((num) => (
                                    <Button
                                        key={num}
                                        onClick={() => setQuantity(num.toString())}
                                        variant={quantity === num.toString() ? "contained" : "outlined"}
                                        size="small"
                                        sx={{
                                            minWidth: 40,
                                            backgroundColor:
                                                quantity === num.toString() ? "primary.main" : "transparent",
                                            color: quantity === num.toString() ? "white" : "text.primary",
                                        }}
                                    >
                                        {num}
                                    </Button>
                                ))}
                            </ButtonGroup>

                            {/* Quantity Adjustment Buttons */}
                            <IconButton
                                size="small"
                                onClick={() => {
                                    const current = parseInt(quantity) || 0;
                                    if (current > 0) setQuantity((current - 1).toString());
                                }}
                                disabled={!quantity || parseInt(quantity) <= 0}
                            >
                                <Remove fontSize="small" />
                            </IconButton>
                            <IconButton
                                size="small"
                                onClick={() => {
                                    const current = parseInt(quantity) || 0;
                                    setQuantity((current + 1).toString());
                                }}
                            >
                                <Add fontSize="small" />
                            </IconButton>
                        </Box>
                    </Collapse>
                )}

                {/* Collapsible Date Input */}
                {showDatePicker && (
                    <Collapse in={showDateInput}>
                        <Box
                            className="date-section"
                            sx={{ display: "flex", gap: 0.75, mt: 0.75, alignItems: "center" }}
                        >
                            <TextField
                                fullWidth
                                type="date"
                                variant="outlined"
                                label={dateLabel}
                                value={date}
                                onChange={(e) => setDate(e.target.value)}
                                onKeyPress={handleKeyPress}
                                onClick={(e) => e.stopPropagation()}
                                disabled={isLoading}
                                size="small"
                                inputRef={dateInputRef}
                                sx={{ "& .MuiOutlinedInput-root": { borderRadius: 2 } }}
                                InputLabelProps={{ shrink: true }}
                                InputProps={{
                                    sx: {
                                        "& .MuiOutlinedInput-notchedOutline": {
                                            border: "1px solid",
                                            borderColor: "divider",
                                        },
                                        "& .MuiInputBase-input": { py: 0.75 },
                                        "&:hover .MuiOutlinedInput-notchedOutline": {
                                            borderColor: "primary.main",
                                        },
                                        "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
                                            borderColor: "primary.main",
                                            borderWidth: 2,
                                        },
                                    },
                                }}
                            />
                        </Box>
                    </Collapse>
                )}
            </Paper>
        );
    },
);

(AddItemInput as any).displayName = "AddItemInput";
