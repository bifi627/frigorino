/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ListItemDto } from '../models/ListItemDto';
import type { ReorderItemRequest } from '../models/ReorderItemRequest';
import type { UpdateListItemRequest } from '../models/UpdateListItemRequest';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class ItemsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @param itemId
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public getApiItems(
        itemId: number,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/Items/{itemId}',
            path: {
                'itemId': itemId,
            },
        });
    }
    /**
     * @param itemId
     * @param requestBody
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public putApiItems(
        itemId: number,
        requestBody?: UpdateListItemRequest,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/Items/{itemId}',
            path: {
                'itemId': itemId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @param itemId
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiItems(
        itemId: number,
    ): CancelablePromise<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/Items/{itemId}',
            path: {
                'itemId': itemId,
            },
        });
    }
    /**
     * @param itemId
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public patchApiItemsToggleStatus(
        itemId: number,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'PATCH',
            url: '/api/Items/{itemId}/toggle-status',
            path: {
                'itemId': itemId,
            },
        });
    }
    /**
     * @param itemId
     * @param requestBody
     * @returns ListItemDto OK
     * @throws ApiError
     */
    public patchApiItemsReorder(
        itemId: number,
        requestBody?: ReorderItemRequest,
    ): CancelablePromise<ListItemDto> {
        return this.httpRequest.request({
            method: 'PATCH',
            url: '/api/Items/{itemId}/reorder',
            path: {
                'itemId': itemId,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
