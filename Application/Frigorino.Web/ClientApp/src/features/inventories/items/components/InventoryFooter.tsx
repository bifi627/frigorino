import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    expiryFeature,
    quantityFeature,
    type DuplicateConfig,
    type Suggestion,
    type SuggestionsConfig,
} from "../../../../components/composer";
import type { InventoryItemResponse } from "../../../../lib/api";

interface InventoryFooterProps {
    editingItem: InventoryItemResponse | null;
    existingItems: InventoryItemResponse[];
    onAddItem: (data: string, quantity?: string, expiryDate?: Date) => void;
    onUpdateItem: (data: string, quantity?: string, expiryDate?: Date) => void;
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
        isLoading,
        onScrollToLastUnchecked,
    }: InventoryFooterProps) => {
        const { t } = useTranslation();

        const toSuggestion = (item: InventoryItemResponse): Suggestion => ({
            id: item.id,
            label: item.text,
            secondaryLabel: item.quantity ?? undefined,
            badge: item.isExpiring ? (
                <Chip
                    label="!"
                    size="small"
                    color="warning"
                    variant="outlined"
                    sx={{ ml: 1, height: 16, fontSize: "0.7rem" }}
                />
            ) : undefined,
        });

        const suggestions = useMemo<SuggestionsConfig>(
            () => ({
                getItems: (query) => {
                    const q = query.trim().toLowerCase();
                    return existingItems
                        .filter(
                            (item) =>
                                item.id !== editingItem?.id &&
                                item.text.toLowerCase().startsWith(q),
                        )
                        .map(toSuggestion);
                },
            }),
            [existingItems, editingItem?.id],
        );

        const duplicate = useMemo<DuplicateConfig>(
            () => ({
                check: (text) => {
                    const needle = text.trim().toLowerCase();
                    // Don't flag duplicates on 1–2 char input; matches the autocomplete minChars floor.
                    if (needle.length < 3) {
                        return null;
                    }
                    const match = existingItems.find(
                        (item) =>
                            item.text.toLowerCase() === needle &&
                            item.id !== editingItem?.id,
                    );
                    if (!match) {
                        return null;
                    }
                    return {
                        message: `"${match.text}" ${t("common.alreadyExists")}`,
                        block: true,
                    };
                },
            }),
            [existingItems, editingItem?.id, t],
        );

        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: {
                              quantity: editingItem.quantity ?? "",
                              expiry: editingItem.expiryDate
                                  ? new Date(editingItem.expiryDate)
                                  : null,
                          },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: {
                kind: "text";
                mode: "create" | "edit";
                text: string;
                quantity: string;
                expiry: Date | null;
            }) => {
                if (r.mode === "edit") {
                    onUpdateItem(r.text, r.quantity || undefined, r.expiry ?? undefined);
                } else {
                    onAddItem(r.text, r.quantity || undefined, r.expiry ?? undefined);
                    onScrollToLastUnchecked();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLastUnchecked],
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
                <Composer
                    features={[quantityFeature, expiryFeature]}
                    disabled={isLoading}
                    editing={{ active: Boolean(editingItem), onCancel: onCancelEdit }}
                    initialDraft={initialDraft}
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

InventoryFooter.displayName = "InventoryFooter";
