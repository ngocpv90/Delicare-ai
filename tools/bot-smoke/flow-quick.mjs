// AI-25.3 (D-143) – bot 💬 Hỏi nhanh (không AI) trên DB dev: Telegram giả (fake-telegram.mjs 18081) → messaging Polling (bot VH và/hoặc DH, Messaging__WebUrl=https://…)
// → api 5125. Tài khoản thử KHÔNG phải admin (mặc định uat.ops-wh – quyền thật): liên kết bằng mã web → /hoinhanh → chọn mảng → chạy câu → câu có ô nhập → hủy liên kết.
// Chạy: DC_USER=uat.ops-wh DC_PASS=<mật khẩu> BOT=VH node tools/bot-smoke/flow-quick.mjs   (mật khẩu chỉ đọc từ biến môi trường, không in ra).
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const USER = process.env.DC_USER || 'uat.ops-wh', BOT = process.env.BOT || 'VH';
const user = 880000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 100); // tăng nhanh hơn số update mỗi lượt chạy → chạy liền nhiều lượt không bị Telegram giả bỏ qua (offset)
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const from = { id: user, is_bot: false, first_name: 'Thử', last_name: 'AI253' };
const chat = { id: user, type: 'private' };
const msg = (extra) => ({ update_id: ++uid, message: { message_id: uid, date: Math.floor(Date.now() / 1000), from, chat, ...extra } });
const tap = (data, messageId = 1) => ({ update_id: ++uid, callback_query: { id: 'cb' + uid, from, data, message: { message_id: messageId, date: 1, chat } } });
let seen = 0;
const fail = (label) => { console.log('✗ ' + label); process.exitCode = 1; };
const BAD = /HTTP \d|Không thực hiện được|lỗi máy chủ|InternalServer|chưa có quyền/i;
async function wait(pred, label) {
  for (let i = 0; i < 60; i++) {
    const sent = (await (await fetch(TG + '/_sent')).json()).filter(s => String(s.chat_id) === String(user));
    const hit = sent.slice(seen).find(pred);
    if (hit) {
      seen = sent.indexOf(hit) + 1;
      if (BAD.test(hit.text || '')) fail(label + ' → ' + (hit.text || '').slice(0, 200)); else console.log('✓ ' + label + '\n    ' + (hit.text || '').split('\n').slice(0, 14).join('\n    '));
      return hit;
    }
    await sleep(400);
  }
  fail(label + ' (không có trả lời)'); return null;
}
const buttons = (m) => m?.reply_markup?.inline_keyboard?.flat() ?? [];
const login = await post(API + '/api/Authentication/Login', { userName: USER, passWord: process.env.DC_PASS });
if (!login.data?.token) { console.log('✗ không đăng nhập được ' + USER); process.exit(1); }
const auth = { authorization: 'Bearer ' + login.data.token };
const code = (await (await fetch(API + '/api/TelegramLink/CreateCode', { method: 'POST', headers: auth })).json()).data.code;
await post(TG + '/_push', msg({ text: '/start ' + code }));
await wait(s => (s.text || '').includes('Xin chào'), `${USER} liên kết bot ${BOT} bằng mã web`);
await post(TG + '/_push', msg({ text: '/menu' }));
const menu = await wait(s => s.reply_markup?.inline_keyboard, '/menu');
if (!buttons(menu).some(b => b.text.includes('Hỏi nhanh'))) fail('menu thiếu 💬 Hỏi nhanh');
await post(TG + '/_push', msg({ text: '/hoinhanh' }));
let list = await wait(s => (s.text || '').includes('Hỏi nhanh') && s.reply_markup?.inline_keyboard, '/hoinhanh');
const ag = buttons(list).find(b => b.callback_data === 'qk:ag:' + process.env.DC_AGENT) || buttons(list).find(b => b.callback_data?.startsWith('qk:ag:')); // DC_AGENT=VIVI chọn mảng
if (ag) {
  console.log('  mảng: ' + buttons(list).map(b => b.text).join(' | '));
  await post(TG + '/_push', tap(ag.callback_data, list.message_id));
  list = await wait(s => buttons(s).some(b => b.callback_data?.startsWith('qk:q:')), 'chọn ' + ag.text);
}
const qs = buttons(list).filter(b => b.callback_data?.startsWith('qk:q:'));
console.log('  câu: ' + qs.map(b => b.text).join(' | '));
for (const q of qs.slice(0, 4)) {
  await post(TG + '/_push', tap(q.callback_data, list.message_id));
  const r = await wait(s => (s.text || '').includes('không dùng AI') || (s.text || '').startsWith('💬') || BAD.test(s.text || ''), q.text);
  if (r && (r.text || '').startsWith('💬') && (r.text || '').includes('Gõ ')) {
    await post(TG + '/_push', msg({ text: process.env.DC_INPUT || 'thit', reply_to_message: { message_id: r.message_id, date: 1, chat } }));
    await wait(s => (s.text || '').includes('không dùng AI') || BAD.test(s.text || '') || (s.text || '').includes('Nhập'), q.text + ' (đã gõ)');
  } else if (r) {
    const web = buttons(r).find(b => b.url);
    console.log('    nút: ' + buttons(r).map(b => b.text + (b.url ? ' → ' + b.url : '')).join(' | '));
    if (!web && process.env.EXPECT_WEB) fail('thiếu nút Mở trên web');
  }
}
await post(TG + '/_push', msg({ text: '/huylienket' }));
await wait(s => (s.text || '').toLowerCase().includes('hủy liên kết'), '/huylienket (dọn)');
console.log(process.exitCode ? 'KẾT QUẢ: CÓ LỖI' : 'KẾT QUẢ: ĐẠT');
