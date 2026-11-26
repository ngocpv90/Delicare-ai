// REAL-1 (D-129): hàm dùng chung cho các bước nạp dữ liệu vận hành thật qua API (không chèn SQL trực tiếp).
// Biến môi trường:
//   REAL1_API   – mặc định http://localhost:5125/api
//   REAL1_DIR   – thư mục Chuan-hoa-REAL1 (KHÔNG chép vào repo – có giá mua / doanh thu thật)
//   DC_USER / DC_PASS – tài khoản quản trị (nạp Excel); UAT_PASS – mật khẩu chung tài khoản uat.*
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

export const API = (process.env.REAL1_API || 'http://localhost:5125/api').replace(/\/+$/, '');
export const DIR = (process.env.REAL1_DIR || 'D:/Duan/Tai lieu nghiep vu du an/Delicare-Share/Chuan-hoa-REAL1').replace(/[\\/]+$/, '') + '/';
export const WORK = fs.mkdtempSync(path.join(os.tmpdir(), 'real1-'));   // tệp tạm (vd. 05 đã đổi lượng tinh) – ngoài repo

/** Ngày hôm nay giờ Việt Nam (yyyy-MM-dd). */
export const today = () => new Date(Date.now() + 7 * 3600e3).toISOString().slice(0, 10);
export const addDays = (d, n) => new Date(Date.parse(d + 'T00:00:00Z') + n * 864e5).toISOString().slice(0, 10);

export function need(name) {
  if (!process.env[name]) { console.error(`Thiếu biến môi trường ${name}`); process.exit(2); }
  return process.env[name];
}

export async function waitApi() {
  for (let i = 0; i < 60; i++) {
    try { const r = await fetch(API + '/Authentication/Login', { method: 'POST' }); if (r.status) return; } catch { /* chưa lên */ }
    await new Promise(r => setTimeout(r, 2000));
  }
  throw new Error('API không phản hồi: ' + API);
}

/** Đăng nhập → client { get, form, json, upload }. user 'admin' dùng DC_USER / DC_PASS, uat.* dùng UAT_PASS. */
export async function login(user) {
  const isAdmin = user === 'admin';
  const body = { userName: isAdmin ? need('DC_USER') : user, passWord: isAdmin ? need('DC_PASS') : need('UAT_PASS') };
  const j = await (await fetch(API + '/Authentication/Login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })).json();
  const token = j.data?.token;
  if (!token) throw new Error(`Đăng nhập ${user} thất bại: ${j.responseMessage ?? ''}`);
  const H = { Authorization: 'Bearer ' + token };
  const parse = async r => { const t = await r.text(); try { return JSON.parse(t); } catch { return { responseCode: r.status, responseMessage: t.slice(0, 200) }; } };
  const clean = o => Object.fromEntries(Object.entries(o).filter(([, v]) => v !== undefined && v !== null).map(([k, v]) => [k, String(v)]));
  return {
    get: async u => parse(await fetch(API + u, { headers: H })),
    form: async (u, o) => parse(await fetch(API + u, { method: 'POST', headers: { ...H, 'Content-Type': 'application/x-www-form-urlencoded' }, body: new URLSearchParams(clean(o)) })),
    json: async (u, o) => parse(await fetch(API + u, { method: 'POST', headers: { ...H, 'Content-Type': 'application/json' }, body: JSON.stringify(o) })),
    upload: async (u, fields, fileField, filePath) => {
      const fd = new FormData();
      for (const [k, v] of Object.entries(fields)) fd.append(k, String(v));
      fd.append(fileField, new Blob([fs.readFileSync(filePath)]), path.basename(filePath));
      return parse(await fetch(API + u, { method: 'POST', headers: H, body: fd }));
    },
  };
}

export const file = prefix => { const f = fs.readdirSync(DIR).find(x => x.startsWith(prefix)); if (!f) throw new Error('Không thấy tệp ' + prefix + '* trong ' + DIR); return DIR + f; };
export const ok = r => r && r.responseCode == 200;
