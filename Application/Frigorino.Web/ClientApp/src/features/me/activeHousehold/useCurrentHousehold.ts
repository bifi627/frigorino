import { useQuery } from "@tanstack/react-query";
import { getActiveHouseholdOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useCurrentHousehold = () =>
    useQuery({
        ...getActiveHouseholdOptions(),
        staleTime: 1000 * 60 * 5,
        retry: false,
    });
