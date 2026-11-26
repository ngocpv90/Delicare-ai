// REAL-1 (D-129 / D-130): dựng TRỌN bộ dữ liệu vận hành thật (làm dữ liệu demo / UAT) trên 1 DB – máy dev, máy FE, server test – cho đồng nhất.
// Thứ tự: dọn [DEMO] (SQL) → nạp Excel → dọn lần 2 → công thức → kho / dây chuyền → hợp đồng → thực đơn → đăng ký suất + dự báo → tồn đầu kỳ → gắn nhãn (SQL).
// Mọi bước chạy lại = 0 việc.
//
//   node tools/real1/run-all.mjs          → CHẠY THỬ: in kế hoạch + chạy thử 2 script SQL (hoàn tác), không ghi gì
//   node tools/real1/run-all.mjs --yes    → chạy thật
//
// Biến môi trường:
//   DC_USER / DC_PASS          tài khoản quản trị web (bắt buộc)
//   UAT_PASS                   mật khẩu chung uat.* (không có → mọi bước dùng quản trị)
//   REAL1_API                  mặc định http://localhost:5125/api   (server test: http://<server>:5125/api)
//   REAL1_SQL_SERVER           mặc định (localdb)\MSSQLLocalDB       (server test: <host>,<port>)
//   REAL1_SQL_DB               mặc định AppDelicare
//   REAL1_SQL_USER / REAL1_SQL_PASS   đăng nhập SQL (server test); không đặt → Windows (-E)
//   REAL1_START                thứ Hai tuần thực đơn đầu (mặc định 2026-11-23) – dựng muộn hơn thì đặt thứ Hai sắp tới
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { API, DIR, START, need, waitApi } from './lib.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const apply = process.argv.includes('--yes');
const server = process.env.REAL1_SQL_SERVER || '(localdb)\\MSSQLLocalDB';
const db = process.env.REAL1_SQL_DB || 'AppDelicare';
need('DC_USER'); need('DC_PASS');

function sql(script, dryRun) {
  const args = ['-S', server, '-d', db, '-b', '-f', '65001', '-v', `DryRun=${dryRun ? 1 : 0}`, '-i', path.join(repo, 'DB', 'scripts', script), '-s', '|', '-W'];
  const env = { ...process.env };
  if (process.env.REAL1_SQL_USER) { args.push('-U', process.env.REAL1_SQL_USER); env.SQLCMDPASSWORD = need('REAL1_SQL_PASS'); } else args.push('-E');
  console.log(`\n=== ${script} (${dryRun ? 'chạy thử' : 'THẬT'})`);
  const r = spawnSync('sqlcmd', args, { env, encoding: 'utf8' });
  if (r.error) throw new Error('Không chạy được sqlcmd: ' + r.error.message);
  const out = (r.stdout || '') + (r.stderr || '');
  console.log(out.split(/\r?\n/).filter(l => l && !/\|0$/.test(l) && !/^-+\|/.test(l)).join('\n'));
  if (r.status !== 0) throw new Error(`${script} lỗi (mã ${r.status})`);
}
function node(step, ...args) {
  console.log(`\n=== ${step} ${args.join(' ')}`);
  const r = spawnSync(process.execPath, [path.join(here, step + '.mjs'), ...args], { stdio: 'inherit', env: process.env });
  if (r.status !== 0) throw new Error(`${step} lỗi (mã ${r.status})`);
}

console.log(`REAL-1 → API ${API} · DB ${server} / ${db} · dữ liệu ${DIR} · tuần đầu ${START} · ${apply ? 'CHẠY THẬT' : 'CHẠY THỬ (thêm --yes để ghi)'}`);
sql('REAL1_retire_demo.sql', true);
if (!apply) {
  console.log('\nChạy thử xong – không ghi gì. Xem số dòng ở trên rồi chạy lại với --yes.');
  process.exit(0);
}
await waitApi();
sql('REAL1_retire_demo.sql', false);
node('01-import');
sql('REAL1_retire_demo.sql', false);   // ngừng món ngân hàng cũ trùng tên + nhóm cũ đã rỗng (cần món REAL-1 đã nạp)
node('03-recipes');
node('04-setup');
node('05-contracts');
node('06-menus');
node('07-orders');
node('08-stock');
sql('REAL1_tag.sql', false);
console.log('\nXONG. Định biên ca (appsettings) xem tools/real1/README.md.');
