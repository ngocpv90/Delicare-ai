/* GHEP-FE – nối các action nghiệp vụ còn lại của backend vào giao diện (chỉ chạy ở chế độ API).
 * Tab chi tiết:  GoodsReceipt · MaterialIssue · PurchaseRequest · ContainerReturn · OrderReconciliation · CustomerComplaint · DeliveryRoute ·
 *                MenuChangeRequest /GetDetail · MaterialRequirement/GetDetail · PackingOrder/GetByBatch · OrderReconciliation/GetByOrder ·
 *                InventoryTransaction/GetLedger · OutboundMessage/GetConversation · công đoạn mẻ (ProductionBatch/StartStage · FinishStage)
 * Nút trong ngăn chi tiết: InventoryTransaction/Reverse · PurchaseOrder/UpdateLines · Approve / Reject (Contract, PurchaseOrder, PurchaseRequest,
 *                StockCount, MenuChangeRequest) · Order/RequestChange · DeliveryRoute/RecordTemperature · CustomerComplaint/AddComment ·
 *                ProductionBatch/Pack · ExportProductionSheet · OutboundMessage/Retry · Enqueue · InboundMessage/Reprocess · UserExternalIdentity/Unlink ·
 *                TelegramLink/CreateCodeForEmployee
 * Nút đầu màn hình: Order/LockByMealDate · ProductionBatch/GetStageBoard · InventoryTransaction/GetLedger · AnalyticsRefreshLog/GetList ·
 *                UserExternalIdentity/Link · OutboundMessage/Enqueue · Common/GetStatusSummary (số trên chip lọc nhanh)
 * Màn hình mới:  hr-my "Việc của tôi" (ShiftAssignment/GetMine · GetSwapCandidates · LeaveRequest/GetMine · ShiftSwapRequest/GetMine ·
 *                DeliveryRoute/GetMine · TelegramLink/GetMine · CreateCode · Unlink) · adm-job "Job tự động" (Job/GetAll · Run)
 * Tài liệu: docs/frontend-handoff (P3, P4, P5, P7, P8, BE2.1, BE2.2, BE2.3, BE2.4, BE2.7, BE3.4, BOT-1.1, BOT-1.2, BOT-1.5a–c, T23). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const dtx = v => (v ? U.dt(String(v).replace(/\.\d+$/, '')) : '');
  const is404 = x => /404/.test((x && x.message) || '');
  const failText = x => (is404(x) ? 'Máy chủ chưa có chức năng này' : (x && x.message) || 'Không tải được dữ liệu');
  const LOADING = '<div class="empty">Đang tải…</div>';
  const today = () => U.day(U.iso(new Date()));
  const addDays = n => { const d = new Date(); d.setDate(d.getDate() + n); return U.day(U.iso(d)); };
  const okMsg = (r, def) => { const m = r && r.res && r.res.responseMessage; return esc(m && m !== 'Lưu thông tin thành công' ? m : def); };
  const fld = (label, html, full) => `<div class="field ${full ? 'full' : ''}"><label>${label}</label>${html}</div>`;
  const val = (m, id) => { const e = m.querySelector('#' + id); return e ? e.value.trim() : ''; };

  // ---------- Hiển thị kết quả API (object / mảng) không cần biết trước từng trường ----------
  const isGuid = v => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-/i.test(v);
  // Nhãn tiếng Việt cho trường của kết quả API: theo nhãn cột (UTIL.LABEL), tên → cột khóa ngoại tương ứng, rồi bảng bổ sung dưới đây.
  const EXTRA = { statusName: 'Trạng thái', status: 'Trạng thái', note: 'Ghi chú', reason: 'Lý do', comment: 'Ghi chú', unitName: 'Đơn vị', warehouseName: 'Kho', storageLocationName: 'Vị trí', ingredientName: 'Nguyên liệu', ingredientCode: 'Mã nguyên liệu', supplierName: 'Nhà cung cấp', customerName: 'Khách hàng', dishName: 'Món', shiftName: 'Ca', teamName: 'Tổ', stageName: 'Công đoạn', employeeName: 'Nhân viên', driverName: 'Tài xế', licensePlate: 'Biển số', lotNumber: 'Số lô', materialLotName: 'Số lô', quantity: 'Số lượng', quantityIn: 'Nhập', quantityOut: 'Xuất', unitPrice: 'Đơn giá', unitCost: 'Đơn giá', totalAmount: 'Thành tiền', lineAmount: 'Thành tiền', createdByName: 'Người tạo', createdDate: 'Ngày tạo', approvedByName: 'Người duyệt', approvedAt: 'Duyệt lúc', requestedByName: 'Người đề nghị', transactionNumber: 'Số giao dịch', transactionDate: 'Ngày giao dịch', transactionType: 'Loại giao dịch', waiting: 'Chờ làm', inProgress: 'Đang làm', done: 'Đã xong', inProgressPortions: 'Suất đang làm', donePortions: 'Suất đã xong', jobCode: 'Job', runStatus: 'Kết quả', startedAt: 'Bắt đầu', completedAt: 'Kết thúc', finishedAt: 'Kết thúc', rowsProcessed: 'Số dòng xử lý', message: 'Thông báo', trigger: 'Kích hoạt', targetDate: 'Ngày xử lý', sequenceNo: 'Thứ tự', workDate: 'Ngày làm', fromDate: 'Từ ngày', toDate: 'Đến ngày', requestNo: 'Số đơn', line: 'Dòng', stop: 'Điểm giao', warning: 'Cảnh báo', history: 'Lịch sử', step: 'Bước duyệt', allowedActions: 'Thao tác được phép' };
  const lab = k => { const K = k.charAt(0).toUpperCase() + k.slice(1), L = U.LABEL || {}; return L[K] || EXTRA[k] || (/Name$/.test(K) && (L[K.replace(/Name$/, 'Id')] || L[K.replace(/Name$/, '')])) || U.colLabel(K); };
  const hidden = (o, k) => /Id$/.test(k) || k === 'id' || isGuid(o[k]) || (o[k + 'Name'] !== undefined && o[k + 'Name'] !== null) || /Json$|Hash$|^rowVersion$/i.test(k);
  function fmt(k, v) {
    if (v === null || v === undefined || v === '') return '<span class="muted">–</span>';
    if (typeof v === 'boolean') return v ? '<span class="tag green">Có</span>' : '<span class="tag">Không</span>';
    if (typeof v === 'number') return esc(/Amount|Cost|Price|Value$/i.test(k) ? U.money(v) : U.num(v, Number.isInteger(v) ? 0 : 2));
    if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(v)) return esc(/T00:00:00/.test(v) ? U.date(v) : dtx(v));
    if (/status(Name)?$/i.test(k)) return `<span class="badge gray">${esc(v)}</span>`;
    return esc(v);
  }
  function listHtml(rows, max) {
    if (!rows.length) return '<div class="muted small">Không có dòng nào</div>';
    if (typeof rows[0] !== 'object' || rows[0] === null) return rows.map(x => `<div class="small" style="padding:3px 0">• ${esc(x)}</div>`).join('');
    const keys = []; rows.slice(0, 20).forEach(r => Object.keys(r).forEach(k => { if (!keys.includes(k) && !hidden(r, k) && (r[k] === null || typeof r[k] !== 'object')) keys.push(k); }));
    const cols = keys.slice(0, max || 8);
    return `<div class="table-wrap table-responsive"><table class="table dc-table dt"><thead><tr>${cols.map(k => `<th class="${typeof rows[0][k] === 'number' ? 'num' : ''}">${esc(lab(k))}</th>`).join('')}</tr></thead><tbody>${rows.map(r => `<tr>${cols.map(k => `<td class="${typeof r[k] === 'number' ? 'num' : ''}">${fmt(k, r[k])}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
  }
  function objHtml(d, depth) {
    if (Array.isArray(d)) return listHtml(d);
    if (!d || typeof d !== 'object') return '<div class="empty">Không có dữ liệu</div>';
    const scal = [], arrs = [], objs = [];
    Object.keys(d).forEach(k => { const v = d[k]; if (Array.isArray(v)) arrs.push(k); else if (v && typeof v === 'object') objs.push(k); else if (!hidden(d, k)) scal.push(k); });
    const warn = arrs.filter(k => /warning|error/i.test(k) && d[k].length);
    return (warn.length ? `<div class="notice warn">${I('alert')}<div>${warn.map(k => d[k].map(w => esc(typeof w === 'string' ? w : w.message || JSON.stringify(w))).join('<br>')).join('<br>')}</div></div>` : '') +
      (scal.length ? `<div class="kv mb">${scal.map(k => `<div><label>${esc(lab(k))}</label><span>${fmt(k, d[k])}</span></div>`).join('')}</div>` : '') +
      (depth ? '' : objs.map(k => `<h3 class="mt">${esc(lab(k))}</h3>${objHtml(d[k], 1)}`).join('')) +
      arrs.filter(k => !warn.includes(k)).map(k => `<h3 class="mt">${esc(lab(k.replace(/^list/, '')) || lab(k))} <span class="muted small">${d[k].length} dòng</span></h3>${listHtml(d[k])}`).join('');
  }
  const pane = (path, params, render, empty) => p => {
    Store.query(path, params).then(x => {
      if (!p.isConnected) return;
      if (!x.ok) { p.innerHTML = `<div class="empty">${esc(failText(x))}</div>`; return; }
      const h = render ? render(x, p) : objHtml(x.data !== undefined && x.data !== null ? x.data : x.list);
      p.innerHTML = h || `<div class="empty">${empty || 'Chưa có dữ liệu'}</div>`;
      if (p._bind) p._bind();
    });
    return LOADING;
  };
  // Gọi action ghi rồi vẽ lại màn hình; lỗi đã hiện toast trong Store.act.
  const run = (path, opt, def, after) => Store.act(path, opt).then(r => { if (r.ok && !r.demo) { UI.toast(okMsg(r, def), 'ok'); if (after) after(r); APP.refresh(); } return r; });
  const dialog = (title, body, okText, onOk, size, mount) => UI.modal({
    title, size: size || 'sm', body, foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="gxOk">${okText}</button>`,
    onMount: (m, close) => { m.querySelector('#gxOk').onclick = () => onOk(m, close); if (mount) mount(m); }
  });
  const resultModal = (title, data) => UI.modal({ title, size: 'lg', body: objHtml(data) });

  // ---------- Tab chi tiết theo bảng ----------
  const DETAIL = { GoodsReceipt: 'GoodsReceipt/GetDetail', MaterialIssue: 'MaterialIssue/GetDetail', PurchaseRequest: 'PurchaseRequest/GetDetail', ContainerReturn: 'ContainerReturn/GetDetail', OrderReconciliation: 'OrderReconciliation/GetDetail', CustomerComplaint: 'CustomerComplaint/GetDetail', DeliveryRoute: 'DeliveryRoute/GetDetail', MenuChangeRequest: 'MenuChangeRequest/GetDetail' };
  const LEDGER_KEY = { MaterialLot: 'materialLotId', Ingredient: 'ingredientId', Warehouse: 'warehouseId' };
  const ledgerHtml = x => (x.list.length ? listHtml(x.list.map(r => ({ transactionNumber: r.transactionNumber, transactionDate: r.transactionDate, transactionType: r.transactionTypeName || r.transactionType, warehouseName: r.warehouseName, ingredientName: r.ingredientName, lotNumber: r.materialLotName, quantityIn: r.quantityIn, quantityOut: r.quantityOut, unitName: r.unitName, reason: r.reason || r.note })), 10) : '');
  const chatHtml = x => { const it = (x.data && x.data.items) || []; return it.length ? `<div class="chat">${it.slice().reverse().map(m => { let t = m.payloadJson; try { t = JSON.parse(m.payloadJson).text || t; } catch (e) { /* giữ nguyên */ } return `<div class="msg ${m.direction === 'OUT' ? 'out' : ''}">${esc(t || '')}<small>${esc(dtx(m.at))} · ${esc(m.botCode || '')} · ${esc(m.status || '')}${m.error ? ' · ' + esc(m.error) : ''}</small></div>`; }).join('')}</div>` : ''; };
  const STAGES = [['SO_CHE', 'Sơ chế'], ['NAU', 'Nấu'], ['CHIA_SUAT', 'Chia suất']];
  const BATCH_TABLES = ['ProductionBatche', 'ProductionOrder', 'ProductionPlan', 'TemperatureLog', 'ProductionOutput', 'PackingOrder'];
  function stagePane(id) {
    return p => {
      const draw = () => Store.query('ProductionBatch/GetDetail', { id }).then(x => {
        if (!p.isConnected) return;
        if (!x.ok) { p.innerHTML = `<div class="empty">${esc(failText(x))}</div>`; return; }
        const done = {}; ((x.data && x.data.listStage) || []).forEach(s => { done[s.stageCode] = s; });
        p.innerHTML = `<div class="small muted mb">Ghi công đoạn theo tổ. Mẻ đã ghi công đoạn thì kết thúc nấu cần công đoạn Nấu xong, đóng gói cần Chia suất xong.</div>` +
          STAGES.map(([c, n]) => { const s = done[c]; return `<div class="spread" style="padding:9px 0;border-bottom:1px solid #F0F2ED"><div><b>${n}</b><div class="small muted">${s ? esc(s.statusName || s.status || '') + (s.teamName ? ' · ' + esc(s.teamName) : '') + (s.startedAt ? ' · bắt đầu ' + esc(dtx(s.startedAt)) : '') + (s.finishedAt ? ' · xong ' + esc(dtx(s.finishedAt)) : '') : 'Chưa ghi'}</div></div><div class="row-flex">${s && s.finishedAt ? '' : s && s.startedAt ? `<button class="btn sm primary" data-stg="FinishStage" data-code="${c}">Xong</button>` : `<button class="btn sm orange" data-stg="StartStage" data-code="${c}">Bắt đầu</button>`}</div></div>`; }).join('');
        p.querySelectorAll('[data-stg]').forEach(b => b.onclick = () => Store.act('ProductionBatch/' + b.dataset.stg, { form: { Id: id, StageCode: b.dataset.code }, tables: BATCH_TABLES }).then(r => { if (r.ok) { UI.toast(okMsg(r, 'Đã ghi công đoạn'), 'ok'); draw(); } }));
      });
      draw();
      return LOADING;
    };
  }
  const baseTabs = UI.liveTabs;
  UI.liveTabs = (t, id) => {
    const tabs = baseTabs ? baseTabs(t, id) : [];
    if (!Store.isLive) return tabs;
    const row = Store.get(t, id) || {};
    if (DETAIL[t]) tabs.push({ label: 'Chi tiết chứng từ', html: pane(DETAIL[t], { id }) });
    if (t === 'ProductionPlan') tabs.push({ label: 'Nhu cầu nguyên liệu', html: pane('MaterialRequirement/GetDetail', { productionPlanId: id }, null, 'Chưa tính nhu cầu nguyên liệu cho kế hoạch này') });
    if (t === 'MaterialRequirement' && row.ProductionPlanId) tabs.push({ label: 'Theo kế hoạch', html: pane('MaterialRequirement/GetDetail', { productionPlanId: row.ProductionPlanId }) });
    if (t === 'ProductionBatche') {
      tabs.push({ label: 'Công đoạn', html: stagePane(id) });
      tabs.push({ label: 'Lệnh đóng gói', html: pane('PackingOrder/GetByBatch', { id }, null, 'Mẻ chưa có lệnh đóng gói') });
    }
    if (t === 'Order') tabs.push({ label: 'Đối soát', html: pane('OrderReconciliation/GetByOrder', { orderId: id }, null, 'Đơn chưa có bản đối soát') });
    if (LEDGER_KEY[t]) tabs.push({ label: 'Sổ kho', html: pane('InventoryTransaction/GetLedger', { [LEDGER_KEY[t]]: id, page: 1, pageSize: 50, sortField: 'TransactionDate', sortOrder: 'desc' }, ledgerHtml, 'Chưa có giao dịch kho') });
    if (t === 'UserExternalIdentity') tabs.push({ label: 'Hội thoại', html: pane('OutboundMessage/GetConversation', { IdentityId: id, Page: 1, PageSize: 50 }, chatHtml, 'Chưa có tin nhắn') });
    if (t === 'Employee') tabs.push({ label: 'Tin nhắn bot', html: pane('OutboundMessage/GetConversation', { EmployeeId: id, Page: 1, PageSize: 50 }, chatHtml, 'Chưa có tin nhắn') });
    return tabs;
  };

  // ---------- Nút thao tác trong ngăn chi tiết (UI.detail gọi UI.liveFoot / UI.liveAct) ----------
  const APPROVABLE = { Contract: ['Contract', ['Contract', 'ContractMeal']], PurchaseOrder: ['PurchaseOrder', ['PurchaseOrder', 'PurchaseOrderLine']], PurchaseRequest: ['PurchaseRequest', ['PurchaseRequest', 'PurchaseRequestLine']], StockCount: ['StockCount', ['StockCount', 'StockCountLine']], MenuChangeRequest: ['MenuChangeRequest', ['MenuChangeRequest', 'ProductionPlan', 'ProductionPlanLine']] };
  const statusOf = (t, row) => { const sc = Store.statusCol(t); return sc ? String(row[sc] || '') : ''; };
  const isPending = s => /^(Pending|PendingApproval|Submitted|WaitingApproval|InReview)$/i.test(s);
  const btn = (act, text, cls, dis) => `<button class="btn ${cls || ''}" data-act="${act}" ${dis ? 'disabled title="Tài khoản không có quyền Sửa"' : ''}>${text}</button>`;
  UI.liveFoot = (t, row, perm) => {
    if (!Store.isLive) return '';
    const st = statusOf(t, row), ro = perm && perm.edit === false; let h = '';
    if (APPROVABLE[t] && isPending(st)) h += btn('gx-approve', 'Duyệt', 'primary') + btn('gx-reject', 'Từ chối', 'danger');
    if (t === 'InventoryTransaction') h += btn('gx-reverse', 'Đảo bút toán', 'danger', ro);
    if (t === 'PurchaseOrder' && st === 'Draft') h += btn('gx-po-lines', 'Sửa dòng hàng', '', ro);
    if (t === 'Order' && ['Draft', 'Submitted', 'Confirmed', 'Locked'].includes(st)) h += btn('gx-order-change', 'Xin sửa sau giờ chốt', '', ro);
    if (t === 'DeliveryRoute') h += btn('gx-route-temp', 'Ghi nhiệt độ', '', ro);
    if (t === 'CustomerComplaint') h += btn('gx-comment', 'Thêm ghi chú', '', ro);
    if (t === 'ProductionBatche' && !(perm && perm.exp === false)) h += btn('gx-sheet', 'Phiếu sản xuất (Excel)', '');
    if (t === 'ProductionBatche' && st === 'Passed') h += btn('gx-pack', 'Đã đóng gói xong', '', ro);
    if (t === 'OutboundMessage' && ['Failed', 'Cancelled'].includes(st)) h += btn('gx-retry', 'Gửi lại', 'orange', ro);
    if (t === 'InboundMessage' && ['Failed', 'Ignored'].includes(st)) h += btn('gx-reprocess', 'Xử lý lại', 'orange', ro);
    if (t === 'UserExternalIdentity' && st !== 'Revoked') h += btn('gx-unlink', 'Gỡ liên kết', 'danger', ro);
    if (t === 'Employee') h += (Store.perm('adm-employee').edit !== false ? btn('gx-tg-code', 'Tạo mã liên kết bot', '') : '') + btn('gx-send', 'Gửi tin qua bot', '');
    return h;
  };
  // FE-E.1: link + ảnh QR (UI.qr nếu engine có bộ vẽ QR) để quét bằng điện thoại.
  const qrOf = link => (UI.qr && link ? `<div class="tg-qr">${UI.qr(link, { size: 180 })}</div>` : '');
  function showTelegramCode(d) {
    const links = (d && d.deepLinks) || {};
    UI.modal({ title: 'Mã liên kết Telegram', size: 'sm', body: `<div class="kv"><div style="grid-column:1/-1"><label>Mã liên kết</label><span style="font-size:22px;font-weight:800;letter-spacing:2px">${esc((d && d.code) || '')}</span></div><div style="grid-column:1/-1"><label>Hết hạn</label><span>${esc(dtx(d && d.expiresAt))}</span></div></div><div class="grid mt">${Object.keys(links).map(k => `<div class="tg-link">${qrOf(links[k])}<a class="btn primary" href="${esc(links[k])}" target="_blank" rel="noopener">Mở bot ${esc(k === 'VH' ? 'Vận hành' : k === 'DH' ? 'Điều hành' : k)}</a><div class="small muted mono" style="word-break:break-all">${esc(links[k])}</div></div>`).join('')}</div><div class="small muted mt">Mã dùng một lần. Mở bot bằng nút trên hoặc gửi mã cho bot để liên kết.</div>` });
  }
  function sendMessage(preset) {
    const users = Store.rows('User'), emps = Store.rows('Employee');
    dialog('Gửi tin qua bot', (preset ? '' : fld('Người nhận (nhân viên) *', `<select class="select" id="smE"><option value="">— Chọn —</option>${emps.slice(0, 500).map(e => `<option value="${esc(e.Id)}">${esc(Store.label('Employee', e))}</option>`).join('')}</select>`) + fld('hoặc tài khoản', `<select class="select" id="smU"><option value="">— Chọn —</option>${users.slice(0, 500).map(u => `<option value="${esc(u.Id)}">${esc(Store.label('User', u))}</option>`).join('')}</select>`)) +
      fld('Bot', '<select class="select" id="smB"><option value="VH">Vận hành (VH)</option><option value="DH">Điều hành (DH)</option></select>') + fld('Nội dung * (tối đa 4000 ký tự)', '<textarea class="input" id="smT" rows="4" maxlength="4000"></textarea>'), 'Gửi', (m, close) => {
        const form = Object.assign({ BotCode: val(m, 'smB'), Text: val(m, 'smT') }, preset || { EmployeeId: val(m, 'smE'), UserId: val(m, 'smU') });
        if (!form.Text) return UI.toast('Nhập nội dung tin nhắn', 'err');
        if (!form.EmployeeId && !form.UserId) return UI.toast('Chọn người nhận', 'err');
        run('OutboundMessage/Enqueue', { form, tables: ['OutboundMessage'] }, 'Đã xếp tin vào hàng gửi').then(r => { if (r.ok) close(); });
      });
  }
  function poLines(row) {
    const lines = Store.by('PurchaseOrderLine', 'PurchaseOrderId', row.Id);
    const ings = Store.rows('Ingredient');
    // Giữ đơn vị tính của dòng cũ (data-u) khi không đổi nguyên liệu – không gửi thì máy chủ đặt lại theo bảng giá NCC / đơn vị cơ sở.
    const tr = l => `<tr data-l="${esc(l.Id || '')}" data-i="${esc(l.IngredientId || '')}" data-u="${esc(l.Id ? l.UnitId || '' : '')}"><td><select class="select gxI" style="min-width:220px">${ings.map(i => `<option value="${esc(i.Id)}" ${i.Id === l.IngredientId ? 'selected' : ''}>${esc(Store.label('Ingredient', i))}</option>`).join('')}</select></td><td><input type="number" min="0" step="any" class="input gxQ" style="width:110px" value="${l.Quantity ?? ''}"></td><td><input type="number" min="0" step="any" class="input gxP" style="width:130px" value="${l.UnitPrice ?? ''}" placeholder="Theo bảng giá"></td><td><input class="input gxN" value="${esc(l.Note || '')}"></td><td><button class="btn xs danger gxD">Xóa</button></td></tr>`;
    dialog('Sửa dòng hàng – ' + esc(Store.label('PurchaseOrder', row)), `<div class="small muted mb">Gửi toàn bộ dòng: dòng bị xóa ở đây sẽ bị xóa khỏi đơn. Mỗi nguyên liệu một dòng; đơn giá trống = theo bảng giá nhà cung cấp.</div>
      <div class="form-grid mb">${fld('Ngày giao dự kiến', `<input type="date" class="input" id="gxE" value="${esc(U.day(row.ExpectedDeliveryDate))}">`)}${fld('Ghi chú', `<input class="input" id="gxNote" value="${esc(row.Note || '')}">`)}</div>
      <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Nguyên liệu</th><th>Số lượng</th><th>Đơn giá</th><th>Ghi chú</th><th></th></tr></thead><tbody id="gxB">${lines.map(tr).join('')}</tbody></table></div><button class="btn sm mt" id="gxAdd">${I('plus')} Thêm dòng</button>`, 'Lưu', (m, close) => {
        const list = [...m.querySelectorAll('#gxB tr')].map(r => ({ id: r.dataset.l || null, ingredientId: r.querySelector('.gxI').value, unitId: (r.dataset.u && r.querySelector('.gxI').value === r.dataset.i) ? r.dataset.u : null, quantity: Number(r.querySelector('.gxQ').value), unitPrice: r.querySelector('.gxP').value === '' ? null : Number(r.querySelector('.gxP').value), note: r.querySelector('.gxN').value.trim() || null }));
        if (!list.length) return UI.toast('Đơn mua phải có ít nhất một dòng', 'err');
        if (list.some(l => !(l.quantity > 0))) return UI.toast('Số lượng phải lớn hơn 0', 'err');
        if (new Set(list.map(l => l.ingredientId)).size !== list.length) return UI.toast('Mỗi nguyên liệu chỉ một dòng', 'err');
        run('PurchaseOrder/UpdateLines', { method: 'PUT', json: { id: row.Id, expectedDeliveryDate: val(m, 'gxE') || null, note: val(m, 'gxNote') || null, listLine: list }, tables: ['PurchaseOrder', 'PurchaseOrderLine'] }, 'Đã lưu dòng hàng').then(r => { if (r.ok) close(); });
      }, 'lg', m => m.addEventListener('click', e => { if (e.target.closest('.gxD')) e.target.closest('tr').remove(); if (e.target.closest('#gxAdd') && ings.length) m.querySelector('#gxB').insertAdjacentHTML('beforeend', tr({ IngredientId: ings[0].Id })); }));
  }
  function orderChange(row) {
    const lines = Store.by('OrderLine', 'OrderId', row.Id);
    dialog('Xin sửa sau giờ chốt – ' + esc(Store.label('Order', row)), `<div class="small muted mb">Chỉ dùng khi đã tới giờ chốt của ngày ăn (trước giờ chốt hãy sửa số suất trực tiếp). Thay đổi chưa ghi ngay – tạo yêu cầu chờ duyệt.</div>
      <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Suất ăn</th><th class="num">Đang đặt</th><th>Số mới</th></tr></thead><tbody>${lines.map(l => `<tr><td>${esc(Store.label('ContractMeal', l.ContractMealId) || Store.label('OrderLine', l))}</td><td class="num">${U.num(l.Quantity)}</td><td><input type="number" min="0" class="input gxQ" data-l="${esc(l.Id)}" data-o="${l.Quantity}" style="width:120px" value="${l.Quantity}"></td></tr>`).join('')}</tbody></table></div>
      <div class="mt">${fld('Lý do *', '<textarea class="input" id="gxR" rows="2"></textarea>')}</div>`, 'Gửi yêu cầu', (m, close) => {
        const reason = val(m, 'gxR'); if (!reason) return UI.toast('Nhập lý do', 'err');
        const changed = [...m.querySelectorAll('.gxQ')].filter(i => Number(i.value) !== Number(i.dataset.o)).map(i => ({ id: i.dataset.l, quantity: Number(i.value) }));
        run('Order/RequestChange', { json: { id: row.Id, reason, listLine: changed, listNewLine: [] }, tables: ['Order', 'OrderLine', 'ApprovalRequest'] }, 'Đã gửi yêu cầu sửa đơn – chờ duyệt').then(r => { if (r.ok) close(); });
      }, 'lg');
  }
  function routeTemp(row) {
    const stops = Store.by('DeliveryStop', 'DeliveryRouteId', row.Id).slice().sort((a, b) => (a.SequenceNo || 0) - (b.SequenceNo || 0));
    dialog('Ghi nhiệt độ – ' + esc(Store.label('DeliveryRoute', row)), fld('Điểm giao', `<select class="select" id="gxS"><option value="">Xuất phát (trước khi xe rời bếp)</option>${stops.map(s => `<option value="${esc(s.Id)}">Điểm ${s.SequenceNo || ''} · ${esc(Store.label('DeliveryStop', s))}</option>`).join('')}</select>`) +
      fld('Nhiệt độ °C *', '<input type="number" step="0.1" class="input" id="gxT">') + fld('Thiết bị đo', '<input class="input" id="gxD" maxlength="50">') + fld('Biện pháp xử lý (bắt buộc khi dưới 60 °C)', '<textarea class="input" id="gxC" rows="2"></textarea>'), 'Lưu', (m, close) => {
        const t = val(m, 'gxT'); if (t === '') return UI.toast('Nhập nhiệt độ', 'err');
        if (Number(t) < 60 && !val(m, 'gxC')) return UI.toast('Nhiệt độ dưới 60 °C: nhập biện pháp xử lý', 'err');
        run('DeliveryRoute/RecordTemperature', { form: { Id: row.Id, DeliveryStopId: val(m, 'gxS'), TemperatureC: t, DeviceCode: val(m, 'gxD'), CorrectiveAction: val(m, 'gxC') }, tables: ['DeliveryRoute', 'DeliveryStop', 'TemperatureLog', 'DeliveryEvent'] }, 'Đã ghi nhiệt độ').then(r => { if (r.ok) close(); });
      });
  }
  UI.liveAct = (act, row, close, t) => {
    if (!/^gx-/.test(act)) return false;
    const id = row.Id, name = esc(Store.label(t, row));
    const done = r => { if (r.ok) close(); };
    const ask = (msg, path, form, tables, def, o) => UI.confirm(msg, note => run(path, { form: form(note), tables }, def).then(done), o);
    if (act === 'gx-approve') { const [c, tb] = APPROVABLE[t]; ask(`Duyệt <b>${name}</b>?`, c + '/Approve', n => ({ Id: id, Note: n }), tb.concat(['ApprovalRequest', 'ApprovalDecision']), 'Đã duyệt', { input: 'Ghi chú', ok: 'Duyệt' }); }
    if (act === 'gx-reject') { const [c, tb] = APPROVABLE[t]; ask(`Từ chối <b>${name}</b>?`, c + '/Reject', n => ({ Id: id, Note: n }), tb.concat(['ApprovalRequest', 'ApprovalDecision']), 'Đã từ chối', { input: 'Lý do', required: true, danger: true, ok: 'Từ chối' }); }
    if (act === 'gx-reverse') ask(`Đảo bút toán <b>${name}</b>? Hệ thống ghi một bút toán ngược tham chiếu dòng này (không sửa, không xóa dòng gốc).`, 'InventoryTransaction/Reverse', n => ({ Id: id, Reason: n }), ['InventoryTransaction', 'InventoryBalance', 'MaterialLot'], 'Đã ghi bút toán đảo', { input: 'Lý do', required: true, danger: true, ok: 'Đảo bút toán' });
    if (act === 'gx-comment') ask(`Thêm ghi chú cho phản ánh <b>${name}</b> (không đổi trạng thái).`, 'CustomerComplaint/AddComment', n => ({ Id: id, Comment: n }), ['CustomerComplaint', 'ComplaintHistory'], 'Đã thêm ghi chú', { input: 'Ghi chú', required: true, ok: 'Lưu' });
    if (act === 'gx-pack') ask(`Đánh dấu mẻ <b>${name}</b> đã đóng gói xong? (Mẻ còn lệnh đóng gói chưa xong thì hoàn tất ở màn Đơn đóng gói.)`, 'ProductionBatch/Pack', n => ({ Id: id, Note: n }), BATCH_TABLES, 'Mẻ đã đóng gói', { input: 'Ghi chú', ok: 'Xác nhận' });
    if (act === 'gx-retry') ask(`Gửi lại tin <b>${name}</b>?`, 'OutboundMessage/Retry', () => ({ id }), ['OutboundMessage'], 'Đã đưa tin vào hàng gửi lại', {});
    if (act === 'gx-reprocess') ask(`Xử lý lại tin <b>${name}</b>?`, 'InboundMessage/Reprocess', () => ({ id }), ['InboundMessage'], 'Đã đưa tin về hàng chờ xử lý', {});
    if (act === 'gx-unlink') ask(`Gỡ liên kết <b>${name}</b>? Bot sẽ ngừng phục vụ tài khoản này.`, 'UserExternalIdentity/Unlink', () => ({ id }), ['UserExternalIdentity'], 'Đã gỡ liên kết', { danger: true, ok: 'Gỡ liên kết' });
    if (act === 'gx-tg-code') Store.act('TelegramLink/CreateCodeForEmployee', { form: { EmployeeId: id } }).then(r => { if (r.ok && !r.demo) showTelegramCode(r.data); });
    if (act === 'gx-send') sendMessage({ EmployeeId: id });
    if (act === 'gx-sheet') Store.download('ProductionBatch/ExportProductionSheet', { id }).then(r => { if (r.ok) UI.toast('Đã tải ' + esc(r.fileName || 'phiếu sản xuất'), 'ok'); });
    if (act === 'gx-po-lines') { close(); Store.reload(['PurchaseOrderLine', 'Ingredient']).then(() => poLines(row)); }
    if (act === 'gx-order-change') { close(); Store.reload(['OrderLine', 'ContractMeal']).then(() => orderChange(row)); }
    if (act === 'gx-route-temp') { close(); Store.reload(['DeliveryStop']).then(() => routeTemp(row)); }
    return true;
  };

  // ---------- Nút / khối thêm trên các màn hình đã có ----------
  function extend(id, after) {
    const base = APP.SCREENS[id];
    APP.register(id, (el, f, p) => {
      if (base) base(el, f, p); else if (f.table) APP.genericList(el, f); else APP.gapScreen(el, f);
      if (Store.isLive) { try { after(el, f, p); } catch (e) { console.warn('[DELICARE] GHEP', id, e); } }
    });
  }
  const headBtn = (el, html, fn) => { const h = el.querySelector('.heading-actions'); if (!h) return; h.insertAdjacentHTML('afterbegin', html + ' '); h.firstElementChild.onclick = fn; };
  const canEdit = id => Store.perm(id).edit !== false;

  // Đơn hàng: chốt đơn theo ngày ăn (nút "Chốt đơn 20h").
  extend('crm-order', el => { if (!canEdit('crm-order')) return; headBtn(el, `<button class="btn">${I('key')} Chốt đơn theo ngày</button>`, () => dialog('Chốt đơn theo ngày ăn', `<div class="small muted mb">Khóa mọi đơn Đã xác nhận của ngày ăn đã chọn. Chỉ chạy được khi đã tới giờ chốt của ngày đó.</div>${fld('Ngày ăn *', `<input type="date" class="input" id="gxD" value="${addDays(1)}">`)}`, 'Chốt đơn', (m, close) => {
    const d = val(m, 'gxD'); if (!d) return UI.toast('Chọn ngày ăn', 'err');
    Store.act('Order/LockByMealDate', { form: { MealDate: d }, tables: ['Order', 'OrderLine'] }).then(r => { if (!r.ok || r.demo) return; close(); const x = r.data || {}, li = (t, a) => (a && a.length ? `<h3 class="mt">${t} <span class="muted small">${a.length} đơn</span></h3><div class="row-flex">${a.map(n => `<span class="tag">${esc(n)}</span>`).join('')}</div>` : ''); UI.modal({ title: 'Kết quả chốt đơn ngày ' + U.date(d), body: `<div class="notice ok">${I('ok')}<div><b>Đã chốt ${U.num(x.lockedCount || 0)} đơn</b>${esc((r.res && r.res.responseMessage) || '')}</div></div>${li('Đơn đã chốt', x.listLockedNumber)}${li('Đơn chưa xác nhận – cần xử lý tay', x.listNotConfirmedNumber)}${li('Đơn đang chờ duyệt', x.listPendingApprovalNumber)}` }); APP.refresh(); });
  })); });

  // Mẻ sản xuất: tiến độ bếp theo tổ / công đoạn.
  function stageBoard(date) {
    UI.modal({ title: 'Tiến độ bếp theo tổ', size: 'lg', body: `<div class="row-flex mb"><span class="small muted">Ngày sản xuất</span><input type="date" class="input" id="gxD" value="${date}"></div><div id="gxOut">${LOADING}</div>`, onMount: m => {
      const load = () => { const d = val(m, 'gxD') || today(); m.querySelector('#gxOut').innerHTML = LOADING; Store.query('ProductionBatch/GetStageBoard', { ProductionDate: d }).then(x => { const o = m.querySelector('#gxOut'); if (!o) return; if (!x.ok) { o.innerHTML = `<div class="empty">${esc(failText(x))}</div>`; return; } const lt = (x.data && x.data.listTeam) || []; o.innerHTML = lt.length ? listHtml(lt.map(r => ({ stageName: r.stageName || r.stageCode, teamName: r.teamName || r.teamCode || '(chưa gán tổ)', waiting: r.waiting, inProgress: r.inProgress, done: r.done, inProgressPortions: r.inProgressPortions, donePortions: r.donePortions })), 8) + objHtml(Object.fromEntries(Object.entries(x.data).filter(([k]) => k !== 'listTeam')), 1) : '<div class="empty">Chưa có công đoạn nào được ghi trong ngày này</div>'; }); };
      m.querySelector('#gxD').onchange = load; load();
    } });
  }
  extend('prod-batch', el => headBtn(el, `<button class="btn">${I('users')} Tiến độ theo tổ</button>`, () => stageBoard(Store.ctx.date || today())));

  // Giao dịch tồn kho: sổ cái lọc theo khoảng ngày (truy vấn phía máy chủ).
  extend('scm-inv-txn', el => headBtn(el, `<button class="btn">${I('clipboard')} Sổ cái theo ngày</button>`, () => UI.modal({
    title: 'Sổ cái kho', size: 'lg', body: `<div class="row-flex mb"><input type="date" class="input" id="gxF" value="${addDays(-7)}"><span class="small muted">→</span><input type="date" class="input" id="gxTo" value="${today()}"><select class="select" id="gxW"><option value="">Tất cả kho</option>${Store.rows('Warehouse').map(w => `<option value="${esc(w.Id)}">${esc(Store.label('Warehouse', w))}</option>`).join('')}</select><input class="input" id="gxQ" placeholder="Tìm số phiếu, nguyên liệu, lô…"><button class="btn primary" id="gxGo">Xem</button></div><div id="gxOut">${LOADING}</div>`,
    onMount: m => {
      const load = () => { m.querySelector('#gxOut').innerHTML = LOADING; Store.query('InventoryTransaction/GetLedger', { searchText: val(m, 'gxQ'), warehouseId: val(m, 'gxW'), fromDate: val(m, 'gxF'), toDate: val(m, 'gxTo'), page: 1, pageSize: 200, sortField: 'TransactionDate', sortOrder: 'desc' }).then(x => { const o = m.querySelector('#gxOut'); if (!o) return; o.innerHTML = x.ok ? (ledgerHtml(x) ? `<div class="small muted mb">${x.res && x.res.totalRecords !== undefined ? U.num(x.res.totalRecords) + ' giao dịch' + (x.res.totalRecords > x.list.length ? ' · hiện ' + x.list.length + ' dòng mới nhất' : '') : ''}</div>` + ledgerHtml(x) : '<div class="empty">Không có giao dịch trong khoảng này</div>') : `<div class="empty">${esc(failText(x))}</div>`; }); };
      m.querySelector('#gxGo').onclick = load; load();
    } })));

  // Nhật ký làm mới: lần chạy job / làm mới KPI gần đây (máy chủ lọc 30 ngày, mới nhất trước).
  extend('kpi-refresh', el => {
    const anchor = el.querySelector('.page-heading'); if (!anchor) return;
    const host = document.createElement('div'); host.className = 'card panel mb'; host.innerHTML = `<div class="panel-title mb">Lần chạy gần đây</div>${LOADING}`;
    anchor.insertAdjacentElement('afterend', host);
    Store.query('AnalyticsRefreshLog/GetList', { Page: 1, PageSize: 20 }).then(x => {
      if (!host.isConnected) return; if (!x.ok) { if (is404(x)) host.remove(); else host.innerHTML = `<div class="panel-title mb">Lần chạy gần đây</div><div class="muted small">${esc(failText(x))}</div>`; return; }
      const rows = x.list.length ? x.list : ((x.data && (x.data.items || x.data.listItem)) || []);
      host.innerHTML = `<div class="panel-title mb">Lần chạy gần đây</div>${rows.length ? listHtml(rows, 8) : '<div class="muted small">Chưa có lần chạy nào trong 30 ngày gần nhất</div>'}`;
    });
  });

  // Tích hợp bot: danh sách thật + thao tác qua action nghiệp vụ (không dùng khối trình diễn của mockup).
  const liveList = (id, opt) => { const base = APP.SCREENS[id]; APP.register(id, (el, f, p) => { if (!Store.isLive) return base ? base(el, f, p) : APP.genericList(el, f, opt); APP.genericList(el, f, Object.assign({ noAdd: true, noTransit: true }, opt)); }); };
  liveList('int-inbound'); liveList('int-outbound'); liveList('int-identity');
  extend('int-outbound', el => { if (canEdit('int-outbound')) headBtn(el, `<button class="btn primary">${I('msg')} Gửi tin</button>`, () => Store.reload(['User', 'Employee']).then(() => sendMessage())); });
  extend('int-identity', el => { if (!canEdit('int-identity')) return; headBtn(el, `<button class="btn primary">${I('plus')} Liên kết tay</button>`, () => Store.reload(['User', 'Employee']).then(() => dialog('Liên kết tài khoản bot (đã xác minh trực tiếp)',
    fld('Bot *', '<select class="select" id="gxB"><option value="VH">Vận hành (VH)</option><option value="DH">Điều hành (DH)</option></select>') +
    fld('Nhân viên', `<select class="select" id="gxE"><option value="">— Chọn —</option>${Store.rows('Employee').slice(0, 500).map(e => `<option value="${esc(e.Id)}">${esc(Store.label('Employee', e))}</option>`).join('')}</select>`) +
    fld('Tài khoản', `<select class="select" id="gxU"><option value="">— Chọn —</option>${Store.rows('User').slice(0, 500).map(u => `<option value="${esc(u.Id)}">${esc(Store.label('User', u))}</option>`).join('')}</select>`) +
    fld('Telegram user id *', '<input class="input" id="gxX" maxlength="100">') + fld('Tên người dùng Telegram', '<input class="input" id="gxN" maxlength="100">') + fld('Tên hiển thị', '<input class="input" id="gxDn" maxlength="200">'), 'Liên kết', (m, close) => {
      if (!val(m, 'gxX')) return UI.toast('Nhập Telegram user id', 'err');
      if (!val(m, 'gxE') && !val(m, 'gxU')) return UI.toast('Chọn nhân viên hoặc tài khoản', 'err');
      run('UserExternalIdentity/Link', { form: { BotCode: val(m, 'gxB'), EmployeeId: val(m, 'gxE'), UserId: val(m, 'gxU'), ExternalUserId: val(m, 'gxX'), ExternalUsername: val(m, 'gxN'), DisplayName: val(m, 'gxDn') }, tables: ['UserExternalIdentity'] }, 'Đã liên kết').then(r => { if (r.ok) close(); });
    }))); });

  // Chip lọc nhanh của danh sách chung: số theo trạng thái lấy từ máy chủ (Common/GetStatusSummary).
  const baseList = APP.genericList;
  APP.genericList = (el, f, opt) => {
    baseList(el, f, opt);
    if (!Store.isLive || !f.table || (opt && opt.rows) || !Store.statusCol(f.table) || !el.querySelector('[data-st]')) return;
    const res = Store.controllerOf(f.table), scr = APP.current;
    APP.resources().then(set => (set && set.has(String(res).toLowerCase())) ? Store.query('Common/GetStatusSummary', { Resource: res }) : null).then(x => {
      if (!x || !x.ok || !x.data || APP.current !== scr) return;
      const by = {}; (x.data.listByStatus || []).forEach(s => { by[s.status === null || s.status === undefined ? '' : String(s.status)] = s; });
      el.querySelectorAll('[data-st]').forEach(c => { if (c.dataset.st === '') { if (x.data.total !== undefined) c.textContent = 'Tất cả · ' + x.data.total; return; } const s = by[c.dataset.st]; if (s) c.textContent = (s.statusName || c.dataset.st) + ' · ' + s.count; });
    });
  };

  // ---------- Màn hình mới ----------
  const onlyLive = (el, f) => { el.innerHTML = APP.header(f, '', { noNote: true }) + `<div class="card panel empty"><h2>Màn hình này chỉ có ở chế độ dữ liệu thật</h2><p class="muted">Chuyển sang dữ liệu API để dùng.</p></div>`; };
  const box = (id, title, desc, tools) => `<div class="card panel mb"><div class="panel-head"><div><div class="panel-title">${title}</div>${desc ? `<div class="panel-desc">${desc}</div>` : ''}</div><div class="row-flex">${tools || ''}</div></div><div id="${id}">${LOADING}</div></div>`;
  const fill = (el, id, path, params, render, empty) => Store.query(path, params).then(x => { const o = el.querySelector('#' + id); if (!o || !o.isConnected) return; o.innerHTML = x.ok ? (render(x) || `<div class="muted small">${empty}</div>`) : `<div class="muted small">${esc(failText(x))}</div>`; if (o._bind) o._bind(x); });

  // Việc của tôi: ca, đơn nghỉ, đổi ca, chuyến giao của chính người đăng nhập + liên kết Telegram.
  APP.FN['hr-my'] = { extra: true, ph: 'P8', grp: 'Nhân sự & Phê duyệt', fn: 'Việc của tôi', id: 'hr-my', name: 'Việc của tôi', types: ['LS'], table: null, rel: [], st: 'MAPPED', note: '' };
  function swapShift(a, done) {
    Store.query('ShiftAssignment/GetSwapCandidates', { assignmentId: a.assignmentId }).then(x => {
      if (!x.ok) return UI.toast(esc(failText(x)), 'err');
      const d = x.data || {}, shifts = d.listShift || [], col = d.listColleague || [];
      dialog('Xin đổi ca – ' + esc(U.date(a.workDate) + ' · ' + (a.shiftName || '')), fld('Đổi với ca của đồng nghiệp', `<select class="select" id="gxP"><option value="">— Không chọn ca cụ thể —</option>${shifts.map(s => `<option value="${esc(s.assignmentId)}" data-e="${esc(s.employeeId)}">${esc(s.employeeName)} · ${esc(U.date(s.workDate))} · ${esc(s.shiftName || '')}</option>`).join('')}</select>`) +
        fld('hoặc nhờ đồng nghiệp làm thay', `<select class="select" id="gxC"><option value="">— Chọn —</option>${col.map(c => `<option value="${esc(c.employeeId)}">${esc(c.employeeName)}</option>`).join('')}</select>`) + fld('Lý do *', '<textarea class="input" id="gxR" rows="2"></textarea>'), 'Gửi đơn', (m, close) => {
          const p = m.querySelector('#gxP'), sel = p.options[p.selectedIndex];
          if (!val(m, 'gxR')) return UI.toast('Nhập lý do', 'err');
          if (!p.value && !val(m, 'gxC')) return UI.toast('Chọn ca muốn đổi hoặc người làm thay', 'err');
          Store.act('ShiftSwapRequest/Create', { form: { CurrentShiftAssignmentId: a.assignmentId, ProposedShiftAssignmentId: p.value, TargetEmployeeId: p.value ? sel.dataset.e : val(m, 'gxC'), Reason: val(m, 'gxR') }, tables: ['ShiftSwapRequest', 'ApprovalRequest'] }).then(r => { if (!r.ok) return; close(); UI.toast(okMsg(r, 'Đã tạo đơn đổi ca và gửi duyệt'), 'ok'); done(); });
        });
    });
  }
  // FE-E.1 (FE-BOT-1): danh sách bot đã liên kết (TelegramLink/GetMine) + gỡ liên kết (Unlink) trong khung #myTg của el.
  UI.telegramBox = el => {
    const o = el.querySelector('#myTg'); if (!o) return Promise.resolve();
    o._bind = () => o.querySelectorAll('[data-un]').forEach(b => b.onclick = () => UI.confirm('Gỡ liên kết Telegram này? Bot sẽ ngừng phục vụ tài khoản của bạn.', () => Store.act('TelegramLink/Unlink', { form: { id: b.dataset.un } }).then(r => { if (r.ok) { UI.toast('Đã gỡ liên kết', 'ok'); UI.telegramBox(el); } }), { danger: true, ok: 'Gỡ liên kết' }));
    return fill(el, 'myTg', 'TelegramLink/GetMine', null, x => (x.list.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Bot</th><th>Tài khoản Telegram</th><th>Trạng thái</th><th>Liên kết lúc</th><th></th></tr></thead><tbody>${x.list.map(l => `<tr><td>${esc(l.botCode === 'VH' ? 'Vận hành' : l.botCode === 'DH' ? 'Điều hành' : l.botCode || '')}</td><td>${esc(l.externalUsername ? '@' + l.externalUsername : l.displayName || '')}</td><td>${esc(l.identityStatus === 'Active' ? 'Đã liên kết' : l.identityStatus === 'PendingCode' ? 'Chờ xác nhận' : l.identityStatus || '')}</td><td>${esc(dtx(l.linkedAt))}</td><td><button class="btn xs danger" data-un="${esc(l.id)}">Gỡ</button></td></tr>`).join('')}</tbody></table></div>` : ''), 'Chưa liên kết bot nào – bấm Tạo mã liên kết');
  };
  // Menu tài khoản (góc phải) → Liên kết Telegram: trạng thái + Tạo mã (15′) + Hủy liên kết.
  UI.telegramLink = () => UI.modal({
    title: 'Liên kết Telegram', sub: 'Nhận thông báo và thao tác qua bot', size: 'md',
    body: `<div id="myTg">${LOADING}</div><div class="grid mt"><button class="btn primary" data-tgnew>${I('plus')} Tạo mã liên kết</button></div><div class="small muted mt">Mã dùng một lần, hết hạn sau 15 phút. Mở bot bằng nút hoặc quét QR, bot tự liên kết với tài khoản này.</div>`,
    onMount: (m, close) => {
      UI.telegramBox(m);
      m.querySelector('[data-tgnew]').onclick = () => Store.act('TelegramLink/CreateCode', {}).then(r => { if (r.ok && !r.demo) { close(); showTelegramCode(r.data); } });
    }
  });
  // adm-employee: nút "Tạo mã liên kết bot" trên dòng – chỉ khi có quyền Sửa màn hồ sơ nhân viên (mã 72 giờ).
  APP.register('adm-employee', (el, f) => {
    APP.genericList(el, f);
    if (!Store.isLive || Store.perm('adm-employee').edit === false) return;
    APP.rowExtra(el, tr => {
      const cell = tr.lastElementChild; if (!cell || cell.querySelector('[data-tgemp]')) return;
      cell.insertAdjacentHTML('beforeend', ` <button class="btn xs" data-tgemp title="Tạo mã liên kết bot (hạn 72 giờ)">${I('msg')}</button>`);
      cell.querySelector('[data-tgemp]').onclick = e => { e.stopPropagation(); Store.act('TelegramLink/CreateCodeForEmployee', { form: { EmployeeId: tr.dataset.row } }).then(r => { if (r.ok && !r.demo) showTelegramCode(r.data); }); };
    });
  });
  APP.register('hr-my', (el, f) => {
    if (!Store.isLive) return onlyLive(el, f);
    el.innerHTML = APP.header(f, '', { noNote: true, sub: 'Ca làm việc, đơn nghỉ, đơn đổi ca, chuyến giao của tài khoản đang đăng nhập và liên kết bot Telegram.' }) +
      box('myShift', 'Ca của tôi', '14 ngày tới') + `<div class="grid g2">${box('myLeave', 'Đơn nghỉ của tôi', '', `<a class="btn sm" href="#/hr-leave">Tạo đơn nghỉ →</a>`)}${box('mySwap', 'Đơn đổi ca của tôi', '', `<a class="btn sm" href="#/hr-swap">Màn đổi ca →</a>`)}</div>` +
      box('myRoute', 'Chuyến giao của tôi', 'Hôm nay – dành cho tài xế') + box('myTg', 'Liên kết Telegram', 'Nhận thông báo và thao tác qua bot', `<button class="btn sm primary" id="myTgNew">${I('plus')} Tạo mã liên kết</button>`);
    const shifts = () => {
      const o = el.querySelector('#myShift'); let list = [];
      o._bind = () => o.querySelectorAll('[data-sw]').forEach(b => b.onclick = () => swapShift(list[+b.dataset.sw], shiftsAndSwaps));
      return fill(el, 'myShift', 'ShiftAssignment/GetMine', { days: 14 }, x => { list = x.list; return list.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Ngày</th><th>Ca</th><th>Giờ</th><th>Trạng thái</th><th></th></tr></thead><tbody>${list.map((s, i) => `<tr><td>${esc(U.date(s.workDate))}</td><td>${esc(s.shiftName || s.shiftCode || '')}</td><td>${esc(U.time(s.plannedStart))} – ${esc(U.time(s.plannedEnd))}</td><td>${UI.badge(s.assignmentStatus, 'ShiftAssignment', 'AssignmentStatus')}</td><td class="actions"><button class="btn xs" data-sw="${i}">Xin đổi ca</button></td></tr>`).join('')}</tbody></table></div>` : ''; }, 'Không có ca nào trong 14 ngày tới');
    };
    const swaps = () => fill(el, 'mySwap', 'ShiftSwapRequest/GetMine', null, x => (x.list.length ? listHtml(x.list, 6) : ''), 'Chưa có đơn đổi ca');
    const shiftsAndSwaps = () => { shifts(); swaps(); };
    shiftsAndSwaps();
    fill(el, 'myLeave', 'LeaveRequest/GetMine', null, x => (x.list.length ? listHtml(x.list, 6) : ''), 'Chưa có đơn nghỉ');
    fill(el, 'myRoute', 'DeliveryRoute/GetMine', null, x => { const lr = (x.data && x.data.listRoute) || []; return lr.length ? lr.map(r => `<div class="mb"><div class="spread"><b>${esc((r.route && (r.route.routeNo || r.route.routeNumber)) || '')} · ${esc(r.licensePlate || '')}</b><span class="badge gray">${esc(r.statusName || '')}</span></div><div class="small muted">${U.num(r.loadMeals || 0)} suất${r.departureTemperatureC !== null && r.departureTemperatureC !== undefined ? ' · xuất phát ' + U.num(r.departureTemperatureC, 1) + ' °C' : ''}</div>${listHtml(r.listStop || [], 6)}</div>`).join('') + `<a class="btn sm" href="#/log-route">Mở màn Tuyến giao hàng →</a>` : ''; }, 'Hôm nay không có chuyến giao nào của bạn');
    const tg = () => UI.telegramBox(el);
    tg();
    el.querySelector('#myTgNew').onclick = () => Store.act('TelegramLink/CreateCode', {}).then(r => { if (r.ok && !r.demo) showTelegramCode(r.data); });
  });

  // Job tự động (chỉ quản trị): danh sách job, lần chạy gần nhất / kế tiếp, chạy tay.
  APP.FN['adm-job'] = { extra: true, ph: 'P0', grp: 'Quản trị hệ thống', fn: 'Job tự động', id: 'adm-job', name: 'Job tự động', types: ['LS'], table: null, rel: [], st: 'MAPPED', note: '' };
  APP.register('adm-job', (el, f) => {
    if (!Store.isLive) return onlyLive(el, f);
    el.innerHTML = APP.header(f, '', { noNote: true, sub: 'Các job chạy theo giờ (khóa đơn, tính nhu cầu nguyên liệu, làm mới KPI…). Chạy tay khi cần – chỉ quản trị.' }) + `<div class="card panel"><div id="jobList">${LOADING}</div></div>`;
    const load = () => {
      const o = el.querySelector('#jobList');
      o._bind = () => o.querySelectorAll('[data-job]').forEach(b => b.onclick = () => UI.confirm(`Chạy ngay job <b>${esc(b.dataset.name)}</b>?`, () => Store.act('Job/Run', { form: { jobCode: b.dataset.job } }).then(r => { if (!r.ok || r.demo) return; UI.toast(okMsg(r, 'Đã chạy job'), 'ok'); if (r.data && typeof r.data === 'object') resultModal('Kết quả chạy job ' + b.dataset.name, r.data); load(); }), { ok: 'Chạy ngay' }));
      fill(el, 'jobList', 'Job/GetAll', null, x => (x.list.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Job</th><th>Giờ chạy</th><th>Lần kế tiếp</th><th>Lần gần nhất</th><th>Kết quả</th><th></th></tr></thead><tbody>${x.list.map(j => { const l = j.lastRun || {}; return `<tr><td><b>${esc(j.jobName || j.jobCode)}</b><span class="sub" style="white-space:normal;max-width:420px">${esc(j.description || '')}</span></td><td>${esc(j.at || '')}${j.enabled === false ? ' <span class="tag">Tắt</span>' : ''}</td><td>${esc(dtx(j.nextRunAt))}</td><td>${esc(dtx(l.startedAt))}</td><td>${l.runStatus ? `<span class="badge ${/Succeeded|Success/i.test(l.runStatus) ? 'green' : /Fail/i.test(l.runStatus) ? 'red' : 'orange'}">${esc(l.runStatus)}</span>` : ''}<span class="sub" style="white-space:normal;max-width:320px">${esc(l.message || '')}</span></td><td class="actions"><button class="btn xs primary" data-job="${esc(j.jobCode)}" data-name="${esc(j.jobName || j.jobCode)}">Chạy ngay</button></td></tr>`; }).join('')}</tbody></table></div>` : ''), 'Chưa có job nào được khai báo');
    };
    load();
  });
})();
