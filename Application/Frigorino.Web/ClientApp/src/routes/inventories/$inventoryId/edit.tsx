import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { InventoryEditPage } from "../../../features/inventories/pages/InventoryEditPage";

export const Route = createFileRoute("/inventories/$inventoryId/edit")({
    beforeLoad: requireAuth,
    component: InventoryEditPage,
});
