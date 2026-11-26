/**
 * Phiên bản tài nguyên tĩnh không có mã băm trong tên (engine assets/delicare/js/*.js, assets/delicare/css/app.css).
 * nginx production cho .js / .css cache 30 ngày (deploy/docker/nginx/default.conf.template) → sau mỗi lần deploy trình duyệt có thể chạy
 * engine cũ. Lấy mã băm của main-<HASH>.js do Angular sinh mỗi lần build production làm tham số ?v= → đổi bản là tải lại.
 * Bản dev (main.js không mã băm): không thêm tham số.
 */
let cached: string | null = null;

export function assetVersion(): string {
  if (cached !== null) return cached;
  const main = Array.from(document.querySelectorAll<HTMLScriptElement>('script[src]'))
    .map((s) => s.getAttribute('src') || '')
    .find((src) => /(^|\/)main-[A-Za-z0-9]+\.js$/.test(src));
  const m = main ? main.match(/main-([A-Za-z0-9]+)\.js$/) : null;
  cached = m ? m[1] : '';
  return cached;
}

/** Thêm ?v=<phiên bản> vào đường dẫn tài nguyên (giữ nguyên khi chạy dev). */
export function versioned(url: string): string {
  const v = assetVersion();
  return v ? url + (url.includes('?') ? '&' : '?') + 'v=' + v : url;
}
