// BOT-1.5a – kịch bản thử bot Vận hành (chung + Bếp + ATTP) trên DB dev: Telegram giả (fake-telegram.mjs 18081) → messaging Polling → api 5125.
// KHÔNG ghi dữ liệu nghiệp vụ: chỉ xem mẻ / tiến độ / lịch ca / checklist (không Hoàn tất); xin nghỉ bằng admin (không gắn nhân viên) → api từ chối có lời báo.
// Chạy: node tools/bot-smoke/flow-vanhanh.mjs
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const user = 860000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 1000) + 900000;
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const from = { id: user, is_bot: false, first_name: 'Thử', last_name: 'BOT15' };
const chat = { id: user, type: 'private' };
const msg = (extra) => ({ update_id: ++uid, message: { message_id: uid, date: Math.floor(Date.now() / 1000), from, chat, ...extra } });
const tap = (data, messageId = 1) => ({ update_id: ++uid, callback_query: { id: 'cb' + uid, from, data, message: { message_id: messageId, date: 1, chat } } });
let seen = 0;
let lastSent = null;
async function wait(pred, label) {
  for (let i = 0; i < 60; i++) {
    const sent = (await (await fetch(TG + '/_sent')).json()).filter(s => String(s.chat_id) === String(user));
    const hit = sent.slice(seen).find(pred);
    if (hit) { seen = sent.indexOf(hit) + 1; lastSent = hit; console.log('✓ ' + label + ' → ' + (hit.text || '').split('\n')[0].slice(0, 100)); return hit; }
    await sleep(400);
  }
  console.log('✗ ' + label); process.exitCode = 1; return null;
}
const buttons = (m) => m?.reply_markup?.inline_keyboard?.flat() ?? [];
const login = await post(API + '/api/Authentication/Login', { userName: 'admin', passWord: 'admin' });
const code = (await (await fetch(API + '/api/TelegramLink/CreateCode', { method: 'POST', headers: { authorization: 'Bearer ' + login.data.token } })).json()).data.code;
await post(TG + '/_push', msg({ text: '/start ' + code }));
await wait(s => s.text.includes('Xin chào'), 'Admin liên kết bằng mã web');
await post(TG + '/_push', msg({ text: '/menu' }));
const menu = await wait(s => s.reply_markup?.inline_keyboard, '/menu');
const mods = buttons(menu).map(b => b.callback_data);
console.log('  phân hệ: ' + buttons(menu).map(b => b.text).join(' | '));
if (!mods.includes('menu:mod:BEP') || !mods.includes('menu:mod:ATTP')) { console.log('✗ thiếu phân hệ Bếp / ATTP'); process.exitCode = 1; }
await post(TG + '/_push', msg({ text: '/me' }));
const batches = await wait(s => s.text.includes('Mẻ cần làm'), '/me → 🍳 Mẻ cần làm hôm nay (ProductionBatch/GetBoard)');
const first = buttons(batches).find(b => b.callback_data?.startsWith('bat:view:'));
if (first) {
  await post(TG + '/_push', tap(first.callback_data));
  await wait(s => s.method === 'editMessageText' && s.text.includes('Nhiệt độ lõi'), 'Thẻ mẻ (chỉ xem)');
} else console.log('  (DB dev không có mẻ hôm nay – chỉ kiểm danh sách rỗng)');
await post(TG + '/_push', msg({ text: '/tiendo' }));
await wait(s => s.text.includes('Tiến độ hôm nay'), '/tiendo → 📈 Tiến độ ca');
await post(TG + '/_push', msg({ text: '/checklist' }));
const lists = await wait(s => s.text.includes('Chọn checklist'), '/checklist → danh sách bộ checklist');
const open = buttons(lists).find(b => b.callback_data?.startsWith('chk:open:'));
if (open) {
  await post(TG + '/_push', tap(open.callback_data, 2));
  const run = await wait(s => s.method === 'editMessageText' && (s.reply_markup?.inline_keyboard?.length ?? 0) > 0, 'Mở checklist trong 1 tin (GetRun) – không Hoàn tất');
  console.log('  mục: ' + buttons(run).slice(0, 4).map(b => b.text).join(' | '));
}
await post(TG + '/_push', msg({ text: '/lichca' }));
await wait(s => s.text.includes('Lịch ca') || s.text.includes('nhân viên'), '/lichca → ShiftAssignment/GetMine (admin không gắn nhân viên → lời báo)');
await post(TG + '/_push', msg({ text: '/nghi' }));
const types = await wait(s => s.text.includes('chọn loại nghỉ'), '/nghi → loại nghỉ (BotChannel/GetLeaveTypes)');
const t = buttons(types)[0];
if (t) {
  await post(TG + '/_push', tap(t.callback_data, 3));
  await wait(s => s.method === 'editMessageText' && s.text.includes('Nghỉ từ ngày nào'), 'Chọn loại → hỏi ngày (sửa cùng tin)');
  await post(TG + '/_push', tap('lv:d:2', 3));
  await wait(s => s.text.includes('Nghỉ bao nhiêu ngày'), 'Chọn ngày kia → hỏi số ngày');
  await post(TG + '/_push', tap('lv:n:1', 3));
  const ask = await wait(s => s.text.includes('Lý do nghỉ'), 'Chọn 1 ngày → hỏi lý do (ForceReply)');
  const sent = await (await fetch(TG + '/_sent')).json();
  const askId = 1000 + sent.length; // fake-telegram đánh message_id tăng dần từ 1001 theo thứ tự gửi
  await post(TG + '/_push', msg({ text: 'Thử BOT-1.5a', reply_to_message: { message_id: askId, date: 1, chat } }));
  await wait(s => s.text.includes('đơn nghỉ'), 'Gửi lý do → LeaveRequest/Create (admin không gắn nhân viên → api từ chối, không tạo đơn)');
}
await post(TG + '/_push', msg({ text: '/huylienket' }));
await wait(s => s.text.includes('Đã hủy liên kết'), '/huylienket');
console.log(process.exitCode ? 'KẾT QUẢ: CÓ LỖI' : 'KẾT QUẢ: ĐẠT');
