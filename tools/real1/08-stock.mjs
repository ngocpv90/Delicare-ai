// REAL-1 (8): tồn đầu kỳ GIẢ ĐỊNH (số cuối kỳ 31/08 của kế toán, 12_TonDauKy) – mỗi mặt hàng 1 lô nhận hôm nay, hạn = hôm nay + số ngày hạn dùng;
// uat.ops-wh điều chỉnh tay vào kho theo kiểu bảo quản (FROZEN → Kho đông, CHILLED → Kho mát, còn lại → Kho khô) → yêu cầu duyệt STOCK_ADJUSTMENT → uat.coo duyệt.
// Máy chủ tự cấp số lô (MA-1.3) → nhận diện lô qua Note "REAL-1 TDK <mã>:". Chạy lại: lô đã có tồn → bỏ qua.
// Dùng: node tools/real1/08-stock.mjs
import { readXlsx } from './xlsx.mjs';
import { addDays, file, login, today, waitApi } from './lib.mjs';

await waitApi();
const admin = await login('admin'), wh = await login('uat.ops-wh'), coo = await login('uat.coo');
const rows = Object.values(readXlsx(file('12_')))[0].slice(1).filter(r => r[0]);
const ingredients = (await wh.get('/Ingredient/GetAll?Page=1&PageSize=5000')).listData || [];
const units = (await admin.get('/UnitMeasure/GetAll?Page=1&PageSize=1000')).listData || [];
const warehouses = (await wh.get('/Warehouse/GetAll?Page=1&PageSize=500')).listData || [];
const W = n => { const w = warehouses.find(x => x.name === n); if (!w) throw new Error('Thiếu ' + n + ' – chạy 04-setup trước'); return w.id; };
let done = 0, skip = 0;
const bad = [];
for (const [code, , uom, qty, price] of rows) {
  const i = ingredients.find(x => x.code === code);
  if (!i) { bad.push(code + ': không có nguyên liệu'); continue; }
  const tag = `REAL-1 TDK ${code}:`;
  const findLot = async () => ((await wh.get('/MaterialLot/GetAll?Page=1&PageSize=100&IngredientId=' + i.id)).listData || []).find(l => (l.note || '').includes(tag));
  let lot = await findLot();
  if (lot && ((await wh.get('/InventoryBalance/GetByLot?ingredientId=' + i.id)).listData || []).some(b => b.materialLotId === lot.id)) { skip++; continue; }
  if (!lot) {
    const li = await wh.form('/MaterialLot/Insert', { IngredientId: i.id, ReceivedDate: today(), ExpiryDate: i.isExpiryManaged ? addDays(today(), i.shelfLifeDays || 30) : undefined,
      QcStatus: 'DAT', UnitCost: price, Note: `${tag} lô tồn đầu kỳ giả định (số cuối kỳ 31/08/2026 của kế toán)` });
    if (li.responseCode != 200) { bad.push(code + ' lô: ' + li.responseMessage); continue; }
    lot = await findLot();
  }
  const warehouseId = i.storageType === 'FROZEN' ? W('Kho đông') : i.storageType === 'CHILLED' ? W('Kho mát') : W('Kho khô');
  const ad = await wh.form('/InventoryTransaction/Adjust', { WarehouseId: warehouseId, IngredientId: i.id, MaterialLotId: lot.id, UnitId: units.find(u => u.code === uom)?.id,
    Quantity: qty, UnitCost: price, Reason: 'REAL-1: tồn đầu kỳ giả định (số cuối kỳ 31/08/2026)' });
  const reqId = ad.data?.approvalRequestId;
  if (!reqId) { bad.push(code + ' điều chỉnh: ' + ad.responseMessage); continue; }
  const de = await coo.form('/ApprovalRequest/Decide', { Id: reqId, Approve: true, Note: 'REAL-1: duyệt tồn đầu kỳ giả định' });
  if (de.responseCode != 200) { bad.push(code + ' duyệt: ' + de.responseMessage); continue; }
  done++;
}
console.log(`tồn đầu kỳ: ghi ${done} · đã có ${skip} · lỗi ${bad.length}`);
bad.forEach(x => console.log('  ', x));
