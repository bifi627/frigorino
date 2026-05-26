import { createFileRoute } from "@tanstack/react-router";
import { requireAuth } from "../common/authGuard";
import { OnboardingPage } from "../features/households/pages/OnboardingPage";

export const Route = createFileRoute("/onboarding")({
    beforeLoad: requireAuth,
    component: OnboardingPage,
});
