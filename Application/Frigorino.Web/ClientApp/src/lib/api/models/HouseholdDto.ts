/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { HouseholdMemberDto } from './HouseholdMemberDto';
import type { HouseholdRole } from './HouseholdRole';
import type { UserDto } from './UserDto';
export type HouseholdDto = {
    id?: number;
    name?: string | null;
    description?: string | null;
    createdAt?: string;
    updatedAt?: string;
    createdByUser?: UserDto;
    currentUserRole?: HouseholdRole;
    memberCount?: number;
    members?: Array<HouseholdMemberDto> | null;
};

