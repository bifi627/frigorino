import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { client } from "../../../lib/api/client.gen";

type Variant = "thumbnail" | "file";

// Fetches an attachment's image bytes (auth'd, via the configured client). Caches the decoded Blob
// (keyed by attachment id + variant) — NOT an object URL. Each consumer derives its own short-lived
// object URL via a paired useEffect (StrictMode-safe). Direct mirror of useItemImage; see that file
// for the why-Blob-not-URL rationale.
export const useAttachmentImage = (
    householdId: number,
    recipeId: number,
    attachmentId: number,
    variant: Variant,
    enabled = true,
) => {
    const query = useQuery({
        queryKey: [
            "attachment-image",
            householdId,
            recipeId,
            attachmentId,
            variant,
        ],
        enabled: enabled && householdId > 0 && recipeId > 0 && attachmentId > 0,
        staleTime: Infinity,
        gcTime: 5 * 60 * 1000,
        queryFn: async () => {
            const { data, error } = await client.get({
                url: `/api/household/${householdId}/recipes/${recipeId}/attachments/${attachmentId}/${variant}`,
                parseAs: "blob",
            });
            if (error || !data) {
                throw new Error("Failed to load image");
            }
            return data as Blob;
        },
    });

    const blob = query.data;
    const [url, setUrl] = useState<string>();
    useEffect(() => {
        if (!blob) {
            // eslint-disable-next-line react-hooks/set-state-in-effect
            setUrl(undefined);
            return;
        }
        const objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
        return () => {
            URL.revokeObjectURL(objectUrl);
        };
    }, [blob]);

    return {
        ...query,
        data: url,
        isLoading: query.isLoading || (!!blob && !url),
    };
};
