import { Chip, Container } from "@mui/material";
import { memo, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
    Composer,
    attachComposerFeature,
    commentComposerFeature,
    draftToQuantity,
    formatQuantity,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
    type DuplicateResult,
} from "../../../../components/composer";
import { useItemComposer } from "../../../../hooks/useItemComposer";
import type { ListItemResponse, QuantityDto } from "../../../../lib/api";

// Lists add via free-text (extraction fills the quantity), so the add composer stays
// quantity-free — but a comment can be attached at add time. Manual quantity entry/correction
// happens in edit mode.
const EDIT_FEATURES = [
    quantityComposerFeature,
    commentComposerFeature,
] as const;
const ADD_FEATURES = [commentComposerFeature, attachComposerFeature] as const;

interface ListFooterProps {
    editingItem: ListItemResponse | null;
    existingItems: ListItemResponse[];
    onAddItem: (data: string, comment: string | null) => void;
    onUpdateItem: (
        data: string,
        quantity: QuantityDto | null,
        comment: string | null,
    ) => void;
    onCancelEdit: () => void;
    onUncheckExisting: (itemId: number) => void;
    isLoading: boolean;
    onScrollToLastUnchecked: () => void;
    onAttachFile: (file: File) => void;
    /** When entering edit mode, start with the quantity panel expanded. */
    openQuantityPanel?: boolean;
    /** When entering edit mode, start with the comment panel expanded. */
    openCommentPanel?: boolean;
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
        onAttachFile,
        openQuantityPanel,
        openCommentPanel,
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

        // handleComplete accepts the union of both feature sets: ADD_FEATURES carries the
        // "attach" action completion (kind: "attach", file), while EDIT_FEATURES is the text
        // superset that knows r.quantity. r.comment is present in both text completions because
        // both feature sets include commentComposerFeature.
        const handleComplete = useCallback(
            (
                r:
                    | Completion<typeof ADD_FEATURES>
                    | Completion<typeof EDIT_FEATURES>,
            ) => {
                if (r.kind === "attach") {
                    onAttachFile(r.file);
                    return;
                }
                // After excluding the attach action, both feature sets yield a text completion.
                // Treat it as the EDIT superset so the edit branch can read r.quantity (the create
                // branch never reads it). r.comment is present in both — both include the comment
                // feature.
                const text = r as Completion<typeof EDIT_FEATURES>;
                if (text.mode === "edit") {
                    // Send the trimmed string (incl. "") so emptying the field clears the
                    // comment — downstream null means "preserve", "" means "clear".
                    onUpdateItem(
                        text.text,
                        draftToQuantity(text.quantity),
                        text.comment.trim(),
                    );
                } else {
                    onAddItem(text.text, text.comment.trim() || null);
                    onScrollToLastUnchecked();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLastUnchecked, onAttachFile],
        );

        // Which modifier panel opens when edit mode starts — comment wins if both were
        // requested (you can only have tapped one affordance), then quantity.
        let initialOpenId: string | undefined;
        if (editingItem && openCommentPanel) {
            initialOpenId = "comment";
        } else if (editingItem && openQuantityPanel) {
            initialOpenId = "quantity";
        }

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
                    initialOpenId={initialOpenId}
                    suggestions={suggestions}
                    duplicate={duplicate}
                    onComplete={handleComplete}
                />
            </Container>
        );
    },
);

ListFooter.displayName = "ListFooter";
