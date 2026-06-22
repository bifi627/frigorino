import { ExpandMore } from "@mui/icons-material";
import { Box, Button, Container, Menu, MenuItem } from "@mui/material";
import { memo, useCallback, useMemo, useState } from "react";
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
import type {
    QuantityDto,
    RecipeItemResponse,
    RecipeSectionResponse,
} from "../../../../lib/api";
import { featureContentPx, sectionColors, tintedActionButtonSx } from "../../../../theme";

const existingSectionsTarget = (
    sections: RecipeSectionResponse[],
    targetSectionId: number,
) => sections.find((s) => s.id === targetSectionId) ?? sections[0];

const EDIT_FEATURES = [
    quantityComposerFeature,
    commentComposerFeature,
] as const;
const ADD_FEATURES = [commentComposerFeature] as const;

interface RecipeFooterProps {
    editingItem: RecipeItemResponse | null;
    existingItems: RecipeItemResponse[];
    sections: RecipeSectionResponse[];
    targetSectionId: number;
    onChangeTargetSection: (sectionId: number) => void;
    onAddItem: (text: string, comment: string | null) => void;
    onUpdateItem: (
        text: string,
        quantity: QuantityDto | null,
        comment: string | null,
    ) => void;
    onCancelEdit: () => void;
    isLoading: boolean;
    onScrollToLast: () => void;
}

export const RecipeFooter = memo(
    ({
        editingItem,
        existingItems,
        sections,
        targetSectionId,
        onChangeTargetSection,
        onAddItem,
        onUpdateItem,
        onCancelEdit,
        isLoading,
        onScrollToLast,
    }: RecipeFooterProps) => {
        const { t } = useTranslation();
        const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);

        const onDuplicate = useCallback(
            (): DuplicateResult => ({
                message: t("recipes.alreadyInRecipe"),
                tone: "warning",
            }),
            [t],
        );

        const getSecondaryLabel = useCallback(
            (item: RecipeItemResponse) =>
                item.quantity ? formatQuantity(t, item.quantity) : undefined,
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
            (
                r:
                    | Completion<typeof ADD_FEATURES>
                    | Completion<typeof EDIT_FEATURES>,
            ) => {
                const text = r as Completion<typeof EDIT_FEATURES>;
                if (text.mode === "edit") {
                    onUpdateItem(
                        text.text,
                        draftToQuantity(text.quantity),
                        text.comment.trim(),
                    );
                } else {
                    onAddItem(text.text, text.comment.trim() || null);
                    onScrollToLast();
                }
            },
            [onAddItem, onUpdateItem, onScrollToLast],
        );

        const targetSection = existingSectionsTarget(sections, targetSectionId);
        const targetLabel =
            targetSection?.name?.trim() || t("recipes.ingredientsHeading");
        const canSwitch = sections.length > 1;

        return (
            <Container
                maxWidth="sm"
                data-testid="recipe-composer-footer"
                sx={{
                    flexShrink: 0,
                    px: featureContentPx,
                    py: 1,
                    borderTop: 1,
                    borderColor: "divider",
                    bgcolor: "background.paper",
                }}
            >
                {!editingItem && (
                    <Box sx={{ mb: 0.5 }}>
                        <Button
                            size="small"
                            endIcon={canSwitch ? <ExpandMore /> : undefined}
                            onClick={(e) =>
                                canSwitch && setMenuAnchor(e.currentTarget)
                            }
                            disabled={!canSwitch}
                            data-testid="recipe-composer-target"
                            sx={{
                                ...tintedActionButtonSx(sectionColors.recipes),
                                borderRadius: 999,
                                textTransform: "none",
                                py: 0.25,
                                px: 1,
                                minWidth: 0,
                                // a disabled tinted button still needs to read as a label
                                "&.Mui-disabled": {
                                    color: sectionColors.recipes,
                                    opacity: 0.9,
                                },
                            }}
                        >
                            {t("recipes.addingTo", { section: targetLabel })}
                        </Button>
                        <Menu
                            anchorEl={menuAnchor}
                            open={Boolean(menuAnchor)}
                            onClose={() => setMenuAnchor(null)}
                        >
                            {sections.map((s) => (
                                <MenuItem
                                    key={s.id}
                                    selected={s.id === targetSectionId}
                                    onClick={() => {
                                        onChangeTargetSection(s.id);
                                        setMenuAnchor(null);
                                    }}
                                    data-testid={`recipe-composer-target-${s.id}`}
                                >
                                    {s.name?.trim() ||
                                        t("recipes.ingredientsHeading")}
                                </MenuItem>
                            ))}
                        </Menu>
                    </Box>
                )}

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

RecipeFooter.displayName = "RecipeFooter";
