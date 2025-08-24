import { ListItemText, Typography } from "@mui/material";
import type { ListItemDto } from "../../lib/api";

interface Props {
    item: ListItemDto;
}
export function ListItemContent({ item }: Props) {
    return (
        <>
            <ListItemText
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
                    item.quantity && (
                        <Typography
                            variant="caption"
                            sx={{
                                color: item.status
                                    ? "text.disabled"
                                    : "text.secondary",
                                textDecoration: item.status
                                    ? "line-through"
                                    : "none",
                            }}
                        >
                            {item.quantity}
                        </Typography>
                    )
                }
            />
        </>
    );
}
