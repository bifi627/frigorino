import { useContext } from "react";
import { AddInputContext } from "../context/AddInputContext";

export const useAddInputContext = () => {
    const context = useContext(AddInputContext);
    if (!context) {
        throw new Error(
            "useAddInputContext must be used within AddInputProvider",
        );
    }
    return context;
};
