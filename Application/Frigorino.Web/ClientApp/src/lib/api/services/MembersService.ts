/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AddMemberRequest } from '../models/AddMemberRequest';
import type { HouseholdMemberDto } from '../models/HouseholdMemberDto';
import type { MemberResponse } from '../models/MemberResponse';
import type { UpdateMemberRoleRequest } from '../models/UpdateMemberRoleRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class MembersService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @returns MemberResponse OK
     * @throws ApiError
     */
    public getMembers(
        householdId: number,
    ): CancelablePromise<Array<MemberResponse>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/members',
            path: {
                'householdId': householdId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param requestBody
     * @returns MemberResponse Created
     * @throws ApiError
     */
    public addMember(
        householdId: number,
        requestBody: AddMemberRequest,
    ): CancelablePromise<MemberResponse> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/members',
            path: {
                'householdId': householdId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
                403: `Forbidden`,
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param userId
     * @returns void
     * @throws ApiError
     */
    public removeMember(
        householdId: number,
        userId: string,
    ): CancelablePromise<void> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/members/{userId}',
            path: {
                'householdId': householdId,
                'userId': userId,
            },
            errors: {
                400: `Bad Request`,
                403: `Forbidden`,
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param targetUserId
     * @param requestBody
     * @returns HouseholdMemberDto OK
     * @throws ApiError
     */
    public putApiHouseholdMembersRole(
        householdId: number,
        targetUserId: string,
        requestBody: UpdateMemberRoleRequest,
    ): CancelablePromise<HouseholdMemberDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/Members/{targetUserId}/role',
            path: {
                'householdId': householdId,
                'targetUserId': targetUserId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
