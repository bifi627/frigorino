import { Inventory2Outlined } from "@mui/icons-material";
import { Button, Paper, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { usePromotableForList } from "./promotableStore";
import { PromoteReviewSheet } from "./PromoteReviewSheet";

interface PromoteBarProps {
    householdId: number;
    listId: number;
}

// Sits between the list header and the scrolling item list. Visible only while this list has
// pending promote candidates (perishables checked off but not yet added to inventory).
export const PromoteBar = ({ householdId, listId }: PromoteBarProps) => {
    const { t } = useTranslation();
    const entries = usePromotableForList(listId);
    const [open, setOpen] = useState(false);

    useEffect(() => {
        if (entries.length === 0) {
            setOpen(false);
        }
    }, [entries.length]);

    if (entries.length === 0) {
        return null;
    }

    return (
        <>
            <Paper
                elevation={0}
                data-testid="promote-bar"
                data-count={entries.length}
                sx={{
                    mx: 3,
                    mb: 1,
                    px: 1.5,
                    py: 1,
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                    bgcolor: (theme) => alpha(theme.palette.primary.main, 0.15),
                    color: "text.primary",
                    border: "1px solid",
                    borderColor: (theme) =>
                        alpha(theme.palette.primary.main, 0.3),
                    borderLeft: "3px solid",
                    borderLeftColor: "primary.main",
                }}
            >
                <Inventory2Outlined
                    fontSize="small"
                    sx={{ color: "primary.main" }}
                />
                <Typography variant="body2" sx={{ flex: 1 }}>
                    {t("promote.barReady", { count: entries.length })}
                </Typography>
                <Button
                    size="small"
                    variant="contained"
                    color="primary"
                    data-testid="promote-bar-review"
                    onClick={() => setOpen(true)}
                >
                    {t("promote.review")}
                </Button>
            </Paper>

            <PromoteReviewSheet
                open={open}
                onClose={() => setOpen(false)}
                householdId={householdId}
                listId={listId}
            />
        </>
    );
};
