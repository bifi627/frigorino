import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { BlueprintsPage } from "../../../features/blueprints/pages/BlueprintsPage";

export const Route = createFileRoute("/household/blueprints/")({
    beforeLoad: requireAuth,
    component: BlueprintsPage,
});
