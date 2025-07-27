/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AddMemberRequest } from '../models/AddMemberRequest';
import type { HouseholdMemberDto } from '../models/HouseholdMemberDto';
import type { UpdateMemberRoleRequest } from '../models/UpdateMemberRoleRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class MembersService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @returns HouseholdMemberDto OK
     * @throws ApiError
     */
    public getApiHouseholdMembers(
        householdId: number,
    ): CancelablePromise<Array<HouseholdMemberDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/Members',
            path: {
                'householdId': householdId,
            },
        });
    }
    /**
     * @param householdId
     * @param requestBody
     * @returns HouseholdMemberDto OK
     * @throws ApiError
     */
    public postApiHouseholdMembers(
        householdId: number,
        requestBody?: AddMemberRequest,
    ): CancelablePromise<HouseholdMemberDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/Members',
            path: {
                'householdId': householdId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param memberUserId
     * @param requestBody
     * @returns HouseholdMemberDto OK
     * @throws ApiError
     */
    public putApiHouseholdMembers(
        householdId: number,
        memberUserId: string,
        requestBody?: UpdateMemberRoleRequest,
    ): CancelablePromise<HouseholdMemberDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/Members/{memberUserId}',
            path: {
                'householdId': householdId,
                'memberUserId': memberUserId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param memberUserId
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiHouseholdMembers(
        householdId: number,
        memberUserId: string,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/Members/{memberUserId}',
            path: {
                'householdId': householdId,
                'memberUserId': memberUserId,
            },
        });
    }
    /**
     * @param householdId
     * @returns any OK
     * @throws ApiError
     */
    public postApiHouseholdMembersLeave(
        householdId: number,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/Members/leave',
            path: {
                'householdId': householdId,
            },
        });
    }
}
