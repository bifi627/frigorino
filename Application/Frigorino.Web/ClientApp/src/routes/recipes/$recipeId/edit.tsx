import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { RecipeEditPage } from "../../../features/recipes/pages/RecipeEditPage";

export const Route = createFileRoute("/recipes/$recipeId/edit")({
    beforeLoad: requireAuth,
    component: RecipeEditPage,
});
