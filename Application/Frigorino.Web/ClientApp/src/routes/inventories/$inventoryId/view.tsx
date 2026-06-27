import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { InventoryViewPage } from "../../../features/inventories/pages/InventoryViewPage";

export const Route = createFileRoute("/inventories/$inventoryId/view")({
    beforeLoad: requireAuth,
    // Notification deep-links carry the target household (it may not be the active one).
    validateSearch: (
        search: Record<string, unknown>,
    ): { householdId?: number } => {
        const raw = search.householdId;
        const n = typeof raw === "string" ? Number(raw) : NaN;
        return Number.isInteger(n) && n > 0 ? { householdId: n } : {};
    },
    component: InventoryViewPage,
});
