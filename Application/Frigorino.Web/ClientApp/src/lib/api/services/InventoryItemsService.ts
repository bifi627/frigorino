/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CreateInventoryItemRequest } from '../models/CreateInventoryItemRequest';
import type { InventoryItemDto } from '../models/InventoryItemDto';
import type { ReorderItemRequest } from '../models/ReorderItemRequest';
import type { UpdateInventoryItemRequest } from '../models/UpdateInventoryItemRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class InventoryItemsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param inventoryId
     * @returns InventoryItemDto OK
     * @throws ApiError
     */
    public getApiInventoryInventoryItems(
        inventoryId: number,
    ): CancelablePromise<Array<InventoryItemDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/inventory/{inventoryId}/InventoryItems',
            path: {
                'inventoryId': inventoryId,
            },
        });
    }
    /**
     * @param inventoryId
     * @param requestBody
     * @returns InventoryItemDto OK
     * @throws ApiError
     */
    public postApiInventoryInventoryItems(
        inventoryId: number,
        requestBody?: CreateInventoryItemRequest,
    ): CancelablePromise<InventoryItemDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/inventory/{inventoryId}/InventoryItems',
            path: {
                'inventoryId': inventoryId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param inventoryId
     * @param inventoryItemId
     * @returns InventoryItemDto OK
     * @throws ApiError
     */
    public getApiInventoryInventoryItems1(
        inventoryId: number,
        inventoryItemId: number,
    ): CancelablePromise<InventoryItemDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/inventory/{inventoryId}/InventoryItems/{inventoryItemId}',
            path: {
                'inventoryId': inventoryId,
                'inventoryItemId': inventoryItemId,
            },
        });
    }
    /**
     * @param inventoryId
     * @param inventoryItemId
     * @param requestBody
     * @returns InventoryItemDto OK
     * @throws ApiError
     */
    public putApiInventoryInventoryItems(
        inventoryId: number,
        inventoryItemId: number,
        requestBody?: UpdateInventoryItemRequest,
    ): CancelablePromise<InventoryItemDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/inventory/{inventoryId}/InventoryItems/{inventoryItemId}',
            path: {
                'inventoryId': inventoryId,
                'inventoryItemId': inventoryItemId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param inventoryId
     * @param inventoryItemId
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiInventoryInventoryItems(
        inventoryId: number,
        inventoryItemId: number,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/inventory/{inventoryId}/InventoryItems/{inventoryItemId}',
            path: {
                'inventoryId': inventoryId,
                'inventoryItemId': inventoryItemId,
            },
        });
    }
    /**
     * @param inventoryId
     * @param inventoryItemId
     * @param householdId
     * @param requestBody
     * @returns InventoryItemDto OK
     * @throws ApiError
     */
    public patchApiInventoryInventoryItemsReorder(
        inventoryId: number,
        inventoryItemId: number,
        householdId?: number,
        requestBody?: ReorderItemRequest,
    ): CancelablePromise<InventoryItemDto> {
        return this.httpRequest.request({
            method: 'PATCH',
            url: '/api/inventory/{inventoryId}/InventoryItems/{inventoryItemId}/reorder',
            path: {
                'inventoryId': inventoryId,
                'inventoryItemId': inventoryItemId,
            },
            query: {
                'householdId': householdId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
