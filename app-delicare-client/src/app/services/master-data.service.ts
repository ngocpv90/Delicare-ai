import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { QUESTION_MARK, URL_API } from '../helpers/constant-helper';

// Generic CRUD client for the Master Data controllers (same endpoints on every controller:
// GetAll, GetAllData, GetById, Insert, Update, Delete).
@Injectable({
  providedIn: 'root',
})
export class MasterDataService {
  constructor(private httpClient: HttpClient) {}

  public getAll(controller: string, request: Record<string, any>): Observable<any> {
    const query = Object.keys(request)
      .filter((k) => request[k] !== null && request[k] !== undefined && request[k] !== '')
      .map((k) => `${encodeURIComponent(k)}=${encodeURIComponent(request[k])}`)
      .join('&');
    return this.httpClient.get<any>(`${URL_API}${controller}/GetAll${QUESTION_MARK}${query}`);
  }

  public getAllData(controller: string): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}${controller}/GetAllData`);
  }

  public getById(controller: string, id: any): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}${controller}/GetById${QUESTION_MARK}id=${id}`);
  }

  public insert(controller: string, params: FormData): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}${controller}/Insert`, params);
  }

  public update(controller: string, params: FormData): Observable<any> {
    return this.httpClient.put<any>(`${URL_API}${controller}/Update`, params);
  }

  public delete(controller: string, id: any): Observable<any> {
    return this.httpClient.delete<any>(`${URL_API}${controller}/Delete${QUESTION_MARK}id=${id}`);
  }
}
