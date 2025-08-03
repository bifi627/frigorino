/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateListRequest } from '../models/CreateListRequest';
import type { ListDto } from '../models/ListDto';
import type { UpdateListRequest } from '../models/UpdateListRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class ListsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @returns ListDto OK
     * @throws ApiError
     */
    public getApiHouseholdLists(
        householdId: number,
    ): CancelablePromise<Array<ListDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/Lists',
            path: {
                'householdId': householdId,
            },
        });
    }
    /**
     * @param householdId
     * @param requestBody
     * @returns ListDto OK
     * @throws ApiError
     */
    public postApiHouseholdLists(
        householdId: number,
        requestBody?: CreateListRequest,
    ): CancelablePromise<ListDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/Lists',
            path: {
                'householdId': householdId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param listId
     * @returns ListDto OK
     * @throws ApiError
     */
    public getApiHouseholdLists1(
        householdId: number,
        listId: number,
    ): CancelablePromise<ListDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/Lists/{listId}',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param requestBody
     * @returns ListDto OK
     * @throws ApiError
     */
    public putApiHouseholdLists(
        householdId: number,
        listId: number,
        requestBody?: UpdateListRequest,
    ): CancelablePromise<ListDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/Lists/{listId}',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param householdId
     * @param listId
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiHouseholdLists(
        householdId: number,
        listId: number,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/Lists/{listId}',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
        });
    }
}
