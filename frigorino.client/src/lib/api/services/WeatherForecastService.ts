/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
 
import type { WeatherForecast } from '../models/WeatherForecast';
import type { CancelablePromise } from '../core/CancelablePromise';
import type { BaseHttpRequest } from '../core/BaseHttpRequest';
export class WeatherForecastService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns WeatherForecast OK
     * @throws ApiError
     */
    public getWeatherForecast(): CancelablePromise<Array<WeatherForecast>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/WeatherForecast',
        });
    }
}
