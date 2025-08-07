/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { UserDto } from './UserDto';
export type InventoryDto = {
    id?: number;
    name?: string | null;
    description?: string | null;
    householdId?: number;
    createdAt?: string;
    updatedAt?: string;
    createdByUser?: UserDto;
    totalItems?: number;
    expiringItems?: number;
};

