import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    quantityFeature,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
import { useItemComposer } from "../../../../hooks/useItemComposer";
import type { ListItemResponse } from "../../../../lib/api";

const features = [quantityFeature] as const;

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
                          values: { quantity: editingItem.quantity ?? "" },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: Completion<typeof features>) => {
                if (r.mode === "edit") {
                    onUpdateItem(r.text, r.quantity);
                } else {
                    onAddItem(r.text, r.quantity);
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
