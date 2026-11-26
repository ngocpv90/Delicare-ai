// REAL-1 (7): đăng ký suất tuần (Order/SaveWeek, xác nhận) + dự báo (nhập + công bố) theo 11_SoSuatMau – Sáng chia 3 lựa chọn (LC1 / LC2 / LC3).
// Tuần mặc định 2026-11-30 và 2026-12-07 (đổi: node … 2027-01-04 2027-01-11). Chạy lại: SaveWeek ghi lại cùng số (không đổi); tuần đã có dự báo → bỏ qua dự báo.
// Dùng: node tools/real1/07-orders.mjs [tuần …]
import { readXlsx } from './xlsx.mjs';
import { addDays, file, login, waitApi } from './lib.mjs';

const weeks = process.argv.slice(2).length ? process.argv.slice(2) : ['2026-11-30', '2026-12-07'];
await waitApi();
const sal = await login('uat.sal-head');
const rows = Object.values(readXlsx(file('11_')))[0].slice(1).filter(r => r[0]);
const customers = (await sal.get('/Customer/GetAll?Page=1&PageSize=1000')).listData || [];
const locations = (await sal.get('/CustomerDeliveryLocation/GetAll?Page=1&PageSize=2000')).listData || [];
const contracts = ((await sal.get('/Contract/GetAll?Page=1&PageSize=2000')).listData || []).filter(k => k.status === 'Active');
const meals = {};
for (const week of weeks) {
  const items = [];
  let saved = 0;
  for (const [code, loc, , sang, trua, toi, dem, lc1, lc2, lc3] of rows) {
    const c = customers.find(x => x.code === code), l = locations.find(x => x.code === loc);
    const k = c && contracts.find(x => x.customerId === c.id);
    if (!c || !l || !k) { console.log('THIẾU khách / điểm giao / hợp đồng', code, loc); continue; }
    meals[k.id] ??= (await sal.get('/Contract/GetDetail?id=' + k.id)).data.listMeal;
    const cm = t => meals[k.id].find(m => m.mealType === t);
    const cells = [];
    for (let i = 0; i < 7; i++) {
      const d = addDays(week, i);
      for (const [t, q] of [['SANG', sang], ['TRUA', trua], ['TOI', toi], ['DEM', dem]]) {
        if (!(q > 0) || !cm(t)) continue;
        items.push({ customerId: c.id, deliveryLocationId: l.id, forecastDate: d, mealType: t, forecastQuantity: q });
        if (t === 'SANG') [lc1, lc2, lc3].forEach((x, oi) => x > 0 && cells.push({ mealDate: d, contractMealId: cm(t).id, menuOptionNo: oi + 1, quantity: x }));
        else cells.push({ mealDate: d, contractMealId: cm(t).id, menuOptionNo: 1, quantity: q });
      }
    }
    const sv = await sal.json('/Order/SaveWeek', { customerId: c.id, deliveryLocationId: l.id, weekStart: week, confirm: true, note: 'REAL-1: số suất TB/ngày tháng 8/2026 (giả định)', listCell: cells });
    if (sv.responseCode == 200) saved++; else console.log(week, loc, '→', sv.responseMessage);
  }
  console.log(week, 'đăng ký suất:', saved, 'điểm giao đã lưu + xác nhận');
  const matrix = (await sal.get(`/Forecast/GetMatrix?From=${week}&To=${addDays(week, 6)}`)).data?.rows || [];
  const ids = new Set(items.map(x => x.customerId));
  if (matrix.some(r => ids.has(r.customerId) && Object.keys(r.values || {}).length)) { console.log(week, 'dự báo đã có → bỏ qua'); continue; }
  const fi = await sal.json('/Forecast/Import', { note: `REAL-1: dự báo tuần ${week} theo TB tháng 8`, listItem: items });
  const pb = await sal.form('/Forecast/Publish', { From: week, To: addDays(week, 6) });
  console.log(week, 'dự báo', items.length, 'dòng: nhập →', fi.responseMessage, '| công bố →', pb.responseMessage);
}
