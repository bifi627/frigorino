import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteItemMutation,
    getItemQueryKey,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";
import { useRestoreListItem } from "./useRestoreListItem";

export const useDeleteListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreItem = useRestoreListItem();

    return useMutation({
        ...deleteItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                },
            });

            await queryClient.cancelQueries({ queryKey });

            const previousItems =
                queryClient.getQueryData<ListItemResponse[]>(queryKey);

            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                return old.filter((item) => item.id !== variables.path.itemId);
            });

            queryClient.removeQueries({
                queryKey: getItemQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                        itemId: variables.path.itemId,
                    },
                }),
            });

            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getItemsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            listId: variables.path.listId,
                        },
                    }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("common.itemDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreItem.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
        },
    });
};
