/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { InventoryCreatorResponse } from './InventoryCreatorResponse';
export type InventoryResponse = {
    id: number;
    name: string;
    description: string | null;
    householdId: number;
    createdAt: string;
    updatedAt: string;
    createdByUser: InventoryCreatorResponse;
    totalItems: number;
    expiringItems: number;
};

