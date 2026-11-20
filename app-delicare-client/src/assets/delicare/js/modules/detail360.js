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
  // Tab Lịch sử + Liên quan (Common/GetHistory · Common/GetRelated).
  D.historyTab = (resource, noRelated) => (box, d, id) => {
    if (!Store.isLive) { box.innerHTML = UI.history ? '' : D.demoNote; if (UI.history) UI.history(resource, id, box); return; }
    box.innerHTML = `<h3>Lịch sử</h3><div data-his>${D.loading}</div>${noRelated ? '' : `<h3 class="mt">Liên quan</h3><div data-rel>${D.loading}</div>`}`;
    const his = box.querySelector('[data-his]'), rel = box.querySelector('[data-rel]');
    D.run(his, Store.query('Common/GetHistory', { resource, id }), x => {
      const rows = x.list || [];
      his.innerHTML = rows.length ? rows.map(h => `<div class="c360-rows"><div class="r"><label>${esc(U.dt ? U.dt(h.at) : D.dmy(h.at))} · ${esc(h.byName || h.by || '')}</label><span><b>${esc(h.actionName || h.action || '')}</b>${h.referenceNo ? ' · ' + esc(h.referenceNo) : ''}</span></div>
        ${(h.listChange || []).map(c => `<div class="small muted" style="padding-left:12px">${esc(c.field)}: ${esc(c.masked ? '***' : c.from ?? '∅')} → ${esc(c.masked ? '***' : c.to ?? '∅')}</div>`).join('')}${h.comment ? `<div class="small" style="padding-left:12px">${esc(h.comment)}</div>` : ''}</div>`).join('') : D.none;
    });
    if (noRelated) return;
    D.run(rel, Store.query('Common/GetRelated', { resource, id }), x => {
      const groups = (x.list || []).filter(g => g.count);
      rel.innerHTML = groups.length ? groups.map(g => `<div class="mb"><div class="spread"><b>${esc(g.title)} · ${U.num(g.count)}</b>${g.route && D.can(g.route) ? `<a href="javascript:void 0" data-go="${esc(g.route)}" class="small">Mở màn</a>` : ''}</div>
        <div class="small">${(g.items || []).slice(0, 8).map(i => `${esc(i.label || '')}${i.statusName ? ' (' + esc(i.statusName) + ')' : ''}`).join(' · ')}</div></div>`).join('') : D.none;
      D.bindGo(rel);
    });
  };
  // Thanh tỷ lệ (Đánh giá NCC…): value 0–100, good = ngưỡng tốt.
  D.meter = (label, value, sub, good) => {
    const v = value === null || value === undefined ? null : Math.max(0, Math.min(100, +value));
    const color = v === null ? 'var(--muted)' : v >= (good || 90) ? 'var(--green, #91AF42)' : v >= (good || 90) - 15 ? '#F07825' : '#D9534F';
    return `<div class="d360-meter"><div class="spread small"><span>${esc(label)}</span><b>${v === null ? '–' : U.num(v, 1) + '%'}</b></div><div class="bar"><i style="width:${v || 0}%;background:${color}"></i></div>${sub ? `<div class="small muted">${sub}</div>` : ''}</div>`;
  };
  APP.D360 = D;
})();
