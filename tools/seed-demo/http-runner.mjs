// Trình chạy file .http (cú pháp VS Code REST Client) cho seeder dữ liệu thử – chạy LẦN LƯỢT từ trên xuống.
// Hỗ trợ: biến "@x = …" (ghi đè được), {{x}}, {{<name>.response.body.<đường dẫn>}} (kèm [i] và [field=value]),
// {{$datetime "fmt" N d|h|m}} (giờ UTC như REST Client), {{$localDatetime …}} (giờ máy), "# @name", chốt "→ 400" ở dòng tiêu đề "### …",
// "# @expect <đường dẫn> [length] <==|!=|~|contains|!contains> <giá trị>". Bước trả tệp (pdf / xlsx / docx…) đạt khi HTTP 200 + có nội dung.
import fs from 'fs';

const pad = n => String(n).padStart(2, '0');
function fmt(d, f, utc) {
  const g = k => (utc ? d['getUTC' + k]() : d['get' + k]());
  return f.replace('YYYY', g('FullYear')).replace('MM', pad(g('Month') + 1)).replace('DD', pad(g('Date')))
    .replace('HH', pad(g('Hours'))).replace('mm', pad(g('Minutes'))).replace('ss', pad(g('Seconds')));
}
function dyn(expr) {
  const m = expr.match(/^\$(datetime|localDatetime)\s+"([^"]+)"(?:\s+(-?\d+)\s+(d|h|m))?$/);
  if (!m) return null;
  const d = new Date(Date.now() + (m[3] ? Number(m[3]) * { d: 864e5, h: 36e5, m: 6e4 }[m[4]] : 0));
  return fmt(d, m[2], m[1] === 'datetime');
}
export function getPath(obj, p) {
  let o = obj;
  for (const part of p.split('.').filter(Boolean)) {
    const mm = part.match(/^([^[\]]*)((?:\[[^\]]+\])*)$/);
    if (!mm) return undefined;
    if (mm[1]) o = o?.[mm[1]];
    for (const idx of [...mm[2].matchAll(/\[([^\]]+)\]/g)].map(x => x[1])) {
      if (/^\d+$/.test(idx)) o = o?.[Number(idx)];
      else { const [k, v] = idx.split('='); o = Array.isArray(o) ? o.find(x => String(x?.[k]).toLowerCase() === String(v).toLowerCase()) : undefined; }
    }
  }
  return o;
}

/**
 * Chạy 1 file .http. overrides = biến @ ghi đè (base, password, key, day, dow…). log = hàm in tiến độ (tùy chọn).
 * Trả về danh sách kết quả { title, section, method, url, http, code, expect400, expects, ok, why, ms, msg }.
 */
export async function runHttpFile(file, overrides = {}, log = () => { }) {
  const lines = fs.readFileSync(file, 'utf8').replace(/^﻿/, '').replace(/\r\n/g, '\n').split('\n');
  const blocks = [];
  let cur = { title: '', lines: [] };
  for (const l of lines) {
    if (l.startsWith('###')) { blocks.push(cur); cur = { title: l.replace(/^###\s?/, ''), lines: [] }; } else cur.lines.push(l);
  }
  blocks.push(cur);

  const vars = {}, named = {};
  const subst = s => {
    for (let i = 0; i < 5 && /\{\{[^}]+\}\}/.test(s); i++) {
      s = s.replace(/\{\{([^}]+)\}\}/g, (all, e) => {
        e = e.trim();
        if (e.startsWith('$')) return dyn(e) ?? all;
        const r = e.match(/^([A-Za-z0-9_]+)\.response\.body\.?(.*)$/);
        if (r) { const v = getPath(named[r[1]]?.body, r[2]); return v === undefined || v === null ? '' : typeof v === 'object' ? JSON.stringify(v) : String(v); }
        return e in vars ? vars[e] : all;
      });
    }
    return s;
  };

  const results = [];
  let section = '';
  for (const b of blocks) {
    const sec = b.title.match(/^#+\s*(\d+)\.\s/) || b.title.match(/^(\d+)\.\d+/);
    if (sec) section = sec[1];
    let name = null; const expects = [], req = [];
    for (const l of b.lines) {
      const v = l.match(/^@([A-Za-z0-9_]+)\s*=\s*(.*)$/);
      if (v && req.length === 0) { vars[v[1]] = v[1] in overrides ? String(overrides[v[1]]) : subst(v[2].trim()); continue; }
      const n = l.match(/^#\s*@name\s+(\S+)/); if (n) { name = n[1]; continue; }
      const e = l.match(/^#\s*@expect\s+(.+)$/); if (e) { expects.push(e[1]); continue; }
      if (req.length === 0 && (l.startsWith('#') || l.trim() === '')) continue;
      req.push(l);
    }
    if (req.length === 0) continue;
    const m = req[0].match(/^(GET|POST|PUT|DELETE|PATCH)\s+(.+?)(?:\s+HTTP\/[\d.]+)?\s*$/);
    if (!m) continue;
    const headers = {}; let i = 1;
    for (; i < req.length && req[i].trim() !== ''; i++) { const h = req[i].match(/^([^:]+):\s*(.*)$/); if (h) headers[h[1].trim()] = subst(h[2]); }
    const body = subst(req.slice(i + 1).join('\n').replace(/\n+$/, ''));
    const ct = headers['Content-Type'] || '';
    let payload;
    if (/multipart/i.test(ct)) payload = Buffer.from(body.replace(/\n/g, '\r\n') + '\r\n', 'utf8');
    else if (body.length) payload = /x-www-form-urlencoded/i.test(ct) ? body.split('\n').map(x => x.trim()).join('') : body;
    const url = subst(m[2]);
    const expect400 = /→\s*400/.test(b.title);
    const t0 = Date.now();
    let http = 0, size = 0, json = null; const why = [];
    try {
      const res = await fetch(url, { method: m[1], headers, body: m[1] === 'GET' ? undefined : payload });
      const buf = Buffer.from(await res.arrayBuffer());
      http = res.status; size = buf.length;
      if (/json/.test(res.headers.get('content-type') || '')) { try { json = JSON.parse(buf.toString('utf8')); } catch { } }
    } catch (err) { why.push('Lỗi kết nối: ' + err.message); }
    if (name) named[name] = { body: json };
    const code = json?.responseCode;
    if (why.length === 0) {
      if (http !== 200) why.push(`HTTP ${http}`);
      else if (expect400) { if (code !== 400) why.push(`kỳ vọng responseCode 400 (chốt an toàn), nhận ${code ?? 'không có'}`); }
      else if (json && code !== undefined && code !== 200) why.push(`responseCode ${code}: ${json.responseMessage}`);
      else if (!json && !(size > 0)) why.push('tệp trả về rỗng');
    }
    for (const ex of expects) {
      const mm = subst(ex).match(/^(\S+)\s+(length\s+)?(==|!=|~|!contains|contains)\s*(.*)$/);
      if (!mm) continue;
      let actual = getPath(json, mm[1]); const op = mm[3], want = mm[4].trim();
      if (mm[2]) actual = Array.isArray(actual) ? actual.length : undefined;
      const str = x => x === null || x === undefined ? String(x) : typeof x === 'object' ? JSON.stringify(x) : String(x);
      let pass;
      if (op === '==') pass = str(actual).toLowerCase() === want.toLowerCase() || (want !== '' && Number(actual) === Number(want));
      else if (op === '!=') pass = str(actual).toLowerCase() !== want.toLowerCase();
      else if (op === '~') pass = new RegExp(want).test(str(actual));
      else { const has = str(actual).toLowerCase().includes(want.toLowerCase()); pass = op === 'contains' ? has : !has; }
      if (!pass) why.push(`@expect ${mm[1]} ${mm[2] ?? ''}${op} ${want} – thực tế ${str(actual).slice(0, 120)}`);
    }
    const r = { title: b.title.trim(), section, method: m[1], url: url.replace(/^https?:\/\/[^/]+/, ''), http, code, expect400, expects: expects.length, ok: why.length === 0, why, ms: Date.now() - t0, msg: json?.responseMessage };
    results.push(r);
    log(r);
  }
  return results;
}
