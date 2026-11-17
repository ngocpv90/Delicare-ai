import { Injectable } from '@angular/core';

export type AppTheme = 'delicare' | 'w3crm';

const DELICARE_CSS_ID = 'dc-theme';
const DELICARE_CSS_HREF = 'assets/delicare/css/app.css';
// Stylesheet của template W3CRM (khai báo trong src/index.html) – xung đột class với giao diện DELICARE.
const W3CRM_CSS_IDS = ['w3crm-theme', 'w3crm-custom'];

/**
 * Bật / tắt bộ CSS theo khu vực:
 * - 'delicare': giao diện theo mockup đã duyệt (docs/mockup) – đăng nhập + toàn bộ màn hình nghiệp vụ.
 * - 'w3crm': trang demo cũ của template (/admin/…).
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
    W3CRM_CSS_IDS.forEach((id) => {
      const link = document.getElementById(id) as HTMLLinkElement | null;
      if (link) {
        link.disabled = isDelicare;
      }
    });
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
