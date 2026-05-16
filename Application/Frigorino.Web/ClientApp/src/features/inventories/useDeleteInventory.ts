import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import { inventoryKeys } from "./inventoryKeys";

export const useDeleteInventory = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({
            householdId,
            inventoryId,
        }: {
            householdId: number;
            inventoryId: number;
        }) =>
            ClientApi.inventories.deleteInventory(householdId, inventoryId),
        onSuccess: (_, variables) => {
            queryClient.removeQueries({
                queryKey: inventoryKeys.detail(variables.inventoryId),
            });
            queryClient.invalidateQueries({
                queryKey: inventoryKeys.byHousehold(variables.householdId),
            });
        },
    });
};
