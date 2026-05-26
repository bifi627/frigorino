import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { CreateInventoryPage } from "../../features/inventories/pages/CreateInventoryPage";

export const Route = createFileRoute("/inventories/create")({
    beforeLoad: requireAuth,
    component: () => (
        <RequireHousehold>
            <CreateInventoryPage />
        </RequireHousehold>
    ),
});
