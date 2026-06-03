import { Box, Link, ListItemText, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { ItemQuantityChip } from "../../../../components/common/ItemQuantityChip";
import { useLongPress } from "../../../../hooks/useLongPress";
import type { ListItemResponse } from "../../../../lib/api";

interface Props {
    item: ListItemResponse;
    // Tapping the quantity chip opens the item in edit mode with the quantity panel open.
    onEditQuantity?: () => void;
    // Tapping the comment opens the item in edit mode with the comment panel open.
    onEditComment?: () => void;
}

export function TextItemRenderer({
    item,
    onEditQuantity,
    onEditComment,
}: Props) {
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
        <ListItemText
            {...events}
            slotProps={{ secondary: { component: "div" } }}
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
                                role="button"
                                tabIndex={0}
                                aria-label={t("lists.comment")}
                                onClick={(e) => {
                                    e.stopPropagation();
                                    onEditComment?.();
                                }}
                                onKeyDown={(e) => {
                                    if (e.key === "Enter" || e.key === " ") {
                                        e.preventDefault();
                                        e.stopPropagation();
                                        onEditComment?.();
                                    }
                                }}
                                data-testid={`list-item-comment-${item.id}`}
                                variant="caption"
                                color="text.secondary"
                                sx={{
                                    fontSize: "0.7rem",
                                    fontStyle: "italic",
                                    whiteSpace: "pre-wrap",
                                    wordBreak: "break-word",
                                    cursor: "pointer",
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
                                    onClick={onEditQuantity}
                                    testId={`list-item-quantity-${item.text}`}
                                />
                            </Box>
                        ) : null}
                    </Box>
                ) : null
            }
        />
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
            onClick={() => window.open(src, "_blank")}
            sx={{
                maxWidth: { xs: 280, sm: 320 },
                maxHeight: { xs: 120, sm: 140 },
                width: "auto",
                height: "auto",
                objectFit: "contain",
                cursor: "pointer",
                display: "block",
                margin: "8px 0",
                boxShadow: 2,
                transition: "all 0.2s ease",
                "&:hover": {
                    boxShadow: 4,
                    transform: "scale(1.02)",
                },
            }}
        />
    );
}
