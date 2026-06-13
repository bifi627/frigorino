import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
    deleteBlueprintMutation,
    getBlueprintsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";
import { useRestoreSortBlueprint } from "./useRestoreSortBlueprint";

export const useDeleteSortBlueprint = () => {
    const queryClient = useQueryClient();
    const { t } = useTranslation();
    const restore = useRestoreSortBlueprint();

    return useMutation({
        ...deleteBlueprintMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getBlueprintsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
            toast(t("blueprints.deleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restore.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
    });
};
