import { Close } from "@mui/icons-material";
import {
    Box,
    CircularProgress,
    Dialog,
    IconButton,
    Typography,
} from "@mui/material";
import { useItemImage } from "../useItemImage";

interface Props {
    householdId: number;
    listId: number;
    itemId: number;
    caption?: string | null;
    open: boolean;
    onClose: () => void;
}

export function ImageLightbox({
    householdId,
    listId,
    itemId,
    caption,
    open,
    onClose,
}: Props) {
    const { data: url, isLoading } = useItemImage(
        householdId,
        listId,
        itemId,
        "file",
        open,
    );

    return (
        <Dialog open={open} onClose={onClose} maxWidth="lg" data-testid="image-lightbox">
            <Box sx={{ position: "relative", bgcolor: "common.black" }}>
                <IconButton
                    onClick={onClose}
                    aria-label="close"
                    sx={{ position: "absolute", top: 8, right: 8, color: "common.white", zIndex: 1 }}
                >
                    <Close />
                </IconButton>
                {isLoading || !url ? (
                    <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: 240, minWidth: 240 }}>
                        <CircularProgress sx={{ color: "common.white" }} />
                    </Box>
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={caption ?? ""}
                        sx={{ display: "block", maxWidth: "90vw", maxHeight: "85vh", width: "auto", height: "auto" }}
                    />
                )}
            </Box>
            {caption ? (
                <Typography variant="body2" sx={{ p: 1.5, color: "text.secondary" }}>
                    {caption}
                </Typography>
            ) : null}
        </Dialog>
    );
}
