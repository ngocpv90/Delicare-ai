// REAL-1 (6): thực đơn chung 3 tuần (10_ThucDon_tuan-dd-mm.xlsx): uat.ops-plan CreateWeek (khung trống) → ImportWeek (xem trước, đủ khớp mới ghi) → SubmitWeek
// → uat.coo duyệt → uat.ops-plan công bố. SubmitWeek bị chặn mềm (FC vượt / thiếu công thức) → COO vẫn duyệt (quyết định tự chọn REAL-1, cảnh báo giữ trên lưới).
// Chạy lại: tuần đã Duyệt / Công bố → bỏ qua. Dùng: node tools/real1/06-menus.mjs   (REAL1_START = thứ Hai tuần đầu để dời cả bộ)
import fs from 'node:fs';
import { DIR, START, addDays, login, waitApi } from './lib.mjs';

await waitApi();
const plan = await login('uat.ops-plan'), coo = await login('uat.coo');
const files = fs.readdirSync(DIR).filter(f => /^10_ThucDon_tuan-\d\d-\d\d\.xlsx$/.test(f)).sort((a, b) => {
  const k = f => { const [, d, m] = f.match(/(\d\d)-(\d\d)/); return (+m < 6 ? 100 : 0) + +m * 31 + +d; };   // tháng 1–5 sau tháng 11–12
  return k(a) - k(b);
});
// Tuần thứ n (theo thứ tự tệp) = REAL1_START + 7n – mặc định đúng ngày tệp (23/11, 30/11, 07/12/2026); dựng muộn hơn thì dời cả bộ.
for (const [n, f] of files.entries()) {
  const week = addDays(START, 7 * n);
  let w = (await plan.get('/Menu/GetWeek?WeekStart=' + week)).data;
  let menuId = w?.menuId;
  if (!menuId) {
    const cr = await plan.form('/Menu/CreateWeek', { weekStart: week, blank: 'true' });
    menuId = cr.data?.menuId;
    if (!menuId) { console.log(week, 'LỖI tạo', cr.responseMessage); continue; }
    w = (await plan.get('/Menu/GetWeek?WeekStart=' + week)).data;
  }
  if (w.status === 'Draft') {
    const pre = await plan.upload('/Menu/ImportWeek', { menuId, apply: false }, 'file', DIR + f);
    console.log(week, pre.responseMessage);
    const miss = (pre.data?.listCell || []).filter(c => !c.dishId);
    if (miss.length) { miss.forEach(c => console.log('   không khớp', c.mealType, 'LC' + c.optionNo, 'T' + c.day, c.text)); continue; }
    console.log(week, (await plan.upload('/Menu/ImportWeek', { menuId, apply: true }, 'file', DIR + f)).responseMessage);
    const sb = await plan.form('/Menu/SubmitWeek', { Id: menuId });
    if (sb.responseCode != 200) console.log('   gửi duyệt bị chặn:', sb.responseMessage, '→ COO duyệt trực tiếp');
    const ap = await coo.form('/Menu/Approve', { Id: menuId, Note: 'REAL-1: duyệt thực đơn thật đang phục vụ – FC vượt mục tiêu ghi nhận, Kế hoạch – Cost rà soát' });
    console.log(week, 'duyệt →', ap.responseMessage);
  }
  w = (await plan.get('/Menu/GetWeek?WeekStart=' + week)).data;
  if (w.status === 'Approved') console.log(week, 'công bố →', (await plan.form('/Menu/Publish', { Id: menuId })).responseMessage);
  w = (await plan.get('/Menu/GetWeek?WeekStart=' + week)).data;
  console.log(week, w.menuCode, w.statusName, '· FC TB', w.avgFcPct, '% · ô trống', w.emptyCells, '· thiếu công thức', w.missingRecipe, '· suất FC vượt', w.fcOver);
}
