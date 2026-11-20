// Telegram Bot API giả (chỉ để thử container messaging trên máy dev): getUpdates trả update đã đẩy vào qua POST /_push,
// sendMessage / editMessageText ghi lại (GET /_sent). Không gọi Telegram thật.
// BOT-UX-1: giữ trạng thái từng tin như Telegram – editMessageText sửa ĐÚNG tin đó (cùng message_id), deleteMessage xóa;
// GET /_chat?chat=<id> = các tin của bot còn trong chat (thứ tự gửi); setMyCommands / getMyCommands theo scope; GET /_deleted.
import http from 'node:http';
const port = Number(process.argv[2] || 18081);
const queue = [];
const sent = [];
const messages = new Map(); // message_id → { message_id, chat_id, text, reply_markup, deleted }
const deleted = [];
const commands = new Map(); // scope JSON → commands
const answers = []; // answerCallbackQuery
let msgId = 1000;
const server = http.createServer(async (req, res) => {
  let body = '';
  for await (const chunk of req) body += chunk;
  const json = body ? JSON.parse(body) : {};
  const reply = (obj) => { res.writeHead(200, { 'content-type': 'application/json' }); res.end(JSON.stringify(obj)); };
  const fail = (code, description) => { res.writeHead(code, { 'content-type': 'application/json' }); res.end(JSON.stringify({ ok: false, error_code: code, description })); };
  const url = new URL(req.url, 'http://x');
  if (url.pathname === '/_push') { queue.push(json); return reply({ ok: true }); }
  if (url.pathname === '/_sent') return reply(sent);
  if (url.pathname === '/_deleted') return reply(deleted);
  if (url.pathname === '/_answers') return reply(answers);
  if (url.pathname === '/_chat') {
    const chat = url.searchParams.get('chat');
    return reply([...messages.values()].filter(m => !m.deleted && String(m.chat_id) === chat));
  }
  const m = url.pathname.match(/^\/bot([^/]+)\/(\w+)$/);
  if (!m) { res.writeHead(404); return res.end(); }
  const method = m[2];
  if (method === 'getUpdates') {
    const offset = json.offset || 0;
    const wait = Date.now() + Math.min(3, json.timeout || 0) * 1000;
    while (Date.now() < wait && !queue.some(u => u.update_id >= offset)) await new Promise(r => setTimeout(r, 100));
    return reply({ ok: true, result: queue.filter(u => u.update_id >= offset) });
  }
  if (method === 'sendMessage') {
    const id = ++msgId;
    messages.set(id, { message_id: id, chat_id: json.chat_id, text: json.text, reply_markup: json.reply_markup, deleted: false });
    sent.push({ method, message_id: id, chat_id: json.chat_id, text: json.text, reply_markup: json.reply_markup, disable_notification: json.disable_notification });
    return reply({ ok: true, result: { message_id: id, date: 1, chat: { id: Number(json.chat_id), type: 'private' }, text: json.text } });
  }
  if (method === 'editMessageText') {
    const cur = messages.get(Number(json.message_id));
    if (!cur || cur.deleted) return fail(400, 'Bad Request: message to edit not found');
    if (cur.text === json.text && JSON.stringify(cur.reply_markup) === JSON.stringify(json.reply_markup)) return fail(400, 'Bad Request: message is not modified');
    cur.text = json.text; cur.reply_markup = json.reply_markup;
    sent.push({ method, message_id: cur.message_id, chat_id: json.chat_id, text: json.text, reply_markup: json.reply_markup });
    return reply({ ok: true, result: { message_id: cur.message_id, date: 1, chat: { id: Number(json.chat_id), type: 'private' }, text: json.text } });
  }
  if (method === 'deleteMessage') {
    const cur = messages.get(Number(json.message_id));
    deleted.push({ chat_id: json.chat_id, message_id: Number(json.message_id) });
    if (cur) cur.deleted = true;
    return reply({ ok: true, result: true }); // tin của người dùng (không có trong bản ghi) cũng xóa được
  }
  if (method === 'answerCallbackQuery') { answers.push({ id: json.callback_query_id, text: json.text ?? null }); return reply({ ok: true, result: true }); }
  if (method === 'setMyCommands') { commands.set(JSON.stringify(json.scope ?? null), json.commands); return reply({ ok: true, result: true }); }
  if (method === 'getMyCommands') return reply({ ok: true, result: commands.get(JSON.stringify(json.scope ?? null)) ?? [] });
  return reply({ ok: true, result: true });
});
server.listen(port, () => console.log('fake telegram on ' + port));
