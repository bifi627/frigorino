import {
    Box,
    Chip,
    CircularProgress,
    Link,
    ListItemText,
    Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useLongPress } from "../../../../hooks/useLongPress";
import type { QuantityDto } from "../../../../lib/api";
import type { ListItemResponse } from "../../../../lib/api";
import { formatQuantity } from "../quantityFormat";
import { QuantityEditPopover } from "./QuantityEditPopover";

interface Props {
    item: ListItemResponse;
    isExtracting?: boolean;
    onQuantityChange?: (q: QuantityDto) => void;
}

export function ListItemContent({
    item,
    isExtracting,
    onQuantityChange,
}: Props) {
    const { t } = useTranslation();
    const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
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
                <Box
                    sx={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 0.5,
                    }}
                >
                    {item.quantity ? (
                        <Chip
                            size="small"
                            variant="outlined"
                            data-testid={`list-item-quantity-${item.text}`}
                            label={formatQuantity(t, item.quantity)}
                            onClick={(e) => setAnchorEl(e.currentTarget)}
                            sx={{
                                height: 20,
                                textDecoration: item.status
                                    ? "line-through"
                                    : "none",
                            }}
                        />
                    ) : isExtracting ? (
                        <CircularProgress
                            size={12}
                            data-testid={`list-item-quantity-loading-${item.text}`}
                        />
                    ) : (
                        <Chip
                            size="small"
                            variant="outlined"
                            label={t("common.quantity")}
                            onClick={(e) => setAnchorEl(e.currentTarget)}
                            sx={{ height: 20, opacity: 0.5 }}
                        />
                    )}
                    {onQuantityChange && (
                        <QuantityEditPopover
                            anchorEl={anchorEl}
                            current={item.quantity}
                            onClose={() => setAnchorEl(null)}
                            onSave={onQuantityChange}
                        />
                    )}
                </Box>
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
