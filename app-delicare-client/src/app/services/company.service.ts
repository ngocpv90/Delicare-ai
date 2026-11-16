import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { URL_API } from '../helpers/constant-helper';

// FE-1 only needs the company dropdown (OrganizationUnit.CompanyId is NOT NULL); adm-company screen later.
@Injectable({
  providedIn: 'root',
})
export class CompanyService {
  constructor(private httpClient: HttpClient) {}

  public getAllData(): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}Company/GetAllData`);
  }
}
