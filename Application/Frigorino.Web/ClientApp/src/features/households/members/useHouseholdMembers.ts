import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { householdKeys } from "../householdKeys";

export const useHouseholdMembers = (householdId: number, enabled = true) => {
    return useQuery({
        queryKey: householdKeys.members(householdId),
        queryFn: () => ClientApi.members.getMembers(householdId),
        enabled: enabled && !!householdId,
    });
};
