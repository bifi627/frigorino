import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { ListViewPage } from "../../../features/lists/pages/ListViewPage";

export const Route = createFileRoute("/lists/$listId/view")({
    beforeLoad: requireAuth,
    component: ListViewPage,
});
