import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { InventoryViewPage } from "../../../features/inventories/pages/InventoryViewPage";

export const Route = createFileRoute("/inventories/$inventoryId/view")({
    beforeLoad: requireAuth,
    component: InventoryViewPage,
});
