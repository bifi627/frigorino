import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import type { CreateListRequest } from "../../lib/api";
import { listKeys } from "./listKeys";

export const useCreateList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({
            householdId,
            data,
        }: {
            householdId: number;
            data: CreateListRequest;
        }) => ClientApi.lists.createList(householdId, data),
        onSuccess: (data, variables) => {
            queryClient.invalidateQueries({
                queryKey: listKeys.byHousehold(variables.householdId),
            });
            if (data.id) {
                queryClient.setQueryData(listKeys.detail(data.id), data);
            }
        },
    });
};
