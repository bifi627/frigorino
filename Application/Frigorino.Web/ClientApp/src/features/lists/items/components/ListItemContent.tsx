import type { ListItemResponse } from "../../../../lib/api";
import { DocumentItemRenderer } from "./DocumentItemRenderer";
import { ImageItemRenderer } from "./ImageItemRenderer";
import { TextItemRenderer } from "./TextItemRenderer";

interface Props {
    item: ListItemResponse;
    onEditQuantity?: () => void;
    onEditComment?: () => void;
}

// Renderer switch keyed by item.type.
export function ListItemContent({
    item,
    onEditQuantity,
    onEditComment,
}: Props) {
    if (item.type === "Image") {
        return <ImageItemRenderer item={item} />;
    }
    if (item.type === "Document") {
        return <DocumentItemRenderer item={item} />;
    }
    return (
        <TextItemRenderer
            item={item}
            onEditQuantity={onEditQuantity}
            onEditComment={onEditComment}
        />
    );
}
