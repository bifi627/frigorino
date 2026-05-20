import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createListMutation,
    getListsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useCreateList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createListMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getListsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
