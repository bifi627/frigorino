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
import { useCallback, useEffect, useRef, useState } from "react";
import type {
    CreateListItemRequest,
    ListItemDto,
    UpdateListItemRequest,
} from "../../hooks/useListItemQueries";

interface AddItemInputProps {
    onAdd: (data: CreateListItemRequest) => void;
    onUpdate?: (data: UpdateListItemRequest) => void;
    onCancelEdit?: () => void;
    onUncheckExisting?: (itemId: number) => void;
    editingItem?: ListItemDto | null;
    existingItems?: ListItemDto[];
    isLoading?: boolean;
    hasItems?: boolean;
}

export const AddItemInput = ({
    onAdd,
    onUpdate,
    onCancelEdit,
    onUncheckExisting,
    editingItem = null,
    existingItems = [],
    isLoading = false,
    hasItems = false,
}: AddItemInputProps) => {
    const [text, setText] = useState("");
    const [quantity, setQuantity] = useState("");
    const [showQuantityInput, setShowQuantityInput] = useState(false);
    const inputRef = useRef<HTMLInputElement>(null);
    const quantityInputRef = useRef<HTMLInputElement>(null);
    const parseTimeoutRef = useRef<NodeJS.Timeout | null>(null);

    const isEditing = Boolean(editingItem);

    // Check if current text matches an existing item (only check after 3 characters)
    const existingItem =
        text.trim().length >= 3
            ? existingItems.find(
                  (item) =>
                      item.text?.toLowerCase() === text.trim().toLowerCase() &&
                      (!isEditing || item.id !== editingItem?.id),
              )
            : null;
    const hasDuplicate = Boolean(existingItem && text.trim().length >= 3);

    // Create filter for autocomplete
    const filter = createFilterOptions<ListItemDto>({
        stringify: (option) => option.text || "",
        matchFrom: "start",
        limit: 5,
    });

    // Get autocomplete options (only show items that don't match current editing item)
    const autocompleteOptions = existingItems.filter(
        (item) => !isEditing || item.id !== editingItem?.id,
    ); // Auto-show quantity input when editing or when quantity exists
    useEffect(() => {
        if (isEditing || quantity.trim()) {
            setShowQuantityInput(true);
        }
    }, [isEditing, quantity]);

    // Enhanced text parsing for quantity with support for various patterns
    const parseTextWithQuantity = (input: string) => {
        const trimmedInput = input.trim();

        // Pattern 1: Quantity at the beginning (e.g., "2 apples", "100 bananas", "2.5 kg flour")
        const quantityFirstMatch = trimmedInput.match(
            /^(\d+(?:\.\d+)?(?:\s*(?:kg|g|l|ml|lbs|oz|pcs?|pieces?|units?)?)?)\s+(.+)$/i,
        );
        if (quantityFirstMatch) {
            const [, qty, itemName] = quantityFirstMatch;
            return { text: itemName.trim(), quantity: qty.trim() };
        }

        // Pattern 2: Quantity at the end - be more conservative
        // Only trigger if it has units or if the number is followed by a space and the input seems complete
        const quantityLastWithUnitsMatch = trimmedInput.match(
            /^(.+?)\s+(\d+(?:\.\d+)?\s*(?:kg|g|l|ml|lbs|oz|pcs?|pieces?|units?))\s*$/i,
        );
        if (quantityLastWithUnitsMatch) {
            const [, itemName, qty] = quantityLastWithUnitsMatch;
            return { text: itemName.trim(), quantity: qty.trim() };
        }

        // Pattern 2b: Pure number at end - only if it looks like a complete entry
        // Wait for either a pause in typing or certain indicators that suggest completion
        const quantityLastPureNumberMatch = trimmedInput.match(
            /^(.+?)\s+(\d+(?:\.\d+)?)\s*$/i,
        );
        if (quantityLastPureNumberMatch) {
            const [, itemName, qty] = quantityLastPureNumberMatch;
            // Only parse if:
            // 1. The item name is reasonably long (suggests complete word)
            // 2. The number is substantial (not just single digit that might be partial)
            // 3. Or the input ends with a space (suggesting user finished typing)
            if (
                itemName.length >= 4 &&
                (parseInt(qty) >= 10 || trimmedInput.endsWith(" "))
            ) {
                return { text: itemName.trim(), quantity: qty.trim() };
            }
        }

        // Pattern 3: Just a number (e.g., "5" -> assume it's quantity for current context)
        const numberOnlyMatch = trimmedInput.match(
            /^(\d+(?:\.\d+)?(?:\s*(?:kg|g|l|ml|lbs|oz|pcs?|pieces?|units?)?)?)\s*$/i,
        );
        if (numberOnlyMatch && trimmedInput.length <= 10) {
            // Reasonable limit for pure quantity
            return { text: "", quantity: trimmedInput };
        }

        return { text: trimmedInput, quantity: "" };
    };
    useEffect(() => {
        if (editingItem) {
            setText(editingItem.text || "");
            setQuantity(editingItem.quantity || "");
        } else {
            setText("");
            setQuantity("");
        }
    }, [editingItem]);

    // Focus the input field
    const focusInput = useCallback(() => {
        if (inputRef.current && !isLoading) {
            inputRef.current.focus();
            inputRef.current.scrollTop = 0;

            // Scroll the input into view on mobile to handle virtual keyboard
            if (window.innerWidth <= 600) {
                setTimeout(() => {
                    inputRef.current?.scrollIntoView({
                        behavior: "smooth",
                        block: "center",
                    });
                }, 300); // Delay to allow keyboard to appear
            }
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

    // Cleanup timeout on unmount
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
                // If we're adding a new item and it exists but is checked, uncheck it
                onUncheckExisting(existingItem.id);
                setText("");
                setQuantity("");

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
            onUpdate({
                text: trimmedText,
                quantity: quantity.trim() || undefined,
                status: editingItem?.status || false,
            });
        } else {
            // Add new item
            onAdd({
                text: trimmedText,
                quantity: quantity.trim() || undefined,
            });
        }

        setText("");
        setQuantity("");

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
        setQuantity("");
        if (onCancelEdit) {
            onCancelEdit();
        }
        focusInput();
    };

    const handleDiscard = () => {
        setText("");
        setQuantity("");
        setShowQuantityInput(false);
        focusInput();
    };

    const handleKeyPress = (event: React.KeyboardEvent) => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            handleSubmit();
        }
    };

    const handleContainerClick = (event: React.MouseEvent) => {
        // Don't auto-focus main input if clicking inside the quantity section
        const target = event.target as HTMLElement;
        if (target.closest(".quantity-section")) {
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
                bottom: 8, // Reduced bottom spacing
                left: "50%",
                // transform: hasItems
                //     ? "translateX(-50%)"
                //     : "translate(-50%, 50%)",
                // width: "calc(100% - 32px)",
                // maxWidth: "600px",
                p: 0.75, // Reduced padding
                zIndex: 1000,
                bgcolor: "background.paper",
                borderRadius: 2, // Slightly smaller border radius
                border: "1px solid",
                borderColor: isEditing ? "warning.main" : "primary.200",
                cursor: "text",
                transition: "all 0.3s ease",
                // Modern viewport units for mobile keyboard handling
                "@supports (height: 100dvh)": {
                    // Use dynamic viewport height when available (modern browsers)
                    bottom: 8, // Reduced bottom spacing
                    transform: "translateX(-50%)",
                },
                "@supports (height: 100svh)": {
                    // Use small viewport height for better mobile keyboard support
                    bottom: "max(8px, env(keyboard-inset-height, 8px))", // Reduced bottom spacing
                },
                // Mobile-specific adjustments
                "@media (max-width: 600px)": {
                    width: "calc(100vw - 16px)",
                    left: "8px",
                    right: "8px",
                    transform: "none",
                    // Use safe area insets for devices with notches
                    paddingLeft: "max(8px, env(safe-area-inset-left))",
                    paddingRight: "max(8px, env(safe-area-inset-right))",
                    // Adjust for virtual keyboard
                    bottom: "max(8px, env(keyboard-inset-height, 8px))", // Reduced bottom spacing
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
                        p: 0.75, // Reduced padding
                        backgroundColor: "warning.50",
                        borderRadius: 1,
                        mb: 0.75, // Reduced margin bottom
                    }}
                >
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
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

            {/* Main Input with Controls */}
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                {/* Discard Button - Top Left */}
                {(text.trim() || quantity.trim()) && !isEditing && (
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
                            text.trim().length >= 3 ? autocompleteOptions : []
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
                            // Always allow normal text input first
                            setText(newInputValue);

                            // Clear any existing timeout
                            if (parseTimeoutRef.current) {
                                clearTimeout(parseTimeoutRef.current);
                            }

                            // Only try to parse quantity if the input looks like it might contain one
                            // and we're not currently editing an existing item
                            if (!isEditing && newInputValue.trim()) {
                                // Add a small delay to avoid parsing while user is still typing
                                parseTimeoutRef.current = setTimeout(() => {
                                    const parsed =
                                        parseTextWithQuantity(newInputValue);

                                    // Only apply parsing if we actually found a clear quantity pattern
                                    // and it's different from the raw input (meaning we extracted something)
                                    if (
                                        parsed.quantity &&
                                        parsed.text &&
                                        parsed.text !== newInputValue.trim()
                                    ) {
                                        setText(parsed.text);
                                        setQuantity(parsed.quantity);
                                    }
                                }, 1200); // Wait 1200ms after user stops typing
                            }
                        }}
                        onChange={(_, newValue) => {
                            if (newValue && typeof newValue !== "string") {
                                // User selected an existing item from dropdown
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
                                    {option.quantity && (
                                        <Typography
                                            variant="caption"
                                            color="text.secondary"
                                        >
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
                                        "& .MuiOutlinedInput-notchedOutline": {
                                            border: "none",
                                        },
                                        "& .MuiInputBase-input": {
                                            py: 1, // Reduced vertical padding
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
                                display: "none", // Hide the dropdown arrow
                            },
                            "& .MuiAutocomplete-clearIndicator": {
                                display: "none", // Hide the clear button
                            },
                        }}
                    />
                </Box>

                {/* Right Side Controls - Quantity and Send */}
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                    {/* Quantity Toggle Button */}
                    <IconButton
                        onClick={() => {
                            const newShowState = !showQuantityInput;
                            setShowQuantityInput(newShowState);

                            // Focus quantity input when opening
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
                                sx={{
                                    fontWeight: "bold",
                                    color: "primary.main",
                                    minWidth: "30px",
                                }}
                            >
                                {quantity}
                            </Typography>
                        ) : (
                            <Typography
                                variant="caption"
                                sx={{ fontWeight: "bold", minWidth: "30px" }}
                            >
                                #
                            </Typography>
                        )}
                    </IconButton>

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

            {/* Collapsible Quantity Input with Quick Buttons */}
            <Collapse in={showQuantityInput}>
                <Box
                    className="quantity-section"
                    sx={{
                        display: "flex",
                        gap: 0.75, // Reduced gap
                        mt: 0.75, // Reduced top margin
                        alignItems: "center",
                    }}
                >
                    <TextField
                        fullWidth
                        variant="outlined"
                        placeholder="Menge"
                        value={quantity}
                        onChange={(e) => setQuantity(e.target.value)}
                        onKeyPress={handleKeyPress}
                        onClick={(e) => e.stopPropagation()}
                        disabled={isLoading}
                        size="small"
                        inputRef={quantityInputRef}
                        sx={{
                            "& .MuiOutlinedInput-root": {
                                borderRadius: 2,
                            },
                        }}
                        InputProps={{
                            sx: {
                                "& .MuiOutlinedInput-notchedOutline": {
                                    border: "1px solid",
                                    borderColor: "divider",
                                },
                                "& .MuiInputBase-input": {
                                    py: 0.75, // Reduced vertical padding for quantity input
                                },
                                "&:hover .MuiOutlinedInput-notchedOutline": {
                                    borderColor: "primary.main",
                                },
                                "&.Mui-focused .MuiOutlinedInput-notchedOutline":
                                    {
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
                                variant={
                                    quantity === num.toString()
                                        ? "contained"
                                        : "outlined"
                                }
                                size="small"
                                sx={{
                                    minWidth: 40,
                                    backgroundColor:
                                        quantity === num.toString()
                                            ? "primary.main"
                                            : "transparent",
                                    color:
                                        quantity === num.toString()
                                            ? "white"
                                            : "text.primary",
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
                            if (current > 0)
                                setQuantity((current - 1).toString());
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
        </Paper>
    );
};
