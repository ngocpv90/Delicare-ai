// BOT-1.6 – kịch bản thử bot Điều hành (DH) trên DB dev: Telegram giả (fake-telegram.mjs 18081) → messaging Polling (CHỈ bật bot DH:
// Telegram__Bots__DH__Token=FAKE, Telegram__Bots__VH__Enabled=false) → api 5125. KHÔNG ghi dữ liệu (chỉ xem).
// Chạy: node tools/bot-smoke/flow-dieuhanh.mjs
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const user = 890000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 1000) + 995000;
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const from = { id: user, is_bot: false, first_name: 'Thử', last_name: 'BOT16' };
const chat = { id: user, type: 'private' };
const msg = (extra) => ({ update_id: ++uid, message: { message_id: uid, date: Math.floor(Date.now() / 1000), from, chat, ...extra } });
let seen = 0;
const fail = (label) => { console.log('✗ ' + label); process.exitCode = 1; };
const BAD = /HTTP \d|Không thực hiện được|lỗi máy chủ|InternalServer/i;
async function wait(pred, label) {
  for (let i = 0; i < 60; i++) {
    const sent = (await (await fetch(TG + '/_sent')).json()).filter(s => String(s.chat_id) === String(user));
    const hit = sent.slice(seen).find(pred);
    if (hit) {
      seen = sent.indexOf(hit) + 1;
      const first = (hit.text || '').split('\n').slice(0, 4).join(' ⏎ ').slice(0, 220);
      if (BAD.test(hit.text || '')) fail(label + ' → ' + first); else console.log('✓ ' + label + ' → ' + first);
      return hit;
    }
    await sleep(400);
  }
  fail(label + ' (không có trả lời)'); return null;
}
const buttons = (m) => m?.reply_markup?.inline_keyboard?.flat() ?? [];
const login = await post(API + '/api/Authentication/Login', { userName: 'admin', passWord: 'admin' });
const auth = { authorization: 'Bearer ' + login.data.token };
const code = (await (await fetch(API + '/api/TelegramLink/CreateCode', { method: 'POST', headers: auth })).json()).data.code;
await post(TG + '/_push', msg({ text: '/start ' + code }));
await wait(s => s.text.includes('Xin chào'), 'Admin liên kết bot DH bằng mã web');
await post(TG + '/_push', msg({ text: '/menu' }));
const menu = await wait(s => s.reply_markup?.inline_keyboard, '/menu (bot DH)');
console.log('  phân hệ: ' + buttons(menu).map(b => b.text).join(' | '));
if (!buttons(menu).some(b => b.callback_data === 'menu:mod:DIEUHANH')) fail('thiếu phân hệ Điều hành');
if (buttons(menu).some(b => b.callback_data === 'menu:mod:BEP')) fail('bot DH lẫn phân hệ Bếp');
for (const [cmd, title] of [['/tongquan', 'Tổng quan'], ['/viecmo', 'Việc đang mở'], ['/kpi', 'KPI 7 ngày'], ['/kpithang', 'KPI tháng'], ['/tiendobep', 'Tiến độ hôm nay'],
  ['/giaovantt', 'Giao vận'], ['/tonthieu', 'NL ngày'], ['/truyxuat', 'Gõ mã chứng từ'], ['/cho', 'Trợ giúp']]) { // /cho: admin không được cấp rõ bước duyệt → ẩn (BOT-1.4) → trợ giúp
  await post(TG + '/_push', msg({ text: cmd }));
  await wait(s => (s.text || '').includes(title) || BAD.test(s.text || ''), cmd);
}
await post(TG + '/_push', msg({ text: '/huylienket' }));
await wait(s => s.text.toLowerCase().includes('hủy liên kết'), '/huylienket (dọn)');
console.log(process.exitCode ? 'KẾT QUẢ: CÓ LỖI' : 'KẾT QUẢ: ĐẠT');
