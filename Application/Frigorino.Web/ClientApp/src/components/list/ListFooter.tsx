import { Collapse, Container } from "@mui/material";
import { memo, useCallback, useEffect, useMemo, useState } from "react";
import type { ListItemDto } from "../../hooks/useListItemQueries";
import { AddInput } from "./AddInput";
import { QuantityPanel, QuantityToggle } from "./QuantityPanel";

interface ListFooterProps {
    editingItem: ListItemDto | null;
    existingItems: ListItemDto[];
    onAddItem: (data: string, quantity?: string) => void;
    onUpdateItem: (data: string, quantity?: string) => void;
    onCancelEdit: () => void;
    onUncheckExisting: (itemId: number) => void;
    isLoading: boolean;
    onScrollToLastUnchecked: () => void;
}

export const ListFooter = memo(
    ({
        editingItem,
        existingItems,
        onAddItem,
        onUpdateItem,
        onCancelEdit,
        onUncheckExisting,
        isLoading,
        onScrollToLastUnchecked,
    }: ListFooterProps) => {
        const [quantity, setQuantity] = useState("");
        const [showQuantityPanel, setShowQuantityPanel] = useState(false);

        // Update quantity state when editing item changes
        useEffect(() => {
            if (editingItem) {
                setQuantity(editingItem.quantity || "");
                if (editingItem.quantity) {
                    setShowQuantityPanel(true);
                }
            } else {
                setQuantity("");
            }
        }, [editingItem]);

        const handleAddItem = useCallback(
            (data: string) => {
                onAddItem(data, quantity.trim() || undefined);
                setQuantity("");
                onScrollToLastUnchecked();
            },
            [onAddItem, quantity, onScrollToLastUnchecked],
        );

        const handleUpdateItem = useCallback(
            (data: string) => {
                onUpdateItem(data, quantity.trim() || undefined);
            },
            [onUpdateItem, quantity],
        );

        const handleToggleQuantityPanel = useCallback(() => {
            setShowQuantityPanel(!showQuantityPanel);
        }, [showQuantityPanel]);

        const handleQuantityKeyPress = useCallback(
            (event: React.KeyboardEvent) => {
                if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    // The AddInput component will handle the submit
                }
            },
            [],
        );

        const onClearText = () => {
            setQuantity("");
            setShowQuantityPanel(false);
        };

        // Memoize the existing items mapping to prevent unnecessary re-renders
        const mappedExistingItems = useMemo(
            () =>
                existingItems.map((item) => ({
                    ...item,
                    secondaryText: item.quantity || null,
                })),
            [existingItems],
        );

        // Memoize the editing item mapping
        const mappedEditingItem = useMemo(
            () =>
                editingItem
                    ? {
                          ...editingItem,
                          secondaryText: editingItem?.quantity,
                      }
                    : undefined,
            [editingItem],
        );

        // Memoize right controls
        const rightControls = useMemo(
            () => [
                <QuantityToggle
                    key="quantity-toggle"
                    value={quantity}
                    active={showQuantityPanel}
                    onToggle={handleToggleQuantityPanel}
                />,
            ],
            [quantity, handleToggleQuantityPanel, showQuantityPanel],
        );

        // Memoize bottom panels
        const topPanels = useMemo(
            () => [
                <Collapse key="quantity-panel" in={showQuantityPanel}>
                    <QuantityPanel
                        value={quantity}
                        onChange={setQuantity}
                        isLoading={isLoading}
                        onKeyPress={handleQuantityKeyPress}
                    />
                </Collapse>,
            ],
            [showQuantityPanel, quantity, isLoading, handleQuantityKeyPress],
        );

        return (
            <Container
                maxWidth="sm"
                sx={{
                    flexShrink: 0,
                    px: 3,
                    py: 2,
                    borderTop: 1,
                    borderColor: "divider",
                    bgcolor: "background.paper",
                }}
            >
                <AddInput
                    onAdd={handleAddItem}
                    onUpdate={handleUpdateItem}
                    onCancelEdit={onCancelEdit}
                    onUncheckExisting={onUncheckExisting}
                    onClearText={onClearText}
                    editingItem={mappedEditingItem}
                    existingItems={mappedExistingItems}
                    isLoading={isLoading}
                    hasItems={existingItems.length > 0}
                    rightControls={rightControls}
                    topPanels={topPanels}
                />
            </Container>
        );
    },
);

ListFooter.displayName = "ListFooter";
