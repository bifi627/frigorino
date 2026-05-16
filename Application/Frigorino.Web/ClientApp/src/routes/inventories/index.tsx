import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { InventoriesPage } from "../../features/inventories/pages/InventoriesPage";

export const Route = createFileRoute("/inventories/")({
    beforeLoad: requireAuth,
    component: InventoriesPage,
});
