/**
 * Shared types for AddInput component and its children
 * These are component-specific interfaces, decoupled from API types
 */

export interface AddInputItem {
    id?: number;
    text?: string | null;
    secondaryText?: string | null;
    status?: boolean;
}

export interface AddInputProps {
    onAdd: (data: string) => void;
    onUpdate?: (data: string) => void;
    onCancelEdit?: () => void;
    onUncheckExisting?: (itemId: number) => void;
    editingItem?: AddInputItem;
    existingItems?: AddInputItem[];
    isLoading?: boolean;
    hasItems?: boolean;
    topPanels?: React.ReactNode[];
    bottomPanels?: React.ReactNode[];
    rightControls?: React.ReactNode[];
}

export interface AddInputState {
    isEditing: boolean;
    hasDuplicate: boolean;
    hasText: boolean;
    existingItem?: AddInputItem | null;
    existingItemStatus?: boolean;
}