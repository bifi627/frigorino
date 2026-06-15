import { Container, Link, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useRecipeLinks } from "../useRecipeLinks";

interface RecipeViewLinksProps {
    householdId: number;
    recipeId: number;
}

export const RecipeViewLinks = ({
    householdId,
    recipeId,
}: RecipeViewLinksProps) => {
    const { t } = useTranslation();
    const { data: links = [] } = useRecipeLinks(householdId, recipeId);

    if (links.length === 0) return null;

    return (
        <Container
            maxWidth="sm"
            data-testid="recipe-view-links"
            sx={{ px: 2, pb: 1, flexShrink: 0 }}
        >
            <Typography
                variant="overline"
                color="text.secondary"
                sx={{ fontWeight: 700, letterSpacing: 1 }}
            >
                {t("recipes.sourceLinks")}
            </Typography>
            <Stack spacing={0.5} sx={{ mt: 0.5 }}>
                {links.map((link) => (
                    <Link
                        key={link.id}
                        href={link.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        variant="body2"
                        data-testid={`recipe-link-${link.id}`}
                        sx={{ wordBreak: "break-word" }}
                    >
                        {link.label?.trim() || link.url}
                    </Link>
                ))}
            </Stack>
        </Container>
    );
};
