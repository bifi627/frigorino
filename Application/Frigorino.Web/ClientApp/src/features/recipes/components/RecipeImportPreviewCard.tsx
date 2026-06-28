import {
    Alert,
    Box,
    Card,
    CardContent,
    Skeleton,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import { recipeImportErrorMessage } from "../recipeImportError";

interface RecipeImportPreview {
    name: string;
    imageUrl?: string | null;
}

interface RecipeImportPreviewCardProps {
    isPending: boolean;
    isError: boolean;
    error: unknown;
    preview: RecipeImportPreview | undefined;
}

// Pre-import peek: shows the parsed recipe name + image (rendered straight from the source URL —
// no server processing). Presentational only; the confirm controls live in the consumer.
export const RecipeImportPreviewCard = ({
    isPending,
    isError,
    error,
    preview,
}: RecipeImportPreviewCardProps) => {
    const { t } = useTranslation();

    if (isError) {
        return (
            <Alert severity="error" data-testid="recipe-import-error">
                {recipeImportErrorMessage(error, t)}
            </Alert>
        );
    }

    if (isPending) {
        return (
            <Card variant="outlined" data-testid="recipe-import-preview">
                <CardContent
                    sx={{ display: "flex", gap: 2, alignItems: "center" }}
                >
                    <Skeleton variant="rounded" width={64} height={64} />
                    <Skeleton variant="text" sx={{ flex: 1 }} />
                </CardContent>
            </Card>
        );
    }

    if (!preview) {
        return null;
    }

    return (
        <Card variant="outlined" data-testid="recipe-import-preview">
            <CardContent sx={{ display: "flex", gap: 2, alignItems: "center" }}>
                {preview.imageUrl ? (
                    <Box
                        component="img"
                        src={preview.imageUrl}
                        alt=""
                        data-testid="recipe-peek-image"
                        sx={{
                            width: 64,
                            height: 64,
                            objectFit: "cover",
                            borderRadius: 1,
                            flexShrink: 0,
                        }}
                        onError={(e) => {
                            (
                                e.currentTarget as HTMLImageElement
                            ).style.display = "none";
                        }}
                    />
                ) : null}
                <Typography
                    variant="subtitle1"
                    data-testid="recipe-peek-name"
                    sx={{ fontWeight: 600 }}
                >
                    {preview.name}
                </Typography>
            </CardContent>
        </Card>
    );
};
