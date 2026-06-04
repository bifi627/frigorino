import { Add, ChevronRight, ExpandMore } from "@mui/icons-material";
import {
    Box,
    Card,
    CardContent,
    Chip,
    Collapse,
    Container,
    Divider,
    IconButton,
    List,
    ListItem,
    ListItemText,
    Stack,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAuth } from "../../hooks/useAuth";
import { HouseholdSwitcher } from "../../features/me/activeHousehold/components/HouseholdSwitcher";
import { useCurrentHousehold } from "../../features/me/activeHousehold/useCurrentHousehold";
import { useHouseholdInventories } from "../../features/inventories/useHouseholdInventories";
import { useHouseholdLists } from "../../features/lists/useHouseholdLists";
import { useLongPress } from "../../hooks/useLongPress";
import {
    formatLocalDate,
    getExpiryColor,
    getExpiryInfo,
} from "../../utils/dateUtils";
import { sectionColors, tintedActionButtonSx } from "../../theme";
import { sectionIcons } from "../../common/sections";

export const WelcomePage = () => {
    const { user } = useAuth();
    const navigate = useNavigate();
    const { t } = useTranslation();
    // getExpiryInfo expects a plain (key) => string; the i18n t has stricter overloads.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const translateKey = (key: string): string => t(key as any);

    // Local storage key for expanded sections
    const EXPANDED_SECTIONS_KEY = "frigorino-welcome-expanded-sections";

    // Load expanded sections from local storage or default to empty array
    const loadExpandedSections = (): string[] => {
        try {
            const saved = localStorage.getItem(EXPANDED_SECTIONS_KEY);
            return saved ? JSON.parse(saved) : [];
        } catch (error) {
            console.warn(
                "Failed to load expanded sections from localStorage:",
                error,
            );
            return [];
        }
    };

    const [expandedSections, setExpandedSections] =
        useState<string[]>(loadExpandedSections);

    // Save expanded sections to local storage whenever it changes
    useEffect(() => {
        try {
            localStorage.setItem(
                EXPANDED_SECTIONS_KEY,
                JSON.stringify(expandedSections),
            );
        } catch (error) {
            console.warn(
                "Failed to save expanded sections to localStorage:",
                error,
            );
        }
    }, [expandedSections]);

    // Get current household and lists
    const { data: currentHousehold } = useCurrentHousehold();
    const { data: lists = [], isLoading: listsLoading } = useHouseholdLists(
        currentHousehold?.householdId || 0,
        !!currentHousehold?.householdId,
    );
    const { data: inventories = [], isLoading: inventoriesLoading } =
        useHouseholdInventories(
            currentHousehold?.householdId || 0,
            !!currentHousehold?.householdId,
        );

    const handleCreateHousehold = () => {
        navigate({ to: "/household/create" });
    };

    const toggleSection = (sectionId: string) => {
        setExpandedSections((prev) =>
            prev.includes(sectionId)
                ? prev.filter((id) => id !== sectionId)
                : [...prev, sectionId],
        );
    };

    const handleAddItem = (collectionId: string) => {
        // Navigate to the appropriate create page based on collection
        switch (collectionId) {
            case "einkaufslisten":
                navigate({ to: "/lists/create" });
                break;
            case "inventar":
                navigate({ to: "/inventories/create" });
                break;
            case "rezepte":
                // TODO: Implement recipe add functionality
                window.console.log("Add new recipe");
                break;
            default:
                window.console.log(`Add new item to ${collectionId}`);
        }
    };

    const ListsIcon = sectionIcons.lists;
    const InventoryIcon = sectionIcons.inventory;
    const RecipesIcon = sectionIcons.recipes;

    const collections = [
        {
            id: "einkaufslisten",
            label: t("lists.shoppingLists"),
            icon: <ListsIcon />,
            color: sectionColors.lists,
            items: listsLoading
                ? [
                      {
                          name: t("common.loading"),
                          count: "",
                          status: "Loading",
                          id: 0,
                      },
                  ]
                : lists.length > 0
                  ? lists.map((list) => ({
                        name: list.name || "Unnamed List",
                        count: `${list.uncheckedCount} ${t("dashboard.open")}`,
                        status: `${list.uncheckedCount + list.checkedCount} ${t("dashboard.itemsTotal")}`,
                        id: list.id,
                    }))
                  : [
                        {
                            name: t("lists.noListsYet"),
                            count: "",
                            status: t("lists.createFirstShoppingList"),
                            id: 0,
                        },
                    ],
        },
        {
            id: "inventar",
            label: t("navigation.inventory"),
            icon: <InventoryIcon />,
            color: sectionColors.inventory,
            items: inventoriesLoading
                ? [
                      {
                          name: t("common.loading"),
                          count: "",
                          status: "Loading",
                          id: 0,
                      },
                  ]
                : inventories.length > 0
                  ? inventories.map((inventory) => {
                        const expiry = inventory.earliestExpiryDate;
                        const expiryChip = expiry
                            ? {
                                  label:
                                      getExpiryInfo(expiry, translateKey)
                                          .humanReadable ||
                                      formatLocalDate(expiry),
                                  color: getExpiryColor(expiry),
                              }
                            : null;
                        return {
                            name: inventory.name || "Unnamed Inventory",
                            count: `${inventory.totalItems || 0} ${t("dashboard.items")}`,
                            status:
                                inventory.expiringItems &&
                                inventory.expiringItems > 0
                                    ? `${inventory.expiringItems} ${t("dashboard.expiring")}`
                                    : t("common.current"),
                            id: inventory.id,
                            expiryChip,
                        };
                    })
                  : [
                        {
                            name: t("inventory.noInventoriesYet"),
                            count: "",
                            status: t("inventory.createFirstInventory"),
                            id: 0,
                        },
                    ],
        },
        {
            id: "rezepte",
            label: t("dashboard.recipes"),
            icon: <RecipesIcon />,
            color: sectionColors.recipes,
            items: [
                {
                    name: t("dashboard.comingSoon"),
                    count: "",
                    status: t("dashboard.recipeManagementLater"),
                    id: 0,
                },
            ],
        },
    ];

    const events = useLongPress({
        shouldPreventDefault: true,
        onLongPress: () => {
            navigator.vibrate(100);
            navigator.clipboard.writeText(user?.email ?? "");
            toast.success(t("common.textCopiedToClipboard"), {
                duration: 2000,
            });
        },
    });

    return (
        <Container
            maxWidth="sm"
            sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
        >
            {/* Header with User Info and Household Switcher */}
            <Box sx={{ mb: { xs: 3, sm: 4 }, userSelect: "none" }} {...events}>
                {/* Top Row - User Email and Household Switcher */}
                <Box
                    sx={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        mb: { xs: 2, sm: 3 },
                        gap: 1,
                    }}
                >
                    <Chip
                        label={user?.email || "User"}
                        variant="outlined"
                        size="small"
                        sx={{
                            fontSize: { xs: "0.7rem", sm: "0.8rem" },
                            maxWidth: { xs: "50%", sm: "60%" },
                            "& .MuiChip-label": {
                                px: { xs: 0.75, sm: 1 },
                                overflow: "hidden",
                                textOverflow: "ellipsis",
                            },
                        }}
                    />
                    <HouseholdSwitcher
                        onCreateHousehold={handleCreateHousehold}
                    />
                </Box>
            </Box>

            <Divider sx={{ mb: { xs: 3, sm: 4 } }} />

            {/* Quick Stats */}
            <Typography
                variant="h6"
                gutterBottom
                sx={{
                    fontWeight: 600,
                    mb: { xs: 2, sm: 3 },
                    color: "text.primary",
                    fontSize: { xs: "1.1rem", sm: "1.25rem" },
                }}
            >
                {t("common.overview")}
            </Typography>

            <Stack spacing={{ xs: 1.5, sm: 2 }} sx={{ mb: { xs: 3, sm: 4 } }}>
                {collections.map((collection) => {
                    // Only lists and inventory have expandable item previews;
                    // recipes is a placeholder card with nothing to reveal.
                    const isExpandable =
                        collection.id === "einkaufslisten" ||
                        collection.id === "inventar";
                    const isExpanded =
                        expandedSections.includes(collection.id) &&
                        isExpandable;
                    // Tinted, section-colored action buttons so they read as
                    // clearly tappable controls rather than ghost icons.
                    const actionButtonSx = {
                        ...tintedActionButtonSx(collection.color),
                        width: 34,
                        height: 34,
                    };
                    return (
                        <Card
                            key={collection.id}
                            sx={{
                                borderRadius: 2,
                                boxShadow: "0 2px 8px rgba(0,0,0,0.1)",
                                "&:hover": {
                                    boxShadow: "0 4px 12px rgba(0,0,0,0.15)",
                                },
                                transition: "all 0.3s ease",
                            }}
                        >
                            <CardContent sx={{ py: { xs: 2, sm: 2.5 } }}>
                                {/* Header */}
                                <Box
                                    sx={{
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "space-between",
                                    }}
                                >
                                    <Box
                                        sx={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: 2,
                                            cursor: "pointer",
                                            flex: 1,
                                        }}
                                        onClick={() => {
                                            toggleSection(collection.id);
                                        }}
                                    >
                                        <Box
                                            sx={{
                                                p: 1.5,
                                                borderRadius: 2,
                                                bgcolor: "action.hover",
                                                color: collection.color,
                                                display: "flex",
                                                alignItems: "center",
                                            }}
                                        >
                                            {collection.icon}
                                        </Box>
                                        <Box
                                            sx={{
                                                display: "flex",
                                                alignItems: "center",
                                                gap: 0.5,
                                            }}
                                        >
                                            <Typography
                                                variant="body1"
                                                sx={{
                                                    fontWeight: 600,
                                                    fontSize: "1rem",
                                                }}
                                            >
                                                {collection.label}
                                            </Typography>
                                            {isExpandable && (
                                                <ExpandMore
                                                    fontSize="small"
                                                    sx={{
                                                        color: "text.secondary",
                                                        transition:
                                                            "transform 0.2s ease",
                                                        transform: isExpanded
                                                            ? "rotate(180deg)"
                                                            : "none",
                                                    }}
                                                />
                                            )}
                                        </Box>
                                    </Box>

                                    <Box
                                        sx={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: 1,
                                        }}
                                    >
                                        <IconButton
                                            size="small"
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                handleAddItem(collection.id);
                                            }}
                                            sx={actionButtonSx}
                                        >
                                            <Add fontSize="small" />
                                        </IconButton>

                                        <IconButton
                                            size="small"
                                            onClick={() => {
                                                if (
                                                    collection.id ===
                                                    "einkaufslisten"
                                                ) {
                                                    navigate({ to: "/lists" });
                                                } else if (
                                                    collection.id === "inventar"
                                                ) {
                                                    navigate({
                                                        to: "/inventories",
                                                    });
                                                }
                                            }}
                                            sx={actionButtonSx}
                                        >
                                            <ChevronRight fontSize="small" />
                                        </IconButton>
                                    </Box>
                                </Box>

                                {/* Collapsible Content */}
                                <Collapse
                                    in={isExpanded}
                                    timeout="auto"
                                    unmountOnExit
                                >
                                    <List sx={{ mt: 2, pt: 0 }}>
                                        {collection.items.map((item, index) => {
                                            const isClickable =
                                                (collection.id ===
                                                    "einkaufslisten" &&
                                                    item.id) ||
                                                (collection.id === "inventar" &&
                                                    item.id);
                                            return (
                                                <ListItem
                                                    key={index}
                                                    sx={{
                                                        py: 1,
                                                        px: 0,
                                                        borderRadius: 1,
                                                        "&:hover": {
                                                            bgcolor:
                                                                "action.hover",
                                                        },
                                                        cursor: isClickable
                                                            ? "pointer"
                                                            : "default",
                                                    }}
                                                    onClick={
                                                        isClickable
                                                            ? () => {
                                                                  if (
                                                                      collection.id ===
                                                                      "einkaufslisten"
                                                                  ) {
                                                                      navigate({
                                                                          to: "/lists/$listId/view",
                                                                          params: {
                                                                              listId:
                                                                                  item.id?.toString() ??
                                                                                  "",
                                                                          },
                                                                      });
                                                                  } else if (
                                                                      collection.id ===
                                                                      "inventar"
                                                                  ) {
                                                                      navigate({
                                                                          to: "/inventories/$inventoryId/view",
                                                                          params: {
                                                                              inventoryId:
                                                                                  item.id?.toString() ??
                                                                                  "",
                                                                          },
                                                                      });
                                                                  }
                                                              }
                                                            : undefined
                                                    }
                                                >
                                                    <ListItemText
                                                        sx={{ my: 0 }}
                                                        primary={
                                                            <Typography
                                                                variant="body2"
                                                                sx={{
                                                                    fontWeight: 500,
                                                                }}
                                                            >
                                                                {item.name}
                                                            </Typography>
                                                        }
                                                        secondary={item.status}
                                                    />
                                                    <Box
                                                        sx={{
                                                            display: "flex",
                                                            alignItems:
                                                                "center",
                                                            gap: 0.5,
                                                            flexShrink: 0,
                                                            ml: 1,
                                                        }}
                                                    >
                                                        {"expiryChip" in item &&
                                                            item.expiryChip && (
                                                                <Chip
                                                                    label={
                                                                        item
                                                                            .expiryChip
                                                                            .label
                                                                    }
                                                                    size="small"
                                                                    variant="outlined"
                                                                    sx={{
                                                                        fontSize:
                                                                            "0.7rem",
                                                                        height: 20,
                                                                        color: item
                                                                            .expiryChip
                                                                            .color,
                                                                        borderColor:
                                                                            item
                                                                                .expiryChip
                                                                                .color,
                                                                    }}
                                                                />
                                                            )}
                                                        <Chip
                                                            label={item.count}
                                                            size="small"
                                                            variant="outlined"
                                                            sx={{
                                                                fontSize:
                                                                    "0.7rem",
                                                                height: 20,
                                                            }}
                                                        />
                                                    </Box>
                                                </ListItem>
                                            );
                                        })}
                                    </List>
                                </Collapse>
                            </CardContent>
                        </Card>
                    );
                })}
            </Stack>
        </Container>
    );
};
