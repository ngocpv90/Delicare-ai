import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { PortalData } from './portal.model';

/**
 * Nội dung trang khách lẻ. Chưa có API bán lẻ (GAP-POS-01, R2) nên đọc file cấu hình tĩnh
 * assets/portal/portal-data.json – sửa file là trang đổi theo, không cần build lại.
 */
@Injectable({ providedIn: 'root' })
export class PortalDataService {
  private readonly http = inject(HttpClient);

  public readonly data$: Observable<PortalData> = this.http
    .get<PortalData>('assets/portal/portal-data.json')
    .pipe(shareReplay(1));
}
