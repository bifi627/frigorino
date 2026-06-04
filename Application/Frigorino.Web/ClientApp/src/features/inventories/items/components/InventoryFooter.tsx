import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    draftToQuantity,
    expiryFeature,
    formatQuantity,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
import { useItemComposer } from "../../../../hooks/useItemComposer";
import type { InventoryItemResponse, QuantityDto } from "../../../../lib/api";

const features = [quantityComposerFeature, expiryFeature] as const;

interface InventoryFooterProps {
    editingItem: InventoryItemResponse | null;
    existingItems: InventoryItemResponse[];
    onAddItem: (
        data: string,
        quantity: QuantityDto | null,
        expiryDate?: string,
    ) => void;
    onUpdateItem: (
        data: string,
        quantity: QuantityDto | null,
        expiryDate?: string,
    ) => void;
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

        const getBadge = useCallback(
            (item: InventoryItemResponse) =>
                item.isExpiring ? (
                    <Chip
                        label="!"
                        size="small"
                        color="warning"
                        variant="outlined"
                        sx={{ ml: 1, height: 16, fontSize: "0.7rem" }}
                    />
                ) : undefined,
            [],
        );

        const onDuplicate = useCallback(
            (match: InventoryItemResponse): DuplicateResult => ({
                message: `"${match.text}" ${t("common.alreadyExists")}`,
                block: true,
            }),
            [t],
        );

        const getSecondaryLabel = useCallback(
            (item: InventoryItemResponse) =>
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

        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: {
                              quantity: quantityToDraft(editingItem.quantity),
                              expiry: editingItem.expiryDate ?? null,
                          },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: Completion<typeof features>) => {
                const quantity = draftToQuantity(r.quantity);
                if (r.mode === "edit") {
                    onUpdateItem(r.text, quantity, r.expiry ?? undefined);
                } else {
                    onAddItem(r.text, quantity, r.expiry ?? undefined);
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
                    py: 1,
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
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

InventoryFooter.displayName = "InventoryFooter";
