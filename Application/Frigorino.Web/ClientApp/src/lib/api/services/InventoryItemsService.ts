/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateInventoryItemRequest } from '../models/CreateInventoryItemRequest';
import type { InventoryItemResponse } from '../models/InventoryItemResponse';
import type { ReorderItemRequest } from '../models/ReorderItemRequest';
import type { UpdateInventoryItemRequest } from '../models/UpdateInventoryItemRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class InventoryItemsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param householdId
     * @param inventoryId
     * @returns InventoryItemResponse OK
     * @throws ApiError
     */
    public getInventoryItems(
        householdId: number,
        inventoryId: number,
    ): CancelablePromise<Array<InventoryItemResponse>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/household/{householdId}/inventories/{inventoryId}/items',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param inventoryId
     * @param requestBody
     * @returns InventoryItemResponse Created
     * @throws ApiError
     */
    public createInventoryItem(
        householdId: number,
        inventoryId: number,
        requestBody: CreateInventoryItemRequest,
    ): CancelablePromise<InventoryItemResponse> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/inventories/{inventoryId}/items',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
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
     * @param inventoryId
     * @param itemId
     * @param requestBody
     * @returns InventoryItemResponse OK
     * @throws ApiError
     */
    public updateInventoryItem(
        householdId: number,
        inventoryId: number,
        itemId: number,
        requestBody: UpdateInventoryItemRequest,
    ): CancelablePromise<InventoryItemResponse> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/household/{householdId}/inventories/{inventoryId}/items/{itemId}',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
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
     * @param inventoryId
     * @param itemId
     * @returns void
     * @throws ApiError
     */
    public deleteInventoryItem(
        householdId: number,
        inventoryId: number,
        itemId: number,
    ): CancelablePromise<void> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/household/{householdId}/inventories/{inventoryId}/items/{itemId}',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
                'itemId': itemId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
    /**
     * @param householdId
     * @param inventoryId
     * @param itemId
     * @param requestBody
     * @returns InventoryItemResponse OK
     * @throws ApiError
     */
    public reorderInventoryItem(
        householdId: number,
        inventoryId: number,
        itemId: number,
        requestBody: ReorderItemRequest,
    ): CancelablePromise<InventoryItemResponse> {
        return this.httpRequest.request({
            method: 'PATCH',
            url: '/api/household/{householdId}/inventories/{inventoryId}/items/{itemId}/reorder',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
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
     * @param inventoryId
     * @returns void
     * @throws ApiError
     */
    public compactInventoryItems(
        householdId: number,
        inventoryId: number,
    ): CancelablePromise<void> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/household/{householdId}/inventories/{inventoryId}/items/compact',
            path: {
                'householdId': householdId,
                'inventoryId': inventoryId,
            },
            errors: {
                404: `Not Found`,
            },
        });
    }
}
