import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getInventorySettingsQueryKey,
    updateInventorySettingsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateInventorySettings = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateInventorySettingsMutation(),
        onSuccess: (data, variables) => {
            queryClient.setQueryData(
                getInventorySettingsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        inventoryId: variables.path.inventoryId,
                    },
                }),
                data,
            );
        },
    });
};
