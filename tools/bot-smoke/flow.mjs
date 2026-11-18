// BOT-1.3 – kịch bản thử container messaging trên DB dev: Telegram giả (fake-telegram.mjs, cổng 18081) → messaging (Polling) → api → DB dev.
// Chạy: node tools/bot-smoke/flow.mjs <số điện thoại KHÔNG trùng của 1 tài khoản / nhân viên>. Xem deploy/README.md mục 12.
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const phone = process.argv[2];
const user = 880000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 1000);
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const msg = (extra) => ({ update_id: ++uid, message: { message_id: uid, date: 1, from: { id: user, is_bot: false, first_name: 'Thử', last_name: 'BOT13' }, chat: { id: user, type: 'private' }, ...extra } });
let seen = 0;
async function waitSent(pred, label) {
  for (let i = 0; i < 60; i++) {
    const sent = (await (await fetch(TG + '/_sent')).json()).filter(s => String(s.chat_id) === String(user));
    const hit = sent.slice(seen).find(pred);
    if (hit) seen = sent.indexOf(hit) + 1;
    if (hit) { console.log('✓ ' + label + ' → ' + hit.text.split('\n')[0].slice(0, 90)); return hit; }
    await sleep(500);
  }
  console.log('✗ ' + label + ' (không thấy tin trả lời)'); process.exitCode = 1; return null;
}
let n = 0;
const after = () => n;
await post(TG + '/_push', msg({ text: '/start CHAMCONG' }));
await waitSent(s => s.reply_markup?.keyboard?.[0]?.[0]?.request_contact === true, 'QR cổng: chưa liên kết → nút chia sẻ số điện thoại');
await post(TG + '/_push', msg({ contact: { phone_number: phone, user_id: user + 1 } }));
await waitSent(s => s.text.includes('của chính bạn'), 'Chia sẻ số của người khác → từ chối');
await post(TG + '/_push', msg({ contact: { phone_number: '0900000001', user_id: user } }));
await waitSent(s => s.text.includes('trùng nhiều'), 'Số nhân viên bị trùng 4 hồ sơ → từ chối, gợi ý liên hệ Nhân sự');
await post(TG + '/_push', msg({ contact: { phone_number: phone, user_id: user } }));
const hello = await waitSent(s => s.text.includes('Xin chào'), 'Chia sẻ số của chính mình → liên kết + chào');
if (hello && !hello.text.includes('quyền duyệt')) { console.log('✗ admin phải được nhắc nhập mã (PendingCode)'); process.exitCode = 1; } else if (hello) console.log('✓ admin chỉ khớp số → nhắc lấy mã web (PendingCode)');
await post(TG + '/_push', msg({ text: '⚡ Ca của tôi' }));
await waitSent(s => s.text.length > 0, 'Nút ⚡ Ca của tôi (JWT bot hạn chế → AttendanceEvent/GetMine)');
// Web gửi tay 1 tin qua outbox cho nhân viên vừa liên kết → OutboundPump gửi.
const login = await post(API + '/api/Authentication/Login', { userName: 'admin', passWord: 'admin' });
const token = login.data.token;
const ids = await (await fetch(API + '/api/UserExternalIdentity/GetAllData', { headers: { authorization: 'Bearer ' + token } })).json();
const identity = ids.listData.find(i => i.externalUserId === String(user));
console.log('  định danh: ' + identity.identityStatus + ', EmployeeId ' + identity.employeeId + ', UserId ' + identity.userId);
const form = new URLSearchParams({ BotCode: 'VH', Text: 'Tin thử BOT-1.3 qua outbox' });
if (identity.userId) form.set('UserId', identity.userId); else form.set('EmployeeId', identity.employeeId);
const enq = await (await fetch(API + '/api/OutboundMessage/Enqueue', { method: 'POST', headers: { authorization: 'Bearer ' + token }, body: form })).json();
console.log('  Enqueue: ' + enq.responseCode + ' ' + enq.responseMessage);
const out = await waitSent(s => s.text.includes('Tin thử BOT-1.3 qua outbox'), 'Outbox → OutboundPump gửi tin (THƯỜNG = im lặng)');
if (out && out.disable_notification !== true) { console.log('✗ tin THƯỜNG phải disable_notification'); process.exitCode = 1; }
await sleep(1500);
const conv = await (await fetch(API + '/api/OutboundMessage/GetConversation?IdentityId=' + identity.id, { headers: { authorization: 'Bearer ' + token } })).json();
const statuses = conv.data.items.map(i => i.direction + ':' + i.status);
console.log('  hội thoại: ' + statuses.join(', '));
if (!statuses.includes('OUT:Sent') || statuses.some(s => s === 'IN:Received' || s === 'IN:Processing')) { console.log('✗ trạng thái hội thoại'); process.exitCode = 1; } else console.log('✓ tin vào Processed, tin ra Sent');
await post(TG + '/_push', msg({ text: '/huylienket' }));
await waitSent(s => s.text.includes('Đã hủy liên kết'), '/huylienket → gỡ');
console.log(process.exitCode ? 'KẾT QUẢ: CÓ LỖI' : 'KẾT QUẢ: ĐẠT');
