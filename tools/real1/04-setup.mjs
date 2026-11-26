// REAL-1 (4): 3 kho thật (Kho khô / mát / đông) + 4 dây chuyền theo nhóm món (2000 suất / mẻ, 60 phút – GIẢ ĐỊNH). Chạy lại = 0.
// Định biên ca 6 tổ bếp: BusinessRules:Hr:ShiftMinStaff trong appsettings.Development.json (máy dev, .gitignore) – xem README.
// Dùng: node tools/real1/04-setup.mjs
import { login, waitApi } from './lib.mjs';

await waitApi();
const admin = await login('admin');
const wh = (await admin.get('/Warehouse/GetAll?Page=1&PageSize=500')).listData || [];
for (const [name, type, note] of [['Kho khô', 'RAW', 'Gạo, gia vị, đồ khô, vật tư'], ['Kho mát', 'COLD', 'Rau củ, thịt tươi, giò chả (0–5 °C)'], ['Kho đông', 'COLD', 'Thịt / hải sản đông lạnh (≤ −18 °C)']]) {
  if (wh.some(w => w.name === name)) { console.log('kho đã có', name); continue; }
  const r = await admin.form('/Warehouse/Insert', { Name: name, WarehouseType: type, Status: 'Active', Note: `REAL-1: ${note}` });
  console.log('kho', name, '→', r.responseMessage);
}
const cats = (await admin.get('/DishCategory/GetAll?Page=1&PageSize=200')).listData || [];
const catId = n => { const c = cats.find(x => x.name === n); if (!c) throw new Error('Thiếu nhóm món ' + n + ' – chạy 01-import trước'); return c.id; };
const lines = (await admin.get('/ProductionLine/GetAll?Page=1&PageSize=200')).listData || [];
for (const [code, name, groups] of [
  ['DC-NAU-CHINH', 'Dây chuyền Nấu món chính', ['Món chính', 'Món cơm']],
  ['DC-RAU-CANH', 'Dây chuyền Rau – Canh', ['Món rau', 'Món canh']],
  ['DC-SANG-DEM', 'Dây chuyền Ăn sáng – Ăn đêm', ['Món ăn sáng', 'Món ăn đêm']],
  ['DC-PHU-TM', 'Dây chuyền Món phụ – Tráng miệng', ['Món phụ', 'Món tráng miệng']]]) {
  const ex = lines.find(l => l.code === code);
  if (ex && groups.every(g => cats.find(c => c.name === g)?.productionLineId === ex.id)) { console.log('dây chuyền đã có', code); continue; }
  const r = await admin.json('/ProductionLine/SaveWithCategories', { Id: ex?.id, Code: code, Name: name, CapacityMealPerBatch: 2000, MinutesPerBatch: 60, Status: 'Active',
    Note: 'REAL-1: công suất giả định 2000 suất/mẻ, 60 phút/mẻ', ListDishCategoryId: groups.map(catId) });
  console.log('dây chuyền', code, '→', r.responseMessage);
}
