import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { URL_API } from '../helpers/constant-helper';

// Dropdown sources of the FE-2 screens: api/<Entity>/GetAllData (not cached – master data changes while
// the user works). Every list gets a `label`; a failed call (e.g. 403 when the user cannot view that
// screen) gives an empty list instead of breaking the screen.
@Injectable({
  providedIn: 'root',
})
export class MasterLookupService {
  constructor(private httpClient: HttpClient) {}

  public list(entity: string, label?: (x: any) => string): Observable<any[]> {
    return this.httpClient.get<any>(`${URL_API}${entity}/GetAllData`).pipe(
      map((res) => (res?.responseCode === 200 ? res.listData ?? [] : [])),
      map((list: any[]) =>
        list
          .map((x) => ({ ...x, label: label ? label(x) : codeName(x.code, x.name) }))
          .sort((a, b) => (a.label ?? '').localeCompare(b.label ?? '', 'vi'))
      ),
      catchError(() => of([]))
    );
  }

  public customers(): Observable<any[]> {
    return this.list('Customer', (x) => codeName(x.code, customerName(x)));
  }

  public suppliers(): Observable<any[]> {
    return this.list('Supplier');
  }

  public ingredients(): Observable<any[]> {
    return this.list('Ingredient');
  }

  public ingredientCategories(): Observable<any[]> {
    return this.list('IngredientCategory');
  }

  public units(): Observable<any[]> {
    return this.list('UnitMeasure', (x) => (x.symbol && x.symbol !== x.name ? `${x.name} (${x.symbol})` : x.name ?? ''));
  }

  public warehouses(): Observable<any[]> {
    return this.list('Warehouse');
  }

  public storageZones(): Observable<any[]> {
    return this.list('StorageZone');
  }

  public contracts(): Observable<any[]> {
    return this.list('Contract', (x) => codeName(x.contractNumber, x.name));
  }

  public companies(): Observable<any[]> {
    return this.list('Company');
  }

  public organizationUnits(): Observable<any[]> {
    return this.list('OrganizationUnit');
  }

  // Branches (OrganizationUnit.UnitType = BRANCH) – same source as the header branch selector
  public branches(): Observable<any[]> {
    return this.httpClient.get<any>(`${URL_API}OrganizationUnit/GetBranches`).pipe(
      map((res) => (res?.responseCode === 200 ? res.listData ?? [] : [])),
      map((list: any[]) => list.map((x) => ({ ...x, label: codeName(x.code, x.name) }))),
      catchError(() => of([]))
    );
  }
}

export const codeName = (code?: string | null, name?: string | null): string =>
  code && name ? `${code} – ${name}` : name || code || '';

// Customer has Name and DisplayName (the SPs show DisplayName)
export const customerName = (x: any): string => x?.displayName || x?.name || '';
