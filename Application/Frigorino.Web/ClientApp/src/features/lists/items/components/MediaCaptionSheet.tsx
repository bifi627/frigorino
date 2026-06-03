import { BrokenImage, Close, Save } from "@mui/icons-material";
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
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { COMMENT_MAX_LENGTH } from "../../../../components/composer/features/commentComposerFeature";
import type { ListItemResponse } from "../../../../lib/api";
import { useItemImage } from "../useItemImage";

interface Props {
    householdId: number;
    /** The media item being edited; null closes the sheet. */
    item: ListItemResponse | null;
    isSaving: boolean;
    /** Receives the trimmed caption ("" clears it). */
    onSave: (caption: string) => void;
    onClose: () => void;
}

// Media items carry no text or quantity — only the caption (Comment) is editable. This dedicated
// sheet keeps that surface separate from the text-centric footer composer.
export function MediaCaptionSheet({
    householdId,
    item,
    isSaving,
    onSave,
    onClose,
}: Props) {
    const { t } = useTranslation();
    const [caption, setCaption] = useState("");

    // Reseed the field whenever a new item is opened.
    useEffect(() => {
        setCaption(item?.comment ?? "");
    }, [item?.id, item?.comment]);

    const {
        data: url,
        isLoading,
        isError,
    } = useItemImage(
        householdId,
        item?.listId ?? 0,
        item?.id ?? 0,
        "thumbnail",
        Boolean(item),
    );

    return (
        <Dialog
            open={Boolean(item)}
            onClose={isSaving ? undefined : onClose}
            fullWidth
            maxWidth="xs"
            data-testid="media-caption-sheet"
        >
            <DialogTitle
                sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                }}
            >
                {t("lists.editCaption")}
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
                    {isLoading ? (
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
                    placeholder={t("lists.captionPlaceholder")}
                    value={caption}
                    onChange={(e) => setCaption(e.target.value)}
                    disabled={isSaving}
                    slotProps={{
                        htmlInput: {
                            maxLength: COMMENT_MAX_LENGTH,
                            "data-testid": "media-caption-edit-input",
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
                    data-testid="media-caption-save-button"
                >
                    {t("common.save")}
                </Button>
            </DialogActions>
        </Dialog>
    );
}
