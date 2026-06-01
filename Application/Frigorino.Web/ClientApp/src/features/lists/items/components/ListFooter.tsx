import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    draftToQuantity,
    formatQuantity,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
import { useItemComposer } from "../../../../hooks/useItemComposer";
import type { ListItemResponse, QuantityDto } from "../../../../lib/api";

// Lists add via free-text (extraction fills the quantity), so the add composer is text-only.
// Manual quantity entry/correction happens in edit mode — hence the feature is edit-only.
const EDIT_FEATURES = [quantityComposerFeature] as const;
const NO_FEATURES = [] as const;

interface ListFooterProps {
    editingItem: ListItemResponse | null;
    existingItems: ListItemResponse[];
    onAddItem: (data: string) => void;
    onUpdateItem: (data: string, quantity: QuantityDto | null) => void;
    onCancelEdit: () => void;
    onUncheckExisting: (itemId: number) => void;
    isLoading: boolean;
    onScrollToLastUnchecked: () => void;
    /** When entering edit mode, start with the quantity panel expanded. */
    openQuantityPanel?: boolean;
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
        openQuantityPanel,
    }: ListFooterProps) => {
        const { t } = useTranslation();

        const getBadge = useCallback(
            (item: ListItemResponse) =>
                item.status ? (
                    <Chip
                        label="✓"
                        size="small"
                        color="success"
                        variant="outlined"
                        sx={{ ml: 1, height: 16, fontSize: "0.7rem" }}
                    />
                ) : undefined,
            [],
        );

        const onDuplicate = useCallback(
            (match: ListItemResponse): DuplicateResult => {
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
            [editingItem, onUncheckExisting, t],
        );

        const getSecondaryLabel = useCallback(
            (item: ListItemResponse) =>
                item.quantity ? formatQuantity(t, item.quantity) : undefined,
            [t],
        );

        const { suggestions, duplicate } = useItemComposer({
            editingItem,
            existingItems,
            getBadge,
            getSecondaryLabel,
            onDuplicate,
        });

        const features = editingItem ? EDIT_FEATURES : NO_FEATURES;

        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: {
                              quantity: quantityToDraft(editingItem.quantity),
                          },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: Completion<typeof EDIT_FEATURES>) => {
                if (r.mode === "edit") {
                    onUpdateItem(r.text, draftToQuantity(r.quantity));
                } else {
                    onAddItem(r.text);
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
                    key={editingItem?.id ?? "new"}
                    features={features}
                    disabled={isLoading}
                    editing={{
                        active: Boolean(editingItem),
                        onCancel: onCancelEdit,
                    }}
                    initialDraft={initialDraft}
                    initialOpenId={
                        openQuantityPanel && editingItem
                            ? "quantity"
                            : undefined
                    }
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

ListFooter.displayName = "ListFooter";
