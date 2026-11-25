// UI-6.47 (D-125 d): sinh docs/claude/CONFIG_KEYS.md từ các lớp cấu hình AppDelicare.Model/Option/*.cs (khóa · kiểu · mặc định · ý nghĩa · task).
// Chạy: node tools/config-keys.mjs   (chạy lại khi thêm khóa cấu hình)
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dir = path.join(root, 'AppDelicare/AppDelicare.Model/Option');
const classes = {};
for (const f of fs.readdirSync(dir).filter(x => x.endsWith('.cs'))) {
  const src = fs.readFileSync(path.join(dir, f), 'utf8').replace(/\r\n/g, '\n');
  const re = /public (?:sealed |static )?class (\w+)[^{]*\{/g; let m;
  while ((m = re.exec(src))) {
    // thân lớp: đếm ngoặc
    let i = re.lastIndex, depth = 1; while (depth && i < src.length) { if (src[i] === '{') depth++; else if (src[i] === '}') depth--; i++; }
    const body = src.slice(re.lastIndex, i - 1);
    const section = (body.match(/const string Section = "([^"]+)"/) || [])[1] || null;
    const props = []; const lines = body.split('\n'); let doc = [];
    let nest = 0;
    for (const raw of lines) {
      const line = raw.trim();
      const opens = (raw.match(/\{/g) || []).length, closes = (raw.match(/\}/g) || []).length;
      if (nest === 0 && line.startsWith('///')) { doc.push(line.replace(/^\/\/\/\s?/, '')); continue; }
      const p = nest === 0 && line.match(/^public (?!const|static)([\w<>,\s\[\]?]+?) (\w+) \{ get; (?:set|init); \}(?: = (.+);)?/);
      if (p) props.push({ type: p[1].trim(), name: p[2], def: (p[3] || '').trim(), doc: doc.join(' ').replace(/<\/?summary>/g, '').replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim() });
      if (!line.startsWith('[')) doc = [];
      nest += opens - closes; if (nest < 0) nest = 0;
    }
    classes[m[1]] = { file: f, section, props, doc: '' };
  }
}
// Khóa đầy đủ: lớp gốc có Section → đi theo thuộc tính kiểu lớp cấu hình khác.
const rows = [];
const walk = (cls, prefix, seen) => {
  const c = classes[cls]; if (!c || seen.has(cls)) return; seen = new Set(seen).add(cls);
  for (const p of c.props) {
    const bare = p.type.replace(/\?$/, '');
    if (classes[bare] && !/^(List|Dictionary)/.test(bare)) { walk(bare, `${prefix}:${p.name}`, seen); continue; }
    const tag = (p.doc.match(/\b(UI-6\.\d+|BIZ-\d\.\d+|BOT-[\d.]+\w*|APR-[\w.]+|HR-ACC-[\d.]+|MENU-[\d.]+|BE\d\.\d+|T\d+[A-Z]?(?:\.\d+)?|D-\d+)/) || [])[1] || '';
    rows.push({ key: `${prefix}:${p.name}`, type: p.type, def: p.def || '–', doc: p.doc, tag, file: c.file });
  }
};
for (const [name, c] of Object.entries(classes).sort()) if (c.section) walk(name, c.section, new Set());
const esc = s => String(s).replace(/\|/g, '\\|').replace(/\n/g, ' ');
const bySection = {};
rows.forEach(r => { const s = r.key.split(':').slice(0, 2).join(':'); (bySection[s.split(':')[0]] = bySection[s.split(':')[0]] || []).push(r); });
let md = `# Khóa cấu hình (CONFIG_KEYS)\n\n> Sinh tự động bởi \`node tools/config-keys.mjs\` từ \`AppDelicare.Model/Option/*.cs\` (UI-6.47, D-125 d) – **đừng sửa tay**; thêm / đổi khóa thì sửa lớp cấu hình rồi chạy lại.\n>\n> - Khóa = đường dẫn trong \`appsettings.json\` (vd \`BusinessRules:Hr:AutoMarkAbsentTime\`); biến môi trường thay \`:\` bằng \`__\`.\n> - Khóa **không ghi** trong appsettings / môi trường → dùng **mặc định** ở cột dưới; khi API khởi động, log cảnh báo một dòng liệt kê các khóa \`BusinessRules\` đang dùng mặc định (\`ConfigDefaultsCheck\`) – không làm sập.\n> - Lịch job: \`Jobs:<Mã job>\` (\`Enabled\`, \`At\` "HH:mm" giờ VN) – xem màn **Job tự động** (\`adm-job\`).\n> - Bí mật (chuỗi kết nối, token bot, khóa API) **không** ghi ở đây – xem \`docs/claude/SECRETS_TODO.md\`.\n\nTổng: **${rows.length} khóa** trong ${Object.keys(bySection).length} section.\n`;
// UI-6.49 (4) (D-127 d): cờ "đường lùi" của đợt UI-6 – bool có ghi task UI-6.x; mặc định = hành vi MỚI, đặt giá trị ngược lại = hành vi cũ; bỏ cờ sau UAT.
const flags = rows.filter(r => r.type === 'bool' && /UI-6\.\d+/.test(r.doc));
md += `\n## Cờ "đường lùi" của đợt UI-6 – bỏ cờ sau UAT (UI-6.49 (4), D-127 d)\n\nMọi cờ dưới đây **mặc định = hành vi mới** ở mọi bản appsettings (dev, Staging, Production, Docker không ghi đè – test \`FallbackFlagsUi649Tests\` khóa lại). Đặt giá trị ở cột "Hành vi cũ" chỉ khi cần lùi tạm trong UAT; **bỏ cờ sau UAT** (xóa thuộc tính + nhánh code cũ).\n\n| Khóa | Mặc định (mới) | Hành vi cũ | Task | Việc sau UAT |\n|---|---|---|---|---|\n`;
md += flags.map(r => `| \`${esc(r.key)}\` | ${r.def === 'true' ? 'true' : 'false'} | ${r.def === 'true' ? 'false' : 'true'} | ${(r.doc.match(/UI-6\.\d+/) || [''])[0]} | Bỏ cờ sau UAT |`).join('\n') + '\n';
for (const [sec, list] of Object.entries(bySection)) {
  md += `\n## ${sec}\n\n| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |\n|---|---|---|---|---|\n`;
  md += list.map(r => `| \`${esc(r.key)}\` | ${esc(r.type)} | ${esc(r.def.length > 60 ? r.def.slice(0, 57) + '…' : r.def)} | ${esc(r.doc.length > 220 ? r.doc.slice(0, 217) + '…' : r.doc) || '–'} | ${r.tag || '–'} |`).join('\n') + '\n';
}
fs.writeFileSync(path.join(root, 'docs/claude/CONFIG_KEYS.md'), md);
console.log('CONFIG_KEYS.md:', rows.length, 'khóa,', Object.keys(bySection).length, 'section');
