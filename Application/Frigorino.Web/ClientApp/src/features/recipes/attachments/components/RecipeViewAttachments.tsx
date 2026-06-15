import { BrokenImage } from "@mui/icons-material";
import { Box, Container, Skeleton, Typography } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { RecipeAttachmentResponse } from "../../../../lib/api";
import { useAttachmentImage } from "../useAttachmentImage";
import { useRecipeAttachments } from "../useRecipeAttachments";
import { RecipeAttachmentLightbox } from "./RecipeAttachmentLightbox";

interface RecipeViewAttachmentsProps {
    householdId: number;
    recipeId: number;
}

const Tile = ({
    householdId,
    recipeId,
    attachment,
    onOpen,
}: {
    householdId: number;
    recipeId: number;
    attachment: RecipeAttachmentResponse;
    onOpen: () => void;
}) => {
    const {
        data: url,
        isLoading,
        isError,
    } = useAttachmentImage(householdId, recipeId, attachment.id, "thumbnail");

    return (
        <Box>
            <Box
                role="button"
                tabIndex={0}
                aria-label="open image"
                data-testid={`recipe-attachment-${attachment.id}`}
                onClick={onOpen}
                onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        onOpen();
                    }
                }}
                sx={{
                    aspectRatio: "1 / 1",
                    width: "100%",
                    borderRadius: 1,
                    overflow: "hidden",
                    cursor: "pointer",
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
            {attachment.caption ? (
                <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{
                        display: "block",
                        mt: 0.5,
                        wordBreak: "break-word",
                    }}
                >
                    {attachment.caption}
                </Typography>
            ) : null}
        </Box>
    );
};

export const RecipeViewAttachments = ({
    householdId,
    recipeId,
}: RecipeViewAttachmentsProps) => {
    const { t } = useTranslation();
    const { data: attachments = [] } = useRecipeAttachments(
        householdId,
        recipeId,
    );
    const [openId, setOpenId] = useState<number | null>(null);

    if (attachments.length === 0) return null;

    const openAttachment = attachments.find((a) => a.id === openId) ?? null;

    return (
        <Container
            maxWidth="sm"
            data-testid="recipe-view-attachments"
            sx={{ px: 2, pb: 1 }}
        >
            <Typography
                variant="overline"
                color="text.secondary"
                sx={{ fontWeight: 700, letterSpacing: 1 }}
            >
                {t("recipes.attachments")}
            </Typography>
            <Box
                sx={{
                    mt: 0.5,
                    display: "grid",
                    gridTemplateColumns: {
                        xs: "repeat(2, 1fr)",
                        sm: "repeat(3, 1fr)",
                    },
                    gap: 1,
                }}
            >
                {attachments.map((attachment) => (
                    <Tile
                        key={attachment.id}
                        householdId={householdId}
                        recipeId={recipeId}
                        attachment={attachment}
                        onOpen={() => setOpenId(attachment.id)}
                    />
                ))}
            </Box>
            {openAttachment ? (
                <RecipeAttachmentLightbox
                    householdId={householdId}
                    recipeId={recipeId}
                    attachmentId={openAttachment.id}
                    caption={openAttachment.caption}
                    open={openId !== null}
                    onClose={() => setOpenId(null)}
                />
            ) : null}
        </Container>
    );
};
