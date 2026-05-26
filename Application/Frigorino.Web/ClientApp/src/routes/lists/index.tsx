import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { ListsPage } from "../../features/lists/pages/ListsPage";

export const Route = createFileRoute("/lists/")({
    beforeLoad: requireAuth,
    component: () => (
        <RequireHousehold>
            <ListsPage />
        </RequireHousehold>
    ),
});
