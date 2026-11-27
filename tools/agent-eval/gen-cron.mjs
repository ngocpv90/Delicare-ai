// AI-7 (D-133): sinh lệnh cron Hermes từ agents/schedules.json (MỘT nguồn với màn Lịch chạy).
//   node tools/agent-eval/gen-cron.mjs > hermes-cron.sh     (chỉ lịch enabled; agent tắt trên web thì ToolGateway tự từ chối)
// Mỗi việc gửi X-Correlation-Id = sched:<id>:<yyyyMMddHHmm> qua lời nhắc để màn web biết lần chạy gần nhất (header do skill / MCP gắn).
import fs from 'node:fs';
import path from 'node:path';

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')), '..', '..');
const spec = JSON.parse(fs.readFileSync(path.join(ROOT, 'agents', 'schedules.json'), 'utf8'));
const q = s => `'${String(s).replace(/'/g, `'\\''`)}'`;
const out = ['#!/bin/sh', `# Sinh từ agents/schedules.json (${new Date().toISOString().slice(0, 10)}) – KHÔNG sửa tay; sửa schedules.json rồi sinh lại.`, 'set -e'];
for (const s of spec.items.filter(x => x.enabled)) {
  const prompt = `[sched:${s.id}] ${s.title}. Dùng công cụ: ${s.tools.join(', ')}. Gắn X-Correlation-Id sched:${s.id}:<thời điểm>. Trả lời theo SOUL của ${s.agent}.`;
  out.push(`hermes cron add --name ${q('delicare-' + s.id)} --schedule ${q(s.cron)} --timezone ${q(spec.timezone)} --grace-minutes 10 --skills ${q('delicare-' + s.agent.toLowerCase())} --prompt ${q(prompt)}`);
}
console.log(out.join('\n'));
