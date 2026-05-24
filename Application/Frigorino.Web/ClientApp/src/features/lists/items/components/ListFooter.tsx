import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    quantityFeature,
    type DuplicateConfig,
    type Suggestion,
    type SuggestionsConfig,
} from "../../../../components/composer";
import type { ListItemResponse } from "../../../../lib/api";

interface ListFooterProps {
    editingItem: ListItemResponse | null;
    existingItems: ListItemResponse[];
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
        const { t } = useTranslation();

        const toSuggestion = (item: ListItemResponse): Suggestion => ({
            id: item.id,
            label: item.text,
            secondaryLabel: item.quantity ?? undefined,
            badge: item.status ? (
                <Chip
                    label="✓"
                    size="small"
                    color="success"
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
                    const match = existingItems.find(
                        (item) =>
                            item.text.toLowerCase() === needle &&
                            item.id !== editingItem?.id,
                    );
                    if (!match) {
                        return null;
                    }
                    if (match.status && !editingItem) {
                        return {
                            message: `"${match.text}" ${t("common.alreadyExists")} (${t("common.completed")})`,
                            onResolve: () => onUncheckExisting(match.id),
                        };
                    }
                    return {
                        message: `"${match.text}" ${t("common.alreadyExists")}`,
                        block: true,
                    };
                },
            }),
            [existingItems, editingItem, onUncheckExisting, t],
        );

        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: { quantity: editingItem.quantity ?? "" },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: { kind: "text"; mode: "create" | "edit"; text: string; quantity: string }) => {
                if (r.kind !== "text") {
                    return;
                }
                if (r.mode === "edit") {
                    onUpdateItem(r.text, r.quantity || undefined);
                } else {
                    onAddItem(r.text, r.quantity || undefined);
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
                    features={[quantityFeature]}
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

ListFooter.displayName = "ListFooter";
