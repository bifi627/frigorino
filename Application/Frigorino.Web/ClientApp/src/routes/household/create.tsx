import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { CreateHouseholdPage } from "../../features/households/pages/CreateHouseholdPage";

export const Route = createFileRoute("/household/create")({
    beforeLoad: requireAuth,
    component: CreateHouseholdPage,
});
