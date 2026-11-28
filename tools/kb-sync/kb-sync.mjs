#!/usr/bin/env node
// AI-18 (D-133) – Đồng bộ kho tri thức DELI-CARE xuống Obsidian máy cá nhân. Node 18+, KHÔNG thư viện ngoài.
//
//   DC_KB_URL=https://<máy chủ>/api DC_KB_USER=<tài khoản> DC_KB_PASS=<mật khẩu> node tools/kb-sync/kb-sync.mjs "<thư mục vault Obsidian>" [--dry]
//
// - MỘT CHIỀU (mặc định, mọi thư mục): máy chủ → máy. Chỉ tải ghi chú bạn được xem (quyền thư mục); KHÔNG BAO GIỜ có BOD. Tệp tải về đặt chỉ đọc.
//   Sửa trên máy ở thư mục một chiều KHÔNG được đẩy lên: lần chạy sau bản máy được chuyển vào ".delicare-conflict/" rồi ghi lại bản máy chủ.
// - HAI CHIỀU chỉ thư mục 80 Đào tạo khi IT bật Knowledge:SyncTwoWayTraining VÀ bạn có quyền Sửa thư mục 80: sửa / thêm .md trên máy được đẩy lên qua
//   Knowledge/Save (kiểm dữ liệu cá nhân, khóa băm, lịch sử). Máy chủ cũng đổi kể từ lần đồng bộ trước → không ghi đè: giữ bản máy ở ".delicare-conflict/".
// - Ghi chú bị thu hồi quyền / chuyển khỏi phạm vi → xóa bản trên máy (bản đã sửa trên máy chuyển ".delicare-conflict/").
// - Trạng thái lần trước: "<vault>/.delicare-sync.json" (đường dẫn → băm). Mật khẩu chỉ ở biến môi trường, không ghi ra tệp.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const args = process.argv.slice(2);
const dry = args.includes('--dry');
const vault = args.find(a => !a.startsWith('--'));
const base = (process.env.DC_KB_URL || 'http://localhost:5125/api').replace(/\/+$/, '') + '/';
if (!vault || !process.env.DC_KB_USER || !process.env.DC_KB_PASS) {
  console.error('Cách dùng: DC_KB_URL=… DC_KB_USER=… DC_KB_PASS=… node tools/kb-sync/kb-sync.mjs "<thư mục vault>" [--dry]');
  process.exit(2);
}
const statePath = path.join(vault, '.delicare-sync.json');
const conflictDir = path.join(vault, '.delicare-conflict');
const sha = s => crypto.createHash('sha256').update(s, 'utf8').digest('hex');
const readText = f => fs.readFileSync(f, 'utf8').replace(/^﻿/, '');
const stamp = () => new Date().toISOString().slice(0, 16).replace(/[-:T]/g, '');
const safe = rel => { const full = path.resolve(vault, rel); if (!full.startsWith(path.resolve(vault) + path.sep)) throw new Error('Đường dẫn ra ngoài vault: ' + rel); return full; };
const writeRo = (full, text) => {
  if (dry) return;
  fs.mkdirSync(path.dirname(full), { recursive: true });
  if (fs.existsSync(full)) fs.chmodSync(full, 0o644);
  fs.writeFileSync(full, text, 'utf8');
};
const keepLocal = (rel, full) => {
  const dst = path.join(conflictDir, rel.replace(/\.md$/i, '') + ` (bản máy ${stamp()}).md`);
  if (!dry) { fs.mkdirSync(path.dirname(dst), { recursive: true }); fs.chmodSync(full, 0o644); fs.renameSync(full, dst); }
  return path.relative(vault, dst);
};

async function api(method, p, token, body) {
  const res = await fetch(base + p, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const j = await res.json().catch(() => ({}));
  if (!res.ok || (j.responseCode && j.responseCode !== 200)) throw new Error(`${p}: ${j.responseMessage || j.title || res.status}`);
  return j;
}

const login = await api('POST', 'Authentication/Login', null, { userName: process.env.DC_KB_USER, passWord: process.env.DC_KB_PASS });
const token = login.data?.token || (() => { throw new Error('Đăng nhập không thành công'); })();
const man = (await api('GET', 'Knowledge/GetSyncManifest', token)).data;
const twoWay = new Set(man.twoWayFolders || []);
const state = fs.existsSync(statePath) ? JSON.parse(fs.readFileSync(statePath, 'utf8')) : { notes: {} };
const next = { server: base, user: process.env.DC_KB_USER, at: man.at, twoWayFolders: [...twoWay], notes: {} };
const n = { down: 0, up: 0, same: 0, conflict: 0, removed: 0, created: 0 };
const log = (...a) => console.log(dry ? '[thử]' : '', ...a);
const serverPaths = new Set();

for (const note of man.listNote || []) {
  serverPaths.add(note.path);
  const full = safe(note.path);
  const last = state.notes[note.path];
  const local = fs.existsSync(full) ? sha(readText(full)) : null;
  const canPush = twoWay.has(note.folder) && !note.readOnly;
  const download = async () => {
    const raw = (await api('GET', 'Knowledge/GetNote?path=' + encodeURIComponent(note.path), token)).data?.raw ?? '';
    writeRo(full, raw);
    if (!dry && !canPush) fs.chmodSync(full, 0o444);
    n.down++;
  };
  if (local === note.hash) { n.same++; if (!dry && !canPush && fs.existsSync(full)) fs.chmodSync(full, 0o444); next.notes[note.path] = note.hash; continue; }
  if (local === null || local === last) { await download(); log('↓', note.path); next.notes[note.path] = note.hash; continue; }
  // Bản trên máy đã sửa kể từ lần đồng bộ trước.
  if (canPush && note.hash === last) {
    const text = readText(full);
    const r = dry ? { data: { hash: sha(text) } } : await api('POST', 'Knowledge/Save', token, { path: note.path, content: text, prevHash: note.hash });
    n.up++; log('↑', note.path); next.notes[note.path] = r.data?.hash || sha(text); continue;
  }
  const kept = keepLocal(note.path, full); n.conflict++;
  log('⚠', note.path, canPush ? '– máy chủ cũng đã đổi; bản máy giữ ở' : '– thư mục một chiều, không đẩy lên; bản máy giữ ở', kept);
  await download(); next.notes[note.path] = note.hash;
}

// Ghi chú không còn trong phạm vi (thu hồi quyền / chuyển thư mục / lưu trữ ngoài quyền) → xóa bản máy.
for (const [rel, h] of Object.entries(state.notes)) {
  if (serverPaths.has(rel)) continue;
  const full = safe(rel);
  if (!fs.existsSync(full)) continue;
  if (sha(readText(full)) !== h) { log('⚠', rel, '– không còn trong phạm vi, bản máy đã sửa giữ ở', keepLocal(rel, full)); n.conflict++; continue; }
  if (!dry) { fs.chmodSync(full, 0o644); fs.unlinkSync(full); }
  n.removed++; log('✕', rel);
}

// Ghi chú MỚI tạo trên máy ở thư mục hai chiều → tạo trên máy chủ.
for (const folder of twoWay) {
  const dir = (man.listNote || []).find(x => x.folder === folder)?.path.split('/')[0] || fs.readdirSync(vault).find(d => d.startsWith(folder + ' ')) || null;
  if (!dir || !fs.existsSync(path.join(vault, dir))) continue;
  const walk = d => fs.readdirSync(d, { withFileTypes: true }).flatMap(e => e.isDirectory() ? walk(path.join(d, e.name)) : [path.join(d, e.name)]);
  for (const f of walk(path.join(vault, dir)).filter(f => f.toLowerCase().endsWith('.md'))) {
    const rel = path.relative(vault, f).split(path.sep).join('/');
    if (serverPaths.has(rel)) continue;
    const text = readText(f);
    const r = dry ? { data: { hash: sha(text) } } : await api('POST', 'Knowledge/Save', token, { path: rel, content: text });
    n.created++; log('＋', rel); next.notes[rel] = r.data?.hash || sha(text);
  }
}

if (!dry) fs.writeFileSync(statePath, JSON.stringify(next, null, 1));
console.log(`Xong${dry ? ' (chạy thử – không ghi gì)' : ''}: ${n.down} tải về, ${n.same} không đổi, ${n.up} đẩy lên, ${n.created} tạo mới, ${n.removed} xóa (hết quyền), ${n.conflict} xung đột` +
  (twoWay.size ? ` · hai chiều: ${[...twoWay].join(', ')}` : ' · mọi thư mục một chiều'));
