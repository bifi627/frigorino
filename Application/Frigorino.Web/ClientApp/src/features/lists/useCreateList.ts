import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import type { CreateListRequest } from "../../lib/api";
import { listKeys } from "./listKeys";

export const useCreateList = (householdId: number) => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (data: CreateListRequest) =>
            ClientApi.lists.createList(householdId, data),
        onSuccess: (data) => {
            queryClient.invalidateQueries({
                queryKey: listKeys.byHousehold(householdId),
            });
            if (data.id) {
                queryClient.setQueryData(listKeys.detail(data.id), data);
            }
        },
    });
};
