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
import { COMMENT_MAX_LENGTH } from "../../../../components/composer/features/commentComposerFeature";

interface Props {
    file: File | null;
    isUploading: boolean;
    onSend: (caption: string | null) => void;
    onClose: () => void;
}

export function MediaPreviewSheet({
    file,
    isUploading,
    onSend,
    onClose,
}: Props) {
    const { t } = useTranslation();
    // Caption starts empty and stays for the life of this preview. The parent keys the sheet by
    // whether a file is open, so each newly-attached file mounts a fresh sheet (empty caption) while
    // a failed send keeps the same instance — preserving the typed caption for a retry.
    const [caption, setCaption] = useState("");

    // Local object URL for the picked file (no server round-trip for the preview). createObjectURL
    // and revokeObjectURL MUST be paired in one effect so the URL survives StrictMode's
    // mount→unmount→remount probe (a useMemo here can outlive the cleanup that revoked it → dead URL).
    const [previewUrl, setPreviewUrl] = useState<string | null>(null);
    useEffect(() => {
        if (!file) {
            // The object URL is a browser resource that must be created AND revoked inside one
            // effect (the StrictMode contract above); deriving it in render or useMemo would
            // outlive the cleanup that revoked it and hand back a dead URL.
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
            data-testid="media-preview-sheet"
        >
            <DialogTitle
                sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                }}
            >
                {t("lists.attachPhoto")}
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
                    placeholder={t("lists.captionPlaceholder")}
                    value={caption}
                    onChange={(e) => setCaption(e.target.value)}
                    disabled={isUploading}
                    slotProps={{
                        htmlInput: {
                            maxLength: COMMENT_MAX_LENGTH,
                            "data-testid": "media-caption-input",
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
                    data-testid="media-send-button"
                >
                    {t("common.send")}
                </Button>
            </DialogActions>
        </Dialog>
    );
}
