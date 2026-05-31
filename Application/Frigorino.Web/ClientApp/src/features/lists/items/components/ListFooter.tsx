import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
import { useItemComposer } from "../../../../hooks/useItemComposer";
import type { ListItemResponse } from "../../../../lib/api";
import { formatQuantity } from "../quantityFormat";

const features = [] as const;

interface ListFooterProps {
    editingItem: ListItemResponse | null;
    existingItems: ListItemResponse[];
    onAddItem: (data: string) => void;
    onUpdateItem: (data: string) => void;
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

        const initialDraft = useMemo(
            () => (editingItem ? { text: editingItem.text } : undefined),
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: Completion<typeof features>) => {
                if (r.mode === "edit") {
                    onUpdateItem(r.text);
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
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

ListFooter.displayName = "ListFooter";
