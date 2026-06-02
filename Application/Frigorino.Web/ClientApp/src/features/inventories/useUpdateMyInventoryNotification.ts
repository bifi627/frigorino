import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getMyInventoryNotificationQueryKey,
    updateMyInventoryNotificationMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateMyInventoryNotification = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateMyInventoryNotificationMutation(),
        onSuccess: (data, variables) => {
            queryClient.setQueryData(
                getMyInventoryNotificationQueryKey({
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
