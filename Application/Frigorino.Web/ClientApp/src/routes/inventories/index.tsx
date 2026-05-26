import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { RequireHousehold } from "../../features/households/RequireHousehold";
import { InventoriesPage } from "../../features/inventories/pages/InventoriesPage";

export const Route = createFileRoute("/inventories/")({
    beforeLoad: requireAuth,
    component: () => (
        <RequireHousehold>
            <InventoriesPage />
        </RequireHousehold>
    ),
});
