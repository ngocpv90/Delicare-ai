import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { QUESTION_MARK, URL_API } from '../helpers/constant-helper';

@Injectable({
  providedIn: 'root',
})
export class UserRoleService {
  constructor(private httpClient: HttpClient) {}

  public getByUserGroupId(userGroupId: any): Observable<any> {
    const param = QUESTION_MARK.concat(`userGroupId=${userGroupId}`);
    return this.httpClient.get<any>(
      `${URL_API}UserRole/GetByUserGroupId${param}`
    );
  }

  // form: ListPermission = JSON array of { id, userGroupId, objectMenuId, allowAccess … allowImport }.
  // Rows with id 00000000-… (menu without a UserRole row yet) are created from userGroupId + objectMenuId.
  public update(params: any): Observable<any> {
    return this.httpClient.put<any>(`${URL_API}UserRole/Update`, params);
  }

  // Matrix of one group: data.listMenu = level-1 menus with children (every active menu)
  public getMatrix(userGroupId: any): Observable<any> {
    const param = QUESTION_MARK.concat(`userGroupId=${userGroupId}`);
    return this.httpClient.get<any>(`${URL_API}UserRole/GetMatrix${param}`);
  }

  // form: UserGroupId, AllowAccess – grant / revoke access to every level-2 menu
  public saveAccessAll(params: any): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}UserRole/SaveAccessAll`, params);
  }

  // form: SourceUserGroupId, TargetUserGroupId
  public copyFromUserGroup(params: any): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}UserRole/CopyFromUserGroup`, params);
  }

  // Admin only: data = { userGroupName, screenCount, listMenu, listUser }
  public getPreview(userGroupId: any): Observable<any> {
    const param = QUESTION_MARK.concat(`userGroupId=${userGroupId}`);
    return this.httpClient.get<any>(`${URL_API}UserRole/GetPreview${param}`);
  }

  // Effective permissions of one user (OR of the groups; admin = everything): data.listMenu
  public getByUserId(userId: any): Observable<any> {
    const param = QUESTION_MARK.concat(`userId=${userId}`);
    return this.httpClient.get<any>(`${URL_API}UserRole/GetByUserId${param}`);
  }
}
