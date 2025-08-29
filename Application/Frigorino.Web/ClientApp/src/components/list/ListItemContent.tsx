import { ListItemText, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useLongPress } from "../../hooks/useLongPress";
import type { ListItemDto } from "../../lib/api";

interface Props {
    item: ListItemDto;
}
export function ListItemContent({ item }: Props) {
    const { t } = useTranslation();
    const events = useLongPress({
        shouldPreventDefault: true,
        onLongPress: () => {
            navigator.clipboard.writeText(item.text ?? "");
            toast.success(t("common.textCopiedToClipboard"), {
                duration: 2000,
            });
        },
    });

    return (
        <>
            <ListItemText
                {...events}
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
