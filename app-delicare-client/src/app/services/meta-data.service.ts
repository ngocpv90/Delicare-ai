import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { URL_API } from '../helpers/constant-helper';

export interface MetaItem {
  code: string;
  label: string;
}
export interface StatusSet {
  key: string; // "Table.Column", e.g. "Recipe.Status"
  table: string;
  column: string;
  listItem: MetaItem[];
  source?: string;
}
export interface ReferenceList {
  name: string; // MealType, CustomerType, …
  listItem: MetaItem[];
}

// Badge colour per status code. api/MetaData/GetStatuses has labels only (no colour), so the colour is
// derived from the code here – one table for every screen (STATUS_CATALOG codes are PROPOSED).
const STATUS_COLORS: [string, string[]][] = [
  ['green', ['active', 'approved', 'accepted', 'pass', 'passed', 'delivered', 'reconciled', 'matched', 'sufficient', 'available', 'posted', 'received', 'published', 'completed', 'done', 'ready', 'packed', 'success']],
  ['blue', ['confirmed', 'locked', 'sent', 'converted', 'calculated', 'dispatched', 'loaded', 'inprogress', 'inproduction', 'open', 'arrived', 'check_in', 'cooking', 'planned', 'requested']],
  ['orange', ['pending', 'pendingapproval', 'submitted', 'qcpending', 'hold', 'onhold', 'partiallyaccepted', 'partiallyreceived', 'variance', 'shortage', 'waiting', 'review']],
  ['red', ['rejected', 'reject', 'cancelled', 'canceled', 'failed', 'terminated', 'expired', 'damaged', 'overdue', 'error']],
];

// Shared lookups of api/MetaData (status sets, reference lists): fetched once per session, cached.
@Injectable({
  providedIn: 'root',
})
export class MetaDataService {
  private statuses$?: Observable<StatusSet[]>;
  private referenceLists$?: Observable<ReferenceList[]>;

  constructor(private httpClient: HttpClient) {}

  public getStatuses(): Observable<StatusSet[]> {
    if (!this.statuses$) {
      this.statuses$ = this.httpClient.get<any>(`${URL_API}MetaData/GetStatuses`).pipe(
        map((res) => (res?.responseCode === 200 ? (res.listData as StatusSet[]) : [])),
        catchError(() => {
          this.statuses$ = undefined; // retry on next call
          return of([] as StatusSet[]);
        }),
        shareReplay(1)
      );
    }
    return this.statuses$;
  }

  // Items of one status set, key = "Table.Column" (e.g. "Order.Status") – for filters / dropdowns
  public getStatusSet(key: string): Observable<MetaItem[]> {
    return this.getStatuses().pipe(
      map((list) => list.find((x) => x.key?.toLowerCase() === key?.toLowerCase())?.listItem ?? [])
    );
  }

  public getReferenceLists(): Observable<ReferenceList[]> {
    if (!this.referenceLists$) {
      this.referenceLists$ = this.httpClient.get<any>(`${URL_API}MetaData/GetReferenceLists`).pipe(
        map((res) => (res?.responseCode === 200 ? (res.listData as ReferenceList[]) : [])),
        catchError(() => {
          this.referenceLists$ = undefined;
          return of([] as ReferenceList[]);
        }),
        shareReplay(1)
      );
    }
    return this.referenceLists$;
  }

  // Items of one reference list (MealType, CustomerType, SupplierType, …)
  public getReferenceList(name: string): Observable<MetaItem[]> {
    return this.getReferenceLists().pipe(
      map((list) => list.find((x) => x.name?.toLowerCase() === name?.toLowerCase())?.listItem ?? [])
    );
  }

  public statusColor(code?: string | null): string {
    const c = (code ?? '').toLowerCase();
    return STATUS_COLORS.find(([, codes]) => codes.includes(c))?.[0] ?? 'gray';
  }

  // Drop the cache (e.g. after sign-out)
  public clear(): void {
    this.statuses$ = undefined;
    this.referenceLists$ = undefined;
  }
}
