import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { ListEditPage } from "../../../features/lists/pages/ListEditPage";

export const Route = createFileRoute("/lists/$listId/edit")({
    beforeLoad: requireAuth,
    component: ListEditPage,
});
