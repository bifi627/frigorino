import { Box, Link, ListItemText, Typography } from "@mui/material";
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
            
            if (isImageUrl(href)) {
                return (
                    <InlineImage
                        key={index}
                        src={href}
                        alt={`Image from ${part}`}
                    />
                );
            }
            
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

const IMAGE_EXTENSIONS = /\.(jpg|jpeg|png|gif|webp|svg|bmp)(\?[^\s]*)?$/i;

function isImageUrl(url: string): boolean {
    return IMAGE_EXTENSIONS.test(url);
}

interface InlineImageProps {
    src: string;
    alt: string;
}

function InlineImage({ src, alt }: InlineImageProps) {
    return (
        <Box
            component="img"
            src={src}
            alt={alt}
            loading="lazy"
            onClick={() => window.open(src, '_blank')}
            sx={{
                maxWidth: { xs: 280, sm: 320 },
                maxHeight: { xs: 120, sm: 140 },
                width: 'auto',
                height: 'auto',
                objectFit: 'contain',
                borderRadius: 1,
                cursor: 'pointer',
                display: 'block',
                margin: '8px 0',
                boxShadow: '0 2px 8px rgba(0,0,0,0.1)',
                transition: 'all 0.2s ease',
                '&:hover': {
                    boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
                    transform: 'scale(1.02)'
                }
            }}
        />
    );
}
