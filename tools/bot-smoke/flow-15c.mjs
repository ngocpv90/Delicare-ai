// BOT-1.5c – kịch bản thử bot Vận hành (đổi ca, phản ánh, nhân sự, chức năng chỉ xem Kho / Kế hoạch / Kinh doanh / Kế toán / QA / Quản trị) trên DB dev:
// Telegram giả (fake-telegram.mjs 18081) → messaging Polling → api 5125. KHÔNG ghi dữ liệu: chỉ gửi lệnh xem; đổi ca dừng trước bước lý do; phản ánh chỉ mở thẻ.
// Chạy: node tools/bot-smoke/flow-15c.mjs
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const user = 880000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 1000) + 990000;
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const from = { id: user, is_bot: false, first_name: 'Thử', last_name: 'BOT15c' };
const chat = { id: user, type: 'private' };
const msg = (extra) => ({ update_id: ++uid, message: { message_id: uid, date: Math.floor(Date.now() / 1000), from, chat, ...extra } });
const tap = (data, messageId = 1) => ({ update_id: ++uid, callback_query: { id: 'cb' + uid, from, data, message: { message_id: messageId, date: 1, chat } } });
let seen = 0;
const fail = (label) => { console.log('✗ ' + label); process.exitCode = 1; };
const BAD = /HTTP \d|Không thực hiện được|không lấy được|lỗi máy chủ|InternalServer/i;
async function wait(pred, label) {
  for (let i = 0; i < 60; i++) {
    const sent = (await (await fetch(TG + '/_sent')).json()).filter(s => String(s.chat_id) === String(user));
    const hit = sent.slice(seen).find(pred);
    if (hit) {
      seen = sent.indexOf(hit) + 1;
      const first = (hit.text || '').split('\n').slice(0, 3).join(' ⏎ ').slice(0, 170);
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
await wait(s => s.text.includes('Xin chào'), 'Admin liên kết bằng mã web');
await post(TG + '/_push', msg({ text: '/menu' }));
const menu = await wait(s => s.reply_markup?.inline_keyboard, '/menu');
console.log('  phân hệ: ' + buttons(menu).map(b => b.text).join(' | '));
for (const m of ['KHO', 'KEHOACH', 'KINHDOANH', 'CSKH', 'KETOAN', 'NHANSU', 'QUANTRI']) if (!buttons(menu).some(b => b.callback_data === 'menu:mod:' + m)) fail('thiếu phân hệ ' + m);

const views = [
  ['/ton', 'Tồn dưới mức'], ['/candate', 'cận / hết hạn'], ['/nlmai', 'NL ngày'], ['/po', 'PO sắp về'], ['/hangve', 'Hàng về hôm nay'],
  ['/kehoach', 'Kế hoạch'], ['/thucdon', 'Thực đơn'], ['/haohut', 'Hao hụt'], ['/giathanh', 'Giá thành'],
  ['/donmai', 'Đơn ngày'], ['/chuachot', 'Chưa chốt'], ['/thatthoat', 'thất thoát'], ['/podanhan', 'PO đã nhận'],
  ['/chokiem', 'chờ kiểm'], ['/canhbao', 'Cảnh báo ATTP'], ['/hethong', 'Job tự động'], ['/chuavao', 'Chưa vào ca'],
];
for (const [cmd, title] of views) {
  await post(TG + '/_push', msg({ text: cmd }));
  await wait(s => (s.text || '').includes(title) || BAD.test(s.text || ''), cmd);
}
await post(TG + '/_push', msg({ text: '/phananh' }));
const pa = await wait(s => s.text.includes('Phản ánh đang mở'), '/phananh → CustomerComplaint/GetList');
const paBtn = buttons(pa).find(b => b.callback_data?.startsWith('cmp:v:'));
if (paBtn) {
  await post(TG + '/_push', tap(paBtn.callback_data, 5));
  await wait(s => s.method === 'editMessageText' && s.text.includes('Phụ trách'), 'Thẻ phản ánh (chỉ xem) → nút ' + '…');
}
await post(TG + '/_push', msg({ text: '/saphan' }));
await wait(s => s.text.includes('quá hạn'), '/saphan');
await post(TG + '/_push', msg({ text: '/doica' }));
await wait(s => s.text.includes('Đổi ca') || s.text.includes('nhân viên'), '/doica → ShiftAssignment/GetMine (admin không gắn nhân viên → lời báo)');
// GetSwapCandidates: Id lạ → báo không tìm thấy (admin không gắn nhân viên → báo chưa gắn).
const sc = await (await fetch(API + '/api/ShiftAssignment/GetSwapCandidates?assignmentId=' + crypto.randomUUID(), { headers: auth })).json();
console.log((sc.responseCode !== 500 ? '✓' : '✗') + ' api GetSwapCandidates (admin) → ' + (sc.responseMessage ?? sc.responseCode));
await post(TG + '/_push', msg({ text: '/huylienket' }));
await wait(s => s.text.toLowerCase().includes('hủy liên kết'), '/huylienket (dọn)');
console.log(process.exitCode ? 'KẾT QUẢ: CÓ LỖI' : 'KẾT QUẢ: ĐẠT');
