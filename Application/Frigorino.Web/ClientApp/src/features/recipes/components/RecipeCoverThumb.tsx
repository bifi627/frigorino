import { RestaurantMenu } from "@mui/icons-material";
import { Box } from "@mui/material";
import { useAttachmentImage } from "../attachments/useAttachmentImage";

interface RecipeCoverThumbProps {
    householdId: number;
    recipeId: number;
    coverAttachmentId?: number | null;
    name: string;
}

const SIZE = 52;

export const RecipeCoverThumb = ({
    householdId,
    recipeId,
    coverAttachmentId,
    name,
}: RecipeCoverThumbProps) => {
    const hasCover = Boolean(coverAttachmentId && coverAttachmentId > 0);
    const { data: url } = useAttachmentImage(
        householdId,
        recipeId,
        coverAttachmentId ?? 0,
        "thumbnail",
        hasCover,
    );

    return (
        <Box
            sx={{
                width: SIZE,
                height: SIZE,
                flexShrink: 0,
                borderRadius: 1.5,
                overflow: "hidden",
                bgcolor: "action.hover",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "text.disabled",
            }}
        >
            {url ? (
                <Box
                    component="img"
                    src={url}
                    alt={name}
                    sx={{ width: "100%", height: "100%", objectFit: "cover" }}
                />
            ) : (
                <RestaurantMenu fontSize="small" />
            )}
        </Box>
    );
};
