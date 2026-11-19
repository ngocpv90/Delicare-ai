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
  UI.modal = ({ title, sub, body, foot, size, onMount }) => {
    const ov = document.createElement('div'); ov.className = 'modal overlay show'; ov.style.display = 'block'; ov.tabIndex = -1;
    ov.innerHTML = `<div class="modal-dialog modal-dialog-centered modal-dialog-scrollable ${size === 'lg' ? 'modal-xl' : size === 'sm' ? '' : 'modal-lg'}" role="dialog" aria-modal="true"><div class="modal-content"><div class="modal-header"><div style="flex:1;min-width:0"><h5 class="modal-title">${title}</h5>${sub ? `<div class="panel-desc">${sub}</div>` : ''}</div><button type="button" class="btn-close" data-close aria-label="Đóng"></button></div><div class="modal-body">${body || ''}</div>${foot !== false ? `<div class="modal-footer">${foot || '<button class="btn" data-close>Đóng</button>'}</div>` : ''}</div></div>`;
    document.body.appendChild(ov);
    const close = () => ov.remove();
    ov.addEventListener('click', e => { if (e.target === ov || e.target.closest('[data-close]')) close(); });
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
    w.innerHTML = `<aside class="drawer" ${width ? `style="width:min(${width}px,100%)"` : ''}><div class="drawer-head"><div class="t"><div class="eyebrow">${sub || ''}</div><h2 style="margin-top:3px">${title} ${badge || ''}</h2></div><button type="button" class="btn-close" data-close aria-label="Đóng"></button></div><div class="drawer-body"><div class="tabs">${tabs.map((t, i) => `<button class="tab ${i ? '' : 'active'}" data-tab="${i}">${t.label}</button>`).join('')}</div>${tabs.map((t, i) => `<div class="tabpane ${i ? 'hide' : ''}" data-pane="${i}">${typeof t.html === 'function' ? '' : t.html}</div>`).join('')}</div>${foot ? `<div class="drawer-foot">${foot}</div>` : ''}</aside>`;
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
    const render = () => {
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
  UI.formFields = t => Object.values(Store.colmeta[t]).filter(m => !SKIP.has(m.name) && !Store.isDeprecated(t, m.name) && m.usage !== 'Kỹ thuật' && m.usage !== 'Ẩn (bảo mật)' && m.usage !== 'Hệ thống (PK)');
  UI.input = (t, m, v) => {
    const id = 'f_' + m.name, req = !m.nullable && !m.def;
    if (m.name.endsWith('Id')) {
      const dst = Store.target(t, m.name);
      if (dst) return `<select class="select" id="${id}" name="${m.name}" ${req ? 'required' : ''}><option value="">— Chọn ${esc(dst)} —</option>${Store.rows(dst).slice(0, 400).map(r => `<option value="${esc(r.Id)}" ${r.Id === v ? 'selected' : ''}>${esc(Store.label(dst, r))}</option>`).join('')}</select>`;
      return `<input class="input mono" id="${id}" name="${m.name}" value="${esc(v || '')}" placeholder="Tham chiếu chưa xác định bảng đích (GAP)" ${req ? 'required' : ''}>`;
    }
    const sd = Store.statusDef(t, m.name);
    if (sd) return `<select class="select" id="${id}" name="${m.name}" ${req ? 'required' : ''}><option value="">—</option>${sd.codes.map((c, i) => `<option value="${c}" ${c === v ? 'selected' : ''}>${esc(sd.vi[i] || c)} (${c})</option>`).join('')}</select>`;
    if (m.name === 'Status' || m.name.endsWith('Status')) return `<select class="select" id="${id}" name="${m.name}"><option value="">—</option>${['Active', 'Inactive'].concat(v && !['Active', 'Inactive'].includes(v) ? [v] : []).map(c => `<option ${c === v ? 'selected' : ''}>${c}</option>`).join('')}</select>`;
    if (m.type === 'bit') return `<select class="select" id="${id}" name="${m.name}"><option value="1" ${v ? 'selected' : ''}>Có</option><option value="0" ${!v ? 'selected' : ''}>Không</option></select>`;
    if (m.type === 'date') return `<input type="date" class="input" id="${id}" name="${m.name}" value="${esc(U.day(v))}" ${req ? 'required' : ''}>`;
    if (/^datetime/.test(m.type)) return `<input type="datetime-local" class="input" id="${id}" name="${m.name}" value="${esc(v ? String(v).slice(0, 16) : '')}" ${req ? 'required' : ''}>`;
    if (m.type.startsWith('time')) return `<input type="time" class="input" id="${id}" name="${m.name}" value="${esc(U.time(v))}">`;
    if (/decimal|int|bigint/.test(m.type)) return `<input type="number" step="any" class="input" id="${id}" name="${m.name}" value="${v ?? ''}" ${req ? 'required' : ''}>`;
    if (/max\)$/.test(m.type) && /Note|Description|Instructions|Reason|Resolution|Remark|Address|Changes|Comment/.test(m.name)) return `<textarea class="input" rows="2" id="${id}" name="${m.name}">${esc(v || '')}</textarea>`;
    const ml = (m.type.match(/\((\d+)\)/) || [])[1];
    return `<input class="input" id="${id}" name="${m.name}" value="${esc(v || '')}" ${ml ? `maxlength="${ml}"` : ''} ${req ? 'required' : ''}>`;
  };
  UI.form = (t, row, opt = {}) => {
    const editing = !!(row && row.Id), rec = Object.assign({}, opt.defaults || {}, row || {});
    const fields = (opt.fields ? opt.fields.map(f => Store.colmeta[t][f]).filter(Boolean) : UI.formFields(t));
    // Chế độ thật: không hiện ghi chú kỹ thuật của mockup (tên bảng, cột SQL) – CLAUDE.md 4.4.
    const body = `${Store.isLive ? '' : `<div class="notice info">${I('database')}<div><b>Form sinh từ cấu trúc bảng <code>dbo.${t}</code></b>Trường bắt buộc (<span class="req" style="color:var(--red)">*</span>) = cột NOT NULL không có DEFAULT trong SQL. Tên cột SQL hiển thị cạnh nhãn. Cột deprecated không hiển thị.</div></div>`}
      <form class="form-grid" novalidate>${fields.map(m => { const req = !m.nullable && !m.def; const wide = /max\)$/.test(m.type) && /Note|Description|Instructions|Reason|Resolution|Address/.test(m.name); return `<div class="field ${wide ? 'full' : ''}"><label for="f_${m.name}">${esc(U.colLabel(m.name))}${req ? ' <span class="req">*</span>' : ''} <code>${m.name} · ${m.type}</code></label>${UI.input(t, m, rec[m.name])}${m.def ? `<span class="hint">DEFAULT ${esc(m.def)}</span>` : ''}<span class="err hide"></span></div>`; }).join('')}</form>`;
    UI.modal({
      title: (editing ? 'Chỉnh sửa – ' : 'Tạo mới – ') + (opt.title || t), sub: Store.isLive ? '' : `dbo.${t} · ${fields.length} trường`, size: 'lg', body,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="frmSave">${I('ok')} ${editing ? 'Lưu thay đổi' : 'Tạo mới'}</button>`,
      onMount: (m, close) => m.querySelector('#frmSave').onclick = () => {
        let ok = true; const out = {};
        fields.forEach(f => {
          const el = m.querySelector('#f_' + f.name), fld = el.closest('.field'), err = fld.querySelector('.err');
          let v = el.value; fld.classList.remove('invalid'); err.classList.add('hide');
          if (v === '') v = null;
          else if (f.type === 'bit') v = +v;
          else if (/decimal|int|bigint/.test(f.type)) v = Number(v);
          if ((!f.nullable && !f.def) && (v === null)) { ok = false; fld.classList.add('invalid'); err.textContent = 'Bắt buộc (NOT NULL)'; err.classList.remove('hide'); }
          out[f.name] = v;
        });
        const v2 = opt.validate && opt.validate(out); if (v2) { ok = false; UI.toast(v2, 'err'); }
        if (!ok) return;
        // opt.submit: màn hình tự lưu qua action nghiệp vụ (vd. LeaveRequest/Create) thay cho Insert / Update chung.
        if (opt.submit) return opt.submit(out, close);
        let saved;
        if (editing) saved = Store.update(t, row.Id, out, opt.note); else saved = Store.add(t, Object.assign({}, opt.defaults || {}, out));
        close(); UI.toast((editing ? 'Đã cập nhật ' : 'Đã tạo ') + esc(Store.label(t, saved)) + ` <small>(dbo.${t} · DEMO)</small>`, 'ok');
        opt.onSave && opt.onSave(saved);
      }
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
  UI.kvHtml = (t, row, only) => `<div class="kv">${(only ? only.map(c => Store.colmeta[t][c]).filter(Boolean) : Object.values(Store.colmeta[t])).filter(m => !Store.isDeprecated(t, m.name) && m.usage !== 'Ẩn (bảo mật)' && (only || !['ModifiedBy', 'DeletedBy', 'DeleteddBy', 'DeletedDate'].includes(m.name))).map(m => `<div ${/max\)$/.test(m.type) && /Note|Description|Instructions|Json|Changes/.test(m.name) ? 'style="grid-column:1/-1"' : ''}><label>${esc(U.colLabel(m.name))}<code>${m.name}</code></label><span>${UI.val(t, m.name, row[m.name], row)}</span></div>`).join('')}</div>`;
  UI.children = (t, id) => {
    const inn = (Store.relIn[t] || []).filter(r => Store.data[r.src] && Store.data[r.src].some(x => x[r.col] === id));
    if (!inn.length) return '<div class="empty">Không có bản ghi liên quan</div>';
    return inn.map(r => { const rows = Store.by(r.src, r.col, id); return `<div class="mb"><div class="spread"><h3 style="margin:0">${r.src} <span class="muted small">qua ${r.col} · ${rows.length} dòng</span></h3></div>${rows.slice(0, 8).map(x => `<div class="spread" style="padding:6px 0;border-bottom:1px solid #F0F2ED"><a href="javascript:void 0" data-open="${r.src}" data-id="${x.Id}">${esc(Store.label(r.src, x))}</a>${Store.statusCol(r.src) ? UI.badge(x[Store.statusCol(r.src)], r.src, Store.statusCol(r.src)) : ''}</div>`).join('')}${rows.length > 8 ? `<div class="muted small">… và ${rows.length - 8} dòng khác</div>` : ''}</div>`; }).join('');
  };
  // Sổ chỉ ghi thêm (CLAUDE.md 3.3): dữ liệu thật không Sửa / Xóa – muốn sửa thì ghi bút toán đảo / điều chỉnh.
  UI.APPEND_ONLY = new Set(['InventoryTransaction', 'PackagingLedger', 'TemperatureLog', 'AuditChange', 'IntegrationLog', 'ApprovalDecision', 'AttendanceEvent', 'DeliveryEvent', 'ComplaintHistory']);
  UI.readOnly = t => Store.isLive && UI.APPEND_ONLY.has(t);
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
  UI.kpi = (label, value, foot, icon, color, link) => `<article class="card kpi ${link ? 'link' : ''}" ${link ? `data-go="${link}"` : ''}><div class="kpi-top"><span>${label}</span><span class="kpi-icon ${color || 'green'}">${I(icon || 'chart')}</span></div><div class="kpi-value">${value}</div><div class="kpi-foot">${foot || ''}</div></article>`;
  UI.gapNote = f => f && f.st !== 'MAPPED' && f.note ? `<div class="notice ${f.st === 'GAP' ? 'gap' : 'warn'}">${I('alert')}<div><b>${f.st === 'GAP' ? 'DATABASE GAP – chưa có bảng hỗ trợ' : 'PARTIAL – có bảng nhưng thiếu cột/quan hệ'} · BA/PO CONFIRM</b>${esc(f.note)}</div></div>` : (f && f.note ? `<div class="notice info">${I('database')}<div>${esc(f.note)}</div></div>` : '');
  UI.stepper = (steps, cur, failed) => `<div class="stepper">${steps.map((s, i) => `<div class="step ${i < cur ? 'done' : i === cur ? (failed ? 'fail' : 'current') : ''}"><div class="dot">${i < cur ? '✓' : i + 1}</div><div class="lbl">${s.l || s}</div>${s.s ? `<small>${s.s}</small>` : ''}</div>`).join('')}</div>`;
  window.UI = UI;
})();
