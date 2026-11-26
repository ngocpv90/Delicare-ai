import { PortalMenuDay } from './portal.model';

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

/**
 * Vị trí ngày "hôm nay" trong thực đơn tuần.
 * - Thực đơn từ máy chủ (có ngày): so đúng ngày.
 * - Thực đơn trong file (chỉ có thứ, Thứ Hai → Thứ Bảy): theo thứ trong tuần; Chủ nhật → -1.
 */
export function todayIndex(days: PortalMenuDay[]): number {
  if (days.some((d) => d.date)) return days.findIndex((d) => d.date === iso(new Date()));
  const dow = new Date().getDay(); // 0 = Chủ nhật
  return dow >= 1 && dow <= days.length ? dow - 1 : -1;
}

/** Ngày hiển thị ở trang chủ: hôm nay nếu có, không thì ngày gần nhất sắp tới (vd Chủ nhật → Thứ Hai). */
export function featuredDay(days: PortalMenuDay[]): { day: PortalMenuDay; isToday: boolean } | undefined {
  if (!days.length) return undefined;
  const i = todayIndex(days);
  if (i >= 0) return { day: days[i], isToday: true };
  const today = iso(new Date());
  const next = days.find((d) => d.date && d.date > today);
  return { day: next ?? days[0], isToday: false };
}
