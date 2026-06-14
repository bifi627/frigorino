import { MoreVert } from "@mui/icons-material";
import {
    Box,
    Card,
    CardContent,
    Chip,
    IconButton,
    ListItem,
    ListItemText,
    List as MuiList,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { RecipeResponse } from "../../../lib/api";

interface RecipeSummaryCardProps {
    recipe: RecipeResponse;
    onClick: (recipeId: number) => void;
    onMenuOpen: (
        event: React.MouseEvent<HTMLElement>,
        recipe: RecipeResponse,
    ) => void;
    menuDisabled?: boolean;
}

export const RecipeSummaryCard = ({
    recipe,
    onClick,
    onMenuOpen,
    menuDisabled = false,
}: RecipeSummaryCardProps) => {
    const { t } = useTranslation();

    return (
        <Card elevation={1}>
            <CardContent sx={{ py: 2 }}>
                <MuiList disablePadding>
                    <ListItem
                        data-testid={`recipe-item-${recipe.name}`}
                        sx={{
                            px: 0,
                            cursor: "pointer",
                            "&:hover": { bgcolor: "action.hover" },
                        }}
                        onClick={() => recipe.id && onClick(recipe.id)}
                        secondaryAction={
                            <Box
                                sx={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 1,
                                }}
                            >
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
                                    data-testid={`recipe-item-menu-button-${recipe.name}`}
                                    onClick={(e) => {
                                        e.stopPropagation();
                                        onMenuOpen(e, recipe);
                                    }}
                                    disabled={menuDisabled}
                                >
                                    <MoreVert fontSize="small" />
                                </IconButton>
                            </Box>
                        }
                    >
                        <ListItemText
                            primary={
                                <Typography
                                    variant="body1"
                                    sx={{ fontWeight: 600 }}
                                >
                                    {recipe.name}
                                </Typography>
                            }
                            secondary={
                                recipe.description && (
                                    <Typography
                                        variant="body2"
                                        sx={{
                                            color: "text.secondary",
                                            mt: 0.5,
                                        }}
                                    >
                                        {recipe.description}
                                    </Typography>
                                )
                            }
                        />
                    </ListItem>
                </MuiList>
            </CardContent>
        </Card>
    );
};
