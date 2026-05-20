import { useQuery } from "@tanstack/react-query";
import { getUserHouseholdsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useUserHouseholds = (enabled = true) =>
    useQuery({
        ...getUserHouseholdsOptions(),
        enabled,
        staleTime: 1000 * 60 * 5,
    });
