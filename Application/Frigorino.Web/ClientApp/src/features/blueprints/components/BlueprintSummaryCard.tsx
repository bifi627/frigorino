import { MoreVert } from "@mui/icons-material";
import {
    Box,
    Card,
    CardContent,
    Chip,
    IconButton,
    ListItem,
    ListItemText,
    List as MuiList,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import type { SortBlueprintResponse } from "../../../lib/api";

interface BlueprintSummaryCardProps {
    blueprint: SortBlueprintResponse;
    onClick: (blueprintId: number) => void;
    onMenuOpen: (
        event: React.MouseEvent<HTMLElement>,
        blueprint: SortBlueprintResponse,
    ) => void;
    menuDisabled?: boolean;
}

export const BlueprintSummaryCard = ({
    blueprint,
    onClick,
    onMenuOpen,
    menuDisabled = false,
}: BlueprintSummaryCardProps) => {
    const { t } = useTranslation();

    return (
        <Card elevation={1}>
            <CardContent sx={{ py: 2 }}>
                <MuiList disablePadding>
                    <ListItem
                        data-testid={`blueprint-item-${blueprint.name}`}
                        sx={{
                            px: 0,
                            cursor: "pointer",
                            "&:hover": { bgcolor: "action.hover" },
                        }}
                        onClick={() => onClick(blueprint.id)}
                        secondaryAction={
                            <Box
                                sx={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 1,
                                }}
                            >
                                <Chip
                                    label={t("blueprints.aisleCount", {
                                        count: blueprint.categories.length,
                                    })}
                                    size="small"
                                    variant="outlined"
                                />
                                <IconButton
                                    size="small"
                                    data-testid={`blueprint-item-menu-button-${blueprint.name}`}
                                    onClick={(e) => {
                                        e.stopPropagation();
                                        onMenuOpen(e, blueprint);
                                    }}
                                    disabled={menuDisabled}
                                >
                                    <MoreVert fontSize="small" />
                                </IconButton>
                            </Box>
                        }
                    >
                        <ListItemText
                            primary={
                                <Typography
                                    variant="body1"
                                    sx={{ fontWeight: 600 }}
                                >
                                    {blueprint.name}
                                </Typography>
                            }
                        />
                    </ListItem>
                </MuiList>
            </CardContent>
        </Card>
    );
};
