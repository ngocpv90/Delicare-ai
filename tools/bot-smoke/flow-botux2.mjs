// BOT-UX-2 – kịch bản thử thao tác bot Vận hành (Telegram giả fake-telegram.mjs cổng 18081 → messaging Polling → api 5125, DB dev).
// An toàn với dữ liệu: CHỈ XEM (menu, danh sách mẻ / chuyến, Quay lại, /chamcong) – không bấm nút ghi; cuối kịch bản /huylienket gỡ liên kết thử.
// Kiểm: mỗi màn đúng 1 nút Quay lại, mọi nút bấm có chữ trả lời, không còn ô "·", 3 phím dưới luôn còn (1 tin bàn phím trong chat), /chamcong không đổi bàn phím,
// danh sách ≤ 6 mục / trang.
// Chạy: node tools/bot-smoke/fake-telegram.mjs 18081  +  messaging (Telegram__ApiBaseUrl=http://localhost:18081, Telegram__Bots__VH__Token=FAKE,
//       Telegram__Bots__DH__Enabled=false)  →  node tools/bot-smoke/flow-botux2.mjs
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const user = 870000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 1000) + 800000;
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const get = async (path) => (await fetch(TG + path)).json();
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const from = { id: user, is_bot: false, first_name: 'Thử', last_name: 'BOTUX2' };
const chat = { id: user, type: 'private' };
const msg = (extra) => { const id = ++uid; return { id, update: { update_id: id, message: { message_id: id, date: Math.floor(Date.now() / 1000), from, chat, ...extra } } }; };
const tap = (data, messageId) => ({ update_id: ++uid, callback_query: { id: 'cb' + uid, from, data, message: { message_id: messageId, date: 1, chat } } });
let fails = 0;
const ok = (cond, label, detail = '') => { console.log((cond ? '✓ ' : '✗ ') + label + (detail ? ' – ' + detail : '')); if (!cond) fails++; };
const alive = async () => get('/_chat?chat=' + user);
const panel = async () => (await alive()).filter(m => m.reply_markup?.inline_keyboard?.length).at(-1);
const buttons = (m) => (m?.reply_markup?.inline_keyboard ?? []).flat();
const callbacks = (m) => buttons(m).map(b => b.callback_data);
const firstLine = (m) => (m?.text ?? '').split('\n')[0].replace(/<\/?i>/g, '');
const backs = (m) => buttons(m).filter(b => b.callback_data === 'nav:back' || (b.text || '').includes('Quay lại') || (b.text || '').includes('⬅️')).length;
async function settle(pred, label) {
  for (let i = 0; i < 50; i++) { const p = await panel(); if (pred(p)) return p; await sleep(300); }
  ok(false, label + ' (hết giờ chờ)'); return await panel();
}
async function push(update) { await post(TG + '/_push', update); }
const answersCount = async () => (await get('/_answers')).length;

// 0. Liên kết admin bằng mã web (admin có mọi chức năng → màn đầu theo phân hệ).
const login = await post(API + '/api/Authentication/Login', { userName: 'admin', passWord: 'admin' });
const token = login.data.token;
const code = (await (await fetch(API + '/api/TelegramLink/CreateCode', { method: 'POST', headers: { authorization: 'Bearer ' + token } })).json()).data.code;
await push(msg({ text: '/start ' + code }).update);
await sleep(2500);

// 1. /menu: không còn ô "·", màn đầu không có Quay lại, 3 phím dưới được gửi lại – chỉ 1 tin bàn phím còn trong chat.
await push(msg({ text: '/menu' }).update);
let p = await settle(x => x && callbacks(x).some(c => c?.startsWith('menu:mod:')), '/menu');
ok(!buttons(p).some(b => b.text === '·'), 'màn đầu không còn ô "·"');
ok(backs(p) === 0, 'màn đầu không có nút Quay lại', backs(p) + ' nút');
await push(msg({ text: '/menu' }).update);
p = await settle(x => x && x.message_id !== p.message_id, '/menu lần 2');
// Tin chào khi liên kết (/start) cũng mang bàn phím nhưng là lời chào – giữ lại; chỉ tin "⌨️ Phím dưới" không được chồng (tin cũ bị xóa).
const kbs = (await alive()).filter(m => m.reply_markup?.keyboard && (m.text || '').startsWith('⌨️ Phím dưới'));
ok(kbs.length === 1, '/menu gửi lại 3 phím – chỉ còn 1 tin "Phím dưới"', kbs.length + ' tin');
ok(JSON.stringify(kbs[0]?.reply_markup?.keyboard?.flat().map(b => b.text)) === JSON.stringify(['⚡ Ca của tôi', '📂 Chức năng', '📍 Chấm công']), 'bàn phím dưới đúng 3 phím');

// 2. Bếp → Mẻ cần làm → (thẻ mẻ đầu tiên nếu có) → ◀ ◀ ◀: mỗi màn đúng 1 nút Quay lại, mỗi lần bấm có chữ trả lời.
let taps = 0;
const before = await answersCount();
await push(tap('menu:mod:BEP', p.message_id)); taps++;
p = await settle(x => firstLine(x).includes('Bếp') && callbacks(x).includes('menu:act:bep_batches'), 'phân hệ Bếp');
ok(backs(p) === 1, 'phân hệ Bếp: đúng 1 nút Quay lại', backs(p) + ' nút');
await push(tap('menu:act:bep_batches', p.message_id)); taps++;
p = await settle(x => firstLine(x).endsWith('Mẻ cần làm'), 'Mẻ cần làm');
ok(backs(p) === 1, 'danh sách mẻ: đúng 1 nút Quay lại', backs(p) + ' nút');
ok(callbacks(p).filter(c => c?.startsWith('bat:view:')).length <= 6, 'danh sách ≤ 6 mẻ / trang');
const card = callbacks(p).find(c => c?.startsWith('bat:view:'));
if (card) {
  await push(tap(card, p.message_id)); taps++;
  p = await settle(x => firstLine(x).split(' › ').length === 4, 'thẻ mẻ');
  ok(backs(p) === 1, 'thẻ mẻ: đúng 1 nút Quay lại', firstLine(p));
  const tn = callbacks(p).find(c => c?.startsWith('bat:tn:'));
  if (tn) {
    await push(tap(tn, p.message_id)); taps++;
    p = await settle(x => (x?.text || '').includes('👉'), 'màn số nhiệt độ');
    ok(backs(p) === 1 && callbacks(p).some(c => c?.startsWith('bat:tv:')), 'màn số: 1 nút Quay lại + nút xác nhận');
    await push(tap('nav:back', p.message_id)); taps++;
    p = await settle(x => firstLine(x).split(' › ').length === 4 && !(x?.text || '').includes('👉'), '◀ về thẻ mẻ');
    ok(true, '◀ từ màn số → về thẻ mẻ (không ghi)', firstLine(p));
  }
  await push(tap('nav:back', p.message_id)); taps++;
  p = await settle(x => firstLine(x).endsWith('Mẻ cần làm'), '◀ về danh sách');
}
await push(tap('nav:back', p.message_id)); taps++;
p = await settle(x => callbacks(x).includes('menu:act:bep_batches'), '◀ về phân hệ');
await sleep(800);
const answers = (await get('/_answers')).slice(before);
ok(answers.length >= taps && answers.every(a => a.text && a.text.trim().length > 0), 'mọi nút bấm có chữ trả lời', answers.map(a => a.text).join(' | '));

// 3. /chamcong: chỉ nhắc bấm 📍 trên panel – không gửi bàn phím 1 nút.
const sentBefore = (await get('/_sent')).length;
await push(msg({ text: '/chamcong' }).update);
p = await settle(x => (x?.text || '').includes('📍'), '/chamcong');
const newKbs = (await get('/_sent')).slice(sentBefore).filter(s => s.reply_markup?.keyboard);
ok(newKbs.every(s => s.reply_markup.keyboard.flat().length === 3), '/chamcong không đổi bàn phím dưới (vẫn 3 phím)', newKbs.length + ' tin bàn phím mới');

// Đóng panel + gỡ liên kết thử.
await push(tap('nav:close', p.message_id));
await sleep(1200);
await push(msg({ text: '/huylienket' }).update);
await sleep(1500);
console.log(fails ? `\n${fails} bước KHÔNG đạt` : '\nĐạt hết');
process.exitCode = fails ? 1 : 0;
