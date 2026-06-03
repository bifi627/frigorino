import type { ListItemResponse } from "../../../../lib/api";
import { ImageItemRenderer } from "./ImageItemRenderer";
import { TextItemRenderer } from "./TextItemRenderer";

interface Props {
    item: ListItemResponse;
    onEditQuantity?: () => void;
    onEditComment?: () => void;
}

// Renderer switch keyed by item.type. Document renderer arrives in sub-feature #3.
export function ListItemContent({
    item,
    onEditQuantity,
    onEditComment,
}: Props) {
    if (item.type === "Image") {
        return <ImageItemRenderer item={item} />;
    }
    return (
        <TextItemRenderer
            item={item}
            onEditQuantity={onEditQuantity}
            onEditComment={onEditComment}
        />
    );
}
