/**
 * Shared types for AddInput component and its children
 * These are component-specific interfaces, decoupled from API types
 */

export interface ListItem {
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
    editingItem?: ListItem;
    existingItems?: ListItem[];
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
    existingItem?: ListItem | null;
    existingItemStatus?: boolean;
}
