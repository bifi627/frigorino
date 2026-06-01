import { useMutation } from "@tanstack/react-query";
import { registerFcmTokenMutation } from "../../lib/api/@tanstack/react-query.gen";

export const useRegisterFcmToken = () =>
    useMutation({ ...registerFcmTokenMutation() });
