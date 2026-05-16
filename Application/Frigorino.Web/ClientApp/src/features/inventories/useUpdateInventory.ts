import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import type { UpdateInventoryRequest } from "../../lib/api";
import { inventoryKeys } from "./inventoryKeys";

export const useUpdateInventory = () => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({
            householdId,
            inventoryId,
            data,
        }: {
            householdId: number;
            inventoryId: number;
            data: UpdateInventoryRequest;
        }) =>
            ClientApi.inventories.updateInventory(
                householdId,
                inventoryId,
                data,
            ),
        onSuccess: (data, variables) => {
            if (data?.id) {
                queryClient.setQueryData(inventoryKeys.detail(data.id), data);
            }
            queryClient.invalidateQueries({
                queryKey: inventoryKeys.byHousehold(variables.householdId),
            });
        },
    });
};
