import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import type { CreateHouseholdRequest } from "../../lib/api";
import { householdKeys } from "./householdKeys";

export const useCreateHousehold = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (data: CreateHouseholdRequest) =>
            ClientApi.households.createHousehold(data),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: householdKeys.lists() });
        },
    });
};
