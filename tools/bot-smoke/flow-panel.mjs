// BOT-UX-1 – kịch bản thử bảng điều khiển duy nhất (Telegram giả fake-telegram.mjs cổng 18081 → messaging Polling → api 5125, DB dev).
// An toàn với dữ liệu: chỉ xem (menu, lịch ca, tra cứu) – không duyệt / ghi gì; cuối kịch bản /huylienket gỡ liên kết thử.
// Chạy: node tools/bot-smoke/fake-telegram.mjs 18081  +  messaging (Telegram__ApiBaseUrl=http://localhost:18081, Telegram__Bots__VH__Token=FAKE,
//       Telegram__Bots__DH__Enabled=false)  →  node tools/bot-smoke/flow-panel.mjs
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const user = 880000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 1000) + 700000;
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const get = async (path) => (await fetch(TG + path)).json();
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const from = { id: user, is_bot: false, first_name: 'Thử', last_name: 'BOTUX1' };
const chat = { id: user, type: 'private' };
const msg = (extra) => { const id = ++uid; return { id, update: { update_id: id, message: { message_id: id, date: Math.floor(Date.now() / 1000), from, chat, ...extra } } }; };
const tap = (data, messageId) => ({ update_id: ++uid, callback_query: { id: 'cb' + uid, from, data, message: { message_id: messageId, date: 1, chat } } });
let fails = 0;
const ok = (cond, label, detail = '') => { console.log((cond ? '✓ ' : '✗ ') + label + (detail ? ' – ' + detail : '')); if (!cond) fails++; };
const alive = async () => get('/_chat?chat=' + user);
const panels = async () => (await alive()).filter(m => m.reply_markup?.inline_keyboard?.length);
const panel = async () => (await panels()).at(-1);
const callbacks = (m) => (m?.reply_markup?.inline_keyboard ?? []).flat().map(b => b.callback_data);
const firstLine = (m) => (m?.text ?? '').split('\n')[0].replace(/<\/?i>/g, '');
// Đợi tới khi trạng thái chat đổi (messaging xử lý update qua api – vài trăm ms).
async function settle(pred, label) {
  for (let i = 0; i < 50; i++) { const p = await panel(); if (pred(p)) return p; await sleep(300); }
  ok(false, label + ' (hết giờ chờ)'); return await panel();
}
async function push(update) { await post(TG + '/_push', update); }

// 0. Liên kết admin bằng mã web.
const login = await post(API + '/api/Authentication/Login', { userName: 'admin', passWord: 'admin' });
const token = login.data.token;
const code = (await (await fetch(API + '/api/TelegramLink/CreateCode', { method: 'POST', headers: { authorization: 'Bearer ' + token } })).json()).data.code;
await push(msg({ text: '/start ' + code }).update);
let hello = null;
for (let i = 0; i < 50 && !hello; i++) { hello = (await get('/_sent')).find(s => String(s.chat_id) === String(user) && (s.text || '').includes('Xin chào')); if (!hello) await sleep(300); }
const kb = hello?.reply_markup;
ok(kb?.is_persistent === true && JSON.stringify(kb.keyboard.flat().map(b => b.text)) === JSON.stringify(['⚡ Ca của tôi', '📂 Chức năng', '📍 Chấm công']),
  '/start → bàn phím dưới 3 phím, is_persistent', kb?.keyboard?.flat().map(b => b.text).join(' | '));
await sleep(500);
const cmds = (await post(TG + '/botFAKE/getMyCommands', { scope: { type: 'chat', chat_id: String(user) } })).result.map(c => c.command);
ok(JSON.stringify(cmds) === JSON.stringify(['menu', 'ca', 'chamcong', 'help']), 'getMyCommands = đúng 4 lệnh', cmds.join(' '));

// 1 + 2. Điều hướng 5 lần, Quay lại cấp 3 → 2 → 1.
await push(msg({ text: '/menu' }).update);
let p = await settle(x => x && callbacks(x).some(c => c?.startsWith('menu:mod:')), '/menu');
const p0 = p.message_id;
await push(tap('menu:mod:CHUNG', p.message_id));
p = await settle(x => firstLine(x).startsWith('Menu › ') && callbacks(x).includes('menu:act:my_schedule'), 'phân hệ Cá nhân');
const lv2 = firstLine(p);
ok(lv2.split(' › ').length === 2, 'cấp 2 – đường dẫn', lv2);
await push(tap('menu:act:my_schedule', p.message_id));
p = await settle(x => firstLine(x).endsWith('📅 Lịch ca'), 'chức năng Lịch ca');
ok(firstLine(p) === lv2 + ' › 📅 Lịch ca', 'cấp 3 – đường dẫn', firstLine(p));
ok(['nav:back', 'nav:home', 'nav:close'].every(c => callbacks(p).includes(c)), 'hàng cuối [◀ Quay lại] [🏠 Menu] [✖ Đóng]');
await push(tap('nav:back', p.message_id));
p = await settle(x => firstLine(x) === lv2, 'Quay lại → cấp 2');
ok(firstLine(p) === lv2, 'Quay lại từ cấp 3 → cấp 2', firstLine(p));
await push(tap('nav:back', p.message_id));
p = await settle(x => firstLine(x) === 'Menu', 'Quay lại → cấp 1');
ok(firstLine(p) === 'Menu', 'Quay lại từ cấp 2 → cấp 1 (màn đầu)', firstLine(p));
const withButtons = await panels();
ok(withButtons.length === 1 && withButtons[0].message_id === p0, '5 lần điều hướng → chỉ 1 tin của bot có nút (cùng 1 panel)', withButtons.length + ' tin có nút');

// 6. Nhập tự do: /truyxuatlo hỏi mã (ForceReply) → Quay lại → hủy nháp, câu trả lời muộn không được xử lý.
await push(msg({ text: '/truyxuatlo' }).update);
let ask = null;
for (let i = 0; i < 50 && !ask; i++) { ask = (await alive()).find(m => m.reply_markup?.force_reply); if (!ask) await sleep(300); }
ok(!!ask, '/truyxuatlo → câu hỏi ForceReply');
p = await panel();
ok(firstLine(p).includes('Truy xuất') && (p.text || '').includes('✏️'), 'panel hiện bước đang nhập', firstLine(p));
await push(tap('nav:back', p.message_id));
await sleep(1500);
const deleted = (await get('/_deleted')).map(d => d.message_id);
ok(ask && deleted.includes(ask.message_id), 'Quay lại khi đang nhập → xóa câu hỏi (hủy nháp)');
const before = (await get('/_sent')).length;
await push(msg({ text: 'PN-000000-0000', reply_to_message: { message_id: ask?.message_id ?? 0, date: 1, chat } }).update);
await sleep(2000);
const after = (await get('/_sent')).filter(s => String(s.chat_id) === String(user)).slice(-3).map(s => s.text || '');
ok(!after.some(t => t.includes('Truy xuất') && t.includes('PN-000000-0000') && !t.includes('Tra cứu')), 'trả lời câu hỏi đã hủy → không chạy truy xuất', (await get('/_sent')).length - before + ' tin');

// 4. /menu 3 lần → 1 panel ở cuối, tin lệnh bị xóa.
const cmdIds = [];
for (let i = 0; i < 3; i++) { const m = msg({ text: '/menu' }); cmdIds.push(m.id); await push(m.update); await sleep(1200); }
const chatNow = await alive();
const btnNow = chatNow.filter(m => m.reply_markup?.inline_keyboard?.length);
const del2 = (await get('/_deleted')).map(d => d.message_id);
ok(btnNow.length === 1, '/menu 3 lần → còn đúng 1 panel', btnNow.length + ' tin có nút');
ok(chatNow.at(-1)?.message_id === btnNow[0]?.message_id, 'panel nằm cuối chat');
ok(cmdIds.every(id => del2.includes(id)), 'tin lệnh /menu của người dùng bị xóa');

// 5. Bấm nút trên panel cũ → "Menu này đã cũ" + panel mới.
const oldId = p0; // panel đầu tiên – đã bị thay
await push(tap('menu:mod:CHUNG', oldId));
await sleep(1500);
const answers = (await get('/_answers')).map(a => a.text);
ok(answers.includes('Menu này đã cũ'), 'nút trên panel cũ → "Menu này đã cũ"');
const btnAfter = await panels();
ok(btnAfter.length === 1 && btnAfter[0].message_id !== btnNow[0]?.message_id && firstLine(btnAfter[0]) === 'Menu', 'mở panel mới ở màn đầu', btnAfter.map(firstLine).join(' / '));

// Đóng panel + gỡ liên kết thử.
await push(tap('nav:close', btnAfter[0].message_id));
await sleep(1200);
ok((await panels()).length === 0, '✖ Đóng → xóa panel');
await push(msg({ text: '/huylienket' }).update);
await sleep(1500);
console.log(fails ? `\n${fails} bước KHÔNG đạt` : '\nĐạt hết');
process.exitCode = fails ? 1 : 0;
