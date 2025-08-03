/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { BaseHttpRequest } from './core/BaseHttpRequest';
import type { OpenAPIConfig } from './core/OpenAPI';
import { FetchHttpRequest } from './core/FetchHttpRequest';
import { AuthService } from './services/AuthService';
import { CurrentHouseholdService } from './services/CurrentHouseholdService';
import { DemoService } from './services/DemoService';
import { HouseholdService } from './services/HouseholdService';
import { ItemsService } from './services/ItemsService';
import { ListItemsService } from './services/ListItemsService';
import { ListsService } from './services/ListsService';
import { MembersService } from './services/MembersService';
import { WeatherForecastService } from './services/WeatherForecastService';
type HttpRequestConstructor = new (config: OpenAPIConfig) => BaseHttpRequest;
export class FrigorinoApiClient {
    public readonly auth: AuthService;
    public readonly currentHousehold: CurrentHouseholdService;
    public readonly demo: DemoService;
    public readonly household: HouseholdService;
    public readonly items: ItemsService;
    public readonly listItems: ListItemsService;
    public readonly lists: ListsService;
    public readonly members: MembersService;
    public readonly weatherForecast: WeatherForecastService;
    public readonly request: BaseHttpRequest;
    constructor(config?: Partial<OpenAPIConfig>, HttpRequest: HttpRequestConstructor = FetchHttpRequest) {
        this.request = new HttpRequest({
            BASE: config?.BASE ?? '',
            VERSION: config?.VERSION ?? '1.0',
            WITH_CREDENTIALS: config?.WITH_CREDENTIALS ?? false,
            CREDENTIALS: config?.CREDENTIALS ?? 'include',
            TOKEN: config?.TOKEN,
            USERNAME: config?.USERNAME,
            PASSWORD: config?.PASSWORD,
            HEADERS: config?.HEADERS,
            ENCODE_PATH: config?.ENCODE_PATH,
        });
        this.auth = new AuthService(this.request);
        this.currentHousehold = new CurrentHouseholdService(this.request);
        this.demo = new DemoService(this.request);
        this.household = new HouseholdService(this.request);
        this.items = new ItemsService(this.request);
        this.listItems = new ListItemsService(this.request);
        this.lists = new ListsService(this.request);
        this.members = new MembersService(this.request);
        this.weatherForecast = new WeatherForecastService(this.request);
    }
}

