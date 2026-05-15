import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { CreateListPage } from "../../features/lists/pages/CreateListPage";

export const Route = createFileRoute("/lists/create")({
    beforeLoad: requireAuth,
    component: CreateListPage,
});
