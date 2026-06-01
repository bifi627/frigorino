import { useMutation } from "@tanstack/react-query";
import { unregisterFcmTokenMutation } from "../../lib/api/@tanstack/react-query.gen";

export const useUnregisterFcmToken = () =>
    useMutation({ ...unregisterFcmTokenMutation() });
