import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { URL_API } from '../helpers/constant-helper';

// api/ImportExcel. Save = old flow (TestDemo). T49 / FE-3: GetTemplateList, GetTemplate, Import (master data load).
// Right = Import flag on the target screen of each file type (template.screen, e.g. #/crm-customer).
@Injectable({
  providedIn: 'root',
})
export class ImportExcelService {
  constructor(private httpClient: HttpClient) {}

  public save(params: any): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}ImportExcel/Save`, params);
  }

  // listData = [{ tableName, title, screen, hasTemplate, sheetName, listColumn: [{ header, required, note }] }]
  public getTemplateList(): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}ImportExcel/GetTemplateList`);
  }

  // .xlsx file (blob); 400 = unknown type, 403 = no Import right on the target screen
  public getTemplate(tableName: string): Observable<Blob> {
    const params = new HttpParams().set('tableName', tableName);
    return this.httpClient.get(`${URL_API}ImportExcel/GetTemplate`, { params, responseType: 'blob' });
  }

  // multipart: TableName, File (.xlsx / .xlsm) – data = { totalRows, inserted, updated, skipped, listError [{ row, message }], listNote }
  public import(params: FormData): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}ImportExcel/Import`, params);
  }
}
