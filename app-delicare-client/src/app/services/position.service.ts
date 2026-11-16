import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { QUESTION_MARK, URL_API } from '../helpers/constant-helper';

@Injectable({
  providedIn: 'root',
})
export class PositionService {
  constructor(private httpClient: HttpClient) {}

  // Query: page, pageSize, sortField, sortOrder, searchText + column filters. Empty filters are not sent
  // (Guid? / DateTime? binding).
  public getAll(request: any): Observable<any> {
    let params = new HttpParams();
    Object.keys(request).forEach((key) => {
      const value = request[key];
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    });
    return this.httpClient.get<any>(`${URL_API}Position/GetAll`, { params });
  }

  public insert(params: any): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}Position/Insert`, params);
  }

  public update(params: any): Observable<any> {
    return this.httpClient.put<any>(`${URL_API}Position/Update`, params);
  }

  public delete(id: any): Observable<any> {
    const param = QUESTION_MARK.concat(`id=${id}`);
    return this.httpClient.delete<any>(`${URL_API}Position/Delete${param}`);
  }

  public getById(id: any): Observable<any> {
    const param = QUESTION_MARK.concat(`id=${id}`);
    return this.httpClient.get<any>(`${URL_API}Position/GetById${param}`);
  }

  public getAllData(): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}Position/GetAllData`);
  }
}
