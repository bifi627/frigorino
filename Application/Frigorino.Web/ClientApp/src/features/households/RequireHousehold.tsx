import { Navigate } from "@tanstack/react-router";
import type { PropsWithChildren } from "react";
import { FullPageSpinner } from "../../components/common/FullPageSpinner";
import { useUserHouseholds } from "./useUserHouseholds";

// Gate for household-scoped feature pages. Unlike the dashboard guard, this does
// NOT honor the onboarding skip flag: skipping lets a user peek at the dashboard,
// but entering a feature flow always requires an active household.
export function RequireHousehold({ children }: PropsWithChildren) {
    const { data: households, isLoading } = useUserHouseholds();

    if (isLoading) {
        return <FullPageSpinner />;
    }

    if ((households?.length ?? 0) === 0) {
        return <Navigate to="/onboarding" />;
    }

    return <>{children}</>;
}
