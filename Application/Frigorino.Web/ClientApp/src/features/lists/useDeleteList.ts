import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import { listKeys } from "./listKeys";

export const useDeleteList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({
            householdId,
            listId,
        }: {
            householdId: number;
            listId: number;
        }) => ClientApi.lists.deleteList(householdId, listId),
        onSuccess: (_, variables) => {
            queryClient.removeQueries({
                queryKey: listKeys.detail(variables.listId),
            });
            queryClient.invalidateQueries({
                queryKey: listKeys.byHousehold(variables.householdId),
            });
        },
    });
};
