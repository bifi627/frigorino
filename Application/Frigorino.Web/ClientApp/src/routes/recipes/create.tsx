import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { CreateRecipePage } from "../../features/recipes/pages/CreateRecipePage";

export const Route = createFileRoute("/recipes/create")({
    beforeLoad: requireAuth,
    component: () => (
        <RequireHousehold>
            <CreateRecipePage />
        </RequireHousehold>
    ),
});
