import { Chip, type ChipProps } from "@mui/material";
import { useTranslation } from "react-i18next";
import { formatQuantity } from "../composer";
import type { QuantityDto } from "../../lib/api";

interface Props {
    quantity: QuantityDto;
    // When provided, tapping the chip triggers an edit affordance (e.g. open the quantity panel).
    onClick?: () => void;
    testId?: string;
    color?: ChipProps["color"];
}

// Shared quantity chip used by both list items and inventory items so their
// quantity rendering stays consistent. Outlined, compact (h20).
export function ItemQuantityChip({ quantity, onClick, testId, color }: Props) {
    const { t } = useTranslation();
    return (
        <Chip
            size="small"
            variant="outlined"
            color={color}
            data-testid={testId}
            label={formatQuantity(t, quantity)}
            onClick={onClick}
            sx={{ height: 20 }}
        />
    );
}
