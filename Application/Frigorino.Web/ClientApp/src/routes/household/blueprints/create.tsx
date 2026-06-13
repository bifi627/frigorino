import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../common/authGuard";
import { BlueprintCreatePage } from "../../../features/blueprints/pages/BlueprintCreatePage";

export const Route = createFileRoute("/household/blueprints/create")({
    beforeLoad: requireAuth,
    component: BlueprintCreatePage,
});
