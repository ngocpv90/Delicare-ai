// Đọc .xlsx không thư viện: export readXlsx(path) → { sheetName: [[cell…]…] }. CLI: node xlsx.mjs <file> [sheet] [maxRows]
import fs from 'fs';
import zlib from 'zlib';

function unzip(buf) {
  let eocd = buf.length - 22;
  while (eocd >= 0 && buf.readUInt32LE(eocd) !== 0x06054b50) eocd--;
  const count = buf.readUInt16LE(eocd + 10), cdOff = buf.readUInt32LE(eocd + 16);
  const files = {};
  let p = cdOff;
  for (let i = 0; i < count; i++) {
    const method = buf.readUInt16LE(p + 10), csize = buf.readUInt32LE(p + 20);
    const nlen = buf.readUInt16LE(p + 28), elen = buf.readUInt16LE(p + 30), clen = buf.readUInt16LE(p + 32), lho = buf.readUInt32LE(p + 42);
    const name = buf.toString('utf8', p + 46, p + 46 + nlen);
    const lnlen = buf.readUInt16LE(lho + 26), lelen = buf.readUInt16LE(lho + 28);
    const data = buf.subarray(lho + 30 + lnlen + lelen, lho + 30 + lnlen + lelen + csize);
    files[name] = method === 8 ? zlib.inflateRawSync(data) : data;
    p += 46 + nlen + elen + clen;
  }
  return files;
}
const dec = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&#(\d+);/g, (_, n) => String.fromCharCode(+n)).replace(/&amp;/g, '&');
const colIdx = ref => { const m = ref.match(/^[A-Z]+/)[0]; let n = 0; for (const ch of m) n = n * 26 + ch.charCodeAt(0) - 64; return n - 1; };

export function readXlsx(path) {
  const raw = unzip(fs.readFileSync(path)); const f = {};
  // ClosedXML ghi thẻ có tiền tố namespace (<x:sheet>, <x:row>…) → bỏ tiền tố trước khi tách.
  for (const [k, v] of Object.entries(raw)) f[k] = /[.](xml|rels)$/.test(k) ? Buffer.from(v.toString('utf8').replace(/<([/]?)[a-zA-Z0-9]+:(?=(si|t|r|sheet|sheets|row|c|v|is|workbook|sst|sheetData|worksheet)[\s>/])/g, '<$1')) : v;
  const sst = [];
  if (f['xl/sharedStrings.xml']) {
    for (const si of f['xl/sharedStrings.xml'].toString('utf8').match(/<si>[\s\S]*?<\/si>/g) || []) {
      sst.push(dec((si.match(/<t[^>]*>([\s\S]*?)<\/t>/g) || []).map(t => t.replace(/<t[^>]*>|<\/t>/g, '')).join('')));
    }
  }
  const wb = f['xl/workbook.xml'].toString('utf8'), rels = f['xl/_rels/workbook.xml.rels'].toString('utf8');
  const out = {};
  for (const m of wb.matchAll(/<sheet [^>]*name="([^"]+)"[^>]*r:id="([^"]+)"/g)) {
    const target = rels.match(new RegExp(`Id="${m[2]}"[^>]*Target="([^"]+)"`))?.[1] || rels.match(new RegExp(`Target="([^"]+)"[^>]*Id="${m[2]}"`))?.[1];
    const xml = f['xl/' + target.replace(/^\/?xl\//, '')].toString('utf8');
    const rows = [];
    for (const row of xml.match(/<row[^>]*>[\s\S]*?<\/row>|<row[^>]*\/>/g) || []) {
      const r = [];
      for (const c of row.match(/<c [^>]*?(\/>|>[\s\S]*?<\/c>)/g) || []) {
        const ref = c.match(/r="([A-Z]+\d+)"/)?.[1]; const t = c.match(/ t="(\w+)"/)?.[1];
        let v = c.match(/<v>([\s\S]*?)<\/v>/)?.[1];
        if (t === 's') v = sst[+v]; else if (t === 'inlineStr') v = dec((c.match(/<t[^>]*>([\s\S]*?)<\/t>/) || [])[1] || ''); else if (v !== undefined) v = t === 'str' ? dec(v) : (isNaN(+v) ? dec(v) : +v);
        if (ref) r[colIdx(ref)] = v ?? null;
      }
      rows.push(r);
    }
    out[dec(m[1])] = rows;
  }
  return out;
}

if (process.argv[2] && import.meta.url.endsWith(process.argv[1].replace(/\\/g, '/').split('/').pop())) {
  const book = readXlsx(process.argv[2]);
  for (const [name, rows] of Object.entries(book)) {
    if (process.argv[3] && process.argv[3] !== '*' && name !== process.argv[3]) continue;
    console.log(`=== ${name} (${rows.length} dòng)`);
    rows.slice(0, +(process.argv[4] || 8)).forEach(r => console.log(JSON.stringify(r).slice(0, 400)));
  }
}
