import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { QUESTION_MARK, URL_API } from '../helpers/constant-helper';

// api/Recipe (FE-3). Insert / Update take a FormData with every RecipeRequest field (Update overwrites).
@Injectable({
  providedIn: 'root',
})
export class RecipeService {
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
    return this.httpClient.get<any>(`${URL_API}Recipe/GetAll`, { params });
  }

  public insert(params: any): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}Recipe/Insert`, params);
  }

  public update(params: any): Observable<any> {
    return this.httpClient.put<any>(`${URL_API}Recipe/Update`, params);
  }

  public delete(id: any): Observable<any> {
    const param = QUESTION_MARK.concat(`id=${id}`);
    return this.httpClient.delete<any>(`${URL_API}Recipe/Delete${param}`);
  }

  public getById(id: any): Observable<any> {
    const param = QUESTION_MARK.concat(`id=${id}`);
    return this.httpClient.get<any>(`${URL_API}Recipe/GetById${param}`);
  }

  public getAllData(): Observable<any> {
    return this.httpClient.get<any>(`${URL_API}Recipe/GetAllData`);
  }

  // Versions of a dish (newest first): listData = [{ recipe, listLine, costPerMeal, costComplete }]
  public getByDishId(dishId: any): Observable<any> {
    const param = QUESTION_MARK.concat(`dishId=${dishId}`);
    return this.httpClient.get<any>(`${URL_API}Recipe/GetByDishId${param}`);
  }

  // form: dishId, copyFromRecipeId (optional) -> new Draft version (data = Recipe)
  public createVersion(params: FormData): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}Recipe/CreateVersion`, params);
  }

  // form: id – Draft -> PendingApproval (needs >= 1 line), creates ApprovalRequest RECIPE_VERSION
  public submit(params: FormData): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}Recipe/Submit`, params);
  }

  // form: id, note – Approve right = AllowEdit on #/wf-inbox?type=menu-recipe (D-31)
  public approve(params: FormData): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}Recipe/Approve`, params);
  }

  // form: id, note (required) – back to Draft
  public reject(params: FormData): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}Recipe/Reject`, params);
  }

  // form: id, effectiveFrom (YYYY-MM-DD, empty = today) – Approved -> Locked, old version Obsolete, new StandardCost
  public lock(params: FormData): Observable<any> {
    return this.httpClient.post<any>(`${URL_API}Recipe/Lock`, params);
  }

  // Material cost for mealCount meals at date `on` (preferred supplier prices) – data = RecipeCostModel
  public calculateCost(id: any, mealCount: number, on?: string | null): Observable<any> {
    let params = new HttpParams().set('id', id).set('mealCount', String(mealCount));
    if (on) {
      params = params.set('on', on);
    }
    return this.httpClient.get<any>(`${URL_API}Recipe/CalculateCost`, { params });
  }
}
