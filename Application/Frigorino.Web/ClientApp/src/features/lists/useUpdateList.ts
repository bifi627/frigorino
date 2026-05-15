import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import type { UpdateListRequest } from "../../lib/api";
import { listKeys } from "./listKeys";

export const useUpdateList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({
            householdId,
            listId,
            data,
        }: {
            householdId: number;
            listId: number;
            data: UpdateListRequest;
        }) => ClientApi.lists.updateList(householdId, listId, data),
        onSuccess: (data, variables) => {
            if (data?.id) {
                queryClient.setQueryData(listKeys.detail(data.id), data);
            }
            queryClient.invalidateQueries({
                queryKey: listKeys.byHousehold(variables.householdId),
            });
        },
    });
};
