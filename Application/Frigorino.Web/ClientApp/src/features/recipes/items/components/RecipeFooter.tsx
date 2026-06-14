import { Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    commentComposerFeature,
    draftToQuantity,
    formatQuantity,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
import { useItemComposer } from "../../../../hooks/useItemComposer";
import type { QuantityDto, RecipeItemResponse } from "../../../../lib/api";
import { featureContentPx } from "../../../../theme";

const EDIT_FEATURES = [quantityComposerFeature, commentComposerFeature] as const;
const ADD_FEATURES = [commentComposerFeature] as const;

interface RecipeFooterProps {
    editingItem: RecipeItemResponse | null;
    existingItems: RecipeItemResponse[];
    onAddItem: (text: string, comment: string | null) => void;
    onUpdateItem: (text: string, quantity: QuantityDto | null, comment: string | null) => void;
    onCancelEdit: () => void;
    isLoading: boolean;
    onScrollToLast: () => void;
}

export const RecipeFooter = memo(
    ({
        editingItem,
        existingItems,
        onAddItem,
        onUpdateItem,
        onCancelEdit,
        isLoading,
        onScrollToLast,
    }: RecipeFooterProps) => {
        const { t } = useTranslation();

        const onDuplicate = useCallback(
            (): DuplicateResult => ({ message: t("recipes.alreadyInRecipe"), tone: "warning" }),
            [t],
        );

        const getSecondaryLabel = useCallback(
            (item: RecipeItemResponse) => (item.quantity ? formatQuantity(t, item.quantity) : undefined),
            [t],
        );

        const { suggestions, duplicate } = useItemComposer({
            editingItem,
            existingItems,
            getBadge: () => undefined,
            getSecondaryLabel,
            onDuplicate,
        });

        const features = editingItem ? EDIT_FEATURES : ADD_FEATURES;

        const initialDraft = useMemo(
            () =>
                editingItem
                    ? {
                          text: editingItem.text,
                          values: {
                              quantity: quantityToDraft(editingItem.quantity),
                              comment: editingItem.comment ?? "",
                          },
                      }
                    : undefined,
            [editingItem],
        );

        const handleComplete = useCallback(
            (r: Completion<typeof ADD_FEATURES> | Completion<typeof EDIT_FEATURES>) => {
                const text = r as Completion<typeof EDIT_FEATURES>;
                if (text.mode === "edit") {
                    onUpdateItem(text.text, draftToQuantity(text.quantity), text.comment.trim());
                } else {
                    onAddItem(text.text, text.comment.trim() || null);
                    onScrollToLast();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLast],
        );

        return (
            <Container
                maxWidth="sm"
                sx={{
                    flexShrink: 0,
                    px: featureContentPx,
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

RecipeFooter.displayName = "RecipeFooter";
