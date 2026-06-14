import { Box, ListItemText, Typography } from "@mui/material";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import type { RecipeItemResponse } from "../../../../lib/api";

interface Props {
    item: RecipeItemResponse;
}

export function RecipeItemContent({ item }: Props) {
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
                        {item.quantity ? (
                            <Box
                                sx={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: 0.5,
                                }}
                            >
                                <ItemQuantityChip
                                    quantity={item.quantity}
                                    testId={`recipe-item-quantity-${item.text}`}
                                />
                            </Box>
                        ) : null}
                    </Box>
                ) : null
            }
        />
    );
}
