// AI-7 (D-133): sinh lệnh cron Hermes từ app-delicare-agents/schedules.json (MỘT nguồn với màn Lịch chạy).
//   node tools/agent-eval/gen-cron.mjs > hermes-cron.sh     (chỉ lịch enabled; agent tắt trên web thì ToolGateway tự từ chối)
// AI-15: 3 container Hermes → mỗi container chỉ nhận lịch của agent mình:  node tools/agent-eval/gen-cron.mjs --group van-hanh   (nhóm ở app-delicare-agents/hermes/groups.json)
// Mỗi việc gửi X-Correlation-Id = sched:<id>:<yyyyMMddHHmm> qua lời nhắc để màn web biết lần chạy gần nhất (header do skill / MCP gắn).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const spec = JSON.parse(fs.readFileSync(path.join(ROOT, 'app-delicare-agents', 'schedules.json'), 'utf8'));
const q = s => `'${String(s).replace(/'/g, `'\\''`)}'`;
const out = ['#!/bin/sh', `# Sinh từ app-delicare-agents/schedules.json (${new Date().toISOString().slice(0, 10)}) – KHÔNG sửa tay; sửa schedules.json rồi sinh lại.`, 'set -e'];
const gi = process.argv.indexOf('--group');
let only = null;
if (gi > 0) {
  const groups = JSON.parse(fs.readFileSync(path.join(ROOT, 'app-delicare-agents', 'hermes', 'groups.json'), 'utf8')).groups;
  only = groups[process.argv[gi + 1]];
  if (!only) { console.error('Nhóm không có: ' + process.argv[gi + 1] + ' (có: ' + Object.keys(groups).join(', ') + ')'); process.exit(2); }
  out.push('# Nhóm container: hermes-' + process.argv[gi + 1] + ' (' + only.join(', ') + ')');
}
// AI-25.5 (D-143): chỉ mục mode = "ai" chạy trên Hermes; mục template chạy ở AppDelicare.Job (templateJob) – 0 token.
for (const s of spec.items.filter(x => x.enabled && (x.mode || 'template') === 'ai' && (!only || only.includes(x.agent)))) {
  const prompt = `[sched:${s.id}] ${s.title}. Dùng công cụ: ${s.tools.join(', ')}. Gắn X-Correlation-Id sched:${s.id}:<thời điểm>. Trả lời theo SOUL của ${s.agent}.`;
  out.push(`hermes cron add --name ${q('delicare-' + s.id)} --schedule ${q(s.cron)} --timezone ${q(spec.timezone)} --grace-minutes 10 --skills ${q('delicare-' + s.agent.toLowerCase())} --prompt ${q(prompt)}`);
}
console.log(out.join('\n'));
