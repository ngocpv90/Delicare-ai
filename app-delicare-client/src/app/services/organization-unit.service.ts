import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { QUESTION_MARK, URL_API } from '../helpers/constant-helper';

// Organization units (adm-orgunit) + the branch list of the header selector (FE-0)
@Injectable({
  providedIn: 'root',
})
export class OrganizationUnitService {
  constructor(private httpClient: HttpClient) {}

  // Query: searchText, companyId, parentId + paging. Empty filters are not sent (Guid? binding).
  public getAll(request: any): Observable<any> {
    let params = new HttpParams();
    Object.keys(request).forEach((key) => {
      const value = request[key];
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    });
    return this.httpClient.get<any>(`${URL_API}OrganizationUnit/GetAll`, { params });
  }

  public insert(params: any): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}OrganizationUnit/Insert`, params);
  }

  public update(params: any): Observable<any> {
    return this.httpClient.put<any>(`${URL_API}OrganizationUnit/Update`, params);
  }

  public delete(id: any): Observable<any> {
    const param = QUESTION_MARK.concat(`id=${id}`);
    return this.httpClient.delete<any>(`${URL_API}OrganizationUnit/Delete${param}`);
  }

  public getById(id: any): Observable<any> {
    const param = QUESTION_MARK.concat(`id=${id}`);
    return this.httpClient.get<any>(`${URL_API}OrganizationUnit/GetById${param}`);
  }

  public getAllData(): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}OrganizationUnit/GetAllData`);
  }

  // Tree: data = roots [{ id, code, name, unitType, parentId, managerId, managerName, employeeCount,
  // warehouseCount, status, children }]
  public getTree(companyId?: string | null): Observable<any> {
    const param = companyId ? QUESTION_MARK.concat(`companyId=${companyId}`) : '';
    return this.httpClient.get<any>(`${URL_API}OrganizationUnit/GetTree${param}`);
  }

  // Units with UnitType = BRANCH ({ listData })
  public getBranches(): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}OrganizationUnit/GetBranches`);
  }
}
