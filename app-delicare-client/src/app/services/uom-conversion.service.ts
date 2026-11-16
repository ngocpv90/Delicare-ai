import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { QUESTION_MARK, URL_API } from '../helpers/constant-helper';

// api/UomConversion (FE-2). Insert / Update take a FormData with every UomConversionRequest field (Update overwrites).
@Injectable({
  providedIn: 'root',
})
export class UomConversionService {
  constructor(private httpClient: HttpClient) {}

  // Query: page, pageSize, sortField, sortOrder, searchText + column filters. Empty filters are not sent.
  public getAll(request: any): Observable<any> {
    let params = new HttpParams();
    Object.keys(request).forEach((key) => {
      const value = request[key];
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    });
    return this.httpClient.get<any>(`${URL_API}UomConversion/GetAll`, { params });
  }

  public insert(params: any): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}UomConversion/Insert`, params);
  }

  public update(params: any): Observable<any> {
    return this.httpClient.put<any>(`${URL_API}UomConversion/Update`, params);
  }

  public delete(id: any): Observable<any> {
    const param = QUESTION_MARK.concat(`id=${id}`);
    return this.httpClient.delete<any>(`${URL_API}UomConversion/Delete${param}`);
  }

  public getById(id: any): Observable<any> {
    const param = QUESTION_MARK.concat(`id=${id}`);
    return this.httpClient.get<any>(`${URL_API}UomConversion/GetById${param}`);
  }

  public getAllData(): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}UomConversion/GetAllData`);
  }

  // Quy đổi số lượng: { quantity, fromUnitId, toUnitId, on? } -> data = số lượng sau quy đổi (400 khi chưa có quy đổi)
  public convert(request: { quantity: number; fromUnitId: string; toUnitId: string; on?: string }): Observable<any> {
    let params = new HttpParams()
      .set('quantity', String(request.quantity))
      .set('fromUnitId', request.fromUnitId)
      .set('toUnitId', request.toUnitId);
    if (request.on) {
      params = params.set('on', request.on);
    }
    return this.httpClient.get<any>(`${URL_API}UomConversion/Convert`, { params });
  }
}
