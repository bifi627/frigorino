import { BrokenImage, Delete, Description } from "@mui/icons-material";
import { Box, IconButton, Skeleton, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeAttachmentResponse } from "../../../../lib/api";
import { useAttachmentImage } from "../useAttachmentImage";

const THUMB_SIZE = 56;

interface RecipeAttachmentRowProps {
    householdId: number;
    recipeId: number;
    attachment: RecipeAttachmentResponse;
    onEdit: () => void;
    onDelete: () => void;
    dragHandle: ReactNode;
}

export const RecipeAttachmentRow = ({
    householdId,
    recipeId,
    attachment,
    onEdit,
    onDelete,
    dragHandle,
}: RecipeAttachmentRowProps) => {
    const { t } = useTranslation();
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
        <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: "center" }}
            data-testid={`recipe-attachment-row-${attachment.id}`}
        >
            {dragHandle}
            {/* Thumbnail + caption are one clickable target that opens the edit sheet. */}
            <Stack
                direction="row"
                spacing={1}
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
                    flex: 1,
                    minWidth: 0,
                    alignItems: "center",
                    cursor: "pointer",
                    borderRadius: 1,
                    p: 0.5,
                    "&:hover": { bgcolor: "action.hover" },
                }}
            >
                <Box
                    sx={{
                        width: THUMB_SIZE,
                        height: THUMB_SIZE,
                        flexShrink: 0,
                        borderRadius: 1,
                        overflow: "hidden",
                        bgcolor: "action.hover",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                    }}
                >
                    {isDocument ? (
                        <Description color="action" />
                    ) : isLoading ? (
                        <Skeleton
                            variant="rectangular"
                            width={THUMB_SIZE}
                            height={THUMB_SIZE}
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
                <Typography
                    variant="body2"
                    color={
                        attachment.caption ? "text.primary" : "text.secondary"
                    }
                    sx={{
                        flex: 1,
                        minWidth: 0,
                        wordBreak: "break-word",
                        fontStyle: attachment.caption ? "normal" : "italic",
                    }}
                    data-testid={`recipe-attachment-${attachment.id}-caption`}
                >
                    {attachment.caption ||
                        (isDocument ? attachment.originalFileName : null) ||
                        t("recipes.attachmentCaptionPlaceholder")}
                </Typography>
            </Stack>
            <IconButton
                size="small"
                onClick={onDelete}
                aria-label={t("recipes.deleteAttachment")}
                data-testid={`recipe-attachment-${attachment.id}-delete`}
            >
                <Delete fontSize="small" color="error" />
            </IconButton>
        </Stack>
    );
};
