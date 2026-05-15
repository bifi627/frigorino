import { useUserHouseholds } from "../../households/useUserHouseholds";
import { useCurrentHousehold } from "./useCurrentHousehold";

export const useCurrentHouseholdWithDetails = () => {
    const currentHouseholdQuery = useCurrentHousehold();
    const householdsQuery = useUserHouseholds(
        !!currentHouseholdQuery.data?.householdId,
    );

    const currentHouseholdDetails = householdsQuery.data?.find(
        (h) => h.id === currentHouseholdQuery.data?.householdId,
    );

    return {
        currentHousehold: currentHouseholdQuery.data,
        currentHouseholdDetails,
        isLoading: currentHouseholdQuery.isLoading || householdsQuery.isLoading,
        error: currentHouseholdQuery.error || householdsQuery.error,
        hasActiveHousehold:
            currentHouseholdQuery.data?.hasActiveHousehold &&
            !!currentHouseholdQuery.data?.householdId,
    };
};
