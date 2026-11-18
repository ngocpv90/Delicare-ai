// BOT-1.4 – kịch bản thử khung trải nghiệm + duyệt trên DB dev (Telegram giả fake-telegram.mjs cổng 18081 → messaging Polling → api 5125).
// An toàn với dữ liệu: KHÔNG ghi quyết định duyệt (chỉ xem, bấm duyệt rồi hoàn tác ngay). Chạy: node tools/bot-smoke/flow-experience.mjs
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const user = 870000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 1000) + 500000;
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const from = { id: user, is_bot: false, first_name: 'Thử', last_name: 'BOT14' };
const chat = { id: user, type: 'private' };
const msg = (extra) => ({ update_id: ++uid, message: { message_id: uid, date: Math.floor(Date.now() / 1000), from, chat, ...extra } });
const tap = (data, messageId) => ({ update_id: ++uid, callback_query: { id: 'cb' + uid, from, data, message: { message_id: messageId, date: 1, chat } } });
let seen = 0;
async function wait(pred, label) {
  for (let i = 0; i < 60; i++) {
    const sent = (await (await fetch(TG + '/_sent')).json()).filter(s => String(s.chat_id) === String(user));
    const hit = sent.slice(seen).find(pred);
    if (hit) { seen = sent.indexOf(hit) + 1; console.log('✓ ' + label + ' → ' + (hit.text || '').split('\n')[0].slice(0, 90)); return hit; }
    await sleep(400);
  }
  console.log('✗ ' + label); process.exitCode = 1; return null;
}
const login = await post(API + '/api/Authentication/Login', { userName: 'admin', passWord: 'admin' });
const token = login.data.token;
const code = (await (await fetch(API + '/api/TelegramLink/CreateCode', { method: 'POST', headers: { authorization: 'Bearer ' + token } })).json()).data.code;
await post(TG + '/_push', msg({ text: '/start ' + code }));
const hello = await wait(s => s.text.includes('Xin chào'), 'Admin /start <mã web> → liên kết Active');
const kb = hello?.reply_markup?.keyboard?.flat().map(b => b.text) ?? [];
console.log('  bàn phím: ' + kb.join(' | '));
await post(TG + '/_push', msg({ text: '/menu' }));
const menu = await wait(s => s.reply_markup?.inline_keyboard, '/menu → lưới phân hệ (nút trong tin)');
console.log('  phân hệ: ' + (menu?.reply_markup?.inline_keyboard.flat().map(b => b.text).join(' | ') ?? ''));
const canApprove = kb.includes('⏳ Chờ tôi duyệt') || (menu?.reply_markup?.inline_keyboard.flat().some(b => b.callback_data === 'menu:mod:DUYET') ?? false);
await post(TG + '/_push', msg({ text: '/cho' }));
const list = canApprove
  ? await wait(s => s.text.includes('Chờ tôi duyệt') || s.text.includes('Không có yêu cầu'), '/cho → Chờ tôi duyệt (chỉ bước được cấp rõ)')
  : await wait(s => s.text.includes('Trợ giúp'), '/cho khi KHÔNG có mục Duyệt được cấp rõ (admin không bị ngập) → lệnh ẩn, trả trợ giúp');
const view = list?.reply_markup?.inline_keyboard?.flat().find(b => b.callback_data?.startsWith('apv:view:'));
if (view) {
  await post(TG + '/_push', tap(view.callback_data, 1));
  const card = await wait(s => s.method === 'editMessageText' && s.text.includes('Mức rủi ro') || s.text.includes('không duyệt được'), 'Xem chi tiết 1 yêu cầu');
  const ok = card?.reply_markup?.inline_keyboard?.flat().find(b => b.callback_data?.startsWith('apv:ok:'));
  if (ok) {
    await post(TG + '/_push', tap(ok.callback_data, 1));
    const step = await wait(s => s.method === 'editMessageText' && (s.text.includes('Đang duyệt') || s.text.includes('Xác nhận duyệt')), 'Bấm Duyệt → hoàn tác (thấp) / xác nhận 2 bước (cao)');
    const undo = step?.reply_markup?.inline_keyboard?.flat().find(b => b.callback_data?.startsWith('undo:'));
    if (undo) {
      await post(TG + '/_push', tap(undo.callback_data, 1));
      await wait(s => s.text.includes('Đã hoàn tác'), 'Hoàn tác trong thời gian chờ → không ghi quyết định');
    } else console.log('  (rủi ro cao – không bấm Xác nhận để không đổi dữ liệu)');
  }
} else console.log('  (không có yêu cầu chờ duyệt rõ cho admin trên DB dev)');
await post(TG + '/_push', msg({ location: { latitude: 10.77, longitude: 106.7 }, forward_origin: { type: 'user', date: 1 } }));
await wait(s => s.text.includes('chuyển tiếp'), 'Vị trí chuyển tiếp → từ chối');
await post(TG + '/_push', { update_id: ++uid, message: { message_id: uid, date: Math.floor(Date.now() / 1000) - 600, from, chat, location: { latitude: 10.77, longitude: 106.7 } } });
await wait(s => s.text.includes('đã cũ'), 'Vị trí cũ 10 phút → từ chối');
await post(TG + '/_push', msg({ text: 'PO-261118-0001' }));
await wait(s => s.text.includes('Kết quả') || s.text.includes('Không tìm thấy'), 'Gõ mã PO-… → Common/GlobalSearch bằng JWT user');
await post(TG + '/_push', msg({ text: '/huylienket' }));
await wait(s => s.text.includes('Đã hủy liên kết'), '/huylienket');
const sent = await (await fetch(TG + '/_sent')).json();
console.log('  setMyCommands theo chat: ' + (sent.some(s => s.method === 'setMyCommands') ? 'có' : '(fake không ghi – xem log messaging)'));
console.log(process.exitCode ? 'KẾT QUẢ: CÓ LỖI' : 'KẾT QUẢ: ĐẠT');
