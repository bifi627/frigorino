import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../../common/authGuard";
import { BlueprintViewPage } from "../../../../features/blueprints/pages/BlueprintViewPage";

export const Route = createFileRoute("/household/blueprints/$blueprintId/view")(
    {
        beforeLoad: requireAuth,
        component: BlueprintViewPage,
    },
);
