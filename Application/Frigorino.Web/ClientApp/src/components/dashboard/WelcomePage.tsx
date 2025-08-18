import {
    Add,
    ChevronRight,
    KitchenOutlined,
    RestaurantOutlined,
    TimerOutlined,
} from "@mui/icons-material";
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
    ListItemIcon,
    ListItemText,
    Stack,
    Typography,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { useAuth } from "../../hooks/useAuth";
import { useCurrentHousehold } from "../../hooks/useHouseholdQueries";
import { useHouseholdInventories } from "../../hooks/useInventoryQueries";
import { useHouseholdLists } from "../../hooks/useListQueries";
import { HeroImage } from "../common/HeroImage";
import { HouseholdSwitcher } from "../household/HouseholdSwitcher";

export const WelcomePage = () => {
    const { user } = useAuth();
    const navigate = useNavigate();

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

    const collections = [
        {
            id: "einkaufslisten",
            label: "Einkaufslisten",
            icon: <KitchenOutlined />,
            color: "#2196F3",
            items: listsLoading
                ? [{ name: "Loading...", count: "", status: "Loading", id: 0 }]
                : lists.length > 0
                  ? lists.map((list) => ({
                        name: list.name || "Unnamed List",
                        count: `${list.checkedCount}/${list.uncheckedCount} Artikel`,
                        status: new Date(list.createdAt!).toLocaleDateString(
                            "de-DE",
                        ),
                        id: list.id,
                    }))
                  : [
                        {
                            name: "No lists yet",
                            count: "",
                            status: "Create your first list!",
                            id: 0,
                        },
                    ],
        },
        {
            id: "inventar",
            label: "Inventar",
            icon: <TimerOutlined />,
            color: "#FF9800",
            items: inventoriesLoading
                ? [{ name: "Loading...", count: "", status: "Loading", id: 0 }]
                : inventories.length > 0
                  ? inventories.map((inventory) => ({
                        name: inventory.name || "Unnamed Inventory",
                        count: `${inventory.totalItems || 0} Items`,
                        status:
                            inventory.expiringItems &&
                            inventory.expiringItems > 0
                                ? `${inventory.expiringItems} expiring`
                                : "Current",
                        id: inventory.id,
                    }))
                  : [
                        {
                            name: "No inventories yet",
                            count: "",
                            status: "Create your first inventory!",
                            id: 0,
                        },
                    ],
        },
        {
            id: "rezepte",
            label: "Rezepte",
            icon: <RestaurantOutlined />,
            color: "#4CAF50",
            items: [
                {
                    name: "Coming soon...",
                    count: "",
                    status: "Recipe management will be added later",
                    id: 0,
                },
            ],
        },
    ];

    return (
        <Container
            maxWidth="sm"
            sx={{ py: { xs: 2, sm: 3 }, px: { xs: 1, sm: 2 } }}
        >
            {/* Header with User Info and Household Switcher */}
            <Box sx={{ mb: { xs: 3, sm: 4 } }}>
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

            {/* Welcome Image */}
            <Box sx={{ mb: { xs: 3, sm: 4 } }}>
                <HeroImage
                    src="/full.png"
                    alt="Frigorino Welcome"
                    size="small"
                />
            </Box>

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
                Overview
            </Typography>

            <Stack spacing={{ xs: 1.5, sm: 2 }} sx={{ mb: { xs: 3, sm: 4 } }}>
                {collections.map((collection) => {
                    const isExpanded =
                        expandedSections.includes(collection.id) &&
                        (collection.id === "einkaufslisten" ||
                            collection.id === "inventar");
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
                                                bgcolor: `${collection.color}15`,
                                                color: collection.color,
                                                display: "flex",
                                                alignItems: "center",
                                            }}
                                        >
                                            {collection.icon}
                                        </Box>
                                        <Box>
                                            <Typography
                                                variant="body1"
                                                sx={{
                                                    fontWeight: 600,
                                                    fontSize: "1rem",
                                                }}
                                            >
                                                {collection.label}
                                            </Typography>
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
                                            sx={{
                                                bgcolor: `${collection.color}15`,
                                                color: collection.color,
                                                width: 32,
                                                height: 32,
                                                "&:hover": {
                                                    bgcolor: `${collection.color}25`,
                                                },
                                            }}
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
                                            sx={{
                                                color: collection.color,
                                                width: 32,
                                                height: 32,
                                                "&:hover": {
                                                    bgcolor: `${collection.color}15`,
                                                },
                                            }}
                                        >
                                            <ChevronRight />
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
                                                    <ListItemIcon
                                                        sx={{ minWidth: 36 }}
                                                    >
                                                        <Box
                                                            sx={{
                                                                width: 8,
                                                                height: 8,
                                                                borderRadius:
                                                                    "50%",
                                                                bgcolor:
                                                                    collection.color,
                                                            }}
                                                        />
                                                    </ListItemIcon>
                                                    <ListItemText
                                                        primary={
                                                            <Box
                                                                sx={{
                                                                    display:
                                                                        "flex",
                                                                    justifyContent:
                                                                        "space-between",
                                                                    alignItems:
                                                                        "center",
                                                                }}
                                                            >
                                                                <Typography
                                                                    variant="body2"
                                                                    sx={{
                                                                        fontWeight: 500,
                                                                    }}
                                                                >
                                                                    {item.name}
                                                                </Typography>
                                                                <Chip
                                                                    label={
                                                                        item.count
                                                                    }
                                                                    size="small"
                                                                    variant="outlined"
                                                                    sx={{
                                                                        fontSize:
                                                                            "0.7rem",
                                                                        height: 20,
                                                                    }}
                                                                />
                                                            </Box>
                                                        }
                                                        secondary={item.count}
                                                    />
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
