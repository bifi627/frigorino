import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    deleteProductMutation,
    getProductsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useDeleteProduct = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...deleteProductMutation(),
        onSettled: (_data, _error, variables) => {
            queryClient.invalidateQueries({
                queryKey: getProductsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
