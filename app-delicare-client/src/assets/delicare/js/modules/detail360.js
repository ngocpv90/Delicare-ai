/* UI-3 (D-67, tách từ UI-2): thành phần dùng chung cho các màn chi tiết cả trang (khách hàng 360, nhà cung cấp, nhân viên…).
 * APP.D360: bảng gọn, hàng tab (chỉ tab có quyền, tải khi mở lần đầu), bộ lọc ngày / trạng thái, khối "Cần chú ý", tab Tài liệu, tab Lịch sử + Liên quan. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const D = {};
  D.pas = o => { const r = {}; Object.entries(o || {}).forEach(([k, v]) => { r[k.charAt(0).toUpperCase() + k.slice(1)] = v; }); return r; };
  D.failBox = msg => `<div class="notice warn">${I('alert')}<div>${esc(msg || 'Không tải được dữ liệu')}</div></div>`;
  D.loading = '<div class="muted small">Đang tải…</div>';
  D.none = '<div class="muted small">Chưa có dữ liệu</div>';
  D.demoNote = '<div class="muted small">Chế độ DEMO: tab này đọc số liệu từ máy chủ – chuyển sang dữ liệu API để xem.</div>';
  D.can = (screen, act) => { const p = Store.perm(screen); return p.access !== false && (!act || p[act] !== false); };
  D.initials = name => String(name || '?').replace(/\[DEMO\]\s*/i, '').trim().split(/\s+/).filter(Boolean).slice(-2).map(w => w[0]).join('').toUpperCase() || '?';
  D.dmy = v => (v ? U.date(v) : '');
  D.pct = v => (v === null || v === undefined ? '–' : U.num(v, 1) + '%');
  // Bảng gọn: cols = [{ label, html(row), num }]; clickable → dòng bấm được (bindRows gắn hành động).
  D.table = (rows, cols, clickable) => {
    if (!rows.length) return D.none;
    return `<div class="table-wrap"><table class="table dc-table dt"><thead><tr>${cols.map(c => `<th class="${c.num ? 'num' : ''}">${esc(c.label)}</th>`).join('')}</tr></thead><tbody>${rows.map((r, i) => `<tr ${clickable ? `data-r="${i}" style="cursor:pointer"` : ''}>${cols.map(c => `<td class="${c.num ? 'num' : ''}">${c.html(r)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
  };
  D.bindRows = (box, rows, onRow) => { if (onRow) box.querySelectorAll('tr[data-r]').forEach(tr => tr.onclick = e => { if (e.target.closest('button,a,input,select')) return; onRow(rows[+tr.dataset.r]); }); };
  D.run = (body, q, draw) => q.then(x => { if (!body.isConnected) return; if (!x.ok) { body.innerHTML = D.failBox(x.message); return; } draw(x); });
  D.statusOpts = (t, col, codes) => codes.map(c => [c, UI.statusVi(t, col, c)]);
  // Bộ lọc ngày (từ – đến) + trạng thái (+ lựa chọn riêng opt.extra = [[giá trị, nhãn]]) ; onChange(state, body) vẽ lại.
  D.filterBar = (box, opt, onChange) => {
    const st = { from: opt.from || '', to: opt.to || '', status: '', extra: opt.extraDefault || '' };
    box.innerHTML = `<div class="c360-filter">${opt.dates === false ? '' : `<label class="small muted">Từ</label><input type="date" class="input" data-f="from" value="${st.from}" style="width:auto"><label class="small muted">đến</label><input type="date" class="input" data-f="to" value="${st.to}" style="width:auto">`}
      ${opt.statuses ? `<select class="select" data-f="status" style="width:auto"><option value="">Mọi trạng thái</option>${opt.statuses.map(([v, l]) => `<option value="${esc(v)}">${esc(l)}</option>`).join('')}</select>` : ''}
      ${opt.extra ? `<select class="select" data-f="extra" style="width:auto">${opt.extra.map(([v, l]) => `<option value="${esc(v)}" ${v === st.extra ? 'selected' : ''}>${esc(l)}</option>`).join('')}</select>` : ''}${opt.tools || ''}</div><div data-body>${D.loading}</div>`;
    const body = box.querySelector('[data-body]');
    box.querySelectorAll('[data-f]').forEach(i => i.onchange = () => { st[i.dataset.f] = i.value; onChange(st, body); });
    onChange(st, body);
    return st;
  };
  // Khối "Cần chú ý": alerts = [{ code, severity, message, screen }]; mục có màn và có quyền → bấm mở màn.
  D.alertBox = alerts => (alerts && alerts.length ? `<div class="c360-alert">${I('alert')}<div><b>Cần chú ý</b><ul>${alerts.map(a => `<li class="sev-${esc(a.severity)}">${a.screen && D.can(a.screen) ? `<a data-go="${esc(a.screen)}">${esc(a.message)}</a>` : esc(a.message)}</li>`).join('')}</ul></div></div>` : '');
  D.bindGo = box => box.querySelectorAll('[data-go]').forEach(a => a.onclick = () => APP.go(a.dataset.go));
  // Hàng tab: tabs = [{ label, screen?, render(box, d, id) }] – chỉ tab có quyền xem màn tương ứng; tải khi mở lần đầu.
  D.buildTabs = (bar, pane, tabs, d, id) => {
    const list = tabs.filter(t => !t.screen || D.can(t.screen));
    bar.innerHTML = list.map((t, i) => `<button class="tab ${i === 0 ? 'active' : ''}" data-t="${i}">${esc(t.label)}</button>`).join('');
    const panes = list.map(() => null);
    const show = i => {
      bar.querySelectorAll('.tab').forEach(b => b.classList.toggle('active', +b.dataset.t === i));
      pane.querySelectorAll('[data-p]').forEach(p => p.classList.add('hide'));
      if (!panes[i]) {
        panes[i] = document.createElement('div'); panes[i].dataset.p = i; pane.appendChild(panes[i]);
        panes[i].innerHTML = D.loading;
        Promise.resolve(list[i].render(panes[i], d, id)).catch(e => { panes[i].innerHTML = D.failBox(e && e.message); });
      }
      panes[i].classList.remove('hide');
    };
    bar.onclick = e => { const b = e.target.closest('[data-t]'); if (b) show(+b.dataset.t); };
    show(0);
  };
  // Menu ⋯ của phần đầu: đóng khi bấm ra ngoài.
  /* UI-2R fix (D-80 d – mô phỏng detail-head-compact-mockup.html bản ②): dòng tên của phần đầu 3 màn 360 (không tự bọc .card – màn bọc chung với hàng chỉ số).
   * meta = các phần thông tin phụ (HTML đã escape) → gộp 1 dòng " · ", bỏ phần trống, quá dài "…" (title hiện đủ).
   * buttons = [{ act, icon, label, ok, primary }] theo thứ tự hiện; < 768px chỉ còn nút chính + ⋯ (nút khác có bản sao trong menu ⋯ – class mob-only).
   * more = các nút HTML của menu ⋯. actions = false → không vẽ nút (bản nháp trước khi có dữ liệu). */
  D.headRow = ({ avatar, avatarCls, title, meta, buttons, more, actions }) => {
    const parts = (meta || []).filter(x => x !== null && x !== undefined && String(x).trim() !== '');
    const plain = parts.map(x => String(x).replace(/<[^>]*>/g, '').replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").trim()).join(' · ');
    const btns = (buttons || []).filter(b => b.ok);
    const acts = actions === false ? '' : `<div class="c360-actions">${btns.map(b => `<button class="btn ${b.primary ? 'primary' : 'sec'}" data-h="${b.act}">${I(b.icon)} ${b.label}</button>`).join('')}
      <button class="btn more" data-h="more" aria-label="Thêm thao tác">⋯</button>
      <div class="c360-more hide" data-hmore>${btns.filter(b => !b.primary).map(b => `<button class="mob-only" data-h="${b.act}">${b.label}</button>`).join('')}${(more || []).filter(Boolean).join('')}</div></div>`;
    return `<div class="c360-head"><div class="c360-avatar ${avatarCls || 'biz'}">${esc(avatar || '')}</div>
      <div class="c360-id"><h1>${title}</h1>${parts.length ? `<div class="c360-meta one" title="${esc(plain)}">${parts.join(' · ')}</div>` : ''}</div>${acts}</div>`;
  };
  D.bindMore = (box, onAct) => {
    const more = box.querySelector('[data-hmore]');
    box.querySelectorAll('[data-h]').forEach(b => b.onclick = e => {
      const act = b.dataset.h;
      if (act === 'more') { e.stopPropagation(); if (more) more.classList.toggle('hide'); return; }
      if (more) more.classList.add('hide');
      onAct(act);
    });
    document.addEventListener('click', () => more && more.classList.add('hide'), { once: true });
  };
  // Tab Tài liệu: tệp đính kèm của bản ghi (DocumentFile/GetList · Upload · Download); canUp = quyền sửa màn gốc.
  D.documentsTab = (resource, canUp) => (box, d, id) => {
    if (!Store.isLive) { box.innerHTML = D.demoNote; return; }
    const rules = APP.docRules || { DOC_MAX: 20 * 1024 * 1024, DOC_EXT: ['pdf', 'jpg', 'jpeg', 'png', 'xlsx', 'docx'] };
    box.innerHTML = `${canUp ? `<div class="c360-filter"><button class="btn primary" data-up>${I('plus')} Tải tệp lên</button><input type="file" data-file hidden accept="${rules.DOC_EXT.map(x => '.' + x).join(',')}"><span class="small muted">≤ 20 MB · ${rules.DOC_EXT.join(', ')}</span></div>` : ''}<div data-docs>${D.loading}</div>`;
    const docs = box.querySelector('[data-docs]');
    const load = () => D.run(docs, Store.query('DocumentFile/GetList', { resource, recordId: id, page: 1, pageSize: 100 }), x => {
      const rows = x.list || [];
      docs.innerHTML = D.table(rows, [{ label: 'Tệp', html: r => `<b>${esc(r.fileName || '')}</b>` }, { label: 'Loại', html: r => esc(r.category || r.fileType || '') },
        { label: 'Dung lượng', num: true, html: r => U.num((r.fileSize || 0) / 1024, 0) + ' KB' }, { label: 'Người tải', html: r => esc(r.createdByName || '') }, { label: 'Ngày', html: r => D.dmy(r.createdDate) }], true);
      D.bindRows(docs, rows, r => Store.download('DocumentFile/Download', { id: r.id }));
    });
    if (canUp) {
      const inp = box.querySelector('[data-file]');
      box.querySelector('[data-up]').onclick = () => inp.click();
      inp.onchange = () => {
        const file = inp.files && inp.files[0]; inp.value = '';
        if (!file) return;
        const ext = (file.name.split('.').pop() || '').toLowerCase();
        if (!rules.DOC_EXT.includes(ext)) return UI.toast('Chỉ nhận tệp ' + rules.DOC_EXT.join(', '), 'err');
        if (file.size > rules.DOC_MAX) return UI.toast('Tệp vượt quá 20 MB', 'err');
        const fd = new FormData(); fd.append('File', file); fd.append('Category', 'other'); fd.append('Resource', resource); fd.append('RecordId', id);
        Store.act('DocumentFile/Upload', { json: fd }).then(r => { if (!r.ok) return; UI.toast(r.data && r.data.isDuplicate ? 'Tệp này đã có trong kho tài liệu' : 'Đã tải lên ' + esc(file.name), r.data && r.data.isDuplicate ? 'warn' : 'ok'); load(); });
      };
    }
    load();
  };
  // UI-6.14 (D-95): tab Lịch sử dạng dòng thời gian – MỘT hàm chung cho mọi trang chi tiết / màn 360 (Common/GetTimeline).
  // Trái 2/3: dòng thời gian nhóm theo ngày, chấm màu theo loại, câu "Người + động từ + chứng từ", thẻ thay đổi ngang (≤ 3 + "N thay đổi khác"),
  // ý kiến duyệt dạng trích dẫn, lọc nhóm + người ở máy chủ, tải dần 20. Phải 1/3: "Liên quan" + chú thích màu. Điện thoại: một cột, Liên quan lên trên.
  const TL_KIND = { create: ['plus', 'Tạo'], update: ['edit', 'Sửa'], submit: ['up', 'Gửi duyệt'], approve: ['approve', 'Duyệt'], reject: ['x', 'Từ chối / hủy'],
    file: ['file', 'Tài liệu'], system: ['bot', 'Hệ thống tự làm'], order: ['clipboard', 'Đơn hàng'], complaint: ['msg', 'Phản ánh'] };
  const TL_GROUPS = [['all', 'Tất cả'], ['info', 'Thông tin'], ['status', 'Trạng thái & duyệt'], ['docs', 'Hợp đồng, đơn, phản ánh'], ['file', 'Tài liệu']];
  // Trường kỹ thuật không bao giờ hiện.
  const TL_TECH = /^(Id|RowVersion|ConcurrencyStamp|SecurityStamp|CreatedDate|CreatedBy|ModifiedDate|ModifiedBy|UpdatedAt|UpdatedBy|DeletedDate|DeletedBy|DeleteddBy|IsDeleted|TenantId|SyncedAt|Hash|FileNameHas|NormalizedUserName|NormalizedEmail|XemThemSuaXoaInXuatNhap|ExternalEventId|RecipeSnapshotJson|SnapshotJson|ActorId|ComplaintId|ApprovalStepId|QCInspectionId)$/;
  // Bảng máy chủ → bảng FE (tên lớp FE theo schema.js).
  const TL_FE = { Dish: 'Dishe', ProductionBatch: 'ProductionBatche', DishCategory: 'DishCategorie' };
  const feTable = t => (t && TL_FE[t]) || t;
  const TL_EXTRA = { AssignedAt: 'Giao lúc', ActionCode: 'Thao tác', ItemStatus: 'Trạng thái', ItemType: 'Loại', Denominator: 'Mẫu số', Numerator: 'Tử số', CalculationVersion: 'Phiên bản tính', MetricVersion: 'Phiên bản chỉ số', LotId: 'Lô', TransactionNo: 'Số giao dịch', UomId: 'Đơn vị tính', AvailableQty: 'Tồn khả dụng', OnHandQty: 'Tồn thực tế', NewStatus: 'Trạng thái mới', OldStatus: 'Trạng thái cũ', Comment: 'Ý kiến', MealQty: 'Số suất', ChemicalUsed: 'Hóa chất dùng', CleaningNo: 'Số phiếu vệ sinh', DeliveredQty: 'Số suất đã giao', OrderedQty: 'Số suất đặt', DeliveryStopId: 'Điểm dừng', DeliveryNo: 'Số đơn giao', PackingNo: 'Số phiếu đóng gói', OutputQty: 'Sản lượng', ReservedQty: 'Đã giữ chỗ', IssueNo: 'Số phiếu xuất', ProductionOrderNo: 'Số lệnh sản xuất', BatchNo: 'Số mẻ', LotNo: 'Số lô', ReceiptNo: 'Số phiếu nhập', DecidedBy: 'Người quyết định', DecisionCode: 'Quyết định', ExpectedDate: 'Ngày giao dự kiến', PONo: 'Số đơn mua', PlanNo: 'Số kế hoạch', ExpectedIncomingQty: 'Sắp về', PurchaseQty: 'Cần mua', MenuOptionNo: 'Lựa chọn thực đơn', IsPrimary: 'Liên hệ chính', Position: 'Chức vụ', BusinessRegistrationNumber: 'Số đăng ký kinh doanh', LegalRepresentativeName: 'Người đại diện pháp luật', IsExpiryManaged: 'Quản lý hạn dùng', AccountType: 'Loại tài khoản', CategoryId: 'Nhóm', CompanySize: 'Quy mô', ContactPersonEmail: 'Email người liên hệ', RegistrationDate: 'Ngày đăng ký', RegistrationPlace: 'Nơi đăng ký', FirstName: 'Tên', LastName: 'Họ', IdentityDate: 'Ngày cấp giấy tờ', IdentityNumber: 'Số giấy tờ', UserAssignId: 'Người phụ trách', FormulaDescription: 'Mô tả công thức', PhysicalQty: 'Số đếm thực tế', CountNo: 'Số phiếu kiểm kê', SystemQty: 'Tồn hệ thống', AllowAccess: 'Xem', AllowAdd: 'Thêm', AllowEdit: 'Sửa', AllowDelete: 'Xóa', AllowExport: 'Xuất', AllowImport: 'Nhập', AllowPrint: 'In', TotalQuantity: 'Tổng suất', Quantity: 'Số lượng', MealType: 'Buổi', MealDate: 'Ngày ăn', DeliveryDate: 'Ngày giao', OrderDate: 'Ngày đặt', OrderNumber: 'Số đơn', OrderNo: 'Số đơn',
    ContractNo: 'Số hợp đồng', StartDate: 'Ngày bắt đầu', EndDate: 'Ngày kết thúc', UnitPrice: 'Đơn giá', Amount: 'Thành tiền', TotalAmount: 'Tổng tiền', OptionNo: 'Lựa chọn', ShiftId: 'Ca',
    RequestStatus: 'Trạng thái', ComplaintStatus: 'Trạng thái', IssueStatus: 'Trạng thái', PassWord: 'Mật khẩu', Latitude: 'Vĩ độ', Longitude: 'Kinh độ', Name: 'Tên', Phone: 'Điện thoại' };
  D.fieldLabel = (table, f) => {
    if (!f || TL_TECH.test(f)) return null;
    const L = U.LABEL || {};
    const m = Store.colmeta && Store.colmeta[feTable(table)] && Store.colmeta[feTable(table)][f];
    return L[f] || TL_EXTRA[f] || (m && m.label) || (/Id$/.test(f) && (L[f.replace(/Id$/, '')] || L[f.replace(/Id$/, 'Name')])) || null;
  };
  const TL_ISO = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2}))?/;
  // Nhật ký ghi ngày theo InvariantCulture: M/d/yyyy H:mm:ss.
  const TL_VN = /^(\d{1,2})\/(\d{1,2})\/(\d{4})(?: (\d{1,2}):(\d{2}))?/;
  const p2 = n => String(n).padStart(2, '0');
  D.tlValue = v => {
    if (v === null || v === undefined || v === '') return '—';
    const t = String(v).trim();
    if (/^true$/i.test(t)) return 'Có';
    if (/^false$/i.test(t)) return 'Không';
    if (/^[0-9a-f]{8}-[0-9a-f]{4}-/i.test(t)) return '(đã đổi)';
    if (/^-?\d+(\.\d+)?$/.test(t)) return U.num(+t, t.includes('.') ? 2 : 0);
    let m = t.match(TL_ISO);
    if (m) return m[3] + '/' + m[2] + '/' + m[1] + (m[4] && (m[4] !== '00' || m[5] !== '00') ? ' ' + m[4] + ':' + m[5] : '');
    m = t.match(TL_VN);
    if (m) return p2(m[2]) + '/' + p2(m[1]) + '/' + m[3] + (m[4] && (+m[4] !== 0 || +m[5] !== 0) ? ' ' + p2(m[4]) + ':' + m[5] : '');
    return UI.STATUS_VI && Object.prototype.hasOwnProperty.call(UI.STATUS_VI, t) ? UI.STATUS_VI[t] : t;
  };
  const WD = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy'];
  D.tlDay = at => {
    const d = new Date(at), t = new Date(), y = new Date(); y.setDate(t.getDate() - 1);
    const same = (a, b) => a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
    const dm = p2(d.getDate()) + '/' + p2(d.getMonth() + 1);
    if (same(d, t)) return 'Hôm nay · ' + dm;
    if (same(d, y)) return 'Hôm qua · ' + dm;
    return WD[d.getDay()] + ' · ' + dm + '/' + d.getFullYear();
  };
  const tlChanges = (e, open) => {
    const list = (e.listChange || []).map(c => ({ c, label: D.fieldLabel(e.entityName || e.docKey, c.field) })).filter(x => x.label);
    if (!list.length) return '';
    const show = open ? list : list.slice(0, 3);
    return `<div class="tl-changes">${show.map(({ c, label }) => c.masked ? `<span class="tl-ch">${esc(label)}: <b>(đã ẩn)</b></span>`
      : c.from === null || c.from === undefined ? `<span class="tl-ch">${esc(label)}: <b>${esc(D.tlValue(c.to))}</b></span>`
      : `<span class="tl-ch">${esc(label)}: <s>${esc(D.tlValue(c.from))}</s> → <b>${esc(D.tlValue(c.to))}</b></span>`).join('')}</div>${list.length > 3 ? `<span class="tl-more" data-more>${open ? 'Thu gọn' : '+ ' + (list.length - 3) + ' thay đổi khác'}</span>` : ''}`;
  };
  const tlDoc = e => {
    if (e.isSelf || !e.docId) return '';
    const href = APP.detailHref && APP.detailHref(feTable(e.docKey), e.docId);
    const lbl = esc(e.docLabel || '');
    return ` ${esc((e.docTitle || '').toLowerCase())} ${href ? `<a class="tl-ref" href="${href}">${lbl}</a>` : `<b class="tl-ref">${lbl}</b>`}`;
  };
  D.timeline = opts => (box, d, id) => {
    const resource = opts.resource;
    if (!Store.isLive) { box.innerHTML = UI.history ? '' : D.demoNote; if (UI.history) UI.history(resource, id, box); return; }
    const st = { group: 'all', by: '', page: 1, items: [], open: new Set() };
    box.innerHTML = `<div class="tl-wrap"><div class="tl-main"><div class="tl-bar"><div class="tl-chips" data-chips></div><select class="select tl-by" data-by aria-label="Người thực hiện"><option value="">Mọi người thực hiện</option></select></div>
      <div class="tl-list" data-tl>${D.loading}</div><div class="tl-load hide" data-load><button class="btn sec">Xem thêm 20 hoạt động cũ hơn</button></div></div>
      <div class="tl-side" data-side><h3>Liên quan</h3><div data-rel>${D.loading}</div>
      <div class="tl-legend">${Object.entries(TL_KIND).map(([k, v]) => `<span><i class="tl-mini k-${k}">${I(v[0])}</i> ${esc(v[1])}</span>`).join('')}</div></div></div>`;
    const tl = box.querySelector('[data-tl]'), chips = box.querySelector('[data-chips]'), sel = box.querySelector('[data-by]'), more = box.querySelector('[data-load]'), rel = box.querySelector('[data-rel]');
    let first = true;
    const draw = () => {
      let h = '', day = '';
      st.items.forEach((e, i) => {
        const dd = D.tlDay(e.at);
        if (dd !== day) { day = dd; h += `<div class="tl-day">${esc(dd)}</div>`; }
        const nx = st.items[i + 1], last = !nx || D.tlDay(nx.at) !== dd, k = TL_KIND[e.kind] ? e.kind : 'update';
        const at = new Date(e.at);
        const verb = esc(e.verb || '');
        h += `<div class="tl-ev ${last ? 'last' : ''}" data-i="${i}"><div class="tl-dot k-${k}" title="${esc(TL_KIND[k][1])}">${I(TL_KIND[k][0])}</div><div class="tl-body">
          <div class="tl-head"><div class="tl-title"><b>${esc(e.byName || '')}</b> ${verb}${tlDoc(e)}</div><span class="tl-time">${p2(at.getHours())}:${p2(at.getMinutes())}</span></div>
          ${tlChanges(e, st.open.has(i))}${e.comment ? `<div class="tl-quote">${esc(e.comment)}</div>` : ''}</div></div>`;
      });
      tl.innerHTML = h || '<div class="muted small">Chưa có hoạt động</div>';
      tl.querySelectorAll('[data-more]').forEach(m => m.onclick = () => { const i = +m.closest('[data-i]').dataset.i; if (st.open.has(i)) st.open.delete(i); else st.open.add(i); draw(); });
    };
    const load = reset => {
      if (reset) { st.page = 1; st.items = []; st.open.clear(); tl.innerHTML = D.loading; }
      const q = { resource, id, group: st.group, page: st.page, pageSize: 20 };
      if (st.by === 'system') q.bySystem = true; else if (st.by) q.by = st.by;
      Store.query('Common/GetTimeline', q).then(x => {
        if (!tl.isConnected) return;
        if (!x.ok) { tl.innerHTML = D.failBox(x.message); return; }
        const data = x.data || {};
        st.items = st.items.concat(data.listItem || []);
        const cnt = data.groupCount || {};
        chips.innerHTML = TL_GROUPS.map(([g, l]) => `<button class="chip ${st.group === g ? 'on' : ''}" data-g="${g}">${esc(l)}${cnt[g] !== undefined ? ' · ' + U.num(cnt[g]) : ''}</button>`).join('');
        chips.querySelectorAll('[data-g]').forEach(c => c.onclick = () => { st.group = c.dataset.g; load(true); });
        if (first) {
          first = false;
          sel.innerHTML = '<option value="">Mọi người thực hiện</option>' + (data.listActor || []).map(a => `<option value="${a.isSystem ? 'system' : esc(a.id)}">${esc(a.name)}</option>`).join('');
          sel.onchange = () => { st.by = sel.value; load(true); };
          const groups = data.listRelated || [];
          rel.innerHTML = groups.length ? groups.map(g => {
            const scr = (g.route || '').replace('#/', '');
            return `<div class="tl-rg"><div class="spread"><b>${esc(g.title)} · ${U.num(g.count)}</b>${scr && D.can(scr) ? `<a href="javascript:void 0" data-go="${esc(scr)}" class="small">Mở màn</a>` : ''}</div>
            ${(g.items || []).slice(0, 3).map(it => { const href = APP.detailHref && APP.detailHref(feTable(g.key), it.id); return `<div class="tl-ri small"><span>${esc(it.label || '')}${it.statusName ? ' · <span class="muted">' + esc(it.statusName) + '</span>' : ''}</span>${href ? `<a href="${href}">Mở</a>` : ''}</div>`; }).join('')}</div>`;
          }).join('') : '<div class="muted small">Không có chứng từ liên quan</div>';
          D.bindGo(rel);
        }
        more.classList.toggle('hide', !data.hasMore);
        draw();
      });
    };
    more.querySelector('button').onclick = () => { st.page++; load(false); };
    load(true);
  };
  // Tên cũ (mọi trang đang gọi) → dòng thời gian chung.
  D.historyTab = (resource, noRelated, title) => D.timeline({ resource, noRelated, title });
  // Thanh tỷ lệ (Đánh giá NCC…): value 0–100, good = ngưỡng tốt.
  D.meter = (label, value, sub, good) => {
    const v = value === null || value === undefined ? null : Math.max(0, Math.min(100, +value));
    const color = v === null ? 'var(--muted)' : v >= (good || 90) ? 'var(--green, #91AF42)' : v >= (good || 90) - 15 ? '#F07825' : '#D9534F';
    return `<div class="d360-meter"><div class="spread small"><span>${esc(label)}</span><b>${v === null ? '–' : U.num(v, 1) + '%'}</b></div><div class="bar"><i style="width:${v || 0}%;background:${color}"></i></div>${sub ? `<div class="small muted">${sub}</div>` : ''}</div>`;
  };
  APP.D360 = D;
})();
