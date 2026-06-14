import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { RecipeViewPage } from "../../../features/recipes/pages/RecipeViewPage";

export const Route = createFileRoute("/recipes/$recipeId/view")({
    beforeLoad: requireAuth,
    component: RecipeViewPage,
});
