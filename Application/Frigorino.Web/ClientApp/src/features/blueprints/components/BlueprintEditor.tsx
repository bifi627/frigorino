import {
    closestCenter,
    DndContext,
    PointerSensor,
    TouchSensor,
    useSensor,
    useSensors,
    type DragEndEvent,
} from "@dnd-kit/core";
import {
    arrayMove,
    SortableContext,
    useSortable,
    verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { Add, DragHandle, RemoveCircleOutlined } from "@mui/icons-material";
import {
    Box,
    IconButton,
    List,
    ListItem,
    ListItemButton,
    ListItemText,
    Paper,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ProductCategory } from "../../../lib/api/types.gen";
import { aisleLabelKey, ALL_AISLES } from "../aisles";

interface Props {
    included: ProductCategory[];
    onChange: (next: ProductCategory[]) => void;
    disabled?: boolean;
}

function IncludedAisleRow({
    category,
    onRemove,
    disabled,
}: {
    category: ProductCategory;
    onRemove: () => void;
    disabled?: boolean;
}) {
    const { t } = useTranslation();
    const {
        attributes,
        listeners,
        setNodeRef,
        transform,
        transition,
        isDragging,
    } = useSortable({ id: category, disabled });
    const style = {
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.5 : 1,
    };

    // Listeners live on the dedicated handle only (not the whole row), and that handle sets
    // `touch-action: none` — without it the browser claims the touch for scrolling and the
    // TouchSensor never activates, which is why dragging worked on desktop but not on mobile.
    const dragHandleProps = disabled ? {} : { ...attributes, ...listeners };

    return (
        <Paper
            ref={setNodeRef}
            style={style}
            component="li"
            variant="outlined"
            data-testid={`blueprint-included-${category}`}
            sx={{ mb: 0.5, listStyle: "none" }}
        >
            <ListItem
                component="div"
                disablePadding
                secondaryAction={
                    <IconButton
                        edge="end"
                        onClick={onRemove}
                        disabled={disabled}
                        data-testid={`blueprint-remove-${category}`}
                    >
                        <RemoveCircleOutlined />
                    </IconButton>
                }
            >
                <Box
                    {...dragHandleProps}
                    data-testid={`blueprint-drag-${category}`}
                    sx={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        minWidth: 48,
                        height: 48,
                        cursor: disabled ? "default" : "grab",
                        color: "text.secondary",
                        touchAction: "none",
                        userSelect: "none",
                        WebkitUserSelect: "none",
                        WebkitTouchCallout: "none",
                    }}
                >
                    <DragHandle fontSize="small" />
                </Box>
                <ListItemText primary={t(aisleLabelKey(category))} />
            </ListItem>
        </Paper>
    );
}

export function BlueprintEditor({
    included,
    onChange,
    disabled = false,
}: Props) {
    const { t } = useTranslation();
    const sensors = useSensors(
        useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
        useSensor(TouchSensor, {
            activationConstraint: { delay: 200, tolerance: 5 },
        }),
    );
    const available = ALL_AISLES.filter((c) => !included.includes(c));

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        if (!over || active.id === over.id) {
            return;
        }
        const from = included.indexOf(active.id as ProductCategory);
        const to = included.indexOf(over.id as ProductCategory);
        if (from === -1 || to === -1) {
            return;
        }
        onChange(arrayMove(included, from, to));
    };

    return (
        <Box>
            <Typography variant="subtitle2" sx={{ mt: 1, mb: 0.5 }}>
                {t("blueprints.included")}
            </Typography>
            {included.length === 0 ? (
                <Paper variant="outlined" sx={{ p: 2, textAlign: "center" }}>
                    <Typography variant="body2" color="text.secondary">
                        {t("blueprints.noAislesYet")}
                    </Typography>
                </Paper>
            ) : (
                <DndContext
                    sensors={sensors}
                    collisionDetection={closestCenter}
                    onDragEnd={handleDragEnd}
                >
                    <SortableContext
                        items={included}
                        strategy={verticalListSortingStrategy}
                    >
                        <List
                            data-testid="blueprint-included-list"
                            sx={{ py: 0 }}
                        >
                            {included.map((category) => (
                                <IncludedAisleRow
                                    key={category}
                                    category={category}
                                    disabled={disabled}
                                    onRemove={() =>
                                        onChange(
                                            included.filter(
                                                (c) => c !== category,
                                            ),
                                        )
                                    }
                                />
                            ))}
                        </List>
                    </SortableContext>
                </DndContext>
            )}

            <Typography variant="subtitle2" sx={{ mt: 2, mb: 0.5 }}>
                {t("blueprints.available")}
            </Typography>
            <List data-testid="blueprint-available-list" sx={{ py: 0 }}>
                {available.map((category) => (
                    <Paper
                        key={category}
                        component="li"
                        variant="outlined"
                        data-testid={`blueprint-available-${category}`}
                        sx={{ mb: 0.5, listStyle: "none", overflow: "hidden" }}
                    >
                        <ListItem
                            component="div"
                            disablePadding
                            secondaryAction={
                                <IconButton
                                    edge="end"
                                    disabled={disabled}
                                    onClick={() =>
                                        onChange([...included, category])
                                    }
                                    data-testid={`blueprint-add-${category}`}
                                >
                                    <Add />
                                </IconButton>
                            }
                        >
                            <ListItemButton
                                disabled={disabled}
                                onClick={() =>
                                    onChange([...included, category])
                                }
                            >
                                <ListItemText
                                    primary={t(aisleLabelKey(category))}
                                />
                            </ListItemButton>
                        </ListItem>
                    </Paper>
                ))}
            </List>
        </Box>
    );
}
