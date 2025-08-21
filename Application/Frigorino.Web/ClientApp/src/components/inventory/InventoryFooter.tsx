import { Collapse, Container } from "@mui/material";
import { memo, useCallback, useEffect, useMemo, useState } from "react";
import type { InventoryItemDto } from "../../lib/api";
import { AddInput } from "../list/AddInput";
import { DateInputPanel } from "../list/DateInputPanel";
import { QuantityPanel, QuantityToggle } from "../list/QuantityPanel";

interface ListFooterProps {
    editingItem: InventoryItemDto | null;
    existingItems: InventoryItemDto[];
    onAddItem: (data: string, quantity?: string) => void;
    onUpdateItem: (data: string, quantity?: string) => void;
    onCancelEdit: () => void;
    onUncheckExisting: (itemId: number) => void;
    isLoading: boolean;
    onScrollToLastUnchecked: () => void;
}

export const InventoryFooter = memo(
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
        const [date, setDate] = useState<Date | null>(null);
        const [showPanels, setShowQuantityPanel] = useState(false);

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
            setShowQuantityPanel(!showPanels);
        }, [showPanels]);

        const handlePanelKeyPress = useCallback(
            (event: React.KeyboardEvent) => {
                if (event.key === "Enter" && !event.shiftKey) {
                    event.preventDefault();
                    // The AddInput component will handle the submit
                }
            },
            [],
        );

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
                    active={showPanels}
                    onToggle={handleToggleQuantityPanel}
                />,
            ],
            [quantity, handleToggleQuantityPanel, showPanels],
        );

        const topPanels = useMemo(
            () => [
                <Collapse key="date-panel" in={showPanels}>
                    <DateInputPanel
                        value={date}
                        onChange={(value) => setDate(value)}
                        isLoading={isLoading}
                        onKeyPress={handlePanelKeyPress}
                    ></DateInputPanel>
                </Collapse>,
                <Collapse key="quantity-panel" in={showPanels}>
                    <QuantityPanel
                        value={quantity}
                        onChange={setQuantity}
                        isLoading={isLoading}
                        onKeyPress={handlePanelKeyPress}
                    />
                </Collapse>,
            ],
            [showPanels, date, isLoading, handlePanelKeyPress, quantity],
        );

        const bottomPanels = useMemo(() => [], []);

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
                    editingItem={mappedEditingItem}
                    existingItems={mappedExistingItems}
                    isLoading={isLoading}
                    hasItems={existingItems.length > 0}
                    rightControls={rightControls}
                    bottomPanels={bottomPanels}
                    topPanels={topPanels}
                />
            </Container>
        );
    },
);

InventoryFooter.displayName = "InventoryFooter";
