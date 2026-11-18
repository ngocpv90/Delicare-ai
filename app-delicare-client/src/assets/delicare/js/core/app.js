/* DELICARE – Khung ứng dụng: sidebar, header, router, màn hình chung theo schema */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, CAT = Store.cat;
  const FN = {}; CAT.funcs.forEach(f => FN[f.id] = f);
  // Màn hình điều hướng (không phải bảng) – bổ sung vào menu
  FN['flow-e2e'] = { ph: '—', grp: 'Tổng quan điều hành', fn: 'Luồng nghiệp vụ End-to-End', id: 'flow-e2e', name: 'Luồng nghiệp vụ E2E (9 luồng)', types: ['DB'], table: null, rel: [], st: 'NAV', note: '' };
  const CHILD = new Set(['crm-order-line', 'scm-pr-line', 'scm-po-line', 'scm-gr-line', 'scm-issue-line', 'scm-stockcount-line', 'prod-plan-line', 'qc-inspection-line', 'qc-checklist-item', 'menu-recipe-line', 'menu-menu-line', 'wf-step', 'wf-decision', 'wf-item', 'log-stop', 'log-event', 'log-complaint-history', 'crm-contract-meal']);
  // Ẩn tạm khỏi menu + tìm kiếm khi chạy với dữ liệu thật (chế độ DEMO vẫn hiện đủ như mockup). Bỏ id khỏi danh sách để hiện lại.
  //  - Chưa có API: màn tĩnh / GAP (danh mục tham chiếu, chuyển kho, điều chỉnh tồn) và 2 màn tra cứu cấu trúc database (D-58).
  //  - Tích hợp bot (kênh, bot, định danh, tin vào / ra, nhật ký): backend mới có CRUD thô, nghiệp vụ bot làm sau (D-44).
  const HIDDEN_LIVE = new Set(['adm-reference', 'scm-transfer', 'scm-adjustment', 'dbx-explorer', 'dbx-gap',
    'int-channel', 'int-bot', 'int-identity', 'int-inbound', 'int-outbound', 'int-log']);
  // Ngoài danh sách trên: màn có bảng chính mà backend đang nối chưa có API (404) hoặc API đang lỗi → cũng ẩn (Store.probe dò lúc mở app).
  const hiddenNow = id => Store.isLive && (HIDDEN_LIVE.has(id) || !!(FN[id] && FN[id].table && Store.isBroken(FN[id].table)));
  const SCREENS = {};
  const APP = { FN, SCREENS, current: null, params: {} };
  APP.register = (id, fn) => SCREENS[id] = fn;
  // Tiền tố hash: '#/' khi chạy độc lập, '#/quan-tri/' khi chạy trong Angular (Angular cũng dùng hash routing).
  APP.base = () => (window.DC_HOST && window.DC_HOST.hashBase) || '#/';
  APP.href = id => APP.base() + id;
  APP.go = id => { location.hash = APP.href(id); };
  // ---------- Sidebar ----------
  function buildNav() {
    const nav = document.getElementById('navGroups');
    const order = CAT.menuOrder; const groups = {};
    Object.values(FN).forEach(f => { if (CHILD.has(f.id) || hiddenNow(f.id)) return; (groups[f.grp] = groups[f.grp] || []).push(f); });
    const dash = groups['Tổng quan điều hành'] || []; dash.sort((a, b) => a.id === 'dash-exec' ? -1 : 1);
    nav.innerHTML = order.filter(g => groups[g]).map((g, gi) => {
      const items = groups[g].filter(f => Store.perm(f.id).access !== false);
      if (!items.length) return '';
      return `<section class="group ${gi > 1 ? 'closed' : ''}" data-g="${esc(g)}"><button class="group-toggle">${I(U.GROUP_ICON[g] || 'clipboard')}<span>${esc(g)}</span><span class="chev">⌄</span></button><div class="group-items">${items.map(f => `<a class="nav-link" href="${APP.href(f.id)}" data-nav="${f.id}" title="${esc(f.name)}">${I((U.MENU_ICON && U.MENU_ICON[f.id]) || U.SCREEN_ICON[f.id.split('-')[0]] || 'clipboard')}<span>${esc(f.fn)}</span>${f.st === 'GAP' ? '<i class="gap-dot">GAP</i>' : ''}${f.id === 'wf-inbox' ? `<i class="count" id="inboxCount"></i>` : ''}</a>`).join('')}</div></section>`;
    }).join('');
    nav.querySelectorAll('.group-toggle').forEach(b => b.onclick = () => b.parentElement.classList.toggle('closed'));
    updateCounts();
  }
  function updateCounts() {
    const list = APP.alerts();
    // Chế độ API có kết quả chuông: số chờ duyệt = cảnh báo APPROVAL_PENDING (đúng hộp duyệt của người đăng nhập), không cần tải bảng ApprovalRequest.
    const pendAlert = live.alerts && list.find(a => a.code === 'APPROVAL_PENDING');
    const n = live.alerts ? (pendAlert ? pendAlert.n : 0) : Store.rows('ApprovalRequest').filter(a => ['Pending', 'InProgress'].includes(a.RequestStatus)).length;
    const c = document.getElementById('inboxCount'); if (c) { c.textContent = n; c.style.display = n ? '' : 'none'; }
    const nc = document.getElementById('notifyCount'); if (nc) nc.textContent = live.alerts ? list.reduce((s, a) => s + (a.n || 0), 0) : list.length;
  }
  APP.updateCounts = updateCounts;
  // ---------- Cảnh báo ----------
  // Chế độ API (BE3.3): Common/GetAlerts – server tính theo quyền Xem + chi nhánh đang chọn, cache 60 giây; giao diện hỏi lại mỗi 60 giây.
  // Chưa có kết quả → danh sách rỗng (không kéo 7 bảng về trình duyệt). API lỗi / backend cũ chưa có → tính cục bộ như mockup.
  const SEV_COLOR = { HIGH: 'red', MEDIUM: 'orange', LOW: 'blue' };
  const ALERT_ICON = { STOCK_BELOW_MIN: 'alert', APPROVAL_PENDING: 'approve', QC_INCOMING_PENDING: 'shield', LOT_ON_HOLD: 'shield', TEMPERATURE_OUT_OF_RANGE: 'thermo', COMPLAINT_OVERDUE: 'msg', COMPLAINT_OPEN: 'msg', OUTBOUND_FAILED: 'bot', LOT_EXPIRING: 'lot' };
  const live = { alerts: null, at: 0, busy: false, branch: null, failed: false };
  const ALERT_TTL = 60000;
  function pullAlerts() {
    if (!Store.isLive || live.busy) return;
    const br = Store.ctx.branch || '';
    if (live.branch === br && Date.now() - live.at < ALERT_TTL) return;
    live.busy = true;
    Store.query('Common/GetAlerts', { branchId: br }).then(x => {
      live.busy = false; live.at = Date.now(); live.branch = br; live.failed = !x.ok;
      live.alerts = x.ok ? x.list.map(a => ({ code: a.code, c: SEV_COLOR[a.severity] || 'orange', i: ALERT_ICON[a.code] || 'alert', t: a.title || a.code, s: a.detail || '', go: String(a.route || '').replace(/^#\//, ''), n: a.count || 0 })) : null;
      updateCounts();
      if (APP.current === 'dash-exec' && !overlayOpen() && !cycleActive) APP.refresh();
    });
  }
  APP.alerts = () => {
    if (Store.isLive) {
      pullAlerts();
      if (live.alerts) return live.alerts;
      if (!live.failed) return [];
    }
    const a = [];
    const bal = U.group(Store.rows('InventoryBalance'), 'IngredientId');
    const low = Store.rows('InventoryPolicie').filter(p => U.sum(bal[p.IngredientId] || [], 'AvailableQuantity') < (p.MinimumStock || 0));
    if (low.length) a.push({ c: 'red', i: 'alert', t: low.length + ' nguyên liệu dưới tồn tối thiểu', s: low.slice(0, 4).map(p => Store.label('Ingredient', p.IngredientId)).join(', ') + (low.length > 4 ? '…' : '') + ' · InventoryBalance vs InventoryPolicie', go: 'scm-inv-balance' });
    const pend = Store.rows('ApprovalRequest').filter(r => ['Pending', 'InProgress'].includes(r.RequestStatus)); if (pend.length) a.push({ c: 'orange', i: 'approve', t: pend.length + ' yêu cầu chờ phê duyệt', s: 'ApprovalRequest.RequestStatus = Pending/InProgress', go: 'wf-inbox' });
    Store.rows('QCInspectionLine').filter(l => l.Result === 'HOLD').forEach(l => a.push({ c: 'red', i: 'shield', t: 'QC tạm giữ: ' + Store.label('Ingredient', l.IngredientId), s: (l.Remark || '') + ' · QCInspectionLine.Result = HOLD', go: 'qc-inspection' }));
    const tl = Store.rows('TemperatureLog').filter(x => x.IsWithinRange === 0); if (tl.length) a.push({ c: 'orange', i: 'thermo', t: tl.length + ' lần đo nhiệt độ ngoài ngưỡng (48h)', s: 'TemperatureLog.IsWithinRange = 0', go: 'qc-temperature' });
    const cp = Store.rows('CustomerComplaint').filter(x => ['Open', 'InProgress'].includes(x.ComplaintStatus)); if (cp.length) a.push({ c: 'orange', i: 'msg', t: cp.length + ' khiếu nại đang mở', s: 'CustomerComplaint.ComplaintStatus', go: 'log-complaint' });
    const om = Store.rows('OutboundMessage').filter(x => x.MessageStatus === 'Failed'); if (om.length) a.push({ c: 'red', i: 'bot', t: om.length + ' tin nhắn Bot gửi lỗi', s: 'OutboundMessage.MessageStatus = Failed', go: 'int-outbound' });
    const exp = Store.rows('MaterialLot').filter(l => l.Status === 'Available' && l.ExpiryDate && l.ExpiryDate.slice(0, 10) <= U.addDays(Store.ctx.date, 2)); if (exp.length) a.push({ c: 'orange', i: 'lot', t: exp.length + ' lô sắp hết hạn trong 2 ngày', s: 'MaterialLot.ExpiryDate (FEFO)', go: 'scm-lot' });
    return a;
  };
  // ---------- Header ----------
  // Chế độ API: bỏ các câu "dữ liệu demo" cố định trong mô tả màn hình của mockup.
  APP.liveText = html => (Store.hasHost && Store.mode === 'api') ? html.replace(/\s*\(DỮ LIỆU DEMO\)/g, '').replace(/;?\s*số liệu trên mockup là DEMO\.?/g, '.') : html;
  // Chế độ dữ liệu thật: bỏ các phần chỉ để trình diễn của mockup (tên bảng / cột, ghi chú GAP, truy vết DB) – CLAUDE.md 4.4.
  const LIVE = () => Store.isLive;
  APP.header = (f, actions, opt = {}) => APP.liveText(APP._header(f, actions, opt));
  APP._header = (f, actions, opt = {}) => `<div class="breadcrumb"><a href="${APP.href('dash-exec')}">Trang chủ</a><span>›</span><span>${esc(f.grp)}</span><span>›</span><strong>${esc(f.name)}</strong></div>
    <div class="page-heading"><div><div class="eyebrow">${LIVE() ? esc(f.grp) : (f.ph !== '—' ? f.ph + ' · ' : '') + esc(f.grp) + (f.table ? ' · dbo.' + f.table : '')}</div><h1>${esc(opt.title || f.name)}</h1><div class="subtitle">${opt.sub || esc(f.fn) + (!LIVE() && f.rel && f.rel.length ? ' · Liên quan: ' + f.rel.join(', ') : '')}</div></div><div class="heading-actions">${actions || ''}${f.table && !LIVE() ? `<button class="btn" data-trace="${f.table}">${I('database')} Truy vết DB</button>` : ''}</div></div>${opt.noNote || LIVE() ? '' : UI.gapNote(f)}`;
  // ---------- Màn hình chung theo schema (dùng cho chức năng chưa có màn hình chuyên biệt) ----------
  const PREF_NO = ['OrderNumber', 'ContractNumber', 'RequestNumber', 'PurchaseOrderNumber', 'ReceiptNumber', 'LotNumber', 'IssueNumber', 'CountNumber', 'PlanNumber', 'ProductionOrderNumber', 'BatchNumber', 'PackingOrderNumber', 'DeliveryNumber', 'RouteNo', 'InspectionNumber', 'CleaningBatchNumber', 'TransactionNumber', 'RequirementNo', 'RequestNo', 'ComplaintNo', 'Code', 'MetricCode', 'ChannelCode', 'BotCode', 'ItemCode', 'LicensePlate', 'VersionCode', 'UserName'];
  APP.pickCols = (t, max = 8) => {
    const M = Store.colmeta[t]; const cand = Object.values(M).filter(m => !Store.isDeprecated(t, m.name) && ['Form / Hiển thị', 'Trạng thái (badge + lọc)', 'Lookup (tham chiếu)'].includes(m.usage) && !/Note|Description|Remark|Json|Url|Instructions|Hash|Secret|Address|Email|Phone|IdempotencyKey|Correlation|External|Provider|Watermark|Changes|Specification|Condition|Latitude|Longitude|Avatar|Has$|^CompanyId$|DateOfBirth|Gender/.test(m.name));
    const out = []; const push = m => { if (m && !out.includes(m) && out.length < max) out.push(m); };
    push(cand.find(m => PREF_NO.includes(m.name)));
    push(cand.find(m => ['Name', 'FullName', 'DisplayName', 'MetricName', 'ChannelName', 'BotName', 'ItemName', 'NameVN', 'MealName', 'FileName'].includes(m.name)));
    cand.filter(m => m.usage.startsWith('Lookup') && Store.target(t, m.name)).slice(0, 3).forEach(push);
    cand.filter(m => /Date|At$|Time$/.test(m.name)).slice(0, 1).forEach(push);
    cand.filter(m => /decimal|int/.test(m.type) && !m.name.endsWith('Id')).slice(0, 2).forEach(push);
    cand.filter(m => !m.usage.startsWith('Trạng thái')).forEach(push);
    const s = cand.find(m => m.usage.startsWith('Trạng thái')); if (s) { if (out.length >= max) out.pop(); out.push(s); }
    return out.map(m => ({ k: m.name, label: U.colLabel(m.name), num: /decimal|int/.test(m.type) && !m.name.endsWith('Id') && m.usage !== 'Trạng thái (badge + lọc)' }));
  };
  APP.nextStatus = (t, col, v) => { const d = Store.statusDef(t, col); if (!d) return null; const flow = d.flow.split(';')[0]; const main = flow.split('→').map(s => s.trim().split('/')[0].trim()); const i = main.indexOf(v); return i >= 0 && i < main.length - 1 ? main[i + 1] : null; };
  APP.genericList = (el, f, opt = {}) => {
    const t = f.table, perm = Store.perm(f.id), sc = Store.statusCol(t);
    const rowsFn = opt.rows || (() => Store.rows(t));
    const cols = opt.cols || APP.pickCols(t);
    const stat = sc ? U.group(rowsFn(), sc) : {};
    const statusChips = sc ? Object.entries(stat).sort((a, b) => b[1].length - a[1].length) : [];
    el.innerHTML = APP.header(f, `${perm.exp !== false ? `<button class="btn" data-act="export">${I('export')} Xuất CSV</button>` : ''}${opt.noAdd ? '' : `<button class="btn primary" data-act="add" ${perm.add === false ? 'disabled title="Tài khoản không có quyền Thêm"' : ''}>${I('plus')} Tạo mới</button>`}`) + (opt.top || '') +
      `<div class="kpis">${UI.kpi('Tổng bản ghi', U.num(rowsFn().length), LIVE() ? esc(f.fn) : 'dbo.' + t + ' · ' + APP.srcText(t), 'clipboard', 'green')}${statusChips.slice(0, 3).map(([k, v], i) => UI.kpi(UI.statusVi(t, sc, k) || '(trống)', U.num(v.length), LIVE() ? 'Theo trạng thái' : `${sc} = ${k || 'NULL'}`, 'layers', ['blue', 'orange', 'purple'][i])).join('')}</div>
      <div class="card panel">${sc ? `<div class="row-flex mb"><span class="small muted">${LIVE() ? 'Lọc nhanh:' : 'Lọc nhanh ' + sc + ':'}</span><button class="chip active" data-st="">Tất cả</button>${statusChips.map(([k, v]) => `<button class="chip" data-st="${esc(k)}">${esc(UI.statusVi(t, sc, k) || '(trống)')} · ${v.length}</button>`).join('')}</div>` : ''}<div id="gt"></div></div>`;
    const fks = cols.filter(c => Store.target(t, c.k)).slice(0, 1).map(c => { const vals = [...new Set(rowsFn().map(r => r[c.k]).filter(Boolean))]; return { k: c.k, label: c.label, options: vals.map(v => ({ v, l: Store.label(Store.target(t, c.k), v) })).sort((a, b) => a.l.localeCompare(b.l)) }; });
    const ref = {}; APP._lastTbl = ref;
    const tbl = UI.table(el.querySelector('#gt'), {
      table: t, cols, rows: rowsFn, filters: fks, pageSize: 10, onRow: id => APP.openDetail(t, id, f),
      actions: r => { const nx = sc && APP.nextStatus(t, sc, r[sc]); return `<button class="btn xs" data-view="${r.Id}">${I('eye')}</button> <button class="btn xs" data-edit="${r.Id}" ${perm.edit === false ? 'disabled' : ''}>${I('edit')}</button>${nx && !opt.noTransit && !LIVE() ? ` <button class="btn xs" data-next="${r.Id}" data-to="${nx}" ${perm.edit === false ? 'disabled' : ''} title="${sc}: ${r[sc]} → ${nx}">→ ${esc(UI.statusVi(t, sc, nx))}</button>` : ''}${perm.del ? ` <button class="btn xs danger" data-del="${r.Id}">${I('trash')}</button>` : ''}`; },
      after: tb => {
        tb.querySelectorAll('[data-view]').forEach(b => b.onclick = () => APP.openDetail(t, b.dataset.view, f));
        tb.querySelectorAll('[data-edit]').forEach(b => b.onclick = () => UI.form(t, Store.get(t, b.dataset.edit), { title: f.name, onSave: () => APP.refresh() }));
        tb.querySelectorAll('[data-next]').forEach(b => b.onclick = () => { const r = Store.get(t, b.dataset.next); UI.confirm(`Chuyển <b>${esc(Store.label(t, r))}</b> từ ${UI.badge(r[sc], t, sc)} sang ${UI.badge(b.dataset.to, t, sc)}?<div class="small muted mt">Ghi vào <code>${t}.${sc}</code> và <code>AuditChange</code>. Trạng thái PROPOSED – BA/PO CONFIRM.</div>`, note => { Store.update(t, r.Id, { [sc]: b.dataset.to }, note); UI.toast('Đã chuyển trạng thái (DEMO)', 'ok'); APP.refresh(); }, { input: 'Ghi chú', ok: 'Chuyển trạng thái' }); });
        tb.querySelectorAll('[data-del]').forEach(b => b.onclick = () => UI.confirm(LIVE() ? 'Xóa bản ghi này?' : 'Xóa mềm bản ghi này? (IsDeleted = 1)', () => { Store.remove(t, b.dataset.del); UI.toast(LIVE() ? 'Đã xóa' : 'Đã xóa mềm (DEMO)', 'ok'); APP.refresh(); }, { danger: true, ok: 'Xóa' }));
        if (ref.extra) tb.querySelectorAll('tr[data-row]').forEach(ref.extra);
      }
    });
    el.querySelectorAll('[data-st]').forEach(c => c.onclick = () => { el.querySelectorAll('[data-st]').forEach(x => x.classList.toggle('active', x === c)); tbl.setFilter(sc, c.dataset.st); });
    el.querySelector('[data-act="add"]') && (el.querySelector('[data-act="add"]').onclick = () => UI.form(t, null, { title: f.name, defaults: opt.defaults, onSave: () => APP.refresh() }));
    el.querySelector('[data-act="export"]') && (el.querySelector('[data-act="export"]').onclick = () => { U.csv(rowsFn(), cols.map(c => ({ k: c.k, label: c.label, col: c.k, text: r => UI.text(t, c.k, r[c.k]) })), `DELICARE_${LIVE() ? '' : 'DEMO_'}${t}.csv`); UI.toast('Đã xuất CSV ' + rowsFn().length + ' dòng' + (LIVE() ? '' : ' (DỮ LIỆU DEMO)'), 'ok'); });
    // Chế độ API: Common/ExportList xuất Excel từ server (đủ dòng, đúng quyền Xuất) cho các bảng backend có đăng ký.
    if (Store.isLive && perm.exp !== false) APP.resources().then(set => {
      const b = el.querySelector('[data-act="export"]'), key = Store.controllerOf(t);
      if (!b || !b.isConnected || !set.has(key.toLowerCase()) || el.querySelector('[data-act="xlsx"]')) return;
      b.insertAdjacentHTML('afterend', `<button class="btn" data-act="xlsx">${I('export')} Xuất Excel</button>`);
      el.querySelector('[data-act="xlsx"]').onclick = () => Store.download('Common/ExportList', { Resource: key, Format: 'xlsx' }).then(r => { if (r.ok) UI.toast('Đã xuất ' + esc(r.fileName || 'Excel'), 'ok'); });
    });
    return tbl;
  };
  // Danh sách resource dùng chung của backend (Common/GetResources) – tải 1 lần.
  APP.resources = () => APP._res || (APP._res = Store.query('Common/GetResources').then(x => new Set((x.ok ? (Array.isArray(x.data) ? x.data : x.list) : []).map(r => String(r.resource || '').toLowerCase()))));
  // Gắn nút thao tác riêng theo dòng – giữ nguyên khi lọc/sắp xếp/phân trang
  APP.rowExtra = (el, fn) => { const r = APP._lastTbl; if (r) r.extra = fn; el.querySelectorAll('#gt tr[data-row]').forEach(fn); };
  // Kho nguyên liệu mặc định: WH-RM (dữ liệu mẫu) → kho nguyên liệu đầu tiên → kho đầu tiên.
  APP.rawWarehouseId = () => (Store.one('Warehouse', 'Code', 'WH-RM') || Store.rows('Warehouse').find(w => /RAW/i.test(w.WarehouseType || '')) || Store.rows('Warehouse')[0] || {}).Id || null;
  APP.openDetail = (t, id, f, opt) => UI.detail(t, id, Object.assign({ screen: f && f.id, onChange: () => APP.refresh() }, opt || {}));
  // ---------- Màn hình GAP ----------
  function gapScreen(el, f) {
    const g = (CAT.gaps.func || []).concat(CAT.gaps.col || []).find(x => (x[5] || '').includes(f.id)) || null;
    let extra = '';
    if (f.id === 'adm-reference') extra = `<div class="card panel"><div class="panel-head"><div><div class="panel-title">15 danh mục tham chiếu theo BRD</div><div class="panel-desc">Giá trị PROPOSED dùng cho mockup – lưu dạng text trong các cột liệt kê</div></div></div><div id="rt"></div></div>`;
    if (f.id === 'log-gps') extra = gpsMock();
    if (f.id === 'scm-transfer' || f.id === 'scm-adjustment') extra = `<div class="card panel"><div class="panel-title mb">Dữ liệu hiện có trong DB: sổ cái InventoryTransaction</div><div id="rt"></div></div>`;
    el.innerHTML = APP.header(f, '', { noNote: true }) + `<div class="notice gap">${I('alert')}<div><b>DATABASE GAP${g ? ' · ' + g[0] : ''} – chưa có bảng/cột hỗ trợ trong SQL nguồn</b>${esc(f.note)}${g ? `<div class="mt small"><b>Đề xuất xử lý:</b> ${esc(g[6])} · Ưu tiên: ${esc(g[7])} · Cần xác nhận: ${esc(g[8])}</div>` : ''}<div class="small mt">Mockup KHÔNG giả lập bảng mới. Màn hình chỉ minh họa nhu cầu và dữ liệu sẵn có để BA/PO quyết định.</div></div></div>${extra}`;
    if (f.id === 'adm-reference') UI.table(el.querySelector('#rt'), { cols: [{ k: 0, label: 'Mã' }, { k: 1, label: 'Danh mục' }, { k: 2, label: 'Bảng SQL' }, { k: 3, label: 'Cột đang lưu' }, { k: 4, label: 'Giá trị đề xuất' }, { k: 6, label: 'Trạng thái', render: r => UI.badge(r[6] === 'Có bảng' ? 'Active' : r[6] === 'Open' ? 'Open' : 'OK') }], rows: CAT.gaps.ref.map((r, i) => Object.assign({ Id: 'ref' + i }, r)), pageSize: 15 });
    if (f.id === 'scm-transfer' || f.id === 'scm-adjustment') UI.table(el.querySelector('#rt'), { table: 'InventoryTransaction', cols: APP.pickCols('InventoryTransaction', 8), rows: () => Store.rows('InventoryTransaction').filter(r => f.id === 'scm-transfer' ? /TRANSFER/.test(r.TransactionType) : ['ADJUSTMENT', 'WASTE'].includes(r.TransactionType)), pageSize: 8 });
  }
  APP.gapScreen = gapScreen;
  function gpsMock() {
    const ev = Store.rows('DeliveryEvent').filter(e => e.EventAt && e.EventAt.startsWith(Store.ctx.date) && e.Latitude);
    const xs = ev.map(e => +e.Longitude), ys = ev.map(e => +e.Latitude);
    const mnx = Math.min(...xs, 105.74), mxx = Math.max(...xs, 105.97), mny = Math.min(...ys, 20.99), mxy = Math.max(...ys, 21.22);
    const X = v => 30 + (v - mnx) / (mxx - mnx) * 580, Y = v => 270 - (v - mny) / (mxy - mny) * 240;
    return `<div class="card panel"><div class="panel-head"><div><div class="panel-title">Tọa độ sẵn có: DeliveryEvent.Latitude / Longitude (theo sự kiện, hôm nay)</div><div class="panel-desc">Không phải tracking liên tục – minh họa để BA/PO xác nhận phạm vi GPS</div></div></div><svg class="chart" viewBox="0 0 640 300" style="background:#F7FAF3;border-radius:12px"><rect x="20" y="20" width="600" height="260" fill="none" stroke="#E2E7DC" stroke-dasharray="4 4"/><circle cx="${X(105.8464)}" cy="${Y(21.1388)}" r="9" fill="#91AF42"/><text x="${X(105.8464) + 12}" y="${Y(21.1388) + 4}" style="fill:#4E5C51;font-weight:700">Bếp trung tâm</text>${ev.map(e => `<circle cx="${X(+e.Longitude)}" cy="${Y(+e.Latitude)}" r="5" fill="${e.EventType === 'DELIVERED' ? '#4C83B6' : '#F07825'}"><title>${esc(e.EventType)} · ${esc(e.Description || '')} · ${U.dt(e.EventAt)}</title></circle>`).join('')}</svg><div class="chart-legend mt"><span><i class="legend-dot" style="background:#F07825"></i>ARRIVED</span><span><i class="legend-dot" style="background:#4C83B6"></i>DELIVERED</span><span>${ev.length} sự kiện có tọa độ</span></div></div>`;
  }
  // ---------- Nguồn dữ liệu của màn hình (API thật / DEMO) ----------
  const API_MODE = () => Store.hasHost && Store.mode === 'api';
  APP.srcText = t => {
    if (!t) return 'DỮ LIỆU DEMO';
    const s = Store.srcOf(t);
    return s === 'api' ? 'API ' + Store.controllerOf(t) : s === 'error' ? 'LỖI API ' + Store.controllerOf(t) : s === 'pending' || s === 'idle' ? 'Đang tải API…' : 'DỮ LIỆU DEMO';
  };
  let screenTables = [];
  function setRibbon() {
    const rb = document.getElementById('dataRibbon'); if (!rb) return;
    const tabs = screenTables.filter(t => Store.schema.tables[t]);
    const api = tabs.filter(t => Store.srcOf(t) === 'api'), demo = tabs.filter(t => Store.srcOf(t) === 'demo'), err = tabs.filter(t => Store.srcOf(t) === 'error');
    let text, cls;
    if (!API_MODE()) { text = 'DỮ LIỆU DEMO'; cls = ''; }
    else if (err.length) { text = 'LỖI TẢI ' + err.length + ' BẢNG'; cls = 'err'; }
    else if (api.length && !demo.length) { text = 'DỮ LIỆU THẬT · API'; cls = 'live'; }
    else if (api.length && demo.length) { text = 'API + DEMO'; cls = 'mixed'; }
    else if (!tabs.length) { text = 'MÀN HÌNH TĨNH (HTML)'; cls = 'static'; }
    else { text = 'DỮ LIỆU DEMO'; cls = ''; }
    rb.textContent = text; rb.className = 'demo-ribbon ' + cls;
    rb.title = (api.length ? 'Đọc từ API: ' + api.map(Store.controllerOf).join(', ') + '\n' : '') + (demo.length ? 'Dữ liệu DEMO (chưa có API): ' + demo.join(', ') + '\n' : '') + (err.length ? 'Lỗi API: ' + err.map(t => t + ' – ' + Store.errorOf(t)).join('; ') : '');
  }
  const overlayOpen = () => !!document.querySelector('#content ~ * .overlay, .overlay, .drawer-wrap');
  function setLoading(on) { document.getElementById('app').classList.toggle('dc-loading', !!on); }
  // ---------- Router ----------
  let routeSeq = 0, cycleActive = false, navErrKey = '';
  function route() {
    const seq = ++routeSeq;
    const base = APP.base(), raw = location.hash;
    const h = (raw.startsWith(base) ? raw.slice(base.length) : raw.replace(/^#\/?/, '')) || 'dash-exec';
    const [id, qs] = h.split('?'); const params = Object.fromEntries(new URLSearchParams(qs || ''));
    renderScreen(id, params);
    settle(seq, 0, id, params);
  }
  // Màn hình đọc bảng chưa tải → chờ API rồi vẽ lại (lặp tối đa vài vòng khi khóa ngoại kéo thêm bảng).
  function settle(seq, round, id, params) {
    if (!Store.pendingCount()) { cycleActive = false; setLoading(false); setRibbon(); return; }
    cycleActive = true; setLoading(true);
    Store.idle().then(() => {
      if (seq !== routeSeq) return;
      if (overlayOpen() || round >= 6) { cycleActive = false; setLoading(false); setRibbon(); updateCounts(); return; }
      const y = window.scrollY; renderScreen(id, Object.assign({}, params)); window.scrollTo(0, y);
      settle(seq, round + 1, id, params);
    });
  }
  function renderScreen(id, params) {
    const f = FN[id] || FN['dash-exec'];
    APP.current = f.id; APP.params = params;
    document.querySelectorAll('[data-nav]').forEach(a => a.classList.toggle('active', a.dataset.nav === f.id));
    const act = document.querySelector(`[data-nav="${f.id}"]`); if (act) { act.closest('.group').classList.remove('closed'); }
    const el = document.getElementById('content');
    document.title = f.name + ' | DELI-CARE';
    const p = Store.perm(f.id);
    Store.touched.clear();
    if (p.access === false) {
      el.innerHTML = APP.header(f, '', { noNote: true }) + (API_MODE()
        ? `<div class="card panel empty">${I('key')}<h2 class="mt">Tài khoản chưa được cấp quyền truy cập màn hình này</h2><p class="muted">Quyền lấy từ <code>UserRole</code> (AllowAccess) × <code>ObjectMenu</code> (MetaLink = <code>#/${f.id}</code>) của nhóm người dùng. Liên hệ quản trị hệ thống để được cấp quyền.</p></div>`
        : `<div class="card panel empty">${I('key')}<h2 class="mt">Vai trò "${esc(Store.user.group)}" không có quyền truy cập màn hình này</h2><p class="muted">Theo bảng <code>UserRole</code> (AllowAccess) × <code>ObjectMenu</code> (MetaLink = #/${f.id}).</p><button class="btn primary" data-role>Đổi vai trò</button></div>`);
      screenTables = []; return finish(el);
    }
    try {
      if (SCREENS[f.id]) SCREENS[f.id](el, f, params);
      else if (!f.table) gapScreen(el, f);
      else APP.genericList(el, f);
    } catch (err) {
      const used = [...Store.touched];
      if (Store.pendingCount()) { el.innerHTML = APP.header(f, '', { noNote: true }) + `<div class="card panel empty">${I('database')}<h2 class="mt">Đang tải dữ liệu…</h2></div>`; }
      else {
        const empty = used.filter(t => Store.schema.tables[t] && !Store.rows(t).length);
        if (empty.length) el.innerHTML = APP.header(f, '', { noNote: true }) + UI.emptyData(empty);
        else { console.error(err); el.innerHTML = `<div class="notice gap">${I('alert')}<div><b>Lỗi hiển thị màn hình</b>${esc(err.message)}</div></div>`; }
      }
    }
    screenTables = [...Store.touched];
    // Chỉ báo lỗi khi chính bảng của màn hình đang xem bị lỗi thật. Bảng phụ lỗi / backend chưa có API (404) → bỏ qua (menu của bảng đó đã ẩn), ghi console.
    const failed = screenTables.filter(t => Store.srcOf(t) === 'error');
    const errs = failed.filter(t => t === f.table && !Store.isMissing(t));
    if (failed.length) { const key = failed.join(','); if (key !== navErrKey) { navErrKey = key; buildNav(); document.querySelectorAll('[data-nav]').forEach(a => a.classList.toggle('active', a.dataset.nav === f.id)); const act2 = document.querySelector(`[data-nav="${f.id}"]`); if (act2) act2.closest('.group').classList.remove('closed'); } }
    if (f.table && Store.isMissing(f.table)) el.innerHTML = APP.header(f, '', { noNote: true }) + `<div class="card panel empty">${I('database')}<h2 class="mt">Chức năng này chưa có trên máy chủ đang kết nối</h2><p class="muted">Máy chủ chưa có API cho màn hình này nên mục menu đã được ẩn.</p></div>`;
    if (errs.length) el.insertAdjacentHTML('afterbegin', `<div class="notice gap">${I('alert')}<div><b>Không tải được dữ liệu từ API</b>${errs.map(t => `<code>${esc(Store.controllerOf(t))}</code>: ${esc(Store.errorOf(t))}`).join('<br>')}</div></div>`);
    finish(el);
  }
  // Chế độ dữ liệu thật: dọn phần chữ kỹ thuật của mockup còn nằm trong các màn riêng (CLAUDE.md 4.4) –
  // khung ghi chú GAP / PARTIAL / PROPOSED, tên Bảng.Cột trong ngoặc, hậu tố "PROPOSED – BA/PO CONFIRM".
  const TECH_NOTICE = /^\s*(GAP-[A-Z]|DATABASE GAP|PARTIAL\s*[–-])|BA\/PO CONFIRM|SQL DEFAULT|SQL (chưa|không) có|Mockup/;
  const TECH_PAREN = /\s*\((?:dbo\.)?[A-Z][A-Za-z0-9_]*\.[A-Z][A-Za-z0-9_]*(?:\s*[=<>!]+\s*[^()]*)?\)/g;
  const TECH_WORD = /PROPOSED|BA\/PO CONFIRM|BRD\s*:/;
  function cleanDemoText(root) {
    if (!API_MODE() || !root) return;
    root.querySelectorAll('.notice').forEach(n => { if (TECH_NOTICE.test(n.textContent || '') && !/Không tải được|Lỗi/.test(n.textContent || '')) n.remove(); });
    // Chú thích dưới thẻ số liệu / mô tả khối mà nội dung là tên cột kiểu "QcStatus = PASS", "Σ PlannedQuantity" → bỏ chú thích đó.
    root.querySelectorAll('.kpi-foot, .panel-desc').forEach(n => { if (!n.children.length && /\b[A-Z][a-z]+[A-Z][A-Za-z]+\b/.test(n.textContent || '')) n.textContent = ''; });
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    const edits = [];
    while (walker.nextNode()) {
      const node = walker.currentNode, p = node.parentElement;
      if (!p || p.closest('script,style,textarea,input,select,svg,code,.mono')) continue;
      const v = node.nodeValue; if (!v || v.length < 6) continue;
      // Bỏ cả câu có nhãn kỹ thuật (tách câu theo dấu chấm + khoảng trắng để không cắt nhầm "Order.Status").
      const nv = (TECH_WORD.test(v) ? v.split(/(?<=[.!?])\s+/).filter(s => !TECH_WORD.test(s)).join(' ') : v).replace(TECH_PAREN, '');
      if (nv !== v) edits.push([node, nv]);
    }
    edits.forEach(([node, nv]) => { const p = node.parentElement; node.nodeValue = nv; if (p && !p.textContent.trim() && !p.children.length && p !== root) p.remove(); });
  }
  APP.cleanDemoText = cleanDemoText;
  function finish(el) {
    cleanDemoText(el);
    el.insertAdjacentHTML('beforeend', API_MODE()
      ? '<div class="footer-note">DELI-CARE · ENTERPRISE MANAGEMENT</div>'
      : '<div class="footer-note">DELI-CARE · ENTERPRISE MOCKUP · DỮ LIỆU DEMO – KHÔNG PHẢI DỮ LIỆU THẬT · Cấu trúc theo 20261113_structure_db_appdelicare.sql</div>');
    el.querySelectorAll('[data-trace]').forEach(b => b.onclick = () => UI.trace(b.dataset.trace, APP.current));
    el.querySelectorAll('[data-go]').forEach(b => b.onclick = () => APP.go(b.dataset.go));
    if (APP.params.id) { history.replaceState(history.state, '', location.pathname + location.search + APP.href(APP.current)); APP.params = {}; }
    if (innerWidth <= 760) closeMobile();
    updateCounts();
    setRibbon();
    if (!cycleActive) window.scrollTo(0, 0);
  }
  APP.refresh = () => { const y = window.scrollY; route(); window.scrollTo(0, y); };
  // Dữ liệu API về sau khi màn hình đã vẽ xong (ghi xong tải lại, mở chi tiết…) → vẽ lại nếu không có hộp thoại đang mở.
  let lateTimer = null;
  Store.onLoaded(() => {
    if (cycleActive) return;
    clearTimeout(lateTimer);
    lateTimer = setTimeout(() => { if (!cycleActive && !Store.pendingCount() && !overlayOpen()) APP.refresh(); else updateCounts(); }, 120);
  });
  // ---------- Header controls ----------
  function closeMobile() { document.getElementById('sidebar').classList.remove('mobile-open'); document.getElementById('backdrop').classList.remove('show'); }
  function setUserUi() {
    if (Store.hasHost) {
      const u = window.DC_HOST.user || {};
      const name = u.fullName || u.userName || 'Người dùng';
      document.getElementById('userName').textContent = name;
      document.getElementById('userRole').textContent = (u.userGroupName || (u.isAdmin ? 'Quản trị hệ thống' : '')) + (u.userName ? ' · ' + u.userName : '');
      document.querySelectorAll('.user-copy b').forEach(b => b.textContent = name);
      document.querySelectorAll('.user-copy small').forEach(b => b.textContent = 'Tài khoản');
      document.querySelectorAll('.js-avatar').forEach(a => a.textContent = U.initials(name));
      return;
    }
    const g = Store.user.group; document.getElementById('userName').textContent = g; document.getElementById('userRole').textContent = Store.user.label || 'admin';
    document.querySelectorAll('.js-avatar').forEach(a => a.textContent = U.initials(g));
  }
  // Menu tài khoản thật (khi chạy trong Angular): thông tin, đổi mật khẩu, đăng xuất. Chế độ DEMO vẫn cho đổi vai trò.
  function accountMenu() {
    const H = window.DC_HOST, u = H.user || {};
    UI.modal({
      title: 'Tài khoản', sub: (u.userName || '') + (u.userGroupName ? ' · ' + u.userGroupName : ''), size: 'sm',
      body: `<div class="kv"><div><label>Họ tên</label><span>${esc(u.fullName || '–')}</span></div><div><label>Tên đăng nhập</label><span>${esc(u.userName || '–')}</span></div><div><label>Nhóm người dùng</label><span>${esc(u.userGroupName || (u.isAdmin ? 'Quản trị hệ thống' : '–'))}</span></div><div><label>Quản trị</label><span>${u.isAdmin ? UI.badge('Active') : 'Không'}</span></div></div>
        <div class="grid mt">${Store.mode === 'demo' ? `<button class="btn" id="accRole">${I('users')} Đổi vai trò xem (DEMO)</button>` : ''}<button class="btn" id="accPwd">${I('key')} Đổi mật khẩu</button><button class="btn danger" id="accOut">${I('reset')} Đăng xuất</button></div>`,
      onMount: (m, close) => {
        const r = m.querySelector('#accRole'); if (r) r.onclick = () => { close(); chooseRole(); };
        m.querySelector('#accPwd').onclick = () => { close(); changePassword(); };
        m.querySelector('#accOut').onclick = () => { close(); H.logout(); };
      }
    });
  }
  function changePassword() {
    const H = window.DC_HOST;
    UI.modal({
      title: 'Đổi mật khẩu', sub: 'API User/ChangePassword', size: 'sm',
      body: `<div class="form-grid" style="grid-template-columns:1fr"><div class="field"><label for="pwOld">Mật khẩu hiện tại</label><input class="input" type="password" id="pwOld" autocomplete="current-password"></div><div class="field"><label for="pwNew">Mật khẩu mới (từ 9 ký tự)</label><input class="input" type="password" id="pwNew" autocomplete="new-password"></div><div class="field"><label for="pwNew2">Nhập lại mật khẩu mới</label><input class="input" type="password" id="pwNew2" autocomplete="new-password"></div></div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="pwSave">Lưu</button>`,
      onMount: (m, close) => m.querySelector('#pwSave').onclick = async () => {
        const o = m.querySelector('#pwOld').value, n = m.querySelector('#pwNew').value, n2 = m.querySelector('#pwNew2').value;
        const errs = [];
        if (!o || !n) errs.push('Nhập đủ mật khẩu hiện tại và mật khẩu mới');
        if (o && o === n) errs.push('Mật khẩu mới không được trùng mật khẩu hiện tại');
        if (n !== n2) errs.push('Mật khẩu nhập lại chưa khớp');
        if (n && n.length < 9) errs.push('Mật khẩu mới phải từ 9 ký tự');
        if (errs.length) return UI.toast(errs.join('<br>'), 'err');
        // Backend tìm tài khoản theo Id trong form (ChangePasswordRequest : BaseRequest) – thiếu Id → "Tài khoản của bạn không tồn tại".
        const me = H.user || {};
        const fd = new FormData(); fd.append('Id', me.userId || me.id || ''); fd.append('passWordOld', o); fd.append('passWordNew', n);
        try {
          const res = await H.send('PUT', 'User/ChangePassword', fd);
          if (res && res.responseCode !== undefined && res.responseCode !== 200) return UI.toast(esc(res.responseMessage || 'Không đổi được mật khẩu'), 'err');
          close(); UI.toast('Đã đổi mật khẩu', 'ok');
        } catch (e) { UI.toast(esc(H.errorText(e)), 'err'); }
      }
    });
  }
  function chooseRole() {
    const groups = Store.rows('UserGroup');
    UI.modal({
      title: 'Xem mockup theo vai trò', sub: 'Menu & quyền thao tác lấy từ UserRole × ObjectMenu (DEMO)', size: 'sm',
      body: `<div class="grid">${groups.map(g => { const n = Store.by('UserRole', 'UserGroupId', g.Id).length; return `<button class="btn ${g.Name === Store.user.group ? 'primary' : ''}" data-g="${esc(g.Name)}" style="justify-content:space-between">${esc(g.Name)}<span class="small">${n} chức năng</span></button>`; }).join('')}</div>`,
      onMount: (m, close) => m.querySelectorAll('[data-g]').forEach(b => b.onclick = () => {
        Store.user.group = b.dataset.g; const g = Store.one('UserGroup', 'Name', b.dataset.g); const uc = Store.one('UserClaim', 'UserGroupId', g.Id); const u = uc && Store.get('User', uc.UserId);
        Store.user.id = u ? u.Id : null; Store.user.label = u ? u.UserName : 'demo'; close(); setUserUi(); buildNav(); route(); UI.toast('Đang xem với vai trò: ' + esc(b.dataset.g), 'ok');
      })
    });
  }
  function chooseBranch() {
    const br = Store.rows('OrganizationUnit').filter(o => o.UnitType === 'BRANCH');
    UI.modal({ title: 'Chọn đơn vị làm việc', sub: LIVE() ? 'Số liệu trên các màn hình sẽ lọc theo đơn vị đã chọn' : 'dbo.OrganizationUnit (UnitType = BRANCH) · PO xác nhận là nguồn Chi nhánh', size: 'sm', body: `<div class="grid"><button class="btn ${!Store.ctx.branch ? 'primary' : ''}" data-b="">Tất cả đơn vị</button>${br.map(b => `<button class="btn ${Store.ctx.branch === b.Id ? 'primary' : ''}" data-b="${b.Id}">${esc(b.Name)} <span class="small muted">${b.Code}</span></button>`).join('')}</div>`,
      onMount: (m, close) => m.querySelectorAll('[data-b]').forEach(x => x.onclick = () => { Store.ctx.branch = x.dataset.b || null; const nm = x.dataset.b ? Store.get('OrganizationUnit', x.dataset.b).Name : 'Tất cả đơn vị'; document.getElementById('topBranch').textContent = nm; document.getElementById('sideBranch').textContent = nm; close(); route(); UI.toast('Đã chuyển ngữ cảnh: ' + esc(nm), 'ok'); }) });
  }
  function notifications() {
    const a = APP.alerts();
    UI.drawer({ title: 'Cảnh báo & công việc', sub: a.length + (live.alerts ? ' loại cảnh báo · cập nhật mỗi 60 giây' : ' mục · tính từ dữ liệu DEMO'), width: 480, tabs: [{ label: 'Tất cả', html: `<div class="alert-list">${a.map((x, i) => `<div class="alert"><div class="alert-ico ${x.c}">${I(x.i)}</div><div class="alert-copy"><b>${esc(x.t)}</b><small>${esc(x.s)}</small></div><button class="btn sm" data-go2="${x.go}">Xử lý →</button></div>`).join('') || '<div class="empty">Không có cảnh báo</div>'}</div>` }],
      onMount: (d, close) => d.querySelectorAll('[data-go2]').forEach(b => b.onclick = () => { close(); APP.go(b.dataset.go2); }) });
  }
  function help() {
    const live = API_MODE();
    UI.modal({ title: 'Hướng dẫn sử dụng', size: 'lg', body: (Store.hasHost ? `<div class="notice ${live ? 'info' : 'warn'}">${I('database')}<div><b>Nguồn dữ liệu: ${live ? 'API thật (backend .NET)' : 'DỮ LIỆU DEMO trong trình duyệt'}</b>${live ? 'Bảng có API được đọc / ghi qua <code>api/&lt;Controller&gt;/GetAllData · Insert · Update · Delete</code>. Bảng chưa có API hiển thị dữ liệu DEMO. Nhãn góc trên cho biết nguồn dữ liệu của màn hình đang xem.' : 'Thao tác chỉ lưu trong bộ nhớ trình duyệt, mất khi tải lại trang. Không ghi gì xuống database.'}<div class="mt"><button class="btn sm" id="swMode">${live ? 'Chuyển sang dữ liệu DEMO (trình diễn)' : 'Chuyển về dữ liệu thật (API)'}</button></div></div></div>` : `<div class="notice warn">${I('alert')}<div><b>Toàn bộ số liệu là DỮ LIỆU DEMO</b>Sinh tự động theo đúng tên bảng/cột trong SQL nguồn; lưu trong bộ nhớ trình duyệt và mất khi tải lại trang.</div></div>`) + `
      <h3>Kịch bản demo gợi ý</h3><ol class="small" style="line-height:1.9"><li><b>Tổng quan điều hành</b>: đổi ngày / ca / đơn vị → KPI thay đổi.</li><li><b>Luồng nghiệp vụ E2E</b>: chọn 1 trong 9 luồng, bấm từng bước để mở màn hình.</li><li><b>Đơn hàng</b>: mở đơn Submitted → Xác nhận → Chốt số lượng.</li><li><b>Nhu cầu vật tư (MRP)</b>: tính thiếu hụt → Tạo đề nghị mua → <b>Hộp phê duyệt</b> duyệt 2 cấp.</li><li><b>Nhập kho</b>: phiếu chờ QC → <b>Phiếu QC</b> kết luận PASS/HOLD/REJECT → Ghi sổ nhập kho sinh Lô + Giao dịch kho.</li><li><b>Mẻ sản xuất</b>: bảng điều độ ca × dây chuyền, chuyển trạng thái mẻ, QC thành phẩm.</li><li><b>Thực hiện giao hàng</b>: READY → … → DELIVERED và ghi POD.</li><li><b>Phân ca</b>: bấm ô để phân ca; <b>Phân quyền</b>: tick quyền, sau đó đổi vai trò (bấm avatar) để thấy menu thay đổi.</li><li><b>Database Architecture Explorer</b>: 124 bảng, 2.264 cột, quan hệ logic & GAP.</li></ol>
      <h3>Phím tắt</h3><p class="small">Ô tìm kiếm trên cùng: tìm chức năng, bảng DB, khách hàng, đơn hàng, nguyên liệu, lô.</p>`,
      foot: `<button class="btn danger" id="rst">${I('reset')} ${live ? 'Tải lại dữ liệu từ API' : 'Khôi phục dữ liệu demo'}</button><button class="btn primary" data-close>Đã hiểu</button>`, onMount: (m, close) => {
        m.querySelector('#rst').onclick = () => { Store.reset(); close(); buildNav(); route(); UI.toast(live ? 'Đang tải lại dữ liệu từ API' : 'Đã khôi phục dữ liệu demo ban đầu', 'ok'); };
        const sw = m.querySelector('#swMode'); if (sw) sw.onclick = () => Store.setMode(live ? 'demo' : 'api');
      } });
  }
  // Bỏ dấu tiếng Việt để tìm "don hang" khớp "Đơn hàng".
  const fold = s => String(s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd');
  const screenHits = q => { const k = fold(q); return Object.values(FN).filter(f => !CHILD.has(f.id) && !hiddenNow(f.id) && Store.perm(f.id).access !== false && fold(f.name + ' ' + f.fn + ' ' + f.id).includes(k)).map(f => ({ h: APP.href(f.id), t: f.name, s: 'Chức năng · ' + f.grp, i: 'clipboard' })); };
  function showResults(res, note) {
    const box = document.getElementById('searchResults');
    box.innerHTML = (res.slice(0, 30).map((r, i) => `<a href="${r.h || 'javascript:void 0'}" data-i="${i}">${I(r.i)}<span>${esc(r.t)}</span><small>${esc(r.s)}</small></a>`).join('') || (note ? '' : '<div class="empty">Không tìm thấy</div>')) + (note ? `<div class="empty">${esc(note)}</div>` : '');
    box.classList.add('show');
    box.querySelectorAll('a').forEach(a => a.onclick = () => {
      const r = res[+a.dataset.i]; box.classList.remove('show'); document.getElementById('globalSearch').value = '';
      if (r.open) { const [t, id, sid] = r.open; APP.go(sid); Store.ensure(t).then(() => setTimeout(() => UI.detail(t, id, { screen: sid }), 60)); }
    });
  }
  function localSearch(q) {
    const res = [];
    Object.values(FN).forEach(f => { if ((f.name + ' ' + f.fn + ' ' + f.id).toLowerCase().includes(q)) res.push({ h: APP.href(f.id), t: f.name, s: 'Chức năng · ' + f.ph, i: 'clipboard' }); });
    Object.keys(Store.schema.tables).forEach(t => { if (t.toLowerCase().includes(q)) res.push({ h: APP.href('dbx-explorer?t=' + t), t: 'dbo.' + t, s: 'Bảng DB', i: 'database' }); });
    [['Customer', 'crm-customer'], ['Order', 'crm-order'], ['Ingredient', 'scm-ingredient'], ['Dishe', 'menu-dish'], ['Supplier', 'scm-supplier'], ['MaterialLot', 'scm-lot'], ['Employee', 'adm-employee'], ['PurchaseOrder', 'scm-po']].forEach(([t, sid]) => Store.rows(t).forEach(r => { const l = Store.label(t, r); if (l.toLowerCase().includes(q) && res.length < 40) res.push({ t: l, s: t + ' · mở chi tiết', i: 'eye', open: [t, r.Id, sid] }); }));
    showResults(res);
  }
  // Chế độ API (BE3.3): Common/GlobalSearch – server tìm 8 loại bản ghi theo quyền Xem (mỗi loại ≤ 5). Màn hình lọc tại chỗ theo quyền.
  // Loại bản ghi của API → bảng của giao diện (bảng legacy giữ tên cũ).
  const SEARCH_TABLE = { Dish: 'Dishe' };
  let searchTimer = null, searchSeq = 0, searchLiveOff = false;
  function search(q) {
    const box = document.getElementById('searchResults'); q = q.trim();
    clearTimeout(searchTimer); const seq = ++searchSeq;
    if (!q) { box.classList.remove('show'); return; }
    if (!Store.isLive || searchLiveOff) return localSearch(q.toLowerCase());
    const screens = screenHits(q);
    if (q.length < 2) return showResults(screens, 'Nhập từ 2 ký tự để tìm bản ghi');
    showResults(screens, 'Đang tìm…');
    searchTimer = setTimeout(() => Store.query('Common/GlobalSearch', { q }).then(x => {
      if (seq !== searchSeq) return;
      // Backend cũ chưa có API tìm kiếm → dùng cách tìm tại chỗ cho cả phiên.
      if (!x.ok) { if (/404/.test(x.message || '')) { searchLiveOff = true; return localSearch(q.toLowerCase()); } return showResults(screens, x.message || 'Không tìm được'); }
      const recs = ((x.data && x.data.records) || []).map(r => { const sid = String(r.route || '').replace(/^#\//, ''); return { t: (r.code ? r.code + ' · ' : '') + (r.label || ''), s: (r.typeTitle || r.type) + (r.statusName ? ' · ' + r.statusName : ''), i: 'eye', open: [SEARCH_TABLE[r.type] || r.type, r.id, sid] }; });
      showResults(screens.concat(recs));
    }), 300);
  }
  APP._buildNav = () => buildNav(); APP._setUserUi = () => setUserUi();
  // Gỡ toàn bộ sự kiện gắn vào document/window – Angular gọi khi rời màn hình chính (đăng xuất).
  const docHandlers = [];
  const onDoc = (target, ev, fn, cap) => { target.addEventListener(ev, fn, !!cap); docHandlers.push([target, ev, fn, !!cap]); };
  let alertTimer = null;
  APP.stop = () => { routeSeq++; clearInterval(alertTimer); clearTimeout(searchTimer); docHandlers.splice(0).forEach(([t, ev, fn, cap]) => t.removeEventListener(ev, fn, cap)); document.body.classList.remove('dc-live'); document.querySelectorAll('.overlay, .drawer-wrap').forEach(x => x.remove()); };
  APP.start = () => {
    if (Store.hasHost && Store.mode === 'api') {
      const u = window.DC_HOST.user || {};
      Store.user.id = u.userId || u.id || null; Store.user.group = u.userGroupName || (u.isAdmin ? 'Quản trị hệ thống' : 'Người dùng'); Store.user.label = u.userName;
    } else {
      const g = Store.one('UserGroup', 'Name', 'Quản trị hệ thống'); const uc = g && Store.one('UserClaim', 'UserGroupId', g.Id); Store.user.id = uc ? uc.UserId : null; Store.user.label = 'admin';
    }
    const roleOrAccount = () => (Store.hasHost ? accountMenu() : chooseRole());
    document.body.classList.toggle('dc-live', Store.isLive);
    buildNav(); setUserUi();
    document.getElementById('collapseBtn').onclick = () => document.getElementById('app').classList.toggle('collapsed');
    document.getElementById('mobileMenu').onclick = () => { document.getElementById('sidebar').classList.add('mobile-open'); document.getElementById('backdrop').classList.add('show'); };
    document.getElementById('backdrop').onclick = closeMobile;
    document.getElementById('branchSelect').onclick = chooseBranch; document.getElementById('sideContext').onclick = chooseBranch;
    document.getElementById('notificationBtn').onclick = notifications; document.getElementById('helpBtn').onclick = help; document.getElementById('helpLink').onclick = help;
    document.querySelectorAll('[data-role-btn]').forEach(b => b.onclick = roleOrAccount);
    onDoc(document, 'click', e => { if (e.target.closest('[data-role]')) chooseRole(); const sr = document.getElementById('searchResults'); if (sr && !e.target.closest('.search')) sr.classList.remove('show'); });
    const gs = document.getElementById('globalSearch'); gs.oninput = e => search(e.target.value); gs.onkeydown = e => { if (e.key === 'Escape') { gs.value = ''; search(''); } };
    onDoc(document, 'keydown', e => { if (e.key === 'Escape') { const d = document.querySelector('.drawer-wrap:last-of-type, .overlay:last-of-type'); if (d) d.remove(); } });
    // Liên kết '#/screen-id' trong HTML của mockup → đổi sang tiền tố hash của Angular.
    onDoc(document, 'click', e => {
      const a = e.target.closest && e.target.closest('a[href^="#/"]'); if (!a || a.onclick) return;
      const href = a.getAttribute('href'), base = APP.base(); if (base === '#/' || href.startsWith(base)) return;
      e.preventDefault(); location.hash = base + href.slice(2);
    }, true);
    // Chuông cảnh báo: hỏi lại mỗi 60 giây khi người dùng đứng yên trên một màn hình.
    if (Store.isLive) alertTimer = setInterval(() => { if (!document.hidden) pullAlerts(); }, ALERT_TTL);
    onDoc(window, 'hashchange', route);
    // Dò API của mọi màn hình trước khi vẽ màn đầu tiên: backend đang nối thiếu controller nào thì ẩn menu của nó.
    // Vẽ màn hình đầu tiên NGAY; dò API chạy nền, xong thì dựng lại menu (và vẽ lại nếu màn đang xem thuộc bảng vừa bị ẩn).
    route();
    Store.probe(Object.values(FN).filter(f => f.table && !CHILD.has(f.id) && !HIDDEN_LIVE.has(f.id)).map(f => f.table)).then(() => {
      const before = document.querySelectorAll('[data-nav]').length;
      buildNav();
      document.querySelectorAll('[data-nav]').forEach(a => a.classList.toggle('active', a.dataset.nav === APP.current));
      const act = document.querySelector(`[data-nav="${APP.current}"]`); if (act) act.closest('.group').classList.remove('closed');
      const cur = FN[APP.current];
      if (document.querySelectorAll('[data-nav]').length !== before && cur && cur.table && Store.isMissing(cur.table) && !overlayOpen()) route();
    });
  };
  window.APP = APP;
})();
