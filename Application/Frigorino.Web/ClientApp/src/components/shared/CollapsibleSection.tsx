import { ExpandMore } from "@mui/icons-material";
import {
    Accordion,
    AccordionDetails,
    AccordionSummary,
    Typography,
} from "@mui/material";
import type { ReactNode, SyntheticEvent } from "react";

interface CollapsibleSectionProps {
    title: string;
    expanded: boolean;
    onChange: (expanded: boolean) => void;
    children: ReactNode;
    // Base test id; the summary button and content region derive `-summary` / `-content` from it.
    testId?: string;
    // List-style content brings its own padding — set true to make AccordionDetails flush.
    disableContentPadding?: boolean;
}

export const CollapsibleSection = ({
    title,
    expanded,
    onChange,
    children,
    testId,
    disableContentPadding = false,
}: CollapsibleSectionProps) => {
    return (
        <Accordion
            expanded={expanded}
            onChange={(_event: SyntheticEvent, isExpanded: boolean) =>
                onChange(isExpanded)
            }
            disableGutters
            elevation={4}
            data-testid={testId}
            slotProps={{ heading: { component: "h2" } }}
        >
            <AccordionSummary
                expandIcon={<ExpandMore />}
                data-testid={testId ? `${testId}-summary` : undefined}
            >
                <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                    {title}
                </Typography>
            </AccordionSummary>
            <AccordionDetails
                data-testid={testId ? `${testId}-content` : undefined}
                sx={disableContentPadding ? { p: 0 } : undefined}
            >
                {children}
            </AccordionDetails>
        </Accordion>
    );
};
