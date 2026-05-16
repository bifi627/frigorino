/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateItemRequest } from '../models/CreateItemRequest';
import type { ListItemResponse } from '../models/ListItemResponse';
import type { ReorderItemRequest } from '../models/ReorderItemRequest';
import type { UpdateItemRequest } from '../models/UpdateItemRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class ListItemsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @param listId
     * @returns ListItemResponse OK
     * @throws ApiError
     */
    public getItems(
        householdId: number,
        listId: number,
    ): CancelablePromise<Array<ListItemResponse>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/lists/{listId}/items',
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
     * @returns ListItemResponse Created
     * @throws ApiError
     */
    public createItem(
        householdId: number,
        listId: number,
        requestBody: CreateItemRequest,
    ): CancelablePromise<ListItemResponse> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/lists/{listId}/items',
            path: {
                'householdId': householdId,
                'listId': listId,
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
     * @param listId
     * @param itemId
     * @returns ListItemResponse OK
     * @throws ApiError
     */
    public getItem(
        householdId: number,
        listId: number,
        itemId: number,
    ): CancelablePromise<ListItemResponse> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/lists/{listId}/items/{itemId}',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param itemId
     * @param requestBody
     * @returns ListItemResponse OK
     * @throws ApiError
     */
    public updateItem(
        householdId: number,
        listId: number,
        itemId: number,
        requestBody: UpdateItemRequest,
    ): CancelablePromise<ListItemResponse> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/lists/{listId}/items/{itemId}',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
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
     * @param listId
     * @param itemId
     * @returns void
     * @throws ApiError
     */
    public deleteItem(
        householdId: number,
        listId: number,
        itemId: number,
    ): CancelablePromise<void> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/lists/{listId}/items/{itemId}',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param itemId
     * @returns ListItemResponse OK
     * @throws ApiError
     */
    public toggleItemStatus(
        householdId: number,
        listId: number,
        itemId: number,
    ): CancelablePromise<ListItemResponse> {
        return this.httpRequest.request({
            method: 'PATCH',
            url: '/api/household/{householdId}/lists/{listId}/items/{itemId}/toggle-status',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param listId
     * @param itemId
     * @param requestBody
     * @returns ListItemResponse OK
     * @throws ApiError
     */
    public reorderItem(
        householdId: number,
        listId: number,
        itemId: number,
        requestBody: ReorderItemRequest,
    ): CancelablePromise<ListItemResponse> {
        return this.httpRequest.request({
            method: 'PATCH',
            url: '/api/household/{householdId}/lists/{listId}/items/{itemId}/reorder',
            path: {
                'householdId': householdId,
                'listId': listId,
                'itemId': itemId,
            },
            body: requestBody,
            mediaType: 'application/json',
            errors: {
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
    public compactItems(
        householdId: number,
        listId: number,
    ): CancelablePromise<void> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/lists/{listId}/items/compact',
            path: {
                'householdId': householdId,
                'listId': listId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
}
