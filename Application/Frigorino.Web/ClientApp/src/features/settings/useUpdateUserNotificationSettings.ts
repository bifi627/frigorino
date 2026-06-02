import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getUserSettingsQueryKey,
    updateUserNotificationSettingsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateUserNotificationSettings = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateUserNotificationSettingsMutation(),
        onSuccess: (data) => {
            queryClient.setQueryData(getUserSettingsQueryKey(), data);
        },
    });
};
