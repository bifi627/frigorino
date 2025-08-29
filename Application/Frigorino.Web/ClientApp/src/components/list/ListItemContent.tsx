import { Link, ListItemText, Typography } from "@mui/material";
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
                        {item.text ? renderTextWithLinks(item.text) : ""}
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

function renderTextWithLinks(text: string) {
    const parts = text.split(URL_REGEX);

    return parts.map((part, index) => {
        if (URL_REGEX.test(part)) {
            const href = part.startsWith("http") ? part : `https://${part}`;
            return (
                <Link
                    key={index}
                    href={href}
                    target="_blank"
                    rel="noopener noreferrer"
                    sx={{
                        color: "primary.main",
                        textDecoration: "underline",
                    }}
                >
                    {part}
                </Link>
            );
        }
        return part;
    });
}
const URL_REGEX =
    /(https?:\/\/[^\s]+|www\.[^\s]+|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]*\.(?:[a-zA-Z]{2,}|[a-zA-Z]{2,}\.[a-zA-Z]{2,})(?:\/[^\s]*)?)/gi;
