import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { RecipesPage } from "../../features/recipes/pages/RecipesPage";

export const Route = createFileRoute("/recipes/")({
    beforeLoad: requireAuth,
    component: () => (
        <RequireHousehold>
            <RecipesPage />
        </RequireHousehold>
    ),
});
