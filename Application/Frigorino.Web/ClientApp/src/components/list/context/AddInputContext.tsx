/* eslint-disable react-refresh/only-export-components */
import { createContext } from "react";
import type { AddInputState, ListItem } from "../types";

export interface AddInputContextValue extends AddInputState {
    text: string;
    editingItem?: ListItem;
    existingItems: ListItem[];
    isLoading: boolean;
}

export const AddInputContext = createContext<AddInputContextValue | null>(null);

interface AddInputProviderProps {
    children: React.ReactNode;
    value: AddInputContextValue;
}

export const AddInputProvider = ({
    children,
    value,
}: AddInputProviderProps) => {
    return (
        <AddInputContext.Provider value={value}>
            {children}
        </AddInputContext.Provider>
    );
};
