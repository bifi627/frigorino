import { ExpandLess, ExpandMore, MoreVert } from "@mui/icons-material";
import {
    Box,
    Button,
    Card,
    Chip,
    Collapse,
    IconButton,
    Stack,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";
import { RecipeCoverThumb } from "./RecipeCoverThumb";

interface RecipeSummaryCardProps {
    recipe: RecipeResponse;
    householdId: number;
    expanded: boolean;
    query: string;
    onToggleExpand: (recipeId: number) => void;
    onOpen: (recipeId: number) => void;
    onMenuOpen: (
        event: React.MouseEvent<HTMLElement>,
        recipe: RecipeResponse,
    ) => void;
}

const MAX_PEEK_CHIPS = 8;
const oneLineSx = {
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
} as const;

export const RecipeSummaryCard = ({
    recipe,
    householdId,
    expanded,
    query,
    onToggleExpand,
    onOpen,
    onMenuOpen,
}: RecipeSummaryCardProps) => {
    const { t } = useTranslation();
    const q = query.trim().toLowerCase();
    const ingredients = recipe.ingredients ?? [];
    const shownIngredients = ingredients.slice(0, MAX_PEEK_CHIPS);
    const overflowCount = ingredients.length - shownIngredients.length;

    const open = () => recipe.id && onOpen(recipe.id);
    const toggle = (e: React.MouseEvent<HTMLElement>) => {
        e.stopPropagation();
        if (recipe.id) {
            onToggleExpand(recipe.id);
        }
    };

    return (
        <Card
            elevation={1}
            data-testid={`recipe-card-${recipe.name}`}
            data-recipe-name={recipe.name}
        >
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, p: 1 }}>
                <Box
                    data-testid={`recipe-item-${recipe.name}`}
                    onClick={open}
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        gap: 1.5,
                        flex: 1,
                        minWidth: 0,
                        cursor: "pointer",
                    }}
                >
                    <RecipeCoverThumb
                        householdId={householdId}
                        recipeId={recipe.id ?? 0}
                        coverAttachmentId={recipe.coverAttachmentId}
                        name={recipe.name}
                    />
                    <Box sx={{ minWidth: 0 }}>
                        <Typography
                            variant="body1"
                            sx={{ fontWeight: 600, ...oneLineSx }}
                        >
                            {recipe.name || t("recipes.untitledRecipe")}
                        </Typography>
                        {recipe.description && (
                            <Typography
                                variant="body2"
                                sx={{ color: "text.secondary", ...oneLineSx }}
                            >
                                {recipe.description}
                            </Typography>
                        )}
                    </Box>
                </Box>
                <Chip
                    label={t("recipes.recipeItemCount", {
                        count: recipe.itemCount,
                        defaultValue: `${recipe.itemCount} items`,
                    })}
                    size="small"
                    variant="outlined"
                    data-testid={`recipe-item-count-${recipe.name}`}
                />
                <IconButton
                    size="small"
                    aria-expanded={expanded}
                    data-testid={`recipe-card-toggle-${recipe.name}`}
                    onClick={toggle}
                >
                    {expanded ? (
                        <ExpandLess fontSize="small" />
                    ) : (
                        <ExpandMore fontSize="small" />
                    )}
                </IconButton>
            </Box>

            <Collapse in={expanded} unmountOnExit>
                <Box
                    data-testid={`recipe-card-peek-${recipe.name}`}
                    sx={{
                        px: 1,
                        pb: 1.5,
                        pt: 1,
                        borderTop: 1,
                        borderColor: "divider",
                    }}
                >
                    {recipe.description && (
                        <Typography
                            variant="body2"
                            sx={{ color: "text.secondary", mb: 1.5 }}
                        >
                            {recipe.description}
                        </Typography>
                    )}
                    {ingredients.length > 0 && (
                        <>
                            <Typography
                                variant="overline"
                                sx={{ color: "primary.main", fontWeight: 700 }}
                            >
                                {t("recipes.ingredientsHeading")}
                            </Typography>
                            <Box
                                sx={{
                                    display: "flex",
                                    flexWrap: "wrap",
                                    gap: 0.5,
                                    mb: 1.5,
                                }}
                            >
                                {shownIngredients.map((text, i) => {
                                    const isHit =
                                        q.length > 0 &&
                                        text.toLowerCase().includes(q);
                                    return (
                                        <Chip
                                            key={`${text}-${i}`}
                                            label={text}
                                            size="small"
                                            color={
                                                isHit ? "primary" : "default"
                                            }
                                            variant={
                                                isHit ? "filled" : "outlined"
                                            }
                                        />
                                    );
                                })}
                                {overflowCount > 0 && (
                                    <Chip
                                        label={`+${overflowCount}`}
                                        size="small"
                                        variant="outlined"
                                    />
                                )}
                            </Box>
                        </>
                    )}
                    <Stack
                        direction="row"
                        spacing={1}
                        sx={{ alignItems: "center" }}
                    >
                        <Button
                            variant="contained"
                            size="small"
                            onClick={open}
                            data-testid={`recipe-open-button-${recipe.name}`}
                            sx={{ flex: 1 }}
                        >
                            {t("recipes.openRecipe")}
                        </Button>
                        <IconButton
                            size="small"
                            data-testid={`recipe-item-menu-button-${recipe.name}`}
                            onClick={(e) => {
                                e.stopPropagation();
                                onMenuOpen(e, recipe);
                            }}
                        >
                            <MoreVert fontSize="small" />
                        </IconButton>
                    </Stack>
                </Box>
            </Collapse>
        </Card>
    );
};
