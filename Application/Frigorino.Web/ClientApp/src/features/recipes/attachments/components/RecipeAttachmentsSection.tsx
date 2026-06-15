import { Add } from "@mui/icons-material";
import { Alert, Box, Button, Stack } from "@mui/material";
import { useCallback, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { CollapsibleSection } from "../../../../components/shared/CollapsibleSection";
import { SortableLinkList } from "../../../../components/sortables/SortableLinkList";
import { usePersistedExpanded } from "../../../../hooks/usePersistedExpanded";
import { useCreateRecipeAttachment } from "../useCreateRecipeAttachment";
import { useDeleteRecipeAttachment } from "../useDeleteRecipeAttachment";
import { useRecipeAttachments } from "../useRecipeAttachments";
import { useReorderRecipeAttachment } from "../useReorderRecipeAttachment";
import { RecipeAttachmentRow } from "./RecipeAttachmentRow";

const ACCEPT = "image/jpeg,image/png,image/webp";

interface RecipeAttachmentsSectionProps {
    householdId: number;
    recipeId: number;
}

export const RecipeAttachmentsSection = ({
    householdId,
    recipeId,
}: RecipeAttachmentsSectionProps) => {
    const { t } = useTranslation();
    const [expanded, setExpanded] = usePersistedExpanded(
        "recipe-edit-section:attachments",
        false,
    );

    const { data: attachments = [] } = useRecipeAttachments(
        householdId,
        recipeId,
    );
    const createAttachment = useCreateRecipeAttachment();
    const deleteAttachment = useDeleteRecipeAttachment();
    const reorderAttachment = useReorderRecipeAttachment();

    const fileInputRef = useRef<HTMLInputElement>(null);
    const [uploadError, setUploadError] = useState<string | null>(null);

    const handlePick = useCallback(
        async (e: React.ChangeEvent<HTMLInputElement>) => {
            const file = e.target.files?.[0];
            // Reset the input so picking the same file again re-fires change.
            e.target.value = "";
            if (!file) return;
            setUploadError(null);
            try {
                await createAttachment.mutateAsync({
                    path: { householdId, recipeId },
                    body: { file },
                });
            } catch {
                setUploadError(t("recipes.uploadFailed"));
            }
        },
        [createAttachment, householdId, recipeId, t],
    );

    return (
        <CollapsibleSection
            title={t("recipes.attachments")}
            expanded={expanded}
            onChange={setExpanded}
            testId="recipe-section-attachments"
        >
            <Stack spacing={1}>
                <SortableLinkList
                    links={attachments}
                    onReorder={async (attachmentId, afterId) => {
                        await reorderAttachment.mutateAsync({
                            path: { householdId, recipeId, attachmentId },
                            body: { afterId },
                        });
                    }}
                    renderLink={(attachment, dragHandle) => (
                        <RecipeAttachmentRow
                            householdId={householdId}
                            recipeId={recipeId}
                            attachment={attachment}
                            onDelete={() =>
                                deleteAttachment.mutate({
                                    path: {
                                        householdId,
                                        recipeId,
                                        attachmentId: attachment.id,
                                    },
                                })
                            }
                            dragHandle={dragHandle}
                        />
                    )}
                />

                {uploadError ? (
                    <Alert
                        severity="error"
                        onClose={() => setUploadError(null)}
                        data-testid="recipe-attachment-upload-error"
                    >
                        {uploadError}
                    </Alert>
                ) : null}

                <Box>
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept={ACCEPT}
                        hidden
                        onChange={handlePick}
                        data-testid="recipe-attachment-file-input"
                    />
                    <Button
                        startIcon={<Add />}
                        onClick={() => fileInputRef.current?.click()}
                        disabled={createAttachment.isPending}
                        data-testid="recipe-add-attachment"
                        sx={{ alignSelf: "flex-start" }}
                    >
                        {t("recipes.addAttachment")}
                    </Button>
                </Box>
            </Stack>
        </CollapsibleSection>
    );
};
