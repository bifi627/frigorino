import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../../common/authGuard";
import { UserSettingsPage } from "../../features/settings/pages/UserSettingsPage";

export const Route = createFileRoute("/settings/")({
    beforeLoad: requireAuth,
    component: UserSettingsPage,
});
