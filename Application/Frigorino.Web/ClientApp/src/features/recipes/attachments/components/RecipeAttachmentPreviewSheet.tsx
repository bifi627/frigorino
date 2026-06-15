import { Close, Send } from "@mui/icons-material";
import {
    Box,
    Button,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    IconButton,
    TextField,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

// Mirrors RecipeAttachment.CaptionMaxLength on the backend.
const CAPTION_MAX_LENGTH = 255;

interface Props {
    file: File | null;
    isUploading: boolean;
    onSend: (caption: string | null) => void;
    onClose: () => void;
}

// Shown after a file is picked, before upload: preview the picked image and collect an optional
// caption. Mirrors the lists MediaPreviewSheet. The parent keys this sheet by whether a file is
// open, so each newly-attached file mounts a fresh sheet (empty caption) while a failed send keeps
// the same instance — preserving the typed caption for a retry.
export function RecipeAttachmentPreviewSheet({
    file,
    isUploading,
    onSend,
    onClose,
}: Props) {
    const { t } = useTranslation();
    const [caption, setCaption] = useState("");

    // Local object URL for the picked file (no server round-trip for the preview). createObjectURL
    // and revokeObjectURL MUST be paired in one effect so the URL survives StrictMode's
    // mount→unmount→remount probe (a useMemo here can outlive the cleanup that revoked it → dead URL).
    const [previewUrl, setPreviewUrl] = useState<string | null>(null);
    useEffect(() => {
        if (!file) {
            // eslint-disable-next-line react-hooks/set-state-in-effect
            setPreviewUrl(null);
            return;
        }
        const objectUrl = URL.createObjectURL(file);
        setPreviewUrl(objectUrl);
        return () => {
            URL.revokeObjectURL(objectUrl);
        };
    }, [file]);

    return (
        <Dialog
            open={Boolean(file)}
            onClose={isUploading ? undefined : onClose}
            fullWidth
            maxWidth="xs"
            data-testid="recipe-attachment-preview-sheet"
        >
            <DialogTitle
                sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                }}
            >
                {t("recipes.attachImageTitle")}
                <IconButton
                    onClick={onClose}
                    disabled={isUploading}
                    aria-label={t("common.cancel")}
                >
                    <Close />
                </IconButton>
            </DialogTitle>
            <DialogContent>
                {previewUrl ? (
                    <Box
                        component="img"
                        src={previewUrl}
                        alt=""
                        sx={{
                            width: "100%",
                            maxHeight: "50vh",
                            objectFit: "contain",
                            borderRadius: 1,
                            mb: 2,
                        }}
                    />
                ) : null}
                <TextField
                    fullWidth
                    multiline
                    minRows={1}
                    maxRows={4}
                    size="small"
                    placeholder={t("recipes.attachmentCaptionPlaceholder")}
                    value={caption}
                    onChange={(e) => setCaption(e.target.value)}
                    disabled={isUploading}
                    slotProps={{
                        htmlInput: {
                            maxLength: CAPTION_MAX_LENGTH,
                            "data-testid": "recipe-attachment-caption-input",
                        },
                    }}
                />
            </DialogContent>
            <DialogActions>
                <Button
                    variant="contained"
                    disabled={isUploading}
                    startIcon={
                        isUploading ? (
                            <CircularProgress size={16} color="inherit" />
                        ) : (
                            <Send />
                        )
                    }
                    onClick={() => onSend(caption.trim() || null)}
                    data-testid="recipe-attachment-send-button"
                >
                    {t("common.send")}
                </Button>
            </DialogActions>
        </Dialog>
    );
}
