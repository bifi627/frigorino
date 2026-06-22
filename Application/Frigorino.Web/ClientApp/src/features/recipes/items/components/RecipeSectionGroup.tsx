import { Delete, DriveFileRenameOutline, MoreVert } from "@mui/icons-material";
import {
    Box,
    Collapse,
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
import { sectionColors } from "../../../../theme";
import { useUpdateRecipeSection } from "../../sections/useUpdateRecipeSection";
import { RecipeContainer } from "./RecipeContainer";

const SAVE_DEBOUNCE_MS = 600;
const coral = sectionColors.recipes;

interface RecipeSectionGroupProps {
    householdId: number;
    recipeId: number;
    section: RecipeSectionResponse;
    canDelete: boolean;
    onDelete: () => void;
    editingItem: RecipeItemResponse | null;
    onEditItem: (item: RecipeItemResponse) => void;
    isExtracting?: boolean;
    extractingItemId?: number | null;
    dragHandle: ReactNode;
}

export const RecipeSectionGroup = ({
    householdId,
    recipeId,
    section,
    canDelete,
    onDelete,
    editingItem,
    onEditItem,
    isExtracting,
    extractingItemId,
    dragHandle,
}: RecipeSectionGroupProps) => {
    const { t } = useTranslation();
    const updateSection = useUpdateRecipeSection();

    const [name, setName] = useState(section.name ?? "");
    const [description, setDescription] = useState(section.description ?? "");
    const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
    // Fields are revealed when the section already carries a name/description, or on "Rename".
    const [renaming, setRenaming] = useState(
        Boolean(section.name?.trim() || section.description?.trim()),
    );

    const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    // Latest field state, read by the debounced/blur flush without re-creating the timer.
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
        <Box data-testid={`recipe-section-${section.id}`}>
            <Stack
                direction="row"
                spacing={1}
                sx={{ alignItems: "center", mt: 1, mb: 0.5 }}
            >
                {dragHandle}
                <Typography
                    variant="subtitle2"
                    sx={{
                        fontWeight: 700,
                        color: coral,
                        letterSpacing: 0.8,
                        textTransform: "uppercase",
                        fontSize: "0.72rem",
                    }}
                >
                    {displayName}
                </Typography>
                <Box sx={{ flex: 1, height: "1px", bgcolor: "divider" }} />
                <IconButton
                    size="small"
                    sx={{ opacity: 0.6 }}
                    onClick={(e) => setMenuAnchor(e.currentTarget)}
                    data-testid={`recipe-section-${section.id}-menu`}
                >
                    <MoreVert fontSize="small" />
                </IconButton>
            </Stack>

            <Collapse in={renaming}>
                <Stack spacing={2} sx={{ px: 0.5, pb: 1 }}>
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
            </Collapse>

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

            <Menu
                anchorEl={menuAnchor}
                open={Boolean(menuAnchor)}
                onClose={() => setMenuAnchor(null)}
            >
                <MenuItem
                    onClick={() => {
                        setMenuAnchor(null);
                        setRenaming(true);
                    }}
                    data-testid={`recipe-section-${section.id}-rename`}
                >
                    <ListItemIcon>
                        <DriveFileRenameOutline fontSize="small" />
                    </ListItemIcon>
                    <ListItemText>{t("recipes.renameSection")}</ListItemText>
                </MenuItem>
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
        </Box>
    );
};
