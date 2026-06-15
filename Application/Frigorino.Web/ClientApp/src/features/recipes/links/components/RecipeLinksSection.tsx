import { Add } from "@mui/icons-material";
import { Box, Button, Stack, TextField } from "@mui/material";
import { useCallback, useState } from "react";
import { useTranslation } from "react-i18next";
import { CollapsibleSection } from "../../../../components/shared/CollapsibleSection";
import { SortableLinkList } from "../../../../components/sortables/SortableLinkList";
import { usePersistedExpanded } from "../../../../hooks/usePersistedExpanded";
import { useCreateRecipeLink } from "../useCreateRecipeLink";
import { useDeleteRecipeLink } from "../useDeleteRecipeLink";
import { useRecipeLinks } from "../useRecipeLinks";
import { useReorderRecipeLink } from "../useReorderRecipeLink";
import { RecipeLinkRow } from "./RecipeLinkRow";

// A valid http(s) URL — mirrors the server-side aggregate check.
const isHttpUrl = (value: string): boolean => {
    const trimmed = value.trim();
    if (!trimmed) return false;
    try {
        const parsed = new URL(trimmed);
        return parsed.protocol === "http:" || parsed.protocol === "https:";
    } catch {
        return false;
    }
};

interface RecipeLinksSectionProps {
    householdId: number;
    recipeId: number;
}

export const RecipeLinksSection = ({
    householdId,
    recipeId,
}: RecipeLinksSectionProps) => {
    const { t } = useTranslation();
    const [expanded, setExpanded] = usePersistedExpanded(
        "recipe-edit-section:links",
        false,
    );

    const { data: links = [] } = useRecipeLinks(householdId, recipeId);
    const createLink = useCreateRecipeLink();
    const deleteLink = useDeleteRecipeLink();
    const reorderLink = useReorderRecipeLink();

    // Local draft composer — a link can't be created empty (URL is required), so it POSTs only on
    // submit once a valid URL is entered.
    const [draftOpen, setDraftOpen] = useState(false);
    const [draftLabel, setDraftLabel] = useState("");
    const [draftUrl, setDraftUrl] = useState("");

    const resetDraft = useCallback(() => {
        setDraftOpen(false);
        setDraftLabel("");
        setDraftUrl("");
    }, []);

    const draftUrlInvalid = draftUrl.trim().length > 0 && !isHttpUrl(draftUrl);
    const canSubmitDraft = isHttpUrl(draftUrl);

    const handleSubmitDraft = useCallback(async () => {
        if (!canSubmitDraft) return;
        await createLink.mutateAsync({
            path: { householdId, recipeId },
            body: {
                url: draftUrl.trim(),
                label: draftLabel.trim() || null,
            },
        });
        resetDraft();
    }, [
        canSubmitDraft,
        createLink,
        householdId,
        recipeId,
        draftUrl,
        draftLabel,
        resetDraft,
    ]);

    return (
        <CollapsibleSection
            title={t("recipes.sourceLinks")}
            expanded={expanded}
            onChange={setExpanded}
            testId="recipe-section-links"
        >
            <Stack spacing={1}>
                <SortableLinkList
                    links={links}
                    onReorder={async (linkId, afterId) => {
                        await reorderLink.mutateAsync({
                            path: { householdId, recipeId, linkId },
                            body: { afterId },
                        });
                    }}
                    renderLink={(link, dragHandle) => (
                        <RecipeLinkRow
                            householdId={householdId}
                            recipeId={recipeId}
                            link={link}
                            onDelete={() =>
                                deleteLink.mutate({
                                    path: {
                                        householdId,
                                        recipeId,
                                        linkId: link.id,
                                    },
                                })
                            }
                            dragHandle={dragHandle}
                        />
                    )}
                />

                {draftOpen ? (
                    <Stack
                        spacing={1}
                        data-testid="recipe-link-draft"
                        sx={{ pt: 1 }}
                    >
                        <TextField
                            label={t("recipes.linkLabel")}
                            value={draftLabel}
                            onChange={(e) => setDraftLabel(e.target.value)}
                            size="small"
                            fullWidth
                            placeholder={t("recipes.linkLabelPlaceholder")}
                            slotProps={{
                                htmlInput: {
                                    maxLength: 255,
                                    "data-testid": "recipe-link-draft-label-input",
                                },
                            }}
                        />
                        <TextField
                            label={t("recipes.linkUrl")}
                            value={draftUrl}
                            onChange={(e) => setDraftUrl(e.target.value)}
                            size="small"
                            fullWidth
                            autoFocus
                            error={draftUrlInvalid}
                            helperText={
                                draftUrlInvalid
                                    ? t("recipes.invalidUrl")
                                    : undefined
                            }
                            placeholder={t("recipes.linkUrlPlaceholder")}
                            slotProps={{
                                htmlInput: {
                                    maxLength: 2048,
                                    "data-testid": "recipe-link-draft-url-input",
                                },
                            }}
                        />
                        <Stack direction="row" spacing={1}>
                            <Button
                                size="small"
                                variant="contained"
                                disabled={!canSubmitDraft || createLink.isPending}
                                onClick={handleSubmitDraft}
                                data-testid="recipe-link-draft-submit"
                            >
                                {t("common.add")}
                            </Button>
                            <Button
                                size="small"
                                onClick={resetDraft}
                                data-testid="recipe-link-draft-cancel"
                            >
                                {t("common.cancel")}
                            </Button>
                        </Stack>
                    </Stack>
                ) : (
                    <Box>
                        <Button
                            startIcon={<Add />}
                            onClick={() => setDraftOpen(true)}
                            data-testid="recipe-add-link"
                            sx={{ alignSelf: "flex-start" }}
                        >
                            {t("recipes.addLink")}
                        </Button>
                    </Box>
                )}
            </Stack>
        </CollapsibleSection>
    );
};
