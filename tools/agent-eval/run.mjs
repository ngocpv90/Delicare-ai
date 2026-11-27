// AI-7 (D-133): chạy bộ câu nghiệm thu agents/<MÃ>/eval.yaml qua REST ToolGateway (/tools/<công cụ>) – đúng công cụ + đúng số (so màn web cùng quyền).
//   node tools/agent-eval/run.mjs [VIVI WH KITCHEN QA KNOW]
// Biến môi trường (KHÔNG ghi vào repo):
//   TOOLGATEWAY_URL (mặc định http://localhost:5130), API_URL (mặc định http://localhost:5125/api)
//   AGENT_KEY_<MÃ>  – khóa agent (thiếu → bỏ qua agent đó)
//   EVAL_WEB_PASS    – mật khẩu tài khoản web so số (webUser trong eval.yaml); thiếu → bỏ qua phần so số web
// Kết quả: bảng từng câu + tổng; mã thoát 1 khi có câu sai.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')), '..', '..');
const require = createRequire(path.join(ROOT, 'app-delicare-client', 'package.json'));
const yaml = require('js-yaml');
const GW = (process.env.TOOLGATEWAY_URL || 'http://localhost:5130').replace(/\/$/, '');
const API = (process.env.API_URL || 'http://localhost:5125/api').replace(/\/$/, '');
const agents = process.argv.slice(2).length ? process.argv.slice(2) : ['VIVI', 'WH', 'KITCHEN', 'QA', 'KNOW'];

const get = (obj, p) => !p ? obj : p.split('.').reduce((o, k) => (o == null ? undefined : o[k]), obj);
const num = v => Array.isArray(v) ? v.length : typeof v === 'number' ? v : v && typeof v === 'object' && 'total' in v ? v.total : v && Array.isArray(v.items) ? v.items.length : undefined;
const tokens = {};
async function webToken(user) {
  if (!user || !process.env.EVAL_WEB_PASS) return null;
  if (tokens[user] !== undefined) return tokens[user];
  const r = await (await fetch(API + '/Authentication/Login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: user, passWord: process.env.EVAL_WEB_PASS }) })).json().catch(() => ({}));
  return (tokens[user] = r && r.data && r.data.token || null);
}

let fail = 0, pass = 0, skip = 0;
const rows = [];
for (const code of agents) {
  const file = path.join(ROOT, 'agents', code, 'eval.yaml');
  if (!fs.existsSync(file)) { rows.push([code, '–', '–', 'BỎ QUA', 'không có eval.yaml']); skip++; continue; }
  const spec = yaml.load(fs.readFileSync(file, 'utf8'));
  const key = process.env['AGENT_KEY_' + code];
  if (!key) { rows.push([code, '–', '–', 'BỎ QUA', `thiếu biến môi trường AGENT_KEY_${code}`]); skip += spec.cases.length; continue; }
  const tools = fs.readFileSync(path.join(ROOT, 'agents', code, 'tools.txt'), 'utf8').split(/\r?\n/).map(s => s.trim()).filter(s => s && !s.startsWith('#'));
  for (const c of spec.cases) {
    const errs = [];
    if (!tools.includes(c.tool)) errs.push(`công cụ ${c.tool} không thuộc ${code}`);
    const res = await fetch(`${GW}/tools/${c.tool}`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Agent-Code': code, 'X-Agent-Key': key, 'X-Correlation-Id': `eval:${c.id}` }, body: JSON.stringify(c.args || {}) }).catch(e => ({ status: 0, json: async () => ({ message: e.message }) }));
    const body = await res.json().catch(() => ({}));
    const text = JSON.stringify(body);
    const e = c.expect || {};
    if (e.status !== undefined && res.status !== e.status) errs.push(`HTTP ${res.status} ≠ ${e.status}${body.message ? ' (' + body.message + ')' : ''}`);
    for (const h of e.has || []) if (get(body.data, h) === undefined) errs.push(`thiếu ${h}`);
    if (e.contains && !text.includes(e.contains)) errs.push(`không có «${e.contains}»`);
    if (e.notContains && text.includes(e.notContains)) errs.push(`có «${e.notContains}»`);
    let note = '';
    if (e.equalsWeb && res.status === 200) {
      const tok = await webToken(spec.webUser);
      if (!tok) note = 'bỏ qua so số web (thiếu EVAL_WEB_PASS / webUser)';
      else {
        const w = await (await fetch(`${API}/${e.equalsWeb.web}`, { headers: { Authorization: 'Bearer ' + tok } })).json().catch(() => ({}));
        const a = num(get(body.data, e.equalsWeb.tool)), b = num(get(w, e.equalsWeb.webPath));
        if (a !== b) errs.push(`số công cụ ${a} ≠ màn web ${b}`); else note = `= web ${b}`;
      }
    }
    rows.push([code, c.id, c.tool, errs.length ? 'SAI' : 'ĐÚNG', errs.join('; ') || note]);
    errs.length ? fail++ : pass++;
  }
}
const w = [8, 22, 22, 7];
console.log(['Agent', 'Câu', 'Công cụ', 'Kết quả', 'Ghi chú'].map((h, i) => i < 4 ? h.padEnd(w[i]) : h).join(' '));
for (const r of rows) console.log(r.map((v, i) => i < 4 ? String(v).padEnd(w[i]) : v).join(' '));
console.log(`\nTổng: ${pass} đúng, ${fail} sai, ${skip} bỏ qua`);
process.exit(fail ? 1 : 0);
