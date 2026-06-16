import { Add, Description, Image, PhotoCamera } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    ClickAwayListener,
    Grow,
    ListItemIcon,
    ListItemText,
    MenuItem,
    MenuList,
    Paper,
    Popper,
    Stack,
} from "@mui/material";
import { useCallback, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { CollapsibleSection } from "../../../../components/shared/CollapsibleSection";
import { SortableLinkList } from "../../../../components/sortables/SortableLinkList";
import { usePersistedExpanded } from "../../../../hooks/usePersistedExpanded";
import type { RecipeAttachmentResponse } from "../../../../lib/api";
import { useCreateRecipeAttachment } from "../useCreateRecipeAttachment";
import { useDeleteRecipeAttachment } from "../useDeleteRecipeAttachment";
import { useRecipeAttachments } from "../useRecipeAttachments";
import { useReorderRecipeAttachment } from "../useReorderRecipeAttachment";
import { useUpdateRecipeAttachment } from "../useUpdateRecipeAttachment";
import { RecipeAttachmentCaptionSheet } from "./RecipeAttachmentCaptionSheet";
import { RecipeAttachmentPreviewSheet } from "./RecipeAttachmentPreviewSheet";
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
    const updateAttachment = useUpdateRecipeAttachment();
    const deleteAttachment = useDeleteRecipeAttachment();
    const reorderAttachment = useReorderRecipeAttachment();

    const fileInputRef = useRef<HTMLInputElement>(null);
    const documentInputRef = useRef<HTMLInputElement>(null);
    const [uploadError, setUploadError] = useState<string | null>(null);
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    const menuOpen = Boolean(menuAnchor);

    // The picked file awaiting a caption + upload (drives the preview sheet).
    const [pendingFile, setPendingFile] = useState<File | null>(null);
    // The attachment whose caption is being edited (drives the caption sheet).
    const [editingAttachment, setEditingAttachment] =
        useState<RecipeAttachmentResponse | null>(null);

    const handlePick = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        // Reset the input so picking the same file again re-fires change.
        e.target.value = "";
        if (!file) return;
        setUploadError(null);
        setPendingFile(file);
    }, []);

    const handleSend = useCallback(
        async (caption: string | null) => {
            if (!pendingFile) return;
            try {
                await createAttachment.mutateAsync({
                    path: { householdId, recipeId },
                    body: { file: pendingFile, caption: caption ?? undefined },
                });
                setPendingFile(null);
            } catch {
                setUploadError(t("recipes.uploadFailed"));
            }
        },
        [createAttachment, householdId, recipeId, pendingFile, t],
    );

    const handleSaveCaption = useCallback(
        (caption: string) => {
            if (!editingAttachment) return;
            updateAttachment.mutate({
                path: {
                    householdId,
                    recipeId,
                    attachmentId: editingAttachment.id,
                },
                body: { caption: caption || null },
            });
            setEditingAttachment(null);
        },
        [updateAttachment, householdId, recipeId, editingAttachment],
    );

    // Drive the single hidden input two ways: with `capture` set, mobile opens the camera;
    // with it cleared, the file/gallery picker. Toggling imperatively right before click()
    // avoids Android Chrome's quirk where a no-capture, MIME-restricted input never offers
    // the camera. (Mirrors attachComposerFeature.)
    const openPicker = (useCamera: boolean) => {
        setMenuAnchor(null);
        const input = fileInputRef.current;
        if (!input) {
            return;
        }
        if (useCamera) {
            input.setAttribute("capture", "environment");
        } else {
            input.removeAttribute("capture");
        }
        input.click();
    };

    const openDocumentPicker = () => {
        setMenuAnchor(null);
        documentInputRef.current?.click();
    };

    const handleClickAway = (event: MouseEvent | TouchEvent) => {
        // The trigger button's own click toggles the menu; ignore it here so the
        // open click isn't immediately treated as a click-away.
        if (menuAnchor && menuAnchor.contains(event.target as Node)) {
            return;
        }
        setMenuAnchor(null);
    };

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
                            onEdit={() => setEditingAttachment(attachment)}
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
                    <Button
                        startIcon={<Add />}
                        onClick={(e) =>
                            setMenuAnchor(menuAnchor ? null : e.currentTarget)
                        }
                        disabled={createAttachment.isPending}
                        aria-haspopup="true"
                        aria-expanded={menuOpen ? "true" : undefined}
                        aria-controls={
                            menuOpen ? "recipe-attachment-menu" : undefined
                        }
                        data-testid="recipe-add-attachment"
                        sx={{ alignSelf: "flex-start" }}
                    >
                        {t("recipes.addAttachment")}
                    </Button>

                    <Popper
                        open={menuOpen}
                        anchorEl={menuAnchor}
                        placement="bottom-start"
                        transition
                        sx={{ zIndex: (theme) => theme.zIndex.modal }}
                    >
                        {({ TransitionProps }) => (
                            <Grow
                                {...TransitionProps}
                                style={{ transformOrigin: "left top" }}
                            >
                                <Paper elevation={3}>
                                    <ClickAwayListener
                                        onClickAway={handleClickAway}
                                    >
                                        <MenuList
                                            id="recipe-attachment-menu"
                                            autoFocusItem={false}
                                            disablePadding
                                        >
                                            <MenuItem
                                                data-testid="recipe-attachment-camera"
                                                onClick={() => openPicker(true)}
                                            >
                                                <ListItemIcon>
                                                    <PhotoCamera fontSize="small" />
                                                </ListItemIcon>
                                                <ListItemText>
                                                    {t("lists.takePhoto")}
                                                </ListItemText>
                                            </MenuItem>
                                            <MenuItem
                                                data-testid="recipe-attachment-photo"
                                                onClick={() =>
                                                    openPicker(false)
                                                }
                                            >
                                                <ListItemIcon>
                                                    <Image fontSize="small" />
                                                </ListItemIcon>
                                                <ListItemText>
                                                    {t("lists.choosePhoto")}
                                                </ListItemText>
                                            </MenuItem>
                                            <MenuItem
                                                data-testid="recipe-attachment-document"
                                                onClick={openDocumentPicker}
                                            >
                                                <ListItemIcon>
                                                    <Description fontSize="small" />
                                                </ListItemIcon>
                                                <ListItemText>
                                                    {t("lists.attachDocument")}
                                                </ListItemText>
                                            </MenuItem>
                                        </MenuList>
                                    </ClickAwayListener>
                                </Paper>
                            </Grow>
                        )}
                    </Popper>

                    {/* `capture` is set/cleared imperatively in openPicker() before .click(). */}
                    <input
                        ref={fileInputRef}
                        type="file"
                        accept={ACCEPT}
                        hidden
                        onChange={handlePick}
                        data-testid="recipe-attachment-file-input"
                    />
                    <input
                        ref={documentInputRef}
                        type="file"
                        accept="application/pdf"
                        hidden
                        onChange={handlePick}
                        data-testid="recipe-attachment-document-input"
                    />
                </Box>
            </Stack>

            <RecipeAttachmentPreviewSheet
                key={pendingFile ? "open" : "closed"}
                file={pendingFile}
                isUploading={createAttachment.isPending}
                onSend={handleSend}
                onClose={() => setPendingFile(null)}
            />

            <RecipeAttachmentCaptionSheet
                key={editingAttachment?.id ?? "closed"}
                householdId={householdId}
                recipeId={recipeId}
                attachment={editingAttachment}
                isSaving={updateAttachment.isPending}
                onSave={handleSaveCaption}
                onClose={() => setEditingAttachment(null)}
            />
        </CollapsibleSection>
    );
};
