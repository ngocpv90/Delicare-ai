#!/usr/bin/env node
// DELICARE – seeder dữ liệu thử [DEMO] cho máy dev FE / server test.
// Tạo dữ liệu bằng cách CHẠY LẠI toàn luồng nghiệp vụ R1 qua API (docs/api-tests/full-flow.http – 217 request): danh mục → hợp đồng → dự báo →
// thực đơn → đơn hàng → mua hàng (duyệt 2 cấp, PO Giám đốc) → nhận hàng + QC → kho → kế hoạch → sản xuất → đóng gói → chuyến xe → POD →
// thu hồi hộp → vệ sinh → đối soát → giá thành → in / xuất → nhân sự. Số chứng từ, tồn kho, giá thành… do API tự tính (đúng nghiệp vụ).
// Không chèn SQL trực tiếp, không xóa dữ liệu. Mỗi lần chạy = 1 bộ mới (mã DEMO<key>, ngày ăn chưa có kế hoạch sản xuất).
//
// Dùng:  node tools/seed-demo/seed-demo.mjs --base http://localhost:5125 --password <mật khẩu admin> [--rounds 2] [--key S7K2] [--day 2026-12-02]
//        (mật khẩu có thể đặt ở biến môi trường DELICARE_ADMIN_PASSWORD thay cho --password)
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';
import { runHttpFile } from './http-runner.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const args = {};
for (let i = 2; i < process.argv.length; i++) {
  const a = process.argv[i];
  if (a.startsWith('--')) { const [k, v] = a.slice(2).split('='); args[k] = v ?? process.argv[++i]; }
}
const base = (args.base || 'http://localhost:5125').replace(/\/+$/, '');
const user = args.user || 'admin';
const password = args.password || process.env.DELICARE_ADMIN_PASSWORD;
const rounds = Math.max(1, Number(args.rounds || 1));
const file = path.resolve(args.file || path.join(repo, 'docs', 'api-tests', 'full-flow.http'));
const outDir = path.resolve(args.out || path.join(here, 'out'));
if (!password) { console.error('Thiếu mật khẩu admin: --password <…> hoặc biến môi trường DELICARE_ADMIN_PASSWORD'); process.exit(2); }

// Giờ Việt Nam (UTC+7) – không phụ thuộc múi giờ của máy chạy.
const vnToday = () => new Date(Date.now() + 7 * 3600e3).toISOString().slice(0, 10);
const addDays = (d, n) => new Date(Date.parse(d + 'T00:00:00Z') + n * 864e5).toISOString().slice(0, 10);
const dowOf = d => { const w = new Date(d + 'T00:00:00Z').getUTCDay(); return w === 0 ? 7 : w; }; // 1 = Thứ 2 … 7 = Chủ nhật

async function login() {
  const r = await fetch(base + '/api/Authentication/Login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: user, passWord: password }) });
  const j = await r.json().catch(() => ({}));
  const token = j.data?.token ?? j.data?.accessToken;
  if (!token) throw new Error(`Đăng nhập ${user} thất bại (${r.status} ${j.responseMessage ?? ''}) – kiểm tra --base / --password`);
  return token;
}

/** Ngày ăn hợp lệ: từ hôm nay + 2 (chưa qua giờ chốt 20:00 hôm trước) tới hôm nay + 20 (hạn lô gạo định kỳ trong luồng), CHƯA có kế hoạch sản xuất. */
async function freeDays(token, count) {
  const r = await fetch(base + '/api/ProductionPlan/GetAllData', { headers: { Authorization: 'Bearer ' + token } });
  const j = await r.json().catch(() => ({}));
  const used = new Set((j.listData ?? []).map(p => String(p.productionDate ?? p.planDate ?? '').slice(0, 10)));
  const out = [];
  for (let i = 2; i <= 20 && out.length < count; i++) { const d = addDays(vnToday(), i); if (!used.has(d)) out.push(d); }
  return out;
}

const keyOf = i => 'S' + (Math.floor(Date.now() / 60000) + i).toString(36).toUpperCase().slice(-4); // vd. S7K2 → mã DEMOS7K2

const token = await login();
const days = args.day ? [args.day] : await freeDays(token, rounds);
if (days.length < rounds) console.warn(`Chỉ tìm được ${days.length} ngày trống trong 20 ngày tới – chạy ${days.length} lượt.`);
fs.mkdirSync(outDir, { recursive: true });

let failed = 0;
for (let i = 0; i < days.length; i++) {
  const day = days[i], key = args.key && i === 0 ? args.key : keyOf(i);
  console.log(`\n=== Lượt ${i + 1}/${days.length}: mã DEMO${key}, ngày ăn ${day} (thứ ${dowOf(day) === 7 ? 'CN' : dowOf(day) + 1}) → ${base}`);
  let n = 0;
  const results = await runHttpFile(file, { base, user, password, key, day, dow: dowOf(day) }, r => {
    n++;
    if (!r.ok) console.log(`  ✗ ${r.title.slice(0, 70)} – ${r.method} ${r.url.split('?')[0]} → ${r.why.join(' | ').slice(0, 200)}`);
    else if (n % 25 === 0) console.log(`  … ${n} request`);
  });
  const bad = results.filter(r => !r.ok);
  failed += bad.length;
  const bySection = {};
  for (const r of results) { const s = (bySection[r.section || '0'] ??= { request: 0, ok: 0 }); s.request++; if (r.ok) s.ok++; }
  const report = { key, code: 'DEMO' + key, day, base, at: new Date().toISOString(), total: results.length, ok: results.length - bad.length, failed: bad.length, bySection, results };
  const outFile = path.join(outDir, `seed-DEMO${key}-${day}.json`);
  fs.writeFileSync(outFile, JSON.stringify(report, null, 1));
  console.log(`  Kết quả: ${report.ok}/${report.total} đạt, ${report.failed} lỗi – chi tiết ${path.relative(repo, outFile)}`);
}
console.log(failed === 0 ? '\nXong – dữ liệu [DEMO] đã tạo.' : `\nXong nhưng có ${failed} request lỗi – xem file kết quả (dữ liệu các bước trước đó vẫn đã tạo).`);
process.exit(failed === 0 ? 0 : 1);
