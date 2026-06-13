import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../../../common/authGuard";
import { BlueprintEditPage } from "../../../../features/blueprints/pages/BlueprintEditPage";

export const Route = createFileRoute("/household/blueprints/$blueprintId/edit")(
    {
        beforeLoad: requireAuth,
        component: BlueprintEditPage,
    },
);
