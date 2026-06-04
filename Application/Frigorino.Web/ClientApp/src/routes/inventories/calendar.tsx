import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { ExpiryCalendarPage } from "../../features/inventories/calendar/pages/ExpiryCalendarPage";

export const Route = createFileRoute("/inventories/calendar")({
    beforeLoad: requireAuth,
    component: ExpiryCalendarPage,
});
