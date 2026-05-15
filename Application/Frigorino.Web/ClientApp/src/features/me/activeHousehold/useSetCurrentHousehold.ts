import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { householdKeys } from "../../households/householdKeys";

export const useSetCurrentHousehold = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (householdId: number) =>
            ClientApi.me.setActiveHousehold({ householdId }),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: householdKeys.current(),
            });
            queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
        },
    });
};
