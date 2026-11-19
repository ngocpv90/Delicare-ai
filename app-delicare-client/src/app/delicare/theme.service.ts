import { Injectable } from '@angular/core';

export type AppTheme = 'delicare' | 'w3crm';

const DELICARE_CSS_ID = 'dc-theme';
const DELICARE_CSS_HREF = 'assets/delicare/css/app.css';

/**
 * Toàn bộ ứng dụng dùng theme W3CRM (style.css + custom.css + clinic.css khai báo trong src/index.html).
 * - 'delicare': màn hình nghiệp vụ do engine vẽ – nạp THÊM assets/delicare/css/app.css (lớp bổ sung cho các
 *   thành phần của engine: thẻ số liệu, ngăn kéo, kanban, lưới ca…), KHÔNG tắt CSS của theme.
 * - 'w3crm': trang đăng nhập / trang của template – chỉ CSS của theme.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private current: AppTheme | null = null;

  public use(theme: AppTheme): void {
    if (this.current === theme) {
      return;
    }
    this.current = theme;
    const isDelicare = theme === 'delicare';
    let dc = document.getElementById(DELICARE_CSS_ID) as HTMLLinkElement | null;
    if (!dc && isDelicare) {
      dc = document.createElement('link');
      dc.id = DELICARE_CSS_ID;
      dc.rel = 'stylesheet';
      dc.href = DELICARE_CSS_HREF;
      document.head.appendChild(dc);
    }
    if (dc) {
      dc.disabled = !isDelicare;
    }
    document.body.classList.toggle('dc-body', isDelicare);
  }
}
