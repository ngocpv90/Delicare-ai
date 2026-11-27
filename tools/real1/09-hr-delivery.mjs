// REAL-1 bổ sung (anh yêu cầu 2026-11-26): nạp NHÂN SỰ + ĐỘI GIAO VẬN thật. Tệp nguồn trong repo: DB/data/real1/HR, DB/data/real1/Delivery
// (anh duyệt đưa vào repo 2026-11-26 như D-130 – có họ tên, SĐT, địa chỉ: KHÔNG chia sẻ repo ra ngoài). Phần chưa nạp: docs/claude/GAP_REAL1_HR_DELIVERY.md.
//   HR/DSNS_*.xlsx (mới nhất)      → Chức vụ (Position, mã CV-…) + Nhân viên (ImportExcel Employee: mã NV, họ tên, đơn vị, chức vụ, SĐT)
//   Delivery/Đội Giao vận.xlsx     → SĐT + địa chỉ lái xe / phụ xe (khớp theo họ tên) + 9 xe (Vehicle: biển số, loại, tải trọng kg, ghi chú BH TNDS / tuyến / lái xe, lái xe mặc định)
// Chạy lại = 0 việc (khớp theo mã chức vụ / mã NV / biển số). Không xóa gì.
//
//   DC_USER=admin DC_PASS=… node tools/real1/09-hr-delivery.mjs          → chạy thử: in kế hoạch, không ghi
//   DC_USER=admin DC_PASS=… node tools/real1/09-hr-delivery.mjs --yes    → ghi
//   REAL1_SHARE = thư mục chứa HR/ và Delivery/ (mặc định DB/data/real1 trong repo; có tệp mới hơn thì trỏ tới Delicare-Share)
import fs from 'node:fs';
import path from 'node:path';
import { API, DIR, WORK, login, need, ok, waitApi } from './lib.mjs';
import { readXlsx } from './xlsx.mjs';
import { writeXlsx } from './xlsxw.mjs';

const apply = process.argv.includes('--yes');
const SHARE = (process.env.REAL1_SHARE || DIR).replace(/[\\/]+$/, '');
need('DC_USER'); need('DC_PASS');

const norm = s => String(s ?? '').normalize('NFC').replace(/\s+/g, ' ').trim();
const key = s => norm(s).toLowerCase();
const ascii = s => norm(s).normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');
const slug = s => ascii(s).toUpperCase().replace(/[^A-Z0-9]+/g, '-').replace(/^-|-$/g, '');
const phone = v => { const d = String(v ?? '').replace(/\D/g, ''); return d.length === 9 ? '0' + d : d.length === 10 ? d : null; };
const excelDate = v => typeof v === 'number' ? new Date(Date.UTC(1899, 11, 30) + v * 864e5).toISOString().slice(0, 10).split('-').reverse().join('/') : norm(v);

// ---------------------------------------------------------------- đọc tệp
const hrDir = path.join(SHARE, 'HR');
const hrFile = fs.readdirSync(hrDir).filter(f => /^DSNS_.*\.xlsx$/i.test(f)).sort().pop();
if (!hrFile) throw new Error('Không thấy HR/DSNS_*.xlsx trong ' + SHARE);
const hrRows = Object.values(readXlsx(path.join(hrDir, hrFile)))[0];
const people = [];
for (const r of hrRows.slice(2)) {
  const [, code, name, pos, form, blk, dept] = r;
  if (!norm(name) || (code == null && !pos && !dept)) continue; // dòng trống / tiêu đề khối
  people.push({ code: code == null ? null : norm(code), name: norm(name), pos: norm(pos) || null, form: norm(form) || null, blk: norm(blk) || null, dept: norm(dept) || null });
}

// Bộ phận / tổ trong tệp → đơn vị (OrganizationUnit.Code – cơ cấu ORG_STRUCTURE.md). Chức vụ đặc thù ghi đè.
const DEPT = {
  'tổ chia suất': 'OPS-KIT-PORT', 'giao vận': 'OPS-LOG', 'bếp': 'OPS-KIT-COOK', 'tổ sơ chế': 'OPS-KIT-PREP', 'tổ tạp vụ': 'OPS-KIT-CLEAN', 'kế toán': 'FIN-ACC',
  'hcns': 'FIN-HR', 'kho': 'OPS-SCM', 'thu mua': 'OPS-SCM', 'trợ lý': 'CEO-PMO', 'marketing': 'SAL-DEV', 'kỹ thật': 'OPS-MNT', 'kỹ thuật': 'OPS-MNT',
  'pháp chế': 'FIN', 'vận hành': 'OPS',
};
function unitOf(p) {
  const pos = key(p.pos);
  if (p.code === 'CEO' || pos === 'giám đốc') return 'CEO';
  if (pos === 'quản lý bếp') return 'OPS-KIT';
  if (/nấu cơm|chia canh/.test(pos)) return 'OPS-KIT-RICE';
  return DEPT[key(p.dept)] || 'DLC';
}
// Người không có mã NV trong tệp → mã cố định theo tổ + họ tên (chạy lại không sinh mã mới).
for (const p of people) {
  if (!p.code) p.code = `NV-${slug(unitOf(p)).split('-').pop()}-${slug(p.name).replace(/-/g, '')}`.slice(0, 40);
}
const dupCode = Object.entries(people.reduce((o, p) => ({ ...o, [p.code]: (o[p.code] || 0) + 1 }), {})).filter(([, n]) => n > 1);
if (dupCode.length) throw new Error('Trùng mã NV trong tệp: ' + dupCode.map(([c]) => c).join(', '));

// Đội giao vận: xe + người (SĐT, địa chỉ).
const dvRows = Object.values(readXlsx(path.join(SHARE, 'Delivery', 'Đội Giao vận.xlsx')))[0];
const vehicles = [], crew = [];
let part = '';
for (const r of dvRows) {
  const c1 = norm(r[1]);
  if (/^Danh sách xe$/i.test(c1)) { part = 'xe'; continue; }
  if (/^Danh sách lái xe$/i.test(c1)) { part = 'nguoi'; continue; }
  if (/^(SÁNG|STT|Họ tên|Biển số)$/i.test(c1) || /^STT$/i.test(norm(r[0]))) { if (/^SÁNG$/i.test(c1)) part = ''; continue; }
  if (part === 'xe' && /^\d{2}[A-Z]/.test(c1)) {
    vehicles.push({ plate: c1.replace(/\s*-\s*/, '-').replace(/\s+/g, ''), drivers: norm(r[2]) || null, insurance: r[3] == null ? null : excelDate(r[3]), route: norm(r[4]) || null,
      type: norm(r[5]) || null, capacity: parseFloat(String(r[6] ?? '').replace(/[^\d.]/g, '')) || null, brand: norm(r[7]) || null });
  } else if (part === 'nguoi' && norm(r[1]) && !/^(LÁI XE|PHỤ XE)$/i.test(c1)) {
    crew.push({ name: norm(r[1]), phone: phone(r[3]), address: norm(r[4]) || null, role: norm(r[5]) || null, form: norm(r[6]) || null, note: norm(r[7]) || null });
  }
}
const byName = new Map(people.map(p => [key(p.name), p]));
const crewMissing = crew.filter(c => !byName.has(key(c.name)));
for (const c of crew) { const p = byName.get(key(c.name)); if (p) { p.phone = c.phone; p.address = c.address; } }

// ---------------------------------------------------------------- kế hoạch
const positions = [...new Set(people.map(p => p.pos).filter(Boolean))].map(name => ({ code: ('CV-' + slug(name)).slice(0, 50), name }));
console.log(`Nguồn: ${hrFile} – ${people.length} nhân viên, ${positions.length} chức vụ; Đội Giao vận – ${vehicles.length} xe, ${crew.length} người (khớp ${crew.length - crewMissing.length}).`);
if (crewMissing.length) console.log('   Người trong tệp Giao vận KHÔNG có trong danh sách nhân sự (bỏ qua):', crewMissing.map(c => c.name).join(', '));
const units = people.reduce((o, p) => ({ ...o, [unitOf(p)]: (o[unitOf(p)] || 0) + 1 }), {});
console.log('   Đơn vị:', Object.entries(units).map(([k, v]) => `${k} ${v}`).join(' · '));
const forms = people.reduce((o, p) => ({ ...o, [p.form || '–']: (o[p.form || '–'] || 0) + 1 }), {});
console.log('   Hình thức làm việc (không có cột lưu – chỉ báo):', Object.entries(forms).map(([k, v]) => `${k} ${v}`).join(' · '));
if (!apply) { console.log('CHẠY THỬ – thêm --yes để ghi.'); process.exit(0); }

await waitApi();
const admin = await login('admin');
const tok = (await (await fetch(API + '/Authentication/Login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: process.env.DC_USER, passWord: process.env.DC_PASS }) })).json());
const bearer = 'Bearer ' + (typeof tok?.data === 'string' ? tok.data : tok?.data?.token || tok?.data?.accessToken || tok?.token);
const put = async (u, o) => { const r = await fetch(API + u, { method: 'PUT', headers: { Authorization: bearer, 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams(Object.fromEntries(Object.entries(o).filter(([, v]) => v !== undefined && v !== null).map(([k, v]) => [k, String(v)]))) }); const t = await r.text(); try { return JSON.parse(t); } catch { return { responseCode: r.status, responseMessage: t.slice(0, 200) }; } };
const list = r => r?.listData || r?.data || [];

// 1) Chức vụ
const havePos = new Map(list(await admin.get('/Position/GetAllData')).filter(p => !p.isDeleted).map(p => [p.code, p]));
let posAdded = 0;
for (const p of positions) {
  if (havePos.has(p.code)) continue;
  const r = await admin.form('/Position/Insert', { Code: p.code, Name: p.name, Status: 'Active', Note: '[REAL-1] Chức vụ theo danh sách nhân sự ' + hrFile });
  if (ok(r)) posAdded++; else console.log('   Lỗi chức vụ', p.name, r.responseMessage);
}
console.log(`1) Chức vụ: thêm ${posAdded}, đã có ${positions.length - posAdded}.`);

// 2) Nhân viên – ImportExcel (khớp theo mã NV; SĐT kiểm trùng như form)
const posCode = new Map(positions.map(p => [p.name, p.code]));
const xlsx = path.join(WORK, 'nhan-vien.xlsx');
writeXlsx(xlsx, 'Nhân viên', [
  ['Mã nhân viên', 'Họ tên', 'Mã đơn vị', 'Mã chức vụ', 'Điện thoại', 'Email', 'Ngày vào làm'],
  ...people.map(p => [p.code, p.name, unitOf(p), p.pos ? posCode.get(p.pos) : '', p.phone || '', '', '']),
]);
const imp = await admin.upload('/ImportExcel/Import', { TableName: 'Employee' }, 'File', xlsx);
const d = imp.data || {};
console.log(`2) Nhân viên: thêm ${d.inserted ?? '-'} · sửa ${d.updated ?? '-'} · bỏ qua ${d.skipped ?? '-'} · lỗi ${(d.listError || []).length} – ${imp.responseMessage ?? ''}`);
(d.listError || []).slice(0, 10).forEach(e => console.log('   dòng', e.row ?? e.rowNumber ?? '', e.message));

// 3) Địa chỉ đội giao vận (form Employee/Update – giữ nguyên các trường khác)
const emps = list(await admin.get('/Employee/GetAllData')).filter(e => !e.isDeleted);
const empByCode = new Map(emps.map(e => [e.code || e.employeeCode, e]));
let addr = 0;
for (const p of people.filter(p => p.address)) {
  const e = empByCode.get(p.code);
  if (!e || norm(e.address) === p.address) continue;
  const r = await put('/Employee/Update', { Id: e.id, Code: e.code, FullName: e.fullName, OrganizationUnitId: e.organizationUnitId, PositionId: e.positionId, Phone: e.phone, Email: e.email,
    Address: p.address, Gender: e.gender, DateOfBirth: e.dateOfBirth, HireDate: e.hireDate, Status: e.status, CompanyId: e.companyId });
  if (ok(r)) addr++; else console.log('   Lỗi địa chỉ', p.name, r.responseMessage);
}
console.log(`3) Địa chỉ lái xe / phụ xe: cập nhật ${addr}.`);

// 4) Xe – khớp theo biển số; lái xe mặc định khi cột lái xe chỉ nêu đúng 1 người khớp duy nhất (theo tên gọi) trong đội lái xe.
const drivers = people.filter(p => unitOf(p) === 'OPS-LOG' && /lái xe/i.test(p.pos || ''));
const given = n => ascii(n).toLowerCase().split(' ').pop();
function defaultDriver(text) {
  const names = (text || '').split(/[,+]/).map(s => ascii(s).toLowerCase().replace(/\b(sang|trua|chieu|toi|dem|ca)\b/g, '').replace(/[^a-z ]/g, ' ').trim().split(/\s+/)[0]).filter(Boolean);
  const uniq = [...new Set(names)];
  if (uniq.length !== 1) return null;
  const hit = drivers.filter(p => given(p.name) === uniq[0]);
  return hit.length === 1 ? empByCode.get(hit[0].code) : null;
}
const haveVeh = list(await admin.get('/Vehicle/GetAllData')).filter(v => !v.isDeleted);
let vAdd = 0, vUpd = 0;
for (const v of vehicles) {
  const code = 'XE-' + v.plate.replace(/[^A-Z0-9]/gi, '').toUpperCase();
  const note = ['[REAL-1]', v.brand && 'Hãng ' + v.brand, v.insurance && 'BH TNDS đến ' + v.insurance, v.route && 'Tuyến: ' + v.route, v.drivers && 'Lái xe: ' + v.drivers].filter(Boolean).join(' · ');
  const drv = defaultDriver(v.drivers);
  const body = { Code: code, LicensePlate: v.plate, VehicleType: v.type || (/xe riêng/i.test(v.route || '') ? 'Xe riêng' : null), Capacity: v.capacity, Status: 'Active', Note: note, DefaultDriverEmployeeId: drv?.id };
  const old = haveVeh.find(x => (x.licensePlate || '').replace(/[^A-Z0-9]/gi, '').toUpperCase() === code.slice(3) || x.code === code);
  if (!old) { const r = await admin.form('/Vehicle/Insert', body); if (ok(r)) vAdd++; else console.log('   Lỗi xe', v.plate, r.responseMessage); }
  else if (old.note !== note || (old.defaultDriverEmployeeId || null) !== (drv?.id || null) || Number(old.capacity || 0) !== Number(v.capacity || 0)) {
    const r = await put('/Vehicle/Update', { Id: old.id, ...body }); if (ok(r)) vUpd++; else console.log('   Lỗi xe', v.plate, r.responseMessage);
  }
}
console.log(`4) Xe: thêm ${vAdd}, cập nhật ${vUpd}, tổng ${vehicles.length}.`);
