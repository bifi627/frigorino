import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { CreateInventoryPage } from "../../features/inventories/pages/CreateInventoryPage";

export const Route = createFileRoute("/inventories/create")({
    beforeLoad: requireAuth,
    component: CreateInventoryPage,
});
