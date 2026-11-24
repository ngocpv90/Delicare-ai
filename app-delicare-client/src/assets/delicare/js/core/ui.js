/* DELICARE – Thành phần giao diện dùng chung: badge, bảng dữ liệu, modal, drawer, toast, form, truy vết DB */
(function () {
  const U = UTIL, esc = U.esc, I = U.icon;
  const COLOR = {
    green: 'Active Approved Published Locked Passed PASS DELIVERED Delivered Completed Closed Posted Reconciled Matched Resolved Succeeded Success Processed Available OK Confirmed CheckedIn Issued Packed Accepted APPROVE Loaded CHECK_IN',
    orange: 'Pending PendingApproval Waiting Submitted QCPending HOLD OnHold InProgress Loading PartiallyReceived PartiallyAccepted PartiallyIssued Variance Shortage Queued Received Open Requested Counting Cooking Medium Running PARTIAL Maintenance SHORTAGE DIRTY',
    blue: 'InProduction InTransit DISPATCHED ARRIVED LOADED Released Sent Arrived Packing Reserved Returned Inspected Calculated Sufficient Expected CHECK_OUT',
    red: 'Rejected REJECT Failed FAILED Cancelled Expired Absent Error Terminated High DAMAGED REJECTED FAIL Revoked',
    purple: 'Converted Superseded'
  };
  const CMAP = {}; Object.entries(COLOR).forEach(([c, s]) => s.split(' ').forEach(k => CMAP[k] = c));
  const UI = {};
  UI.statusVi = (t, col, v) => { const d = Store.statusDef(t, col); if (d) { const i = d.codes.indexOf(v); if (i >= 0 && d.vi[i]) return d.vi[i]; } return v; };
  UI.badge = (v, t, col) => v === null || v === undefined || v === '' ? '<span class="muted">–</span>' : `<span class="badge ${CMAP[v] || 'gray'}" title="${esc(t && !Store.isLive ? t + '.' + col + ' = ' + v : (t ? UI.statusVi(t, col, v) : v))}">${esc(t ? UI.statusVi(t, col, v) : v)}</span>`;
  UI.tag = (v, c) => `<span class="tag ${c || ''}">${esc(v)}</span>`;
  // ---------- Hiển thị giá trị theo kiểu cột ----------
  UI.val = (t, col, v, row) => {
    const m = Store.colmeta[t] && Store.colmeta[t][col];
    if (v === null || v === undefined || v === '') return '<span class="muted">–</span>';
    if (!m) return esc(v);
    // Lỗi UAT 2026-11-21: cột mã danh mục tham chiếu (ReferenceLists) hiện nhãn tiếng Việt.
    if (Store.isRefCol(t, col)) return UI.tag(UI.statusVi(t, col, v));
    if (m.usage.startsWith('Trạng thái') || ['MealType', 'TransactionType', 'CountType', 'WasteType', 'Severity', 'EventType', 'CustomerType', 'SupplierType', 'WarehouseType', 'StorageType', 'UnitType', 'BusinessType', 'DomainCode', 'LeaveTypeCode', 'DirectionCode', 'MeasurementType', 'PriorityCode', 'CategoryCode', 'IssueType', 'DataQualityStatus'].includes(col)) return m.usage.startsWith('Trạng thái') ? UI.badge(v, t, col) : UI.tag(v);
    if (col.endsWith('Id') && col !== 'Id') {
      const dst = Store.target(t, col);
      if (dst && Store.get(dst, v)) return `<a href="javascript:void 0" data-open="${dst}" data-id="${esc(v)}">${esc(Store.label(dst, v))}</a>`;
      return `<span class="mono" title="Tham chiếu chưa xác định bảng đích">${esc(String(v).slice(0, 8))}…</span>`;
    }
    if (m.type === 'bit') return v ? '<span class="tag green">Có</span>' : '<span class="tag">Không</span>';
    if (/^(datetime|datetime2)/.test(m.type)) return esc(/T00:00$/.test(v) || String(v).length <= 10 ? U.date(v) : U.dt(v));
    if (m.type === 'date') return esc(U.date(v));
    if (m.type.startsWith('time')) return esc(U.time(v));
    if (/Amount|Cost|Price|Revenue|Margin|Value$/.test(col) && /decimal/.test(m.type) && !/Percent/.test(col)) return esc(U.money(v));
    if (/decimal|int|bigint/.test(m.type)) return esc(U.num(v));
    if (/Json$/.test(col)) return `<span class="mono">${esc(String(v).slice(0, 80))}</span>`;
    if (/Url$/.test(col)) return `<span class="mono">${esc(v)}</span>`;
    return esc(v);
  };
  UI.text = (t, col, v) => { if (v === null || v === undefined) return ''; if (col.endsWith('Id') && Store.target(t, col)) return Store.label(Store.target(t, col), v); const m = Store.colmeta[t][col]; if (m && m.usage.startsWith('Trạng thái')) return UI.statusVi(t, col, v); return v; };
  // ---------- Toast ----------
  // Màn hình chuyên biệt cần dữ liệu nền (nhóm, ca, kho…) mà database chưa có.
  UI.emptyData = (tables, msg) => `<div class="card panel empty">${I('database')}<h2 class="mt">Chưa có dữ liệu để hiển thị màn hình này</h2><p class="muted">${msg ? esc(msg) + '<br>' : ''}Bảng chưa có dữ liệu: ${tables.map(t => '<code>dbo.' + esc(t) + '</code>' + (Store.srcOf(t) === 'api' ? ' (API ' + esc(Store.controllerOf(t)) + ')' : '')).join(', ')}. Nhập dữ liệu danh mục liên quan rồi mở lại.</p></div>`;
  UI.toast = (msg, kind) => {
    // Chế độ API: thao tác ghi thật xuống database → bỏ hậu tố "DEMO" của mockup.
    if (Store.hasHost && Store.mode === 'api') msg = String(msg).replace(/\s*\((DỮ LIỆU )?DEMO\)/g, '').replace(/\s*·\s*DEMO\)/g, ')').replace(/\s*<small>\(dbo\.[^)]*\)<\/small>/g, '');
    let box = document.getElementById('toasts'); const el = document.createElement('div');
    el.className = 'dc-toast ' + (kind || ''); el.innerHTML = I(kind === 'err' ? 'alert' : kind === 'warn' ? 'alert' : 'ok') + '<div>' + msg + '</div>';
    box.appendChild(el); setTimeout(() => el.remove(), 3600);
  };
  // Chế độ thật: hộp thoại / ngăn chi tiết cũng bỏ chữ kỹ thuật của mockup như vùng nội dung chính (APP.cleanDemoText nạp sau ui.js).
  const cleanTech = root => { try { if (window.APP && APP.cleanDemoText) APP.cleanDemoText(root); } catch (e) { console.warn(e); } };
  // ---------- Modal ----------
  // locked (T45R.8): hộp thoại bắt buộc (vd. đổi mật khẩu tạm) – không có nút X, không đóng bằng bấm nền / Esc.
  UI.modal = ({ title, sub, body, foot, size, onMount, locked }) => {
    const ov = document.createElement('div'); ov.className = 'modal overlay show'; ov.style.display = 'block'; ov.tabIndex = -1;
    ov.innerHTML = `<div class="modal-dialog modal-dialog-centered modal-dialog-scrollable ${size === 'lg' ? 'modal-xl' : size === 'sm' ? '' : 'modal-lg'}" role="dialog" aria-modal="true"><div class="modal-content"><div class="modal-header"><div style="flex:1;min-width:0"><h5 class="modal-title">${title}</h5>${sub ? `<div class="panel-desc">${sub}</div>` : ''}</div>${locked ? '' : '<button type="button" class="btn-close" data-close aria-label="Đóng"></button>'}</div><div class="modal-body">${body || ''}</div>${foot !== false ? `<div class="modal-footer">${foot || '<button class="btn" data-close>Đóng</button>'}</div>` : ''}</div></div>`;
    if (locked) ov.dataset.locked = '1';
    document.body.appendChild(ov);
    const close = () => ov.remove();
    ov.addEventListener('click', e => { if (locked) return; if (e.target === ov || e.target.closest('[data-close]')) close(); });
    onMount && onMount(ov.querySelector('.modal-content'), close);
    cleanTech(ov);
    return close;
  };
  UI.confirm = (msg, onOk, opt = {}) => UI.modal({
    title: opt.title || 'Xác nhận thao tác', size: 'sm',
    body: `<p style="margin-top:0">${msg}</p>${opt.input ? `<div class="field"><label>${opt.input}${opt.required ? ' <span class="req">*</span>' : ''}</label><textarea class="input" rows="3" id="cfmInput" placeholder="${opt.placeholder || ''}"></textarea><span class="err hide" id="cfmErr">Vui lòng nhập nội dung</span></div>` : ''}`,
    foot: `<button class="btn" data-close>Hủy</button><button class="btn ${opt.danger ? 'danger' : 'primary'}" id="cfmOk">${opt.ok || 'Đồng ý'}</button>`,
    onMount: (m, close) => m.querySelector('#cfmOk').onclick = () => { const v = opt.input ? m.querySelector('#cfmInput').value.trim() : null; if (opt.required && !v) { m.querySelector('#cfmErr').classList.remove('hide'); return; } close(); onOk(v); }
  });
  // ---------- Drawer ----------
  UI.drawer = ({ title, sub, badge, tabs, foot, onMount, width }) => {
    const w = document.createElement('div'); w.className = 'drawer-wrap show';
    w.innerHTML = `<aside class="drawer" ${width ? `style="width:min(${width}px,100%)"` : ''}><div class="drawer-head"><div class="t"><h2 style="margin-top:3px">${title} ${badge || ''}</h2></div><button type="button" class="btn-close" data-close aria-label="Đóng"></button></div><div class="drawer-body"><div class="tabs">${tabs.map((t, i) => `<button class="tab ${i ? '' : 'active'}" data-tab="${i}">${t.label}</button>`).join('')}</div>${tabs.map((t, i) => `<div class="tabpane ${i ? 'hide' : ''}" data-pane="${i}">${typeof t.html === 'function' ? '' : t.html}</div>`).join('')}</div>${foot ? `<div class="drawer-foot">${foot}</div>` : ''}</aside>`;
    document.body.appendChild(w);
    const close = () => w.remove();
    const loaded = {};
    const show = i => { w.querySelectorAll('.tab').forEach(b => b.classList.toggle('active', +b.dataset.tab === i)); w.querySelectorAll('.tabpane').forEach(p => p.classList.toggle('hide', +p.dataset.pane !== i)); if (typeof tabs[i].html === 'function' && !loaded[i]) { loaded[i] = 1; const p = w.querySelector(`[data-pane="${i}"]`); p.innerHTML = tabs[i].html(p); } cleanTech(w); };
    w.addEventListener('click', e => { if (e.target === w || e.target.closest('[data-close]')) close(); const tb = e.target.closest('[data-tab]'); if (tb) show(+tb.dataset.tab); });
    show(0); onMount && onMount(w.querySelector('.drawer'), close);
    return close;
  };
  // ---------- Bảng dữ liệu: tìm kiếm, sắp xếp, phân trang, lọc ----------
  UI.table = (el, cfg) => {
    const st = { q: '', sort: cfg.sort || null, dir: cfg.dir || 1, page: 1, size: cfg.pageSize || 10, f: {} };
    // UI-6 (D-80): nhớ tìm / lọc / sắp xếp / trang theo màn + bảng; quay lại từ trang chi tiết (APP._restoreList = màn) → khôi phục.
    const tkey = window.APP ? APP.current + '|' + (el.id || '') + '|' + (cfg.table || '') : null;
    UI._tbl = UI._tbl || {};
    if (tkey && window.APP && APP._restoreList === APP.current && UI._tbl[tkey]) Object.assign(st, UI._tbl[tkey], { f: Object.assign({}, UI._tbl[tkey].f) });
    const render = () => {
      if (tkey) UI._tbl[tkey] = { q: st.q, sort: st.sort, dir: st.dir, page: st.page, size: st.size, f: Object.assign({}, st.f) };
      let rows = typeof cfg.rows === 'function' ? cfg.rows() : cfg.rows;
      if (st.q) { const q = st.q.toLowerCase(); rows = rows.filter(r => cfg.cols.some(c => String(c.text ? c.text(r) : (cfg.table ? UI.text(cfg.table, c.k, r[c.k]) : r[c.k]) ?? '').toLowerCase().includes(q))); }
      Object.entries(st.f).forEach(([k, v]) => { if (v) rows = rows.filter(r => String(r[k]) === v); });
      if (st.sort) { const c = cfg.cols.find(x => x.k === st.sort); const g = c && c.sortv ? c.sortv : (r => r[st.sort]); rows = rows.slice().sort((a, b) => { const x = g(a), y = g(b); return (x > y ? 1 : x < y ? -1 : 0) * st.dir; }); }
      const total = rows.length, pages = Math.max(1, Math.ceil(total / st.size)); if (st.page > pages) st.page = pages;
      const view = rows.slice((st.page - 1) * st.size, st.page * st.size);
      const filters = (cfg.filters || []).map(f => `<select class="select" data-f="${f.k}"><option value="">${f.label}: Tất cả</option>${f.options.map(o => `<option value="${esc(o.v)}" ${st.f[f.k] === String(o.v) ? 'selected' : ''}>${esc(o.l)}</option>`).join('')}</select>`).join('');
      el.innerHTML = `${cfg.toolbar === false ? '' : `<div class="toolbar"><input class="input search-in" placeholder="Tìm trong ${total} dòng…" value="${esc(st.q)}" data-q>${filters}<span class="grow"></span>${cfg.tools || ''}</div>`}
      <div class="table-wrap table-responsive"><table class="table dc-table dt"><thead><tr>${cfg.cols.map(c => `<th class="${c.num ? 'num' : ''} ${c.nosort ? '' : 'sortable'}" data-sort="${c.k}">${c.label}${st.sort === c.k ? (st.dir > 0 ? ' ▲' : ' ▼') : ''}${c.col !== false && cfg.table && Store.colmeta[cfg.table][c.col || c.k] ? `<small>${c.col || c.k}</small>` : ''}</th>`).join('')}${cfg.actions ? '<th></th>' : ''}</tr></thead>
      <tbody>${view.length ? view.map(r => `<tr class="${cfg.onRow ? 'clickable' : ''}" data-row="${esc(r.Id)}">${cfg.cols.map(c => `<td class="${c.num ? 'num' : ''}">${c.render ? c.render(r) : (cfg.table ? UI.val(cfg.table, c.k, r[c.k], r) : esc(r[c.k]))}</td>`).join('')}${cfg.actions ? `<td class="actions">${cfg.actions(r)}</td>` : ''}</tr>`).join('') : `<tr><td colspan="${cfg.cols.length + 1}" class="empty">Không có dữ liệu phù hợp bộ lọc</td></tr>`}</tbody></table></div>
      ${cfg.pager === false ? '' : `<div class="pager"><span>Hiển thị ${total ? (st.page - 1) * st.size + 1 : 0}–${Math.min(st.page * st.size, total)} / ${total} dòng · <select class="select" data-size style="min-height:26px;padding:2px 6px">${[5, 10, 20, 50].map(s => `<option ${s === st.size ? 'selected' : ''}>${s}</option>`).join('')}</select> dòng/trang</span><span class="pages"><button data-pg="${st.page - 1}" ${st.page <= 1 ? 'disabled' : ''}>‹</button>${Array.from({ length: pages }, (_, i) => i + 1).filter(p => p === 1 || p === pages || Math.abs(p - st.page) <= 1).map((p, i, a) => (i && p - a[i - 1] > 1 ? '<span>…</span>' : '') + `<button data-pg="${p}" class="${p === st.page ? 'active' : ''}">${p}</button>`).join('')}<button data-pg="${st.page + 1}" ${st.page >= pages ? 'disabled' : ''}>›</button></span></div>`}`;
      const qi = el.querySelector('[data-q]'); if (qi) { qi.oninput = e => { st.q = e.target.value; st.page = 1; const pos = e.target.selectionStart; render(); const n = el.querySelector('[data-q]'); n.focus(); n.setSelectionRange(pos, pos); }; }
      el.querySelectorAll('[data-f]').forEach(s => s.onchange = e => { st.f[s.dataset.f] = e.target.value; st.page = 1; render(); });
      el.querySelectorAll('th.sortable').forEach(th => th.onclick = () => { const k = th.dataset.sort; if (st.sort === k) st.dir *= -1; else { st.sort = k; st.dir = 1; } render(); });
      el.querySelectorAll('[data-pg]').forEach(b => b.onclick = () => { st.page = +b.dataset.pg; render(); });
      const sz = el.querySelector('[data-size]'); if (sz) sz.onchange = e => { st.size = +e.target.value; st.page = 1; render(); };
      if (cfg.onRow) el.querySelectorAll('tr[data-row]').forEach(tr => tr.onclick = e => { if (e.target.closest('button,a,input,select')) return; cfg.onRow(tr.dataset.row); });
      cfg.after && cfg.after(el, rows);
    };
    render();
    return { render, state: st, setFilter(k, v) { st.f[k] = v; st.page = 1; render(); } };
  };
  // ---------- Form sinh từ schema ----------
  const SKIP = new Set(['Id', 'CreatedDate', 'CreatedBy', 'ModifiedDate', 'ModifiedBy', 'DeletedDate', 'DeletedBy', 'DeleteddBy', 'IsDeleted']);
  UI.formFields = t => Object.values(Store.colmeta[t]).filter(m => !SKIP.has(m.name) && !Store.isDeprecated(t, m.name) && !UI.hidden(t, m.name) && m.usage !== 'Kỹ thuật' && m.usage !== 'Ẩn (bảo mật)' && m.usage !== 'Hệ thống (PK)');
  // UI-5.4: nhãn tiếng Việt của bảng cho chữ "— Chọn … —" của ô chọn tham chiếu (không hiện tên bảng tiếng Anh).
  const TABLE_VI = {
    OrganizationUnit: 'đơn vị', User: 'người dùng', Employee: 'nhân viên', Person: 'hồ sơ', Company: 'công ty', Customer: 'khách hàng', Supplier: 'nhà cung cấp',
    Ingredient: 'nguyên liệu', Dishe: 'món ăn', Menu: 'thực đơn', Warehouse: 'kho', Vehicle: 'xe', Contract: 'hợp đồng', Order: 'đơn hàng', Shift: 'ca',
    CustomerDeliveryLocation: 'điểm giao', CustomerContact: 'người liên hệ', UnitOfMeasure: 'đơn vị tính', MaterialLot: 'lô nguyên liệu', Recipe: 'công thức',
  };
  UI.tableVi = t => TABLE_VI[t] || String(((Store.cat.funcs || []).find(f => f.table === t) || {}).name || 'giá trị').toLowerCase();
  // UI-5.2: cột datetime mà nghiệp vụ là NGÀY (tên kết thúc bằng Date / DateOfBirth) → ô chỉ chọn ngày, gửi yyyy-MM-dd.
  // Danh sách duy nhất – cột *Date cần cả giờ thì thêm vào DATETIME_KEEP.
  const DATETIME_KEEP = new Set([]);
  UI.isDateOnly = (t, name) => !DATETIME_KEEP.has(t + '.' + name) && /(Date|DateOfBirth)$/.test(name) && !/^(Created|Modified|Deleted)Date$/.test(name);
  // UI-5.5: trạng thái Active / Inactive → chữ tiếng Việt, mặc định Đang hoạt động, không có lựa chọn trống.
  const ACTIVE_VI = { Active: 'Đang hoạt động', Inactive: 'Ngừng hoạt động' };
  const isActiveInactive = codes => codes.length === 2 && codes.includes('Active') && codes.includes('Inactive');
  const activeSelect = (id, name, v) => `<select class="select" id="${id}" name="${name}">${['Active', 'Inactive'].concat(v && !ACTIVE_VI[v] ? [v] : []).map(c => `<option value="${esc(c)}" ${c === (v || 'Active') ? 'selected' : ''}>${esc(ACTIVE_VI[c] || c)}</option>`).join('')}</select>`;
  // Ô chọn từ danh mục: chỉ chữ tiếng Việt; giá trị cũ ngoài danh mục vẫn hiện (chọn sẵn) để không mất khi lưu.
  // Lỗi UAT 2026-11-21 (rà soát danh mục): ô chọn từ danh mục tham chiếu ReferenceLists (Store.refList) cho form riêng; giá trị cũ ngoài danh mục
  // hiện "(giá trị cũ)". Danh mục chưa tải (DEMO / lỗi mạng) → ô gõ thường để không chặn thao tác (máy chủ vẫn kiểm).
  UI.refSelect = (id, list, opt = {}) => {
    const items = Store.refList(list);
    if (!items.length) return `<input class="input" id="${id}" value="${esc(opt.value || '')}" maxlength="100" placeholder="${esc(opt.placeholder || '')}">`;
    return UI.catalogSelect(id, id, items, opt.value || '', !!opt.required, opt.placeholder || '— Chọn —');
  };
  UI.catalogSelect = (id, name, items, v, req, placeholder) => `<select class="select" id="${id}" name="${name}" ${req ? 'required' : ''}><option value="">${esc(placeholder || '— Chọn —')}</option>${items.map(x => `<option value="${esc(x.code)}" ${x.code === v ? 'selected' : ''}>${esc(x.name)}</option>`).join('')}${v && !items.some(x => x.code === v) ? `<option value="${esc(v)}" selected>${esc(v)} (giá trị cũ)</option>` : ''}</select>`;
  UI.input = (t, m, v) => {
    const id = 'f_' + m.name, req = !m.nullable && !m.def;
    if (m.name.endsWith('Id')) {
      const dst = Store.target(t, m.name);
      if (dst) return `<select class="select" id="${id}" name="${m.name}" ${req ? 'required' : ''}><option value="">— Chọn ${esc(UI.tableVi(dst))} —</option>${Store.rows(dst).slice(0, 400).map(r => `<option value="${esc(r.Id)}" ${r.Id === v ? 'selected' : ''}>${esc(Store.label(dst, r))}</option>`).join('')}</select>`;
      return `<input class="input mono" id="${id}" name="${m.name}" value="${esc(v || '')}" placeholder="Tham chiếu chưa xác định bảng đích (GAP)" ${req ? 'required' : ''}>`;
    }
    const sd = Store.statusDef(t, m.name);
    if (sd && isActiveInactive(sd.codes)) return activeSelect(id, m.name, v);
    if (sd) return `<select class="select" id="${id}" name="${m.name}" ${req ? 'required' : ''}><option value="">${sd.plain ? '— Chọn —' : '—'}</option>${sd.codes.map((c, i) => `<option value="${c}" ${c === v ? 'selected' : ''}>${esc(sd.vi[i] || c)}${sd.plain ? '' : ` (${c})`}</option>`).join('')}${sd.plain && v && !sd.codes.includes(v) ? `<option value="${esc(v)}" selected>${esc(v)} (giá trị cũ)</option>` : ''}</select>`;
    if (m.name === 'Status' || m.name.endsWith('Status')) return activeSelect(id, m.name, v);
    if (m.type === 'bit') return `<select class="select" id="${id}" name="${m.name}"><option value="1" ${v ? 'selected' : ''}>Có</option><option value="0" ${!v ? 'selected' : ''}>Không</option></select>`;
    if (m.type === 'date' || (/^datetime/.test(m.type) && UI.isDateOnly(t, m.name))) return `<input type="date" class="input" id="${id}" name="${m.name}" value="${esc(U.day(v))}" ${req ? 'required' : ''}>`;
    if (/^datetime/.test(m.type)) return `<input type="datetime-local" class="input" id="${id}" name="${m.name}" value="${esc(v ? String(v).slice(0, 16) : '')}" ${req ? 'required' : ''}>`;
    if (m.type.startsWith('time')) return `<input type="time" class="input" id="${id}" name="${m.name}" value="${esc(U.time(v))}">`;
    if (/decimal|int|bigint/.test(m.type)) return `<input type="number" step="any" class="input" id="${id}" name="${m.name}" value="${v ?? ''}" ${req ? 'required' : ''}>`;
    if (/max\)$/.test(m.type) && /Note|Description|Instructions|Reason|Resolution|Remark|Address|Changes|Comment/.test(m.name)) return `<textarea class="input" rows="2" id="${id}" name="${m.name}">${esc(v || '')}</textarea>`;
    const ml = (m.type.match(/\((\d+)\)/) || [])[1];
    return `<input class="input" id="${id}" name="${m.name}" value="${esc(v || '')}" ${ml ? `maxlength="${ml}"` : ''} ${req ? 'required' : ''}>`;
  };
  // ---------- MA-1 (D-63): cột ẩn + form khách hàng theo loại ----------
  // CIF = Code (máy chủ gán) – chỉ dùng cho môi trường ngân hàng, KHÔNG hiển thị ở form / danh sách / hồ sơ (D-63 h, j).
  // Segment (Phân khúc) – anh bỏ khỏi hệ thống 2026-11-22 (GAP-REF-05); cột DB giữ nguyên, không dùng.
  const HIDDEN = { Customer: new Set(['CIF', 'Segment']) };
  UI.hidden = (t, col) => !!(HIDDEN[t] && HIDDEN[t].has(col));
  // D-63 g: trường theo loại khách hàng. req = bắt buộc trên giao diện (máy chủ kiểm lại; Chi nhánh chỉ bắt buộc khi đã có chi nhánh).
  const CUSTOMER_FIELDS = {
    BUSINESS: {
      req: ['LegalName', 'DisplayName', 'TaxCode', 'LegalRepresentativeName', 'ContactPersonName', 'ContactPersonPhone', 'Address', 'PhoneNumber'],
      opt: ['BranchId', 'BusinessRegistrationNumber', 'RegistrationDate', 'RegistrationPlace', 'Email', 'ContactPersonEmail', 'Industry', 'CompanySize', 'Source', 'UserAssignId', 'Status', 'Note'],
    },
    INDIVIDUAL: {
      req: ['LastName', 'FirstName', 'Address', 'PhoneNumber'],
      opt: ['BranchId', 'Gender', 'DateOfBirth', 'IdentityNumber', 'IdentityDate', 'IdentityPlace', 'Occupation', 'Email', 'TaxCode', 'Source', 'UserAssignId', 'Status', 'Note'],
    },
  };
  const CUSTOMER_LABEL = {
    LegalName: 'Tên doanh nghiệp', DisplayName: 'Tên giao dịch', TaxCode: 'Mã số thuế', LegalRepresentativeName: 'Người đại diện pháp luật',
    ContactPersonName: 'Người liên hệ', ContactPersonPhone: 'SĐT người liên hệ', ContactPersonEmail: 'Email người liên hệ', PhoneNumber: 'Điện thoại',
    BranchId: 'Chi nhánh phục vụ', BusinessRegistrationNumber: 'Số ĐKKD (trống = MST)', RegistrationDate: 'Ngày cấp ĐKKD', RegistrationPlace: 'Nơi cấp ĐKKD',
    Industry: 'Ngành', CompanySize: 'Quy mô', Source: 'Nguồn', UserAssignId: 'NV phụ trách', LastName: 'Họ', FirstName: 'Tên',
    Gender: 'Giới tính', DateOfBirth: 'Ngày sinh', IdentityNumber: 'Số CCCD (12 số)', IdentityDate: 'Ngày cấp CCCD', IdentityPlace: 'Nơi cấp CCCD', Occupation: 'Nghề nghiệp',
  };
  const customerType = v => (String(v || '').toUpperCase() === 'INDIVIDUAL' ? 'INDIVIDUAL' : 'BUSINESS');
  // UI-5.7: kiểm tại ô – CÙNG quy tắc với máy chủ (nguồn: AppDelicare.Services/Implement/CustomerService.cs – CheckRules + các GeneratedRegex).
  const CUS_RX = { bizTax: /^\d{10}(-\d{3})?$/, personTax: /^\d{10}$/, identity: /^\d{12}$/, phone: /^(0\d{9}|\+84\d{9})$/, phoneSep: /[\s.\-()]/g, email: /^[^@\s]+@[^@\s]+\.[^@\s]+$/ };
  const CUS_DATE_NOT_FUTURE = new Set(['DateOfBirth', 'IdentityDate', 'RegistrationDate']);
  /** Lỗi của 1 ô khách hàng ('' = hợp lệ). vals = giá trị đang nhập của cả form (kiểm chéo ngày cấp CCCD ≥ ngày sinh). */
  UI.customerFieldError = (name, value, cusType, vals) => {
    const v = value === null || value === undefined ? '' : String(value).trim();
    if (!v) return '';
    if (name === 'TaxCode') return (cusType === 'INDIVIDUAL' ? CUS_RX.personTax : CUS_RX.bizTax).test(v) ? '' : (cusType === 'INDIVIDUAL' ? 'Mã số thuế cá nhân gồm 10 số' : 'Mã số thuế gồm 10 số hoặc 10-3 số (vd. 0312345678-001)');
    if (name === 'IdentityNumber') return CUS_RX.identity.test(v) ? '' : 'Số CCCD gồm đúng 12 số';
    if (name === 'PhoneNumber' || name === 'ContactPersonPhone') return CUS_RX.phone.test(v.replace(CUS_RX.phoneSep, '')) ? '' : 'Số điện thoại: 0 + 9 số hoặc +84 + 9 số';
    if (name === 'Email' || name === 'ContactPersonEmail') return CUS_RX.email.test(v) ? '' : 'Email không đúng định dạng';
    if (CUS_DATE_NOT_FUTURE.has(name)) {
      if (v > Store.ctx.date) return 'Không được sau hôm nay';
      if (name === 'IdentityDate' && vals && vals.DateOfBirth && v < String(vals.DateOfBirth)) return 'Ngày cấp CCCD phải từ ngày sinh trở đi';
    }
    return '';
  };
  // UI-5 (D-63 r): dữ liệu form khách hàng – chế độ API lấy Customer/GetFormOptions; DEMO dựng từ dữ liệu cục bộ + catalog.
  const localCustomerOptions = () => {
    const cat = c => { const d = Store.statusDef('Customer', c); return d ? d.codes.map((code, i) => ({ code, name: d.vi[i] || code })) : []; };
    const branches = Store.rows('OrganizationUnit').filter(u => u.UnitType === 'BRANCH');
    return {
      branchRequired: branches.length > 0,
      listBranch: branches.map(u => ({ code: u.Id, name: u.Name || u.Code, hint: u.Code })),
      listAssignee: Store.rows('User').filter(u => u.IsActive !== false && u.IsActive !== 0).map(u => ({ code: u.Id, name: Store.label('User', u), hint: u.UserName })),
      catalogs: { Industry: cat('Industry'), CompanySize: cat('CompanySize'), Gender: cat('Gender'), Source: cat('Source') },
    };
  };
  UI.customerOptions = () => (Store.isLive ? Store.query('Customer/GetFormOptions').then(x => (x.ok && x.data ? x.data : localCustomerOptions())) : Promise.resolve(localCustomerOptions()));
  // UI-5.8: khối Điểm giao đầu tiên (chỉ khi tạo mới) – khóa gửi lên FirstLocation.* (CustomerRequest.FirstLocation).
  const FIRST_LOC = [['Name', 'Tên điểm giao', 'text'], ['Address', 'Địa chỉ điểm giao', 'text'], ['DeliveryWindowFrom', 'Giờ nhận từ', 'time'], ['DeliveryWindowTo', 'Giờ nhận đến', 'time']];
  UI.form = (t, row, opt = {}) => {
    const editing = !!(row && row.Id), rec = Object.assign({}, opt.defaults || {}, row || {});
    const isCustomer = t === 'Customer' && !opt.fields;
    let cusType = customerType(rec.CustomerType);
    // UI-5.6: tạo mới khách hàng → chọn loại trước (2 nút lớn), chưa chọn sẵn; sửa → giữ loại (D-63 i).
    let typeChosen = !isCustomer || editing || !!(opt.defaults && opt.defaults.CustomerType);
    let cusOpt = isCustomer ? localCustomerOptions() : null;
    const firstLoc = { open: false, Name: '', Address: '', DeliveryWindowFrom: '', DeliveryWindowTo: '' };
    let autoDisplay = rec.DisplayName && rec.DisplayName === rec.LegalName ? rec.DisplayName : null;   // UI-5.9
    // Trường của form: khách hàng theo loại (D-63 g); bảng khác sinh từ schema như cũ.
    const fieldsOf = () => {
      if (!isCustomer) return (opt.fields ? opt.fields.map(f => Store.colmeta[t][f]).filter(Boolean) : UI.formFields(t));
      if (!typeChosen) return [];
      const d = CUSTOMER_FIELDS[cusType];
      return ['Code', 'CustomerType'].concat(d.req, d.opt).map(f => Store.colmeta[t][f]).filter(Boolean);
    };
    // opt.optional: cột NOT NULL nhưng máy chủ tự điền khi trống (vd. MENU-4.5 mã / thứ tự tiêu chí ATTP).
    const isReq = m => isCustomer ? CUSTOMER_FIELDS[cusType].req.includes(m.name) || m.name === 'CustomerType' || (m.name === 'BranchId' && !!cusOpt.branchRequired) : (!m.nullable && !m.def && !(opt.optional || []).includes(m.name));
    const labelOf = m => (isCustomer && CUSTOMER_LABEL[m.name]) || U.colLabel(m.name);
    // MA-1 (D-63 k): nhóm mã từ api/Common/GetCodeRules – G1 chỉ đọc "(tự sinh khi lưu)", G2 chữ hoa không dấu.
    const ruleOf = m => Store.codeRule(t, m.name);
    const searchable = (id, selectHtml, placeholder) => `<div class="pick-search"><input type="search" class="input" data-search-for="${id}" placeholder="${esc(placeholder)}" autocomplete="off">${selectHtml}</div>`;
    const customerInput = m => {
      const v = rec[m.name], id = 'f_' + m.name, cats = cusOpt.catalogs || {};
      if (m.name === 'BranchId') {
        const items = (cusOpt.listBranch || []).map(x => ({ code: x.code, name: x.name + (x.hint && x.hint !== x.name ? ' · ' + x.hint : '') }));
        const html = UI.catalogSelect(id, m.name, items, v, isReq(m), '— Chọn chi nhánh —');
        return items.length > 8 ? searchable(id, html, 'Tìm chi nhánh…') : html;
      }
      if (m.name === 'UserAssignId') {
        const items = (cusOpt.listAssignee || []).map(x => ({ code: x.code, name: x.name + (x.hint && x.hint !== x.name ? ' (' + x.hint + ')' : '') }));
        return searchable(id, UI.catalogSelect(id, m.name, items, v, false, '— Chọn nhân viên phụ trách —'), 'Tìm theo tên / tên đăng nhập…');
      }
      if (m.name === 'Status') return null;   // UI-5.5: UI.input → Đang hoạt động / Ngừng hoạt động, mặc định Đang hoạt động, không có ô trống
      if (cats[m.name]) return UI.catalogSelect(id, m.name, cats[m.name], v, isReq(m), '— Chọn ' + labelOf(m).toLowerCase() + ' —');
      return null;
    };
    // LOC-1 (2026-11-22): điểm giao của khách doanh nghiệp – Người nhận chọn trong danh sách người liên hệ của khách (máy chủ kiểm lại), chọn xong tự điền SĐT.
    const isLocation = t === 'CustomerDeliveryLocation';
    const recipientPick = () => {
      const cus = Store.get('Customer', valueOfRec('CustomerId'));
      if (!cus || String(cus.CustomerType || '').toUpperCase() !== 'BUSINESS') return null;
      const cts = Store.by('CustomerContact', 'CustomerId', cus.Id).filter(c => !c.IsDeleted && (!c.Status || c.Status === 'Active'))
        .sort((a, b) => (b.IsPrimary ? 1 : 0) - (a.IsPrimary ? 1 : 0) || String(a.FullName || a.Name).localeCompare(String(b.FullName || b.Name)));
      const cur = rec.ContactName || '';
      const nameOf = c => c.FullName || c.Name || '';
      const known = cts.some(c => nameOf(c).toLowerCase() === cur.toLowerCase());
      return `<select class="select" id="f_ContactName" name="ContactName" required data-recipient="1"><option value="">${cts.length ? '— Chọn người liên hệ —' : '— Khách chưa có người liên hệ –  thêm ở mục Người liên hệ —'}</option>${cur && !known ? `<option value="${esc(cur)}" data-phone="${esc(rec.ContactPhone || '')}" selected>${esc(cur)} (giá trị cũ)</option>` : ''}${cts.map(c => `<option value="${esc(nameOf(c))}" data-phone="${esc(c.Phone || '')}" ${nameOf(c).toLowerCase() === cur.toLowerCase() ? 'selected' : ''}>${esc(nameOf(c))}${c.Position ? ' – ' + esc(c.Position) : ''}${c.Phone ? ' · ' + esc(c.Phone) : ''}${c.IsPrimary ? ' (chính)' : ''}</option>`).join('')}</select>`;
    };
    function valueOfRec(name) { const el = document.querySelector('.modal #f_' + name); return el && el.value ? el.value : rec[name]; }
    const inputOf = m => {
      const r = ruleOf(m), v = rec[m.name];
      if (isLocation && m.name === 'ContactName') { const pick = recipientPick(); if (pick) return pick; }
      if (r && r.group === 'G1') return `<input class="input mono" id="f_${m.name}" name="${m.name}" value="${esc(v || '')}" placeholder="(tự sinh khi lưu)" readonly data-g1="1" title="Mã do máy chủ tự sinh${r.prefix ? ' – ' + esc(r.prefix) : ''}">`;
      if (isCustomer && m.name === 'CustomerType') {
        // Loại khách khóa sau khi lưu (D-63 i) – đổi loại chỉ khi chưa có hợp đồng / đơn, máy chủ kiểm và cấp mã mới.
        return `<select class="select" id="f_CustomerType" name="CustomerType" required ${editing && !opt.allowTypeChange ? 'data-locked="1"' : ''}>${['BUSINESS', 'INDIVIDUAL'].map(c => `<option value="${c}" ${c === cusType ? 'selected' : ''}>${c === 'BUSINESS' ? 'Doanh nghiệp' : 'Cá nhân'}</option>`).join('')}</select>`;
      }
      let html = (isCustomer && customerInput(m)) || UI.input(t, m, v);
      if ((opt.optional || []).includes(m.name)) html = html.replace(' required', '').replace('<input ', `<input placeholder="${m.name === 'SortOrder' ? '(cuối danh sách)' : '(tự sinh khi để trống)'}" `);
      return r && r.group === 'G2' ? html.replace('<input ', '<input data-g2="1" ') : html;
    };
    const fieldHtml = m => {
      const req = isReq(m) && !(ruleOf(m) && ruleOf(m).group === 'G1');
      const wide = /max\)$/.test(m.type) && /Note|Description|Instructions|Reason|Resolution|Address/.test(m.name);
      return `<div class="field ${wide ? 'full' : ''}"><label for="f_${m.name}">${esc(labelOf(m))}${req ? ' <span class="req">*</span>' : ''}${Store.isLive ? '' : ` <code>${m.name} · ${m.type}</code>`}</label>${inputOf(m)}${m.def && !Store.isLive ? `<span class="hint">DEFAULT ${esc(m.def)}</span>` : ''}<span class="err hide"></span></div>`;
    };
    const typePickHtml = () => `<div class="type-pick"><p class="muted">Chọn loại khách hàng trước – mỗi loại có bộ thông tin riêng (mã KHB- / KHC- do máy chủ cấp).</p><div class="type-pick-grid">
      <button type="button" class="type-pick-btn" data-pick="BUSINESS"><b>Doanh nghiệp</b><span>Công ty, bệnh viện, nhà máy, trường học… – có mã số thuế, người đại diện, người liên hệ</span></button>
      <button type="button" class="type-pick-btn" data-pick="INDIVIDUAL"><b>Cá nhân</b><span>Khách lẻ / hộ gia đình – họ tên, CCCD, ngày sinh</span></button></div></div>`;
    const firstLocHtml = () => (!isCustomer || editing || !Store.isLive) ? '' : `<div class="first-loc mt" id="flBox"><div class="spread"><span><b>Điểm giao đầu tiên</b> <span class="muted small">(tùy chọn – tạo cùng lúc với khách hàng)</span></span>
      <button type="button" class="btn xs" id="flToggle">${firstLoc.open ? 'Bỏ điểm giao' : I('plus') + ' Thêm điểm giao ngay'}</button></div>
      <div class="form-grid ${firstLoc.open ? '' : 'hide'}" id="flBody">${FIRST_LOC.map(([k, l, type]) => `<div class="field ${k === 'Address' ? 'full' : ''}"><label for="fl_${k}">${l}${k === 'Name' || k === 'Address' ? ' <span class="req">*</span>' : ''}</label><input class="input" type="${type}" id="fl_${k}" value="${esc(firstLoc[k] || '')}"><span class="err hide"></span></div>`).join('')}</div></div>`;
    let fields = fieldsOf();
    // BIZ-3.6 (D-77 – A10): nhóm Điều khoản – ngưỡng thất thoát khay % lưu Customer.ExternalData.containerLossMaxPct (trống = cấu hình chung).
    const extOf = () => { try { return JSON.parse((row && row.ExternalData) || '{}') || {}; } catch (e) { return {}; } };
    const termsHtml = () => !isCustomer ? '' : `<div class="mt"><div class="panel-title mb">Điều khoản</div><div class="form-grid"><div class="field"><label>Ngưỡng thất thoát khay (%)</label><input type="number" class="input" id="f_xLoss" min="0" max="100" step="0.1" value="${esc(extOf().containerLossMaxPct ?? '')}" placeholder="Theo cấu hình chung"><span class="err hide"></span><span class="small muted">Vượt ngưỡng → cảnh báo thất thoát khay của khách. Trống = theo cấu hình chung.</span></div></div></div>`;
    const formHtml = () => (isCustomer && !typeChosen) ? typePickHtml() : `<div id="frmErr" class="notice warn hide"></div><form class="form-grid" novalidate>${fields.map(fieldHtml).join('')}</form>${termsHtml()}${firstLocHtml()}`;
    // Chế độ thật: không hiện ghi chú kỹ thuật của mockup (tên bảng, cột SQL) – CLAUDE.md 4.4.
    const body = `${Store.isLive ? '' : `<div class="notice info">${I('database')}<div><b>Form sinh từ cấu trúc bảng <code>dbo.${t}</code></b>Trường bắt buộc (<span class="req" style="color:var(--red)">*</span>) = cột NOT NULL không có DEFAULT trong SQL. Tên cột SQL hiển thị cạnh nhãn. Cột deprecated không hiển thị.</div></div>`}<div id="frmBody">${formHtml()}</div>`;
    UI.modal({
      title: (editing ? 'Chỉnh sửa – ' : 'Tạo mới – ') + (opt.title || t), sub: Store.isLive ? '' : `dbo.${t} · ${fields.length} trường`, size: 'lg', body,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="frmSave">${I('ok')} ${editing ? 'Lưu thay đổi' : 'Tạo mới'}</button>`,
      onMount: (m, close) => {
        const host = m.querySelector('#frmBody');
        const btn = m.querySelector('#frmSave');
        const showErr = msg => { const box = m.querySelector('#frmErr'); if (!box) return; box.innerHTML = I('alert') + '<div>' + esc(msg) + '</div>'; box.classList.remove('hide'); box.scrollIntoView({ block: 'nearest' }); };
        const setErr = (el, msg) => { const fld = el && el.closest('.field'); if (!fld) return; const err = fld.querySelector('.err'); fld.classList.toggle('invalid', !!msg); err.textContent = msg || ''; err.classList.toggle('hide', !msg); };
        const valueOf = name => { const el = m.querySelector('#f_' + name); return el ? el.value : rec[name]; };
        const valuesNow = () => { const o = {}; fields.forEach(f => { o[f.name] = valueOf(f.name); }); return o; };
        // UI-5.7: lỗi định dạng hiện ngay khi rời ô (cùng quy tắc máy chủ).
        const checkField = name => { const el = m.querySelector('#f_' + name); if (!el || !isCustomer) return ''; const msg = UI.customerFieldError(name, el.value, cusType, valuesNow()); setErr(el, msg); return msg; };
        const checkFirstLoc = full => {
          if (!firstLoc.open) return true;
          let ok = true;
          const el = k => m.querySelector('#fl_' + k);
          ['Name', 'Address'].forEach(k => { const miss = full && !el(k).value.trim(); setErr(el(k), miss ? 'Bắt buộc' : ''); if (miss) ok = false; });
          const from = el('DeliveryWindowFrom').value, to = el('DeliveryWindowTo').value, bad = from && to && from >= to;
          setErr(el('DeliveryWindowTo'), bad ? 'Giờ nhận từ phải nhỏ hơn giờ nhận đến' : '');
          return ok && !bad;
        };
        // Giữ giá trị đã nhập khi vẽ lại form (đổi loại khách / tải xong dữ liệu form).
        const collect = () => {
          fields.forEach(f => { const el = m.querySelector('#f_' + f.name); if (el && !el.dataset.g1) rec[f.name] = el.value === '' ? null : el.value; });
          FIRST_LOC.forEach(([k]) => { const el = m.querySelector('#fl_' + k); if (el) firstLoc[k] = el.value; });
        };
        const redraw = () => { fields = fieldsOf(); host.innerHTML = formHtml(); wire(); };
        // Gắn sự kiện 1 lần cho mỗi phần tử (wire chạy lại sau khi thay từng ô – không gắn trùng).
        const fresh = el => !!el && !el.dataset.w && !!(el.dataset.w = '1');
        const wire = () => {
          btn.disabled = isCustomer && !typeChosen;
          host.querySelectorAll('[data-pick]').forEach(b => b.onclick = () => { cusType = b.dataset.pick; rec.CustomerType = cusType; typeChosen = true; redraw(); const first = host.querySelector('.form-grid input:not([readonly]), .form-grid select:not([disabled])'); first && first.focus(); });
          host.querySelectorAll('[data-g2]').forEach(el => fresh(el) && el.addEventListener('input', () => { const p = el.selectionStart, v = Store.userCode(el.value); if (v !== el.value) { el.value = v; try { el.setSelectionRange(p, p); } catch (e) { /* bỏ qua */ } } }));
          // Ô chọn có tìm kiếm: gõ để lọc danh sách (bỏ dấu, không phân biệt hoa thường).
          const fold = s => String(s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
          host.querySelectorAll('[data-search-for]').forEach(inp => fresh(inp) && inp.addEventListener('input', () => {
            const sel = host.querySelector('#' + inp.dataset.searchFor), q = fold(inp.value.trim());
            [...sel.options].forEach((o, i) => { o.hidden = i > 0 && !!q && !fold(o.textContent).includes(q) && !o.selected; });
            const hits = [...sel.options].filter((o, i) => i > 0 && !o.hidden);
            if (q && hits.length === 1) { sel.value = hits[0].value; }
          }));
          if (isCustomer) {
            fields.forEach(f => { const el = host.querySelector('#f_' + f.name); if (fresh(el)) el.addEventListener('blur', () => checkField(f.name)); });
            const dob = host.querySelector('#f_DateOfBirth'); if (dob && !dob.dataset.wd) { dob.dataset.wd = '1'; dob.addEventListener('change', () => checkField('IdentityDate')); }
            // UI-5.9: nhập Tên doanh nghiệp → tự điền Tên giao dịch khi ô này còn trống (hoặc vẫn là giá trị tự điền).
            const legal = host.querySelector('#f_LegalName'), disp = host.querySelector('#f_DisplayName');
            if (legal && disp && !legal.dataset.wl) legal.dataset.wl = '1', legal.addEventListener('input', () => { if (!disp.value || disp.value === autoDisplay) { disp.value = legal.value; autoDisplay = legal.value; } });
            const tg = host.querySelector('#flToggle');
            if (tg) tg.onclick = () => { collect(); firstLoc.open = !firstLoc.open; if (firstLoc.open && !firstLoc.Address) firstLoc.Address = valueOf('Address') || ''; redraw(); if (firstLoc.open) { const n = host.querySelector('#fl_Name'); n && n.focus(); } };
            host.querySelectorAll('#flBody input').forEach(el => fresh(el) && el.addEventListener('blur', () => checkFirstLoc(false)));
          }
          const ct = host.querySelector('#f_CustomerType');
          if (ct && isCustomer) {
            if (ct.dataset.locked) { ct.disabled = true; ct.title = 'Loại khách hàng khóa sau khi lưu (D-63 i)'; }
            ct.onchange = () => { collect(); cusType = customerType(ct.value); rec.CustomerType = cusType; redraw(); };
          }
        };
        // UI-5.4: chi nhánh đúng 1 → chọn sẵn khi tạo mới.
        const applyOptions = o => {
          cusOpt = o || cusOpt;
          if (!editing && !rec.BranchId && (cusOpt.listBranch || []).length === 1) rec.BranchId = cusOpt.listBranch[0].code;
        };
        if (isCustomer) applyOptions(cusOpt);
        wire();
        if (isLocation) {
          // Đổi khách → vẽ lại ô Người nhận; chọn người nhận → điền SĐT theo người liên hệ.
          const redrawRecipient = () => { const el = m.querySelector('#f_ContactName'), fld = el && el.closest('.field'); if (!fld) return; rec.ContactName = el.value || null; const meta = fields.find(f => f.name === 'ContactName'); if (!meta) return; const tmp = document.createElement('div'); tmp.innerHTML = fieldHtml(meta); fld.replaceWith(tmp.firstElementChild); };
          m.addEventListener('change', e => {
            if (e.target.id === 'f_CustomerId') { rec.CustomerId = e.target.value; redrawRecipient(); }
            if (e.target.id === 'f_ContactName' && e.target.dataset.recipient) { const o = e.target.selectedOptions[0], ph = m.querySelector('#f_ContactPhone'); if (o && ph && o.dataset.phone) ph.value = o.dataset.phone; }
          });
          Store.ensure('CustomerContact').then(() => { if (host.isConnected) redrawRecipient(); });
        }
        // Quy tắc mã / dữ liệu form tải xong sau khi mở form → CHỈ thay các ô phụ thuộc dữ liệu đó (ô mã, ô chọn danh mục / chi nhánh / NV phụ trách).
        // Không vẽ lại cả form: người dùng có thể đang mở hộp chọn ngày / đang gõ – thay ô sẽ làm mất giá trị vừa chọn (lỗi UAT 2026-11-21).
        const OPTION_FIELDS = new Set(['BranchId', 'UserAssignId', 'Industry', 'CompanySize', 'Gender', 'Source']);
        const softRefresh = () => {
          if (!host.isConnected || (isCustomer && !typeChosen)) return;
          fields.filter(f => ruleOf(f) || (isCustomer && OPTION_FIELDS.has(f.name)) || Store.isRefCol(t, f.name) || (f.name.endsWith('Id') && !!Store.target(t, f.name))).forEach(f => {
            const el = m.querySelector('#f_' + f.name), fld = el && el.closest('.field');
            if (!fld || fld.contains(document.activeElement)) return;
            if (!el.dataset.g1) rec[f.name] = el.value === '' ? null : el.value;
            const tmp = document.createElement('div'); tmp.innerHTML = fieldHtml(f);
            fld.replaceWith(tmp.firstElementChild);
          });
          wire();
        };
        // Lỗi UAT 2026-11-21: tải sẵn bảng đích của các ô tham chiếu (…Id) – ô chọn vẽ khi bảng chưa tải sẽ trống, tải xong vẽ lại ô đó.
        const fkTables = [...new Set(fields.filter(f => f.name.endsWith('Id') && Store.target(t, f.name)).map(f => Store.target(t, f.name)))];
        if (Store.isLive) Promise.all([Store.loadCodeRules(), Store.loadRefLists(), Promise.all(fkTables.map(x => Store.ensure(x))), isCustomer ? UI.customerOptions().then(applyOptions) : null]).then(softRefresh);
        // Lỗi máy chủ: hiện đầu form + tô đỏ ô có nhãn nằm trong thông báo (nhãn dài khớp trước, tránh "Email" ăn vào "Email người liên hệ").
        const markServer = msg => {
          let rest = String(msg || '');
          fields.map(f => ({ f, l: labelOf(f).replace(/\s*\(.*\)$/, '') })).sort((a, b) => b.l.length - a.l.length).forEach(({ f, l }) => {
            if (l && rest.includes(l)) { rest = rest.split(l).join(' '); setErr(m.querySelector('#f_' + f.name), ' '); }
          });
          if (/^Điểm giao đầu tiên/.test(String(msg || ''))) FIRST_LOC.forEach(([k]) => { const el = m.querySelector('#fl_' + k); if (el && (!el.value || /giờ/.test(msg))) setErr(el, ' '); });
        };
        btn.onclick = () => {
          if (isCustomer && !typeChosen) return;
          let ok = true; const out = {};
          const box = m.querySelector('#frmErr'); box && box.classList.add('hide');
          fields.forEach(f => {
            const el = m.querySelector('#f_' + f.name); if (!el) return;
            setErr(el, '');
            if (el.dataset.g1) return;   // G1: máy chủ sinh – không gửi
            let v = el.value;
            if (v === '') v = null;
            else if (f.type === 'bit') v = +v;
            else if (/decimal|int|bigint/.test(f.type)) v = Number(v);
            if (isReq(f) && v === null) { ok = false; setErr(el, 'Bắt buộc'); }
            else if (isCustomer && checkField(f.name)) ok = false;
            out[f.name] = v;
          });
          if (isCustomer) out.CustomerType = cusType;
          if (isCustomer) { const xl = m.querySelector('#f_xLoss'); if (xl) { const n = xl.value === '' ? null : Number(xl.value); if (n !== null && !(n >= 0 && n <= 100)) { ok = false; setErr(xl, 'Từ 0 đến 100'); } else out.ExternalData = JSON.stringify(Object.assign(extOf(), { containerLossMaxPct: n })); } }
          if (isCustomer && !checkFirstLoc(true)) ok = false;
          const v2 = opt.validate && opt.validate(out); if (v2) { ok = false; showErr(v2); }
          if (!ok) { const bad = m.querySelector('.field.invalid'); bad && bad.scrollIntoView({ block: 'nearest' }); return; }
          // opt.submit: màn hình tự lưu qua action nghiệp vụ (vd. LeaveRequest/Create) thay cho Insert / Update chung.
          if (opt.submit) return opt.submit(out, close);
          // MA-1.8: ghi CHỜ API – đóng form + báo thành công chỉ khi máy chủ xác nhận; lỗi → giữ form, hiện lỗi tại chỗ.
          const payload = editing ? Object.assign({}, row, out) : Object.assign({}, opt.defaults || {}, out);
          if (isCustomer && !editing && firstLoc.open) FIRST_LOC.forEach(([k]) => { const el = m.querySelector('#fl_' + k); if (el && el.value.trim()) payload['FirstLocation.' + k] = el.value.trim(); });
          btn.disabled = true; const old = btn.innerHTML; btn.innerHTML = 'Đang lưu…';
          Store.save(t, editing ? 'Update' : 'Insert', payload).then(r => {
            btn.disabled = false; btn.innerHTML = old;
            if (!r.ok) { showErr(r.message || 'Không lưu được'); markServer(r.message); return; }
            close();
            const saved = r.row || payload;
            // UI-5.8: khách mới có liên hệ chính / điểm giao do máy chủ tạo → tải lại 2 bảng con.
            if (isCustomer && !editing && Store.isLive) Store.reload(['CustomerContact', 'CustomerDeliveryLocation']);
            UI.toast(esc(r.message || ((editing ? 'Đã cập nhật ' : 'Đã tạo ') + (Store.label(t, saved) || ''))) + (r.demo ? ` <small>(dbo.${t} · DEMO)</small>` : ''), 'ok');
            opt.onSave && opt.onSave(saved);
          });
        };
      }
    });
  };
  // ---------- UI-2R (D-69 b): khối thông tin rút gọn – thành phần chung của mọi màn chi tiết ----------
  // UI.infoPanel(cfg) → HTML; UI.wireInfoPanel(root, cfg) gắn sự kiện (gập / mở, nhớ nhóm mở, sao chép, Sửa, Bổ sung).
  // cfg = { key: 'customer' (nhớ nhóm mở theo loại màn), title, canEdit, onEdit(),
  //         core: [{ icon, html }]                     – dòng cốt lõi luôn hiện, html rỗng → ẩn,
  //         groups: [{ key, title, summary, rows: [[nhãn, giá trị html]], html, locked, open }] – nhóm gập; dòng giá trị rỗng → ẩn; nhóm rỗng → "Chưa có thông tin · Bổ sung" }
  const infoOpenKey = key => 'dc-info-open:' + key;
  const infoOpenGet = (key, def) => { try { const v = localStorage.getItem(infoOpenKey(key)); return v ? JSON.parse(v) : def; } catch (e) { return def; } };
  const infoOpenSet = (key, list) => { try { localStorage.setItem(infoOpenKey(key), JSON.stringify(list)); } catch (e) { /* trình duyệt chặn lưu – bỏ qua */ } };
  const filled = v => v !== null && v !== undefined && String(v).replace(/<[^>]*>/g, '').trim() !== '';
  // Điện thoại / email bấm được; sao chép.
  UI.tel = (phone, note) => (filled(phone) ? `<a href="tel:${esc(String(phone).replace(/[^\d+]/g, ''))}">${esc(phone)}</a>${note ? ` <span class="ip-muted">(${esc(note)})</span>` : ''}` : '');
  UI.mail = email => (filled(email) ? `<a href="mailto:${esc(email)}">${esc(email)}</a>` : '');
  UI.copyBtn = text => (filled(text) ? `<button type="button" class="ip-copy" data-copy="${esc(text)}" title="Sao chép" aria-label="Sao chép ${esc(text)}">${I('copy')}</button>` : '');
  UI.infoPanel = cfg => {
    const open = infoOpenGet(cfg.key, (cfg.groups || []).filter(g => g.open).map(g => g.key));
    const core = (cfg.core || []).filter(c => filled(c.html));
    const group = g => {
      const rows = (g.rows || []).filter(r => filled(r[1]));
      const body = rows.map(([l, v]) => `<div class="ip-r"><span>${esc(l)}</span><span>${v}</span></div>`).join('') + (filled(g.html) ? `<div class="ip-text">${g.html}</div>` : '');
      if (!body) {
        return `<div class="ip-sec ip-empty"><div class="ip-sh" aria-disabled="true"><span>${esc(g.title)}<small>Chưa có thông tin${cfg.canEdit ? ' · <a href="javascript:void 0" data-ip-edit>Bổ sung</a>' : ''}</small></span></div></div>`;
      }
      const isOpen = open.includes(g.key);
      return `<div class="ip-sec ${isOpen ? 'open' : ''}" data-ipk="${esc(g.key)}"><button type="button" class="ip-sh" aria-expanded="${isOpen}" aria-controls="ip-${esc(cfg.key)}-${esc(g.key)}">
        <span>${g.locked ? `<span class="ip-lock" title="Chỉ người có quyền xem">${I('lock')}</span>` : ''}${esc(g.title)}${filled(g.summary) ? `<small>${esc(g.summary)}</small>` : ''}</span>${I('chevron', 'ip-ch')}</button>
        <div class="ip-sb" id="ip-${esc(cfg.key)}-${esc(g.key)}">${body}</div></div>`;
    };
    return `<div class="card panel ip" data-ip="${esc(cfg.key)}"><div class="ip-h"><h3>${esc(cfg.title)}</h3>${cfg.canEdit ? `<button class="btn xs" data-ip-edit>${I('edit')} Sửa</button>` : ''}</div>
      ${core.length ? `<div class="ip-core">${core.map(c => `<div class="ip-row">${I(c.icon || 'clipboard')}<span>${c.html}</span></div>`).join('')}</div>` : ''}
      ${(cfg.groups || []).map(group).join('')}</div>`;
  };
  UI.wireInfoPanel = (root, cfg) => {
    const panel = root.querySelector(`[data-ip="${cfg.key}"]`); if (!panel) return;
    panel.querySelectorAll('.ip-sec[data-ipk] > .ip-sh').forEach(btn => btn.onclick = () => {
      const sec = btn.parentElement, now = !sec.classList.contains('open');
      sec.classList.toggle('open', now); btn.setAttribute('aria-expanded', String(now));
      infoOpenSet(cfg.key, [...panel.querySelectorAll('.ip-sec.open[data-ipk]')].map(s => s.dataset.ipk));
    });
    panel.querySelectorAll('[data-ip-edit]').forEach(b => b.onclick = e => { e.preventDefault(); cfg.onEdit && cfg.onEdit(); });
    panel.querySelectorAll('[data-copy]').forEach(b => b.onclick = e => {
      e.stopPropagation();
      const done = () => { b.classList.add('done'); b.innerHTML = I('check'); UI.toast('Đã sao chép ' + esc(b.dataset.copy), 'ok'); };
      try { navigator.clipboard.writeText(b.dataset.copy).then(done, () => UI.toast('Trình duyệt không cho sao chép', 'warn')); } catch (x) { UI.toast('Trình duyệt không cho sao chép', 'warn'); }
    });
  };
  // ---------- FE-E.2 (D-65): mã QR do máy chủ vẽ (Common/GetQrBatch – QRCoder) ----------
  // UI.qrSlot(text) → ô chờ; UI.fillQr(root) gom mọi ô chưa vẽ trong root, gọi 1 lần API rồi chèn SVG. Chế độ DEMO: hiện link chữ.
  UI.qrSlot = (text, size) => `<div class="qr-box" data-qr="${esc(text)}" style="width:${size || 160}px;height:${size || 160}px"><span class="muted small">QR…</span></div>`;
  UI.fillQr = root => {
    const slots = [...(root || document).querySelectorAll('[data-qr]:not([data-qr-done])')];
    if (!slots.length) return Promise.resolve();
    slots.forEach(s => s.dataset.qrDone = '1');
    if (!Store.isLive) { slots.forEach(s => { s.innerHTML = `<span class="small mono" style="word-break:break-all">${esc(s.dataset.qr)}</span>`; }); return Promise.resolve(); }
    const texts = [...new Set(slots.map(s => s.dataset.qr))];
    return Store.act('Common/GetQrBatch', { json: { listText: texts }, quiet: true }).then(r => {
      const list = (r.ok && r.res && (r.res.listData || r.res.ListData)) || [];
      const svgOf = {}; list.forEach(x => { if (x.svg) svgOf[x.text] = x.svg; });
      slots.forEach(s => { s.innerHTML = svgOf[s.dataset.qr] || '<span class="muted small">Không vẽ được mã QR</span>'; });
    });
  };
  // In QR hiện trường (Common/GetFieldQrList): mở cửa sổ in, mỗi QR 1 thẻ (tên, mã, link), khổ A4 nhiều thẻ / trang.
  UI.printFieldQr = (group, title) => {
    if (!Store.isLive) { UI.toast('Chế độ DEMO không in được QR – chuyển sang dữ liệu API.', 'warn'); return; }
    const w = window.open('', '_blank');   // mở ngay khi bấm (tránh bị chặn cửa sổ bật lên)
    if (!w) { UI.toast('Trình duyệt chặn cửa sổ in – cho phép cửa sổ bật lên rồi thử lại.', 'err'); return; }
    w.document.write('<p style="font-family:sans-serif">Đang tạo mã QR…</p>');
    Store.query('Common/GetFieldQrList', { group: group || '', withSvg: true }).then(x => {
      if (!x.ok) { w.close(); UI.toast(esc(x.message || 'Không lấy được danh sách QR'), 'err'); return; }
      const list = x.list || [];
      if (!list.length) { w.close(); UI.toast('Không có mã QR nào để in (chưa có dữ liệu hoặc không có quyền xem).', 'warn'); return; }
      const card = q => `<div class="c"><div class="q">${q.svg || ''}</div><b>${esc(q.label)}</b><div class="g">${esc(q.groupName)}${q.code ? ' · ' + esc(q.code) : ''}</div><div class="l">${esc(q.link)}</div></div>`;
      w.document.open();
      w.document.write(`<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>${esc(title || 'Mã QR hiện trường')}</title><style>
        body{font-family:"Be Vietnam Pro",Arial,sans-serif;margin:12mm;color:#263238}h1{font-size:16px;margin:0 0 8mm}
        .grid{display:grid;grid-template-columns:repeat(3,1fr);gap:6mm}.c{border:1px dashed #9AA39A;border-radius:6px;padding:5mm;text-align:center;break-inside:avoid}
        .q svg{width:42mm;height:42mm}.c b{display:block;font-size:13px;margin-top:2mm}.g{font-size:11px;color:#5F6B66}.l{font-size:8px;color:#7B857F;word-break:break-all;margin-top:1mm}
        @media print{body{margin:8mm}h1{display:none}}</style></head><body><h1>${esc(title || 'Mã QR hiện trường')} – ${list.length} mã · bấm Ctrl+P để in</h1><div class="grid">${list.map(card).join('')}</div></body></html>`);
      w.document.close();
      setTimeout(() => { try { w.focus(); w.print(); } catch (e) { /* người dùng tự in */ } }, 300);
    });
  };
  // ---------- Truy vết DB: Module → Function → Screen → Table → Column ----------
  UI.traceHtml = (t, screenId) => {
    const T = Store.schema.tables[t]; if (!T) return '<div class="notice gap">Không có bảng vật lý tương ứng (GAP).</div>';
    const fns = Store.cat.funcs.filter(f => f.table === t || (f.rel || []).includes(t));
    const f = Store.cat.funcs.find(x => x.id === screenId) || fns.find(x => x.table === t) || fns[0];
    const out = Store.schema.rels.filter(r => r.src === t), inn = Store.relIn[t] || [], un = Store.schema.unres.filter(u => u.src === t);
    return `<div class="trace"><div class="chain">${f ? `<span>${f.ph}</span><i>→</i><span>${esc(f.grp)}</span><i>→</i><span>${esc(f.fn)}</span><i>→</i><span>#/${f.id}</span><i>→</i>` : ''}<span class="mono">dbo.${t}</span><i>→</i><span>${T.cols.length} cột</span></div>
      <div class="kv mb"><div><label>Nhóm phân loại</label><span>${esc(T.grp)}</span></div><div><label>Vai trò dữ liệu</label><span>${esc(T.role)}</span></div><div style="grid-column:1/-1"><label>Ý nghĩa bảng</label><span>${esc(T.desc)}</span><small class="muted">Nguồn: ${esc(T.dsrc)}</small></div><div><label>Primary Key</label><span class="mono">${T.pk}</span></div><div><label>UNIQUE</label><span class="mono">${esc(T.uniq || '–')}</span></div></div>
      <h3>Cột (${T.cols.length})</h3><div class="table-wrap" style="max-height:340px"><table><thead><tr><th>#</th><th>Cột</th><th>Kiểu</th><th>NULL</th><th>DEFAULT</th><th>Cách dùng</th></tr></thead><tbody>${T.cols.map((c, i) => `<tr style="${c[4] === 'Deprecated' ? 'opacity:.5' : ''}"><td>${i + 1}</td><td class="mono">${c[0]}${T.pk === c[0] ? ' <span class="tag green">PK</span>' : ''}</td><td class="mono">${c[1]}</td><td>${c[2] ? 'NULL' : '<b>NOT NULL</b>'}</td><td class="mono">${esc(c[3] || '')}</td><td>${esc(c[4])}${c[5] ? `<div class="muted small">${esc(c[5])}</div>` : ''}</td></tr>`).join('')}</tbody></table></div>
      <h3 class="mt">Quan hệ logic đi ra (${out.length}) <span class="muted small">– không phải FK vật lý</span></h3>${out.length ? `<table><tbody>${out.map(r => `<tr><td class="mono">${r.col}</td><td>→ <a href="#/dbx-explorer?t=${r.dst}" class="mono">${r.dst}</a>.${r.dcol}</td><td>${UI.tag(r.level, r.level.startsWith('PO') ? 'green' : 'orange')}</td></tr>`).join('')}</tbody></table>` : '<div class="muted small">Không có</div>'}
      <h3 class="mt">Được tham chiếu bởi (${inn.length})</h3>${inn.length ? `<div class="row-flex">${inn.map(r => `<a class="tag" href="#/dbx-explorer?t=${r.src}">${r.src}.${r.col}</a>`).join('')}</div>` : '<div class="muted small">Không có</div>'}
      ${un.length ? `<h3 class="mt">Tham chiếu chưa xác định (${un.length})</h3><table><tbody>${un.map(u => `<tr><td class="mono">${u.col}</td><td>${esc(u.reason)}</td><td class="muted">${esc(u.proposal || '')}</td></tr>`).join('')}</tbody></table>` : ''}
      <h3 class="mt">Màn hình sử dụng bảng</h3><div class="row-flex">${fns.map(x => `<a class="tag ${x.table === t ? 'green' : ''}" href="#/${x.id}">${x.ph} · ${esc(x.name)}</a>`).join('') || '<span class="muted small">Chỉ hiển thị trong Database Explorer</span>'}</div></div>`;
  };
  UI.trace = (t, screenId) => UI.modal({ title: 'Truy vết Database – dbo.' + t, sub: 'Module → Function → Screen → Table → Column', size: 'lg', body: UI.traceHtml(t, screenId) });
  // ---------- Drawer chi tiết bản ghi ----------
  UI.history = (t, id, pane) => {
    // Chế độ API: Common/GetHistory gộp AuditChange + quyết định duyệt + lịch sử phản ánh + tệp đính kèm (đã che dữ liệu nhạy cảm).
    // Vẽ ngay bản cục bộ, có kết quả server thì thay; bảng backend không hỗ trợ thì giữ bản cục bộ.
    // Chỉ gọi cho danh sách backend có đăng ký (Common/GetResources) – bảng khác trả 400 "không hỗ trợ chức năng này".
    if (pane && Store.isLive) (window.APP && APP.resources ? APP.resources() : Promise.resolve(null)).then(set => (set && set.size && !set.has(Store.controllerOf(t).toLowerCase())) ? { ok: false } : Store.query('Common/GetHistory', { resource: Store.controllerOf(t), id })).then(x => {
      if (!x.ok || !pane.isConnected) return;
      pane.innerHTML = x.list.length ? `<div class="timeline">${x.list.map(h => `<div class="tl ${/DELETE|REJECT/i.test(h.action || '') ? 'red' : /INSERT|CREATE/i.test(h.action || '') ? '' : 'orange'}"><b>${esc(h.actionName || h.action || '')}${h.referenceNo ? ' · ' + esc(h.referenceNo) : ''}</b><small>${esc(U.dt(String(h.at || '').replace(/\.\d+$/, '')))} · ${esc(h.byName || '')}${h.comment ? ' · ' + esc(h.comment) : ''} · ${esc(h.source || '')}</small></div>`).join('')}</div>` : '<div class="empty">Chưa có lịch sử</div>';
    });
    const a = Store.rows('AuditChange').filter(x => x.IdChange === id && (x.EntityName === t));
    const extra = [];
    if (t === 'CustomerComplaint') Store.by('ComplaintHistory', 'ComplaintId', id).forEach(h => extra.push({ at: h.EventAt, b: (h.OldStatus ? h.OldStatus + ' → ' : '') + (h.NewStatus || h.EventType), s: h.Comment + ' · dbo.ComplaintHistory' }));
    const appr = Store.by('ApprovalRequest', 'BusinessId', id);
    appr.forEach(ar => { extra.push({ at: ar.RequestedAt, b: 'Gửi phê duyệt ' + ar.RequestNo, s: (Store.isLive ? '' : 'dbo.ApprovalRequest · ') + (Store.isLive ? UI.statusVi('ApprovalRequest', 'RequestStatus', ar.RequestStatus) || ar.RequestStatus : ar.RequestStatus) }); Store.by('ApprovalDecision', 'ApprovalRequestId', ar.Id).forEach(dc => extra.push({ at: dc.DecidedAt, b: dc.DecisionCode + ' – ' + (dc.Comment || ''), s: (Store.isLive ? '' : 'dbo.ApprovalDecision · ') + Store.label('User', dc.DecidedBy) })); });
    const row = Store.get(t, id);
    const items = a.map(x => { let s = ''; try { s = JSON.parse(x.Changes).summary; } catch (e) { s = x.Changes; } return { at: x.CreatedDate, b: x.Action, s: s + (Store.isLive ? '' : ' · dbo.AuditChange'), c: x.Action === 'SOFT_DELETE' ? 'red' : x.Action === 'INSERT' ? '' : 'orange' }; }).concat(extra);
    if (row && row.CreatedDate) items.push({ at: row.CreatedDate, b: 'Tạo bản ghi', s: 'CreatedDate / CreatedBy', c: 'blue' });
    items.sort((x, y) => (y.at || '').localeCompare(x.at || ''));
    return items.length ? `<div class="timeline">${items.map(x => `<div class="tl ${x.c || ''}"><b>${esc(x.b)}</b><small>${esc(U.dt(x.at))} · ${esc(x.s)}</small></div>`).join('')}</div>` : '<div class="empty">Chưa có lịch sử</div>';
  };
  UI.kvHtml = (t, row, only) => `<div class="kv">${(only ? only.map(c => Store.colmeta[t][c]).filter(Boolean) : Object.values(Store.colmeta[t])).filter(m => !Store.isDeprecated(t, m.name) && !UI.hidden(t, m.name) && m.usage !== 'Ẩn (bảo mật)' && (only || !['ModifiedBy', 'DeletedBy', 'DeleteddBy', 'DeletedDate'].includes(m.name))).map(m => `<div ${/max\)$/.test(m.type) && /Note|Description|Instructions|Json|Changes/.test(m.name) ? 'style="grid-column:1/-1"' : ''}><label>${esc(U.colLabel(m.name))}<code>${m.name}</code></label><span>${UI.val(t, m.name, row[m.name], row)}</span></div>`).join('')}</div>`;
  UI.children = (t, id) => {
    const inn = (Store.relIn[t] || []).filter(r => Store.data[r.src] && Store.data[r.src].some(x => x[r.col] === id));
    if (!inn.length) return '<div class="empty">Không có bản ghi liên quan</div>';
    return inn.map(r => { const rows = Store.by(r.src, r.col, id); return `<div class="mb"><div class="spread"><h3 style="margin:0">${r.src} <span class="muted small">qua ${r.col} · ${rows.length} dòng</span></h3></div>${rows.slice(0, 8).map(x => `<div class="spread" style="padding:6px 0;border-bottom:1px solid #F0F2ED"><a href="javascript:void 0" data-open="${r.src}" data-id="${x.Id}">${esc(Store.label(r.src, x))}</a>${Store.statusCol(r.src) ? UI.badge(x[Store.statusCol(r.src)], r.src, Store.statusCol(r.src)) : ''}</div>`).join('')}${rows.length > 8 ? `<div class="muted small">… và ${rows.length - 8} dòng khác</div>` : ''}</div>`; }).join('');
  };
  // Sổ chỉ ghi thêm (CLAUDE.md 3.3): dữ liệu thật không Sửa / Xóa – muốn sửa thì ghi bút toán đảo / điều chỉnh.
  UI.APPEND_ONLY = new Set(['InventoryTransaction', 'PackagingLedger', 'TemperatureLog', 'AuditChange', 'IntegrationLog', 'ApprovalDecision', 'AttendanceEvent', 'DeliveryEvent', 'ComplaintHistory',
    // Đối soát: máy chủ chặn thêm / sửa / xóa thẳng – chỉ ghi qua Ghi suất phục vụ / Ghi lại / Chốt (OrderReconciliation/Reconcile / Close).
    'OrderReconciliation']);
  // MENU-4 nhóm C (D-75 e): dữ liệu sinh từ thao tác nghiệp vụ – chỉ xem, không Thêm / Sửa tay: Sản lượng (ghi khi kết thúc mẻ nấu),
  // Xếp hàng lên xe (DeliveryRoute/Load), Xác nhận đã giao (màn Theo dõi chuyến / bot), Liên kết tài khoản chat (mã liên kết D-62 + nút Liên kết / Gỡ ở modules/ghep.js).
  UI.VIEW_ONLY = new Set(['ProductionOutput', 'DeliveryLoading', 'ProofOfDeliverie', 'UserExternalIdentity']);
  UI.viewOnly = t => Store.isLive && UI.VIEW_ONLY.has(t);
  UI.readOnly = t => Store.isLive && (UI.APPEND_ONLY.has(t) || UI.VIEW_ONLY.has(t));
  UI.detail = (t, id, opt = {}) => {
    const row = Store.get(t, id); if (!row) { UI.toast('Không tìm thấy bản ghi', 'err'); return; }
    const sc = Store.statusCol(t);
    const perm = opt.screen ? Store.perm(opt.screen) : { edit: true };
    // UI.liveTabs / UI.related (modules/be3.js): tab theo API BE-3 ở chế độ dữ liệu thật; không có thì dùng cách cũ.
    const tabs = [{ label: 'Thông tin', html: (opt.top || '') + UI.kvHtml(t, row) }].concat(opt.tabs || []).concat(UI.liveTabs ? UI.liveTabs(t, id) : []).concat([
      { label: 'Liên quan', html: p => (UI.related ? UI.related(t, id, p) : UI.children(t, id)) },
      { label: 'Lịch sử xử lý', html: p => UI.history(t, id, p) },
      { label: 'Truy vết DB', html: () => UI.traceHtml(t, opt.screen) }]).filter(x => !(Store.isLive && x.label === 'Truy vết DB'));
    UI.drawer({
      title: esc(Store.label(t, row)), sub: Store.isLive ? esc((opt.screen && window.APP && APP.FN[opt.screen] && APP.FN[opt.screen].name) || '') : 'dbo.' + t + (opt.screen ? ' · #/' + opt.screen : ''), badge: sc ? UI.badge(row[sc], t, sc) : '', tabs,
      foot: (opt.foot ? opt.foot(row) : '') + (UI.liveFoot ? UI.liveFoot(t, row, perm) : '') + (opt.noEdit || UI.readOnly(t) ? '' : `<button class="btn" data-act="edit" ${perm.edit === false ? 'disabled title="Tài khoản không có quyền Sửa"' : ''}>${I('edit')} Chỉnh sửa</button>`) + '<button class="btn primary" data-close>Đóng</button>',
      onMount: (d, close) => { d.addEventListener('click', e => { const b = e.target.closest('[data-act]'); if (!b) return; if (b.dataset.act === 'edit') { close(); UI.form(t, row, { onSave: () => { opt.onChange && opt.onChange(); APP.refresh(); } }); } else if (UI.liveAct && UI.liveAct(b.dataset.act, row, close, t)) { /* action nghiệp vụ ghép thêm (modules/ghep.js) */ } else if (opt.onAct) opt.onAct(b.dataset.act, row, close, b); }); }
    });
  };
  // mở chi tiết khi click link tham chiếu
  document.addEventListener('click', e => { const a = e.target.closest('[data-open]'); if (a) { e.preventDefault(); if (window.APP && APP.openDetail) APP.openDetail(a.dataset.open, a.dataset.id, APP.FN[APP.current]); else UI.detail(a.dataset.open, a.dataset.id); } });
  // ---------- Biểu đồ SVG (không cần CDN) ----------
  UI.bars = (labels, series, opt = {}) => {
    const W = 640, H = opt.h || 210, pl = 42, pb = 26, pt = 10, n = labels.length, max = Math.max(1, ...series.flatMap(s => s.values)) * 1.1;
    const gw = (W - pl - 10) / n, bw = Math.min(18, gw / (series.length + 1.2));
    let g = ''; for (let i = 0; i <= 4; i++) { const y = pt + (H - pt - pb) * i / 4; g += `<line class="grid-line" x1="${pl}" x2="${W - 5}" y1="${y}" y2="${y}"/><text x="${pl - 6}" y="${y + 3}" text-anchor="end">${U.num(max * (1 - i / 4), 0)}</text>`; }
    labels.forEach((l, i) => {
      const x0 = pl + gw * i + (gw - bw * series.length - 2 * (series.length - 1)) / 2;
      series.forEach((s, j) => { const v = s.values[i] || 0, h = (H - pt - pb) * v / max; g += `<rect x="${x0 + j * (bw + 2)}" y="${H - pb - h}" width="${bw}" height="${Math.max(h, 1)}" rx="3" fill="${s.color}"><title>${esc(l)} · ${esc(s.name)}: ${U.num(v)}</title></rect>`; });
      g += `<text x="${pl + gw * i + gw / 2}" y="${H - 8}" text-anchor="middle">${esc(l)}</text>`;
    });
    return `<svg class="chart" viewBox="0 0 ${W} ${H}" role="img" aria-label="${esc(opt.title || 'Biểu đồ')}">${g}</svg>`;
  };
  UI.lines = (labels, series, opt = {}) => {
    const W = 640, H = opt.h || 210, pl = 42, pb = 26, pt = 10, n = labels.length;
    const all = series.flatMap(s => s.values).concat(opt.bands ? opt.bands.flat() : []);
    const mn = opt.min !== undefined ? opt.min : Math.min(...all), mx = opt.max !== undefined ? opt.max : Math.max(...all), span = (mx - mn) || 1;
    const X = i => pl + (W - pl - 10) * i / Math.max(1, n - 1), Y = v => pt + (H - pt - pb) * (1 - (v - mn) / span);
    let g = ''; for (let i = 0; i <= 4; i++) { const v = mx - span * i / 4, y = Y(v); g += `<line class="grid-line" x1="${pl}" x2="${W - 5}" y1="${y}" y2="${y}"/><text x="${pl - 6}" y="${y + 3}" text-anchor="end">${U.num(v, 1)}</text>`; }
    (opt.bands || []).forEach(([a, b]) => g += `<rect x="${pl}" width="${W - pl - 10}" y="${Y(b)}" height="${Math.abs(Y(a) - Y(b))}" fill="#91AF42" opacity=".08"/>`);
    (opt.refs || []).forEach(r => g += `<line x1="${pl}" x2="${W - 5}" y1="${Y(r.v)}" y2="${Y(r.v)}" stroke="${r.color}" stroke-dasharray="5 4"/><text x="${W - 8}" y="${Y(r.v) - 4}" text-anchor="end" style="fill:${r.color}">${esc(r.label)}</text>`);
    series.forEach(s => { g += `<polyline fill="none" stroke="${s.color}" stroke-width="2" points="${s.values.map((v, i) => X(i) + ',' + Y(v)).join(' ')}"/>`; s.values.forEach((v, i) => { const bad = opt.badFn && opt.badFn(v); if (bad || n < 32) g += `<circle cx="${X(i)}" cy="${Y(v)}" r="${bad ? 4 : 2.2}" fill="${bad ? '#D95C4F' : s.color}"><title>${esc(labels[i])}: ${U.num(v, 2)}</title></circle>`; }); });
    const step = Math.ceil(n / 12); labels.forEach((l, i) => { if (i % step === 0) g += `<text x="${X(i)}" y="${H - 8}" text-anchor="middle">${esc(l)}</text>`; });
    return `<svg class="chart" viewBox="0 0 ${W} ${H}" role="img">${g}</svg>`;
  };
  UI.donut = (parts, opt = {}) => {
    const tot = parts.reduce((a, p) => a + p.v, 0) || 1; let a0 = -Math.PI / 2, g = '';
    parts.forEach(p => { const a1 = a0 + 2 * Math.PI * p.v / tot, large = a1 - a0 > Math.PI ? 1 : 0, r = 54, R = 70, c = 80; const P = (a, rr) => (c + rr * Math.cos(a)) + ',' + (c + rr * Math.sin(a)); if (p.v > 0) g += `<path d="M${P(a0, R)} A${R},${R} 0 ${large} 1 ${P(a1, R)} L${P(a1, r)} A${r},${r} 0 ${large} 0 ${P(a0, r)}Z" fill="${p.c}"><title>${esc(p.l)}: ${p.v}</title></path>`; a0 = a1; });
    return `<div class="row-flex" style="gap:16px"><svg viewBox="0 0 160 160" width="${opt.size || 130}" height="${opt.size || 130}">${g}<text x="80" y="78" text-anchor="middle" style="font-size:20px;font-weight:800;fill:#29352D">${U.num(tot)}</text><text x="80" y="96" text-anchor="middle" style="font-size:9px;fill:#98A198">${esc(opt.label || 'tổng')}</text></svg><div>${parts.map(p => `<div class="small" style="margin:4px 0"><i class="legend-dot" style="background:${p.c}"></i>${esc(p.l)} <b>${U.num(p.v)}</b></div>`).join('')}</div></div>`;
  };
  // UI-1 (D-64 c): thẻ chỉ số – ô biểu tượng bên trái; nhãn, số chính + số phụ bên phải; không còn dòng chú thích chân.
  // Giữ chữ ký cũ: tham số thứ 3 (trước là chú thích chân) nay là SỐ PHỤ ngắn hiện cùng dòng sau số chính (vd "/ 2 · 100%").
  // value rỗng / null / undefined → "–" (đang tải). color: green | lime | blue | orange | red | purple | gray.
  // UI-1R (D-64 c): số phụ ngắn (≤ 20 ký tự, vd. "/ 2 · 100%", "96 đơn"); câu dài / chữ kỹ thuật (tên bảng, cột, mã tiếng Anh) → chú thích khi rê chuột.
  const KPI_TECH = /\b[A-Z][a-z]+[A-Z][A-Za-z]*\b|\bdbo\.|Σ|=\s*[A-Z_]{3,}|\b[A-Z]{2,}_[A-Z_]+\b/;
  UI.kpi = (label, value, sub, icon, color, link) => {
    const v = value === null || value === undefined || value === '' ? '–' : value;
    const plain = String(sub || '').replace(/<[^>]*>/g, '').replace(/&[a-z]+;/g, ' ').trim();
    const long = plain.length > 20 || KPI_TECH.test(plain);
    const tip = String(label).replace(/<[^>]*>/g, '') + (plain && long && !KPI_TECH.test(plain) ? ' – ' + plain : '');
    if (long) sub = '';
    return `<article class="card kpi ${link ? 'link' : ''}" title="${esc(tip)}" ${link ? `data-go="${link}" role="link" tabindex="0"` : ''}><span class="kpi-icon ${color || 'green'}">${I(icon || 'chart')}</span><div class="kpi-body"><div class="kpi-label">${label}</div><div class="kpi-line"><span class="kpi-value">${v}</span>${sub ? `<span class="kpi-sub">${sub}</span>` : ''}</div></div></article>`;
  };
  UI.gapNote = f => f && f.st !== 'MAPPED' && f.note ? `<div class="notice ${f.st === 'GAP' ? 'gap' : 'warn'}">${I('alert')}<div><b>${f.st === 'GAP' ? 'DATABASE GAP – chưa có bảng hỗ trợ' : 'PARTIAL – có bảng nhưng thiếu cột/quan hệ'} · BA/PO CONFIRM</b>${esc(f.note)}</div></div>` : (f && f.note ? `<div class="notice info">${I('database')}<div>${esc(f.note)}</div></div>` : '');
  UI.stepper = (steps, cur, failed) => `<div class="stepper">${steps.map((s, i) => `<div class="step ${i < cur ? 'done' : i === cur ? (failed ? 'fail' : 'current') : ''}"><div class="dot">${i < cur ? '✓' : i + 1}</div><div class="lbl">${s.l || s}</div>${s.s ? `<small>${s.s}</small>` : ''}</div>`).join('')}</div>`;
  window.UI = UI;
})();
