import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { URL_API } from '../helpers/constant-helper';

// FE-0 only needs the branch list for the header selector; FE-1 (adm-orgunit) adds CRUD here.
@Injectable({
  providedIn: 'root',
})
export class OrganizationUnitService {
  constructor(private httpClient: HttpClient) {}

  // Units with UnitType = BRANCH ({ listData })
  public getBranches(): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}OrganizationUnit/GetBranches`);
  }
}
