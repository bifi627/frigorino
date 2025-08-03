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
import { useState } from "react";
import { useAuth } from "../../hooks/useAuth";
import { useCurrentHousehold } from "../../hooks/useHouseholdQueries";
import { useHouseholdLists } from "../../hooks/useListQueries";
import { HeroImage } from "../common/HeroImage";
import { HouseholdSwitcher } from "../household/HouseholdSwitcher";

export const WelcomePage = () => {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [expandedSections, setExpandedSections] = useState<string[]>([]);

    // Get current household and lists
    const { data: currentHousehold } = useCurrentHousehold();
    const { data: lists = [], isLoading: listsLoading } = useHouseholdLists(
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
                // TODO: Implement inventory add functionality
                console.log("Add new item to inventory");
                break;
            case "rezepte":
                // TODO: Implement recipe add functionality
                console.log("Add new recipe");
                break;
            default:
                console.log(`Add new item to ${collectionId}`);
        }
    };

    const collections = [
        {
            id: "einkaufslisten",
            label: "Einkaufslisten",
            icon: <KitchenOutlined />,
            color: "#2196F3",
            items: listsLoading
                ? [{ name: "Loading...", count: "", status: "Loading" }]
                : lists.length > 0
                  ? lists.map((list) => ({
                        name: list.name || "Unnamed List",
                        count: "List",
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
                        },
                    ],
        },
        {
            id: "inventar",
            label: "Inventar",
            icon: <TimerOutlined />,
            color: "#FF9800",
            items: [
                { name: "Kühlschrank", count: "23 Artikel", status: "Aktuell" },
                {
                    name: "Gefrierschrank",
                    count: "15 Artikel",
                    status: "Aktuell",
                },
                {
                    name: "Vorratsschrank",
                    count: "34 Artikel",
                    status: "Zu prüfen",
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
                    name: "Pasta Bolognese",
                    count: "4 Portionen",
                    status: "Favorit",
                },
                { name: "Gemüsecurry", count: "6 Portionen", status: "Neu" },
                {
                    name: "Apfelkuchen",
                    count: "8 Portionen",
                    status: "Klassiker",
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
                    const isExpanded = expandedSections.includes(collection.id);
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
                                            <Typography
                                                variant="body2"
                                                sx={{
                                                    color: "text.secondary",
                                                    fontSize: "0.85rem",
                                                }}
                                            >
                                                {collection.items.length}{" "}
                                                Einträge
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
                                                collection.id ===
                                                    "einkaufslisten" &&
                                                (item as any).id;
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
                                                            ? () =>
                                                                  navigate({
                                                                      to: "/lists/$listId/view",
                                                                      params: {
                                                                          listId: (
                                                                              item as any
                                                                          ).id.toString(),
                                                                      },
                                                                  })
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
                                                                        item.status
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
