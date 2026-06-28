import { Box, Typography } from "@mui/material";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import type { RecipeItemResponse } from "../../../../lib/api";

interface Props {
    item: RecipeItemResponse;
}

export function RecipeItemContent({ item }: Props) {
    return (
        <Box
            data-testid={`recipe-item-${item.id}`}
            sx={{
                display: "flex",
                width: "100%",
                alignItems: "flex-start",
                justifyContent: "space-between",
                gap: 1,
            }}
        >
            <Box sx={{ flex: 1, minWidth: 0 }}>
                <Typography
                    variant="body2"
                    sx={{ fontWeight: 500, wordBreak: "break-word" }}
                >
                    {item.text}
                </Typography>
                {item.comment ? (
                    <Typography
                        data-testid={`recipe-item-comment-${item.id}`}
                        variant="caption"
                        color="text.secondary"
                        sx={{
                            display: "block",
                            fontSize: "0.7rem",
                            fontStyle: "italic",
                            whiteSpace: "pre-wrap",
                            wordBreak: "break-word",
                        }}
                    >
                        {item.comment}
                    </Typography>
                ) : null}
            </Box>
            {item.quantity ? (
                <ItemQuantityChip
                    quantity={item.quantity}
                    testId={`recipe-item-quantity-${item.text}`}
                />
            ) : null}
        </Box>
    );
}
