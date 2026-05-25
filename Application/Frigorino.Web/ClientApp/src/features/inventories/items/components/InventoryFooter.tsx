import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    expiryFeature,
    quantityFeature,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
import { useItemComposer } from "../../../../hooks/useItemComposer";
import type { InventoryItemResponse } from "../../../../lib/api";

const features = [quantityFeature, expiryFeature] as const;

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

        const { suggestions, duplicate } = useItemComposer({
            editingItem,
            existingItems,
            getBadge,
            onDuplicate,
        });

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
            (r: Completion<typeof features>) => {
                if (r.mode === "edit") {
                    onUpdateItem(r.text, r.quantity, r.expiry ?? undefined);
                } else {
                    onAddItem(r.text, r.quantity, r.expiry ?? undefined);
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
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

InventoryFooter.displayName = "InventoryFooter";
