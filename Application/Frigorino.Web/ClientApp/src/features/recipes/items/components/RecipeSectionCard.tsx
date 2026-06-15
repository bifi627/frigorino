import { Delete, MoreVert } from "@mui/icons-material";
import {
    Accordion,
    AccordionDetails,
    AccordionSummary,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import type { ReactNode } from "react";
import {
    useCallback,
    useEffect,
    useLayoutEffect,
    useRef,
    useState,
} from "react";
import { useTranslation } from "react-i18next";
import type {
    RecipeItemResponse,
    RecipeSectionResponse,
} from "../../../../lib/api";
import { useUpdateRecipeSection } from "../../sections/useUpdateRecipeSection";
import { RecipeContainer } from "./RecipeContainer";

const SAVE_DEBOUNCE_MS = 600;

interface RecipeSectionCardProps {
    householdId: number;
    recipeId: number;
    section: RecipeSectionResponse;
    expanded: boolean;
    onToggle: (expanded: boolean) => void;
    canDelete: boolean;
    onDelete: () => void;
    editingItem: RecipeItemResponse | null;
    onEditItem: (item: RecipeItemResponse) => void;
    isExtracting?: boolean;
    extractingItemId?: number | null;
    dragHandle: ReactNode;
}

export const RecipeSectionCard = ({
    householdId,
    recipeId,
    section,
    expanded,
    onToggle,
    canDelete,
    onDelete,
    editingItem,
    onEditItem,
    isExtracting,
    extractingItemId,
    dragHandle,
}: RecipeSectionCardProps) => {
    const { t } = useTranslation();
    const updateSection = useUpdateRecipeSection();

    const [name, setName] = useState(section.name ?? "");
    const [description, setDescription] = useState(section.description ?? "");
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    // Latest field state, read by the debounced/blur flush without re-creating the timer.
    // Synced in useLayoutEffect (not during render) so the React Compiler's ref rule is happy
    // and the value is current before any pending timer or blur handler fires.
    const latest = useRef({ name, description });
    useLayoutEffect(() => {
        latest.current = { name, description };
    });

    const { mutate } = updateSection;

    const save = useCallback(() => {
        mutate({
            path: { householdId, recipeId, sectionId: section.id },
            body: {
                name: latest.current.name.trim() || null,
                description: latest.current.description.trim() || null,
            },
        });
    }, [mutate, householdId, recipeId, section.id]);

    const scheduleSave = useCallback(() => {
        if (timerRef.current) clearTimeout(timerRef.current);
        timerRef.current = setTimeout(save, SAVE_DEBOUNCE_MS);
    }, [save]);

    const flushSave = useCallback(() => {
        if (timerRef.current) {
            clearTimeout(timerRef.current);
            timerRef.current = null;
        }
        save();
    }, [save]);

    useEffect(
        () => () => {
            if (timerRef.current) clearTimeout(timerRef.current);
        },
        [],
    );

    const displayName = section.name?.trim() || t("recipes.ingredientsHeading");

    return (
        <Accordion
            expanded={expanded}
            onChange={(_e, isExpanded) => onToggle(isExpanded)}
            disableGutters
            elevation={4}
            data-testid={`recipe-section-${section.id}`}
        >
            <AccordionSummary
                data-testid={`recipe-section-${section.id}-summary`}
                sx={{
                    "& .MuiAccordionSummary-content": { alignItems: "center" },
                }}
            >
                {dragHandle}
                <Typography
                    variant="subtitle1"
                    sx={{ fontWeight: 600, flex: 1 }}
                >
                    {displayName}
                </Typography>
                <IconButton
                    size="small"
                    onClick={(e) => {
                        e.stopPropagation();
                        setMenuAnchor(e.currentTarget);
                    }}
                    data-testid={`recipe-section-${section.id}-menu`}
                >
                    <MoreVert fontSize="small" />
                </IconButton>
            </AccordionSummary>
            <AccordionDetails sx={{ p: 0 }}>
                <Stack spacing={2} sx={{ px: 2, pt: 1, pb: 2 }}>
                    <TextField
                        label={t("recipes.sectionName")}
                        value={name}
                        onChange={(e) => {
                            setName(e.target.value);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        size="small"
                        fullWidth
                        placeholder={t("recipes.sectionNamePlaceholder")}
                        slotProps={{
                            htmlInput: {
                                maxLength: 100,
                                "data-testid": `recipe-section-${section.id}-name-input`,
                            },
                        }}
                    />
                    <TextField
                        label={t("recipes.sectionDescription")}
                        value={description}
                        onChange={(e) => {
                            setDescription(e.target.value);
                            scheduleSave();
                        }}
                        onBlur={flushSave}
                        size="small"
                        fullWidth
                        multiline
                        minRows={2}
                        placeholder={t("recipes.sectionDescriptionPlaceholder")}
                        slotProps={{
                            htmlInput: {
                                maxLength: 2000,
                                "data-testid": `recipe-section-${section.id}-description-input`,
                            },
                        }}
                    />
                </Stack>
                <RecipeContainer
                    householdId={householdId}
                    recipeId={recipeId}
                    sectionId={section.id}
                    editingItem={editingItem}
                    onEdit={onEditItem}
                    isExtracting={isExtracting}
                    extractingItemId={extractingItemId}
                    scrollable={false}
                />
            </AccordionDetails>

            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={() => setMenuAnchor(null)}
            >
                <MenuItem
                    disabled={!canDelete}
                    onClick={() => {
                        setMenuAnchor(null);
                        onDelete();
                    }}
                    data-testid={`recipe-section-${section.id}-delete`}
                >
                    <ListItemIcon>
                        <Delete
                            fontSize="small"
                            color={canDelete ? "error" : "disabled"}
                        />
                    </ListItemIcon>
                    <ListItemText>{t("recipes.deleteSection")}</ListItemText>
                </MenuItem>
            </Menu>
        </Accordion>
    );
};
