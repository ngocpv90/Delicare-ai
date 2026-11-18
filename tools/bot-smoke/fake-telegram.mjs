// Telegram Bot API giả (chỉ để thử container messaging trên máy dev): getUpdates trả update đã đẩy vào qua POST /_push,
// sendMessage ghi lại (GET /_sent). Không gọi Telegram thật.
import http from 'node:http';
const port = Number(process.argv[2] || 18081);
const queue = [];
const sent = [];
let msgId = 1000;
const server = http.createServer(async (req, res) => {
  let body = '';
  for await (const chunk of req) body += chunk;
  const json = body ? JSON.parse(body) : {};
  const reply = (obj) => { res.writeHead(200, { 'content-type': 'application/json' }); res.end(JSON.stringify(obj)); };
  if (req.url === '/_push') { queue.push(json); return reply({ ok: true }); }
  if (req.url === '/_sent') return reply(sent);
  const m = req.url.match(/^\/bot([^/]+)\/(\w+)$/);
  if (!m) { res.writeHead(404); return res.end(); }
  const method = m[2];
  if (method === 'getUpdates') {
    const offset = json.offset || 0;
    const wait = Date.now() + Math.min(3, json.timeout || 0) * 1000;
    while (Date.now() < wait && !queue.some(u => u.update_id >= offset)) await new Promise(r => setTimeout(r, 100));
    return reply({ ok: true, result: queue.filter(u => u.update_id >= offset) });
  }
  if (method === 'sendMessage' || method === 'editMessageText') {
    sent.push({ method, chat_id: json.chat_id, text: json.text, reply_markup: json.reply_markup, disable_notification: json.disable_notification });
    return reply({ ok: true, result: { message_id: ++msgId, date: 1, chat: { id: Number(json.chat_id), type: 'private' }, text: json.text } });
  }
  return reply({ ok: true, result: true });
});
server.listen(port, () => console.log('fake telegram on ' + port));
