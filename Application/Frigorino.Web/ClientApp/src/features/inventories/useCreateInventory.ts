import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../common/apiClient";
import type { CreateInventoryRequest } from "../../lib/api";
import { inventoryKeys } from "./inventoryKeys";

export const useCreateInventory = (householdId: number) => {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (data: CreateInventoryRequest) =>
            ClientApi.inventories.createInventory(householdId, data),
        onSuccess: (data) => {
            queryClient.invalidateQueries({
                queryKey: inventoryKeys.byHousehold(householdId),
            });
            if (data.id) {
                queryClient.setQueryData(inventoryKeys.detail(data.id), data);
            }
        },
    });
};
