import { Box, ListItemText, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import { formatQuantity, scaleQuantity } from "../../../../components/composer";
import type { RecipeItemResponse } from "../../../../lib/api";

interface Props {
    item: RecipeItemResponse;
    // Display-only scale factor for quantities. 1 = unscaled (default).
    multiplier?: number;
}

export function RecipeItemContent({ item, multiplier = 1 }: Props) {
    const { t } = useTranslation();
    const isScaled = multiplier !== 1 && !!item.quantity;
    const displayQuantity =
        item.quantity && isScaled
            ? scaleQuantity(item.quantity, multiplier)
            : item.quantity;

    return (
        <ListItemText
            data-testid={`recipe-item-${item.id}`}
            slotProps={{ secondary: { component: "div" } }}
            primary={
                <Typography
                    variant="body2"
                    sx={{
                        fontWeight: 500,
                        wordBreak: "break-word",
                    }}
                >
                    {item.text}
                </Typography>
            }
            secondary={
                item.quantity || item.comment ? (
                    <Box
                        sx={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 0.25,
                        }}
                    >
                        {item.comment ? (
                            <Typography
                                component="div"
                                data-testid={`recipe-item-comment-${item.id}`}
                                variant="caption"
                                color="text.secondary"
                                sx={{
                                    fontSize: "0.7rem",
                                    fontStyle: "italic",
                                    whiteSpace: "pre-wrap",
                                    wordBreak: "break-word",
                                }}
                            >
                                {item.comment}
                            </Typography>
                        ) : null}
                        {displayQuantity ? (
                            <Box
                                sx={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: 0.5,
                                }}
                            >
                                <ItemQuantityChip
                                    quantity={displayQuantity}
                                    color={isScaled ? "primary" : undefined}
                                    testId={`recipe-item-quantity-${item.text}`}
                                />
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
                ) : null
            }
        />
    );
}
