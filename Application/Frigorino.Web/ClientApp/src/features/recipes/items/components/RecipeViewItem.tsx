import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { formatQuantity, scaleQuantity } from "../../../../components/composer";
import type { RecipeItemResponse } from "../../../../lib/api";

interface Props {
    item: RecipeItemResponse;
    // Display-only scale factor for quantities. 1 = unscaled (default).
    multiplier?: number;
}

export function RecipeViewItem({ item, multiplier = 1 }: Props) {
    const { t } = useTranslation();
    const isScaled = multiplier !== 1 && !!item.quantity;
    const displayQuantity =
        item.quantity && isScaled
            ? scaleQuantity(item.quantity, multiplier)
            : item.quantity;

    return (
        <Box
            data-testid={`recipe-item-${item.id}`}
            sx={{
                display: "flex",
                gap: 1.5,
                py: 1,
                alignItems: "baseline",
                borderBottom: 1,
                borderColor: "divider",
            }}
        >
            <Box sx={{ flex: "0 0 84px", minWidth: 84 }}>
                {displayQuantity ? (
                    <Box sx={{ display: "flex", flexDirection: "column" }}>
                        <Typography
                            component="span"
                            data-testid={`recipe-item-quantity-${item.text}`}
                            variant="body2"
                            sx={{
                                fontWeight: 600,
                                color: isScaled ? "success.dark" : "success.main",
                            }}
                        >
                            {formatQuantity(t, displayQuantity)}
                        </Typography>
                        {isScaled && item.quantity ? (
                            <Typography
                                component="span"
                                data-testid={`recipe-item-quantity-base-${item.id}`}
                                variant="caption"
                                color="text.disabled"
                                sx={{
                                    textDecoration: "line-through",
                                    fontSize: "0.7rem",
                                }}
                            >
                                {formatQuantity(t, item.quantity)}
                            </Typography>
                        ) : null}
                    </Box>
                ) : null}
            </Box>
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
                            fontStyle: "italic",
                            fontSize: "0.7rem",
                            whiteSpace: "pre-wrap",
                            wordBreak: "break-word",
                        }}
                    >
                        {item.comment}
                    </Typography>
                ) : null}
            </Box>
        </Box>
    );
}
