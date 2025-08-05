import {
    Box,
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    TextField,
} from "@mui/material";
import { useEffect, useState } from "react";
import type {
    CreateListItemRequest,
    ListItemDto,
    UpdateListItemRequest,
} from "../../hooks/useListItemQueries";

interface ListItemDialogProps {
    open: boolean;
    onClose: () => void;
    onSave: (data: CreateListItemRequest | UpdateListItemRequest) => void;
    item?: ListItemDto | null;
    isLoading?: boolean;
}

export const ListItemDialog = ({
    open,
    onClose,
    onSave,
    item,
    isLoading = false,
}: ListItemDialogProps) => {
    const [text, setText] = useState("");
    const [quantity, setQuantity] = useState("");

    const isEditing = Boolean(item);

    useEffect(() => {
        if (open) {
            setText(item?.text || "");
            setQuantity(item?.quantity || "");
        }
    }, [open, item]);

    const handleClose = () => {
        setText("");
        setQuantity("");
        onClose();
    };

    const handleSave = () => {
        if (!text.trim()) return;

        const data = {
            text: text.trim(),
            quantity: quantity.trim() || undefined,
            ...(isEditing && { status: item?.status || false }),
        };

        onSave(data);
        handleClose();
    };

    const handleKeyPress = (event: React.KeyboardEvent) => {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            handleSave();
        }
    };

    return (
        <Dialog
            open={open}
            onClose={handleClose}
            maxWidth="sm"
            fullWidth
            PaperProps={{
                sx: { borderRadius: 2 },
            }}
        >
            <DialogTitle sx={{ pb: 1 }}>
                {isEditing ? "Edit Item" : "Add New Item"}
            </DialogTitle>

            <DialogContent>
                <Box
                    sx={{
                        display: "flex",
                        flexDirection: "column",
                        gap: 2,
                        pt: 1,
                    }}
                >
                    <TextField
                        autoFocus
                        label="Item Name"
                        fullWidth
                        value={text}
                        onChange={(e) => setText(e.target.value)}
                        onKeyPress={handleKeyPress}
                        placeholder="e.g., Milk, Bread, Apples..."
                        error={!text.trim() && text.length > 0}
                        helperText={
                            !text.trim() && text.length > 0
                                ? "Item name is required"
                                : ""
                        }
                    />

                    <TextField
                        label="Quantity (optional)"
                        fullWidth
                        value={quantity}
                        onChange={(e) => setQuantity(e.target.value)}
                        onKeyPress={handleKeyPress}
                        placeholder="e.g., 2 liters, 1 loaf, 6 pieces..."
                    />
                </Box>
            </DialogContent>

            <DialogActions sx={{ px: 3, pb: 3 }}>
                <Button onClick={handleClose} disabled={isLoading}>
                    Cancel
                </Button>
                <Button
                    onClick={handleSave}
                    variant="contained"
                    disabled={!text.trim() || isLoading}
                    sx={{ minWidth: 100 }}
                >
                    {isLoading ? "Saving..." : isEditing ? "Update" : "Add"}
                </Button>
            </DialogActions>
        </Dialog>
    );
};
