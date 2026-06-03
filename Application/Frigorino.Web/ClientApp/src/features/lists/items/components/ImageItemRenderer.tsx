import { BrokenImage } from "@mui/icons-material";
import { Box, Skeleton, Typography } from "@mui/material";
import { useState } from "react";
import type { ListItemResponse } from "../../../../lib/api";
import { useCurrentHousehold } from "../../../me/activeHousehold/useCurrentHousehold";
import { useItemImage } from "../useItemImage";
import { ImageLightbox } from "./ImageLightbox";

interface Props {
    item: ListItemResponse;
}

const THUMB_SIZE = 56;

export function ImageItemRenderer({ item }: Props) {
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;
    const [lightboxOpen, setLightboxOpen] = useState(false);

    const {
        data: url,
        isLoading,
        isError,
    } = useItemImage(householdId, item.listId, item.id, "thumbnail");

    return (
        <Box
            sx={{
                display: "flex",
                alignItems: "center",
                gap: 1.5,
                flex: 1,
                minWidth: 0,
            }}
        >
            <Box
                role="button"
                tabIndex={0}
                aria-label="open image"
                data-testid={`list-item-image-${item.id}`}
                onClick={() => setLightboxOpen(true)}
                onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        setLightboxOpen(true);
                    }
                }}
                sx={{
                    width: THUMB_SIZE,
                    height: THUMB_SIZE,
                    flexShrink: 0,
                    borderRadius: 1,
                    overflow: "hidden",
                    cursor: "pointer",
                    bgcolor: "action.hover",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                }}
            >
                {isLoading ? (
                    <Skeleton
                        variant="rectangular"
                        width={THUMB_SIZE}
                        height={THUMB_SIZE}
                    />
                ) : isError || !url ? (
                    <BrokenImage fontSize="small" color="disabled" />
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={item.comment ?? ""}
                        sx={{
                            width: "100%",
                            height: "100%",
                            objectFit: "cover",
                        }}
                    />
                )}
            </Box>

            {item.comment ? (
                <Typography
                    variant="body2"
                    sx={{
                        wordBreak: "break-word",
                        color: "text.secondary",
                        flex: 1,
                        minWidth: 0,
                    }}
                >
                    {item.comment}
                </Typography>
            ) : null}

            {householdId > 0 ? (
                <ImageLightbox
                    householdId={householdId}
                    listId={item.listId}
                    itemId={item.id}
                    caption={item.comment}
                    open={lightboxOpen}
                    onClose={() => setLightboxOpen(false)}
                />
            ) : null}
        </Box>
    );
}
