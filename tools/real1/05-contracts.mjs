// REAL-1 (5): 1 hợp đồng / khách đang hoạt động theo 09_HopDong_DonGia: uat.sal-head lập (kèm suất) → gửi → uat.ceo duyệt → Hiệu lực. Chạy lại = 0.
// Dùng: node tools/real1/05-contracts.mjs
import { readXlsx } from './xlsx.mjs';
import { file, login, waitApi } from './lib.mjs';

await waitApi();
const sal = await login('uat.sal-head'), ceo = await login('uat.ceo');
const rows = Object.values(readXlsx(file('09_')))[0].slice(1).filter(r => r[0]);
const byCustomer = new Map();
for (const r of rows) { if (!byCustomer.has(r[0])) byCustomer.set(r[0], []); byCustomer.get(r[0]).push(r); }
const customers = (await sal.get('/Customer/GetAll?Page=1&PageSize=1000')).listData || [];
const contracts = (await sal.get('/Contract/GetAll?Page=1&PageSize=2000')).listData || [];
const NAME = { SANG: 'Suất sáng', TRUA: 'Suất trưa', TOI: 'Suất tối', DEM: 'Suất đêm' };
let made = 0;
for (const [code, lines] of byCustomer) {
  const c = customers.find(x => x.code === code);
  if (!c) { console.log('THIẾU khách', code, '– chạy 01-import trước'); continue; }
  if (contracts.some(k => k.customerId === c.id && !['Cancelled', 'Terminated', 'Expired'].includes(k.status))) { console.log('đã có hợp đồng', code); continue; }
  const [, name, from, to] = lines[0];
  const cr = await sal.json('/Contract/Create', { customerId: c.id, name: `HĐ suất ăn ${name}`, signedDate: from, effectiveFrom: from, effectiveTo: to, paymentTerm: '30 ngày',
    note: 'REAL-1: đơn giá chưa VAT theo giá phổ biến tháng 8/2026, hiệu lực đến 30/06/2027 (giả định)',
    listMeal: lines.map(l => ({ mealType: l[4], mealName: NAME[l[4]], serviceType: 'SUAT', unitPrice: l[5] })) });
  const id = cr.data?.contract?.id;
  if (!id) { console.log('LỖI tạo', code, cr.responseMessage); continue; }
  await sal.form('/Contract/Submit', { Id: id });
  const ap = await ceo.form('/Contract/Approve', { Id: id, Note: 'REAL-1: duyệt hợp đồng' });
  console.log(code, cr.data.contract.contractNumber, '→', ap.responseMessage);
  made++;
}
console.log('tạo', made, 'hợp đồng');
