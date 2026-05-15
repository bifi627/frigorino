import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { ClientApi } from "../../common/apiClient";
import { householdKeys } from "./householdKeys";

export const useDeleteHousehold = () => {
    const queryClient = useQueryClient();
    const navigate = useNavigate();

    return useMutation({
        mutationFn: (householdId: number) =>
            ClientApi.households.deleteHousehold(householdId),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: householdKeys.current(),
            });
            queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
            navigate({ to: "/" });
        },
    });
};
