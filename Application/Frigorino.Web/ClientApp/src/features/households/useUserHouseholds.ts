import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import { householdKeys } from "./householdKeys";

export const useUserHouseholds = (enabled = true) => {
    return useQuery({
        queryKey: householdKeys.lists(),
        queryFn: () => ClientApi.households.getUserHouseholds(),
        enabled,
        staleTime: 1000 * 60 * 5,
    });
};
