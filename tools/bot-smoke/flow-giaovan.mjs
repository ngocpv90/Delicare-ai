// BOT-1.5b – kịch bản thử bot Vận hành (Giao vận + Điểm phát) trên DB dev: Telegram giả (fake-telegram.mjs 18081) → messaging Polling → api 5125.
// KHÔNG ghi dữ liệu nghiệp vụ: chỉ xem chuyến / điểm / đơn chờ đối soát (không Xuất phát / Giao / Đối soát); admin không gắn nhân viên → "Chuyến của tôi" báo rõ.
// Thêm: gọi thẳng api (JWT admin) ngày gần nhất có chuyến để kiểm hình dạng JSON mà bot đọc (route / listStop / allowedActions / deliveryTempMinC).
// Chạy: node tools/bot-smoke/flow-giaovan.mjs
const TG = 'http://localhost:18081', API = 'http://localhost:5125';
const user = 870000 + Math.floor(Math.random() * 99999);
let uid = Math.floor(Date.now() / 1000) + 950000;
const sleep = (ms) => new Promise(r => setTimeout(r, ms));
const post = async (url, body, headers = {}) => (await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) })).json();
const from = { id: user, is_bot: false, first_name: 'Thử', last_name: 'BOT15b' };
const chat = { id: user, type: 'private' };
const msg = (extra) => ({ update_id: ++uid, message: { message_id: uid, date: Math.floor(Date.now() / 1000), from, chat, ...extra } });
const tap = (data, messageId = 1) => ({ update_id: ++uid, callback_query: { id: 'cb' + uid, from, data, message: { message_id: messageId, date: 1, chat } } });
let seen = 0;
const fail = (label) => { console.log('✗ ' + label); process.exitCode = 1; };
async function wait(pred, label) {
  for (let i = 0; i < 60; i++) {
    const sent = (await (await fetch(TG + '/_sent')).json()).filter(s => String(s.chat_id) === String(user));
    const hit = sent.slice(seen).find(pred);
    if (hit) { seen = sent.indexOf(hit) + 1; console.log('✓ ' + label + ' → ' + (hit.text || '').split('\n')[0].slice(0, 110)); return hit; }
    await sleep(400);
  }
  fail(label); return null;
}
const buttons = (m) => m?.reply_markup?.inline_keyboard?.flat() ?? [];
const login = await post(API + '/api/Authentication/Login', { userName: 'admin', passWord: 'admin' });
const auth = { authorization: 'Bearer ' + login.data.token };
const get = async (path) => (await fetch(API + path, { headers: auth })).json();

// 1) Hình dạng JSON api (ngày gần nhất có chuyến trong DB dev).
// Tham số (tùy chọn): <Id chuyến> <ngày yyyy-MM-dd> – vd. chuyến mới nhất: SELECT TOP 1 Id, DeliveryDate FROM DeliveryRoute WHERE IsDeleted = 0 ORDER BY DeliveryDate DESC.
const anyRoute = process.argv[2] ? { id: process.argv[2].toLowerCase(), deliveryDate: process.argv[3] } : null;
if (anyRoute) {
  const day = String(anyRoute.deliveryDate).slice(0, 10);
  const board = await get('/api/DeliveryRoute/GetBoard?DeliveryDate=' + day);
  const r0 = board.data?.listRoute?.[0];
  if (r0?.route?.id && Array.isArray(r0.listStop) && Array.isArray(r0.allowedActions)) console.log(`✓ GetBoard ${day}: ${board.data.listRoute.length} chuyến, chuyến đầu ${r0.route.routeNo ?? r0.route.code} – ${r0.listStop.length} điểm, thao tác [${r0.allowedActions.join(', ')}]`);
  else fail('GetBoard ' + day + ' – thiếu route / listStop / allowedActions');
  const detail = await get('/api/DeliveryRoute/GetDetail?id=' + anyRoute.id);
  if (detail.data?.deliveryTempMinC !== undefined && Array.isArray(detail.data?.listBlocker)) console.log(`✓ GetDetail: ngưỡng ${detail.data.deliveryTempMinC} °C, chờ xuất phát thiếu ${detail.data.listBlocker.length} điều kiện`);
  else fail('GetDetail – thiếu deliveryTempMinC / listBlocker');
  const stop = detail.data?.listStop?.[0];
  if (stop) {
    const byId = await get('/api/DeliveryStop/GetById?id=' + stop.id);
    if (byId.data?.deliveryRouteId === anyRoute.id) console.log('✓ DeliveryStop/GetById → deliveryRouteId khớp chuyến');
    else fail('DeliveryStop/GetById – deliveryRouteId');
  }
  const mine = await get('/api/DeliveryRoute/GetMine?deliveryDate=' + day);
  console.log((mine.responseCode === 200 || /nhân viên/.test(mine.responseMessage ?? '') ? '✓' : '✗') + ' GetMine (admin) → ' + (mine.responseMessage ?? mine.responseCode));
} else console.log('  (DB dev chưa có chuyến nào – bỏ qua kiểm hình dạng JSON)');
const pending = await get('/api/OrderReconciliation/GetPendingOrders');
console.log((pending.responseCode === 200 ? '✓' : '✗') + ` GetPendingOrders: ${pending.listData?.length ?? 0} đơn chờ đối soát`);

// 2) Bot.
const code = (await (await fetch(API + '/api/TelegramLink/CreateCode', { method: 'POST', headers: auth })).json()).data.code;
await post(TG + '/_push', msg({ text: '/start ' + code }));
await wait(s => s.text.includes('Xin chào'), 'Admin liên kết bằng mã web');
await post(TG + '/_push', msg({ text: '/menu' }));
const menu = await wait(s => s.reply_markup?.inline_keyboard, '/menu');
const mods = buttons(menu).map(b => b.callback_data);
console.log('  phân hệ: ' + buttons(menu).map(b => b.text).join(' | '));
if (!mods.includes('menu:mod:GIAOVAN') || !mods.includes('menu:mod:DIEMPHAT')) fail('thiếu phân hệ Giao vận / Điểm phát');
await post(TG + '/_push', msg({ text: '/chuyen' }));
await wait(s => s.text.includes('nhân viên') || s.text.includes('Chuyến'), '/chuyen → DeliveryRoute/GetMine (admin không gắn nhân viên → lời báo)');
await post(TG + '/_push', msg({ text: '/giaovan' }));
const board = await wait(s => s.text.includes('Giao vận'), '/giaovan → 🗺 Giao vận hôm nay (GetBoard)');
// Hôm nay không có chuyến → mở thẳng thẻ chuyến mẫu (tham số) bằng callback – vẫn chỉ xem.
const routeBtn = buttons(board).find(b => b.callback_data?.startsWith('dlv:r:')) ?? (anyRoute ? { callback_data: 'dlv:r:' + anyRoute.id.replaceAll('-', '') } : null);
if (routeBtn) {
  await post(TG + '/_push', tap(routeBtn.callback_data, 2));
  const card = await wait(s => s.method === 'editMessageText' && s.text.includes('nhiệt độ xuất phát'), 'Thẻ chuyến (chỉ xem)');
  const stopBtn = buttons(card).find(b => b.callback_data?.startsWith('dlv:s:'));
  if (stopBtn) {
    await post(TG + '/_push', tap(stopBtn.callback_data, 2));
    await wait(s => s.method === 'editMessageText' && s.text.includes('Suất: kế hoạch'), 'Thẻ điểm dừng (chỉ xem)');
  }
} else console.log('  (DB dev không có chuyến hôm nay – chỉ kiểm danh sách rỗng)');
await post(TG + '/_push', msg({ text: '/xeden' }));
await wait(s => s.text.includes('Xe đến hôm nay'), '/xeden → 🚚 Xe đến hôm nay');
await post(TG + '/_push', msg({ text: '/doisoat' }));
const rc = await wait(s => s.text.includes('Chờ đối soát'), '/doisoat → 🧮 Đối soát suất (GetPendingOrders hôm nay)');
const rcBtn = buttons(rc).find(b => b.callback_data?.startsWith('rcn:o:'));
if (rcBtn) {
  await post(TG + '/_push', tap(rcBtn.callback_data, 3));
  await wait(s => s.method === 'editMessageText' && s.text.includes('Số suất thực tế'), 'Chọn đơn → số suất điền sẵn (không bấm đối soát)');
}
await post(TG + '/_push', msg({ text: '/hop' }));
await wait(s => s.text.includes('nhân viên') || s.text.includes('hộp'), '/hop → 📥 Nhận hộp về');
await post(TG + '/_push', msg({ text: '/huylienket' }));
await wait(s => s.text.toLowerCase().includes('hủy liên kết') || s.text.toLowerCase().includes('đã hủy'), '/huylienket (dọn)');
console.log(process.exitCode ? 'KẾT QUẢ: CÓ LỖI' : 'KẾT QUẢ: ĐẠT');
