/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateListRequest } from '../models/CreateListRequest';
import type { ListResponse } from '../models/ListResponse';
import type { UpdateListRequest } from '../models/UpdateListRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class ListsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @param requestBody
     * @returns ListResponse Created
     * @throws ApiError
     */
    public createList(
        householdId: number,
        requestBody: CreateListRequest,
    ): CancelablePromise<ListResponse> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/lists',
            path: {
                'householdId': householdId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
                400: `Bad Request`,
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @returns ListResponse OK
     * @throws ApiError
     */
    public getLists(
        householdId: number,
    ): CancelablePromise<Array<ListResponse>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/lists',
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
     * @param listId
     * @returns ListResponse OK
     * @throws ApiError
     */
    public getList(
        householdId: number,
        listId: number,
    ): CancelablePromise<ListResponse> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/lists/{listId}',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param requestBody
     * @returns ListResponse OK
     * @throws ApiError
     */
    public updateList(
        householdId: number,
        listId: number,
        requestBody: UpdateListRequest,
    ): CancelablePromise<ListResponse> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/lists/{listId}',
            path: {
                'householdId': householdId,
                'listId': listId,
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
     * @param listId
     * @returns void
     * @throws ApiError
     */
    public deleteList(
        householdId: number,
        listId: number,
    ): CancelablePromise<void> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/lists/{listId}',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
            errors: {
                403: `Forbidden`,
                404: `Not Found`,
            },
        });
    }
}
