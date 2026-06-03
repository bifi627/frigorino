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
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { COMMENT_MAX_LENGTH } from "../../../../components/composer/features/commentComposerFeature";

interface Props {
    file: File | null;
    isUploading: boolean;
    onSend: (caption: string | null) => void;
    onClose: () => void;
}

export function MediaPreviewSheet({ file, isUploading, onSend, onClose }: Props) {
    const { t } = useTranslation();
    const [caption, setCaption] = useState("");

    // Local object URL for the picked file (no server round-trip for the preview).
    const previewUrl = useMemo(() => (file ? URL.createObjectURL(file) : null), [file]);
    useEffect(() => {
        return () => {
            if (previewUrl) {
                URL.revokeObjectURL(previewUrl);
            }
        };
    }, [previewUrl]);

    // Reset caption whenever a new file is opened.
    useEffect(() => {
        setCaption("");
    }, [file]);

    return (
        <Dialog open={Boolean(file)} onClose={isUploading ? undefined : onClose} fullWidth maxWidth="xs" data-testid="media-preview-sheet">
            <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                {t("lists.attachPhoto")}
                <IconButton onClick={onClose} disabled={isUploading} aria-label={t("common.cancel")}>
                    <Close />
                </IconButton>
            </DialogTitle>
            <DialogContent>
                {previewUrl ? (
                    <Box
                        component="img"
                        src={previewUrl}
                        alt=""
                        sx={{ width: "100%", maxHeight: "50vh", objectFit: "contain", borderRadius: 1, mb: 2 }}
                    />
                ) : null}
                <TextField
                    fullWidth
                    multiline
                    minRows={1}
                    maxRows={4}
                    size="small"
                    placeholder={t("lists.captionPlaceholder")}
                    value={caption}
                    onChange={(e) => setCaption(e.target.value)}
                    disabled={isUploading}
                    slotProps={{ htmlInput: { maxLength: COMMENT_MAX_LENGTH, "data-testid": "media-caption-input" } }}
                />
            </DialogContent>
            <DialogActions>
                <Button
                    variant="contained"
                    disabled={isUploading}
                    startIcon={isUploading ? <CircularProgress size={16} color="inherit" /> : <Send />}
                    onClick={() => onSend(caption.trim() || null)}
                    data-testid="media-send-button"
                >
                    {t("common.send")}
                </Button>
            </DialogActions>
        </Dialog>
    );
}
