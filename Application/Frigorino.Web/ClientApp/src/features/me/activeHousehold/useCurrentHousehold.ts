import { useQuery } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { householdKeys } from "../../households/householdKeys";

export const useCurrentHousehold = () => {
    return useQuery({
        queryKey: householdKeys.current(),
        queryFn: () => ClientApi.me.getActiveHousehold(),
        staleTime: 1000 * 60 * 5,
        retry: false,
    });
};
