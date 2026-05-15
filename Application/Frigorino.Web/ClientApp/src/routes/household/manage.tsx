import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { ManageHouseholdPage } from "../../features/households/pages/ManageHouseholdPage";

export const Route = createFileRoute("/household/manage")({
    beforeLoad: requireAuth,
    component: ManageHouseholdPage,
});
