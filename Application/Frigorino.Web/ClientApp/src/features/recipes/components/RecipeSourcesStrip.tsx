import {
    Add,
    BrokenImage,
    Delete,
    Description,
    Image,
    Link as LinkIcon,
    PhotoCamera,
} from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    Chip,
    ClickAwayListener,
    Grow,
    IconButton,
    ListItemIcon,
    ListItemText,
    MenuItem,
    MenuList,
    Paper,
    Popper,
    Skeleton,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import type { ReactNode } from "react";
import { useCallback, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { SortableLinkList } from "../../../components/sortables/SortableLinkList";
import type {
    RecipeAttachmentResponse,
    RecipeLinkResponse,
} from "../../../lib/api";
import { neutralActionColor, tintedActionButtonSx } from "../../../theme";
import { RecipeAttachmentCaptionSheet } from "../attachments/components/RecipeAttachmentCaptionSheet";
import { RecipeAttachmentPreviewSheet } from "../attachments/components/RecipeAttachmentPreviewSheet";
import { useAttachmentImage } from "../attachments/useAttachmentImage";
import { useCreateRecipeAttachment } from "../attachments/useCreateRecipeAttachment";
import { useDeleteRecipeAttachment } from "../attachments/useDeleteRecipeAttachment";
import { useRecipeAttachments } from "../attachments/useRecipeAttachments";
import { useReorderRecipeAttachment } from "../attachments/useReorderRecipeAttachment";
import { useUpdateRecipeAttachment } from "../attachments/useUpdateRecipeAttachment";
import { useCreateRecipeLink } from "../links/useCreateRecipeLink";
import { useDeleteRecipeLink } from "../links/useDeleteRecipeLink";
import { useRecipeLinks } from "../links/useRecipeLinks";
import { useReorderRecipeLink } from "../links/useReorderRecipeLink";

const ACCEPT = "image/jpeg,image/png,image/webp";
const TILE_SIZE = 64;

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

// Read-only link chip (label or URL, opens in a new tab) with delete. Inline label/URL editing was
// dropped in the strip recompose — re-add via the draft form. Links have no UI integration test.
const LinkChip = ({
    link,
    onDelete,
    dragHandle,
}: {
    link: RecipeLinkResponse;
    onDelete: () => void;
    dragHandle: ReactNode;
}) => (
    <Stack
        direction="row"
        sx={{ alignItems: "center", flexShrink: 0 }}
        data-testid={`recipe-link-chip-${link.id}`}
    >
        {dragHandle}
        <Chip
            component="a"
            href={link.url}
            target="_blank"
            rel="noopener noreferrer"
            clickable
            variant="outlined"
            size="small"
            label={link.label?.trim() || link.url}
            onDelete={onDelete}
            sx={{ maxWidth: 220 }}
        />
    </Stack>
);

// A single photo/document as a square tile with its caption beneath. Keeps every per-attachment
// testid the integration test filters on.
const PhotoTile = ({
    householdId,
    recipeId,
    attachment,
    onEdit,
    onDelete,
    dragHandle,
}: {
    householdId: number;
    recipeId: number;
    attachment: RecipeAttachmentResponse;
    onEdit: () => void;
    onDelete: () => void;
    dragHandle: ReactNode;
}) => {
    const isDocument = attachment.type === "Document";
    const {
        data: url,
        isLoading,
        isError,
    } = useAttachmentImage(
        householdId,
        recipeId,
        attachment.id,
        "thumbnail",
        !isDocument,
    );
    return (
        <Box
            data-testid={`recipe-attachment-row-${attachment.id}`}
            sx={{ position: "relative", flexShrink: 0 }}
        >
            <Box sx={{ position: "absolute", top: -6, left: -6, zIndex: 1 }}>
                {dragHandle}
            </Box>
            <Box
                role="button"
                tabIndex={0}
                onClick={onEdit}
                onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        onEdit();
                    }
                }}
                data-testid={`recipe-attachment-${attachment.id}-edit`}
                sx={{
                    width: TILE_SIZE,
                    height: TILE_SIZE,
                    borderRadius: 1.5,
                    overflow: "hidden",
                    bgcolor: "action.hover",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    cursor: "pointer",
                }}
            >
                {isDocument ? (
                    <Description color="action" />
                ) : isLoading ? (
                    <Skeleton
                        variant="rectangular"
                        width={TILE_SIZE}
                        height={TILE_SIZE}
                    />
                ) : isError || !url ? (
                    <BrokenImage fontSize="small" color="disabled" />
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={attachment.caption ?? ""}
                        sx={{
                            width: "100%",
                            height: "100%",
                            objectFit: "cover",
                        }}
                    />
                )}
            </Box>
            {/* Caption testid the IT filters on; transparent when empty so the strip stays tidy. */}
            <Typography
                variant="caption"
                data-testid={`recipe-attachment-${attachment.id}-caption`}
                sx={{
                    display: "block",
                    maxWidth: TILE_SIZE,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    color: attachment.caption ? "text.secondary" : "transparent",
                }}
            >
                {attachment.caption ||
                    (isDocument ? attachment.originalFileName : "·")}
            </Typography>
            <IconButton
                size="small"
                onClick={onDelete}
                data-testid={`recipe-attachment-${attachment.id}-delete`}
                sx={{ position: "absolute", top: -6, right: -6, zIndex: 1 }}
            >
                <Delete fontSize="small" color="error" />
            </IconButton>
        </Box>
    );
};

interface RecipeSourcesStripProps {
    householdId: number;
    recipeId: number;
}

export const RecipeSourcesStrip = ({
    householdId,
    recipeId,
}: RecipeSourcesStripProps) => {
    const { t } = useTranslation();

    const { data: links = [] } = useRecipeLinks(householdId, recipeId);
    const createLink = useCreateRecipeLink();
    const deleteLink = useDeleteRecipeLink();
    const reorderLink = useReorderRecipeLink();

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

    // Local link draft — a link can't be created empty (URL is required), so it POSTs only on submit
    // once a valid URL is entered.
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
            body: { url: draftUrl.trim(), label: draftLabel.trim() || null },
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

    // Drive the single hidden input two ways: with `capture` set, mobile opens the camera; with it
    // cleared, the file/gallery picker. Toggling imperatively right before click() avoids Android
    // Chrome's quirk where a no-capture, MIME-restricted input never offers the camera.
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
        // The trigger button's own click toggles the menu; ignore it here so the open click isn't
        // immediately treated as a click-away.
        if (menuAnchor && menuAnchor.contains(event.target as Node)) {
            return;
        }
        setMenuAnchor(null);
    };

    return (
        <Box>
            <Stack
                direction="row"
                sx={{
                    alignItems: "center",
                    justifyContent: "space-between",
                }}
            >
                <Typography
                    variant="overline"
                    color="text.secondary"
                    sx={{ fontWeight: 700, letterSpacing: 1 }}
                >
                    {t("recipes.sourcesAndPhotos")}
                </Typography>
                <Stack direction="row" spacing={0.5}>
                    <IconButton
                        size="small"
                        onClick={() => setDraftOpen(true)}
                        data-testid="recipe-add-link"
                        sx={tintedActionButtonSx(neutralActionColor)}
                    >
                        <LinkIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                        size="small"
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
                        sx={tintedActionButtonSx(neutralActionColor)}
                    >
                        <Add fontSize="small" />
                    </IconButton>
                </Stack>
            </Stack>

            <Popper
                open={menuOpen}
                anchorEl={menuAnchor}
                placement="bottom-end"
                transition
                sx={{ zIndex: (theme) => theme.zIndex.modal }}
            >
                {({ TransitionProps }) => (
                    <Grow
                        {...TransitionProps}
                        style={{ transformOrigin: "right top" }}
                    >
                        <Paper elevation={3}>
                            <ClickAwayListener onClickAway={handleClickAway}>
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
                                        onClick={() => openPicker(false)}
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

            {uploadError ? (
                <Alert
                    severity="error"
                    onClose={() => setUploadError(null)}
                    data-testid="recipe-attachment-upload-error"
                    sx={{ mt: 1 }}
                >
                    {uploadError}
                </Alert>
            ) : null}

            {draftOpen ? (
                <Stack spacing={1} data-testid="recipe-link-draft" sx={{ pt: 1 }}>
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
                            draftUrlInvalid ? t("recipes.invalidUrl") : undefined
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
            ) : null}

            {/* Links + photos share one horizontally-scrollable strip: link chips first, then photo
                tiles. Each group keeps its own SortableLinkList (separate reorder endpoints). */}
            <Box
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 1.5,
                    overflowX: "auto",
                    pt: 1,
                }}
            >
                {links.length > 0 ? (
                    <SortableLinkList
                        horizontal
                        links={links}
                        onReorder={async (linkId, afterId) => {
                            await reorderLink.mutateAsync({
                                path: { householdId, recipeId, linkId },
                                body: { afterId },
                            });
                        }}
                        renderLink={(link, dragHandle) => (
                            <LinkChip
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
                ) : null}

                <Box
                    data-testid="recipe-section-attachments-content"
                    sx={{ display: "flex", alignItems: "center", gap: 1.5 }}
                >
                    <SortableLinkList
                        horizontal
                        links={attachments}
                        onReorder={async (attachmentId, afterId) => {
                            await reorderAttachment.mutateAsync({
                                path: { householdId, recipeId, attachmentId },
                                body: { afterId },
                            });
                        }}
                        renderLink={(attachment, dragHandle) => (
                            <PhotoTile
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
                </Box>
            </Box>

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
        </Box>
    );
};
