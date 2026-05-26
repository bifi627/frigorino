import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { CreateListPage } from "../../features/lists/pages/CreateListPage";

export const Route = createFileRoute("/lists/create")({
    beforeLoad: requireAuth,
    component: () => (
        <RequireHousehold>
            <CreateListPage />
        </RequireHousehold>
    ),
});
