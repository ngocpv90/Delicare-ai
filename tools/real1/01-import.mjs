// REAL-1 (2): nạp 01 → 08 bằng ImportExcel (admin), mã giữ nguyên theo tệp. Chạy lại = 0 thêm / 0 sửa (05: công thức đã khóa → từ chối đúng thiết kế).
// 05_DinhMucCongThuc: tệp sinh bởi normalize.py BẢN CŨ ghi lượng TRƯỚC sơ chế (gộp) – hệ thống coi "Định lượng" là lượng TINH (BIZ-1.1)
// → mặc định đổi gộp × (1 − %SC) × (1 − %CB) ra tệp tạm rồi nạp. Tệp 05 sinh lại bằng normalize.py đã sửa (đã là lượng tinh) → đặt REAL1_05_GROSS=0.
// Dùng: node tools/real1/01-import.mjs [01 02 … 08]
import path from 'node:path';
import { readXlsx } from './xlsx.mjs';
import { writeXlsx } from './xlsxw.mjs';
import { WORK, file, login, waitApi } from './lib.mjs';

const MAP = [['01', 'UnitMeasure'], ['02', 'Supplier'], ['03', 'Ingredient'], ['04', 'Dish'], ['05', 'RecipeLine'], ['06', 'SupplierMaterial'], ['07', 'Customer'], ['08', 'CustomerDeliveryLocation']];
const only = process.argv.slice(2);

function netRecipeFile(src) {
  const rows = readXlsx(src).DuLieu;
  const p = v => v === null || v === undefined || v === '' ? 0 : typeof v === 'number' ? (v <= 1 ? v : v / 100) : (parseFloat(String(v).replace('%', '').replace(',', '.')) || 0) / 100;
  const out = [rows[0]];
  for (const r of rows.slice(1)) {
    if (!r[0]) continue;
    out.push([r[0], r[1], Math.round(r[2] * (1 - p(r[4])) * (1 - p(r[5])) * 10000) / 10000, r[3], r[4], r[5], r[6]]);
  }
  const dst = path.join(WORK, '05_DinhMucCongThuc_tinh.xlsx');
  writeXlsx(dst, 'DuLieu', out);
  return dst;
}

await waitApi();
const admin = await login('admin');
for (const [n, table] of MAP) {
  if (only.length && !only.includes(n)) continue;
  let src = file(n + '_');
  if (n === '05' && process.env.REAL1_05_GROSS !== '0') src = netRecipeFile(src);
  const r = await admin.upload('/ImportExcel/Import', { TableName: table }, 'File', src);
  const d = r.data || {};
  const errs = d.listError || [];
  console.log(`${n} ${table}: thêm ${d.inserted ?? '-'} · sửa ${d.updated ?? '-'} · bỏ qua ${d.skipped ?? '-'} · lỗi ${errs.length} – ${r.responseMessage ?? ''}`);
  [...new Set(errs.map(e => e.message))].slice(0, 5).forEach(m => console.log('   ', m));
}
