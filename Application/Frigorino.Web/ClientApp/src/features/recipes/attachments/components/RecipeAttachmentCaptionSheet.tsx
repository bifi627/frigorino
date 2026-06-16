import { BrokenImage, Close, Description, Save } from "@mui/icons-material";
import {
    Box,
    Button,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    IconButton,
    Skeleton,
    TextField,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeAttachmentResponse } from "../../../../lib/api";
import { useAttachmentImage } from "../useAttachmentImage";

// Mirrors RecipeAttachment.CaptionMaxLength on the backend.
const CAPTION_MAX_LENGTH = 255;

interface Props {
    householdId: number;
    recipeId: number;
    /** The attachment being edited; null closes the sheet. */
    attachment: RecipeAttachmentResponse | null;
    isSaving: boolean;
    /** Receives the trimmed caption ("" clears it). */
    onSave: (caption: string) => void;
    onClose: () => void;
}

// Edit an existing attachment's caption. Mirrors the lists MediaCaptionSheet. The parent keys this
// sheet by attachment id, so opening a different attachment remounts and reseeds the caption — no
// reset-in-effect.
export function RecipeAttachmentCaptionSheet({
    householdId,
    recipeId,
    attachment,
    isSaving,
    onSave,
    onClose,
}: Props) {
    const { t } = useTranslation();
    const [caption, setCaption] = useState(attachment?.caption ?? "");
    const isDocument = attachment?.type === "Document";

    const {
        data: url,
        isLoading,
        isError,
    } = useAttachmentImage(
        householdId,
        recipeId,
        attachment?.id ?? 0,
        "thumbnail",
        Boolean(attachment) && !isDocument,
    );

    return (
        <Dialog
            open={Boolean(attachment)}
            onClose={isSaving ? undefined : onClose}
            fullWidth
            maxWidth="xs"
            data-testid="recipe-attachment-caption-sheet"
        >
            <DialogTitle
                sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                }}
            >
                {t("recipes.editAttachmentCaption")}
                <IconButton
                    onClick={onClose}
                    disabled={isSaving}
                    aria-label={t("common.cancel")}
                >
                    <Close />
                </IconButton>
            </DialogTitle>
            <DialogContent>
                <Box
                    sx={{
                        width: "100%",
                        height: 160,
                        mb: 2,
                        borderRadius: 1,
                        overflow: "hidden",
                        bgcolor: "action.hover",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                    }}
                >
                    {isDocument ? (
                        <Description color="action" fontSize="large" />
                    ) : isLoading ? (
                        <Skeleton
                            variant="rectangular"
                            width="100%"
                            height="100%"
                        />
                    ) : isError || !url ? (
                        <BrokenImage color="disabled" />
                    ) : (
                        <Box
                            component="img"
                            src={url}
                            alt=""
                            sx={{
                                width: "100%",
                                height: "100%",
                                objectFit: "contain",
                            }}
                        />
                    )}
                </Box>
                <TextField
                    fullWidth
                    multiline
                    minRows={1}
                    maxRows={4}
                    size="small"
                    placeholder={t("recipes.attachmentCaptionPlaceholder")}
                    value={caption}
                    onChange={(e) => setCaption(e.target.value)}
                    disabled={isSaving}
                    slotProps={{
                        htmlInput: {
                            maxLength: CAPTION_MAX_LENGTH,
                            "data-testid":
                                "recipe-attachment-caption-edit-input",
                        },
                    }}
                />
            </DialogContent>
            <DialogActions>
                <Button
                    variant="contained"
                    disabled={isSaving}
                    startIcon={
                        isSaving ? (
                            <CircularProgress size={16} color="inherit" />
                        ) : (
                            <Save />
                        )
                    }
                    onClick={() => onSave(caption.trim())}
                    data-testid="recipe-attachment-caption-save-button"
                >
                    {t("common.save")}
                </Button>
            </DialogActions>
        </Dialog>
    );
}
