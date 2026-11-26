// REAL-1 (3): công thức Nháp của món MON-xxxx → gửi duyệt (admin) → uat.coo duyệt (duyệt = khóa, tính giá thành chuẩn). Chạy lại: không còn bản Nháp → 0.
// Dùng: node tools/real1/03-recipes.mjs
import { login, ok, today, waitApi } from './lib.mjs';

await waitApi();
const admin = await login('admin'), coo = await login('uat.coo');
const dishes = ((await admin.get('/Dish/GetAll?Page=1&PageSize=5000')).listData || []).filter(d => /^MON-\d{4}$/.test(d.code || ''));
let sent = 0, approved = 0, noRecipe = 0;
const bad = [];
for (const d of dishes) {
  const recipes = ((await admin.get('/Recipe/GetByDishId?dishId=' + d.id)).listData || []).map(x => x.recipe || x);
  if (recipes.length === 0) { noRecipe++; continue; }
  for (const r of recipes.filter(x => x.status === 'Draft' || x.status === 'PendingApproval')) {
    if (r.status === 'Draft') {
      const s = await admin.form('/Recipe/Submit', { id: r.id, effectiveFrom: today() });
      if (!ok(s)) { bad.push(`${d.code} gửi: ${s.responseMessage}`); continue; }
      sent++;
    }
    const a = await coo.form('/Recipe/Approve', { Id: r.id, Note: 'REAL-1: duyệt công thức chuẩn hóa' });
    if (!ok(a)) { bad.push(`${d.code} duyệt: ${a.responseMessage}`); continue; }
    approved++;
  }
}
console.log(`món MON: ${dishes.length} · gửi ${sent} · duyệt ${approved} · chưa có công thức ${noRecipe} · lỗi ${bad.length}`);
bad.slice(0, 10).forEach(x => console.log('  ', x));
