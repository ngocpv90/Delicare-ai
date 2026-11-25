/* UI-6.41 (D-122): "Công ty & đơn vị" (adm-orgunit; adm-company ẩn – link cũ mở màn này chọn sẵn công ty).
 * - Đầu màn: tìm đơn vị, Thêm đơn vị; 4 chỉ số (OrganizationUnit/GetSummary): Đơn vị đang hoạt động · Nhân viên · Chưa có trưởng (bấm lọc cây) · Nhân viên chưa gắn đơn vị.
 * - Trái: cây có công ty làm gốc (GetOrgTree) – biểu tượng theo loại, nhãn "chưa có trưởng", số nhân viên gồm con; tìm mở nhánh chứa kết quả.
 * - Phải: chọn công ty → thông tin (Sửa theo quyền mục Công ty) + đơn vị trực thuộc; chọn đơn vị → đường dẫn, loại (tiếng Việt), trưởng (chọn ngay), nút
 *   ＋ Đơn vị con · Sửa · Chuyển sang đơn vị khác · Ngừng / Hoạt động lại; tab Nhân viên · Đơn vị con · Kho · Lịch sử.
 * Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { sel: null, q: '', noMgr: false, tab: 'emp' };
  const ref = n => (Store.refList ? Store.refList(n) : []) || [];
  const ICON = { COMPANY_ROOT: 'building', COMPANY: 'building', BRANCH: 'home', KITCHEN: 'home', BOARD: 'users', DIVISION: 'layers', DEPARTMENT: 'panel', TEAM: 'users', WAREHOUSE: 'box' };
  const base = APP.SCREENS['adm-orgunit'];

  APP.register('adm-orgunit', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && p.company) st.sel = 'COMPANY_FIRST';
    const re = () => APP.SCREENS['adm-orgunit'](el, f, {});
    const canEdit = Store.perm('adm-orgunit').edit !== false, canAdd = Store.perm('adm-orgunit').add !== false;
    el.innerHTML = APP.header(f, `<input class="input" id="ogQ" placeholder="Tìm đơn vị" value="${esc(st.q)}" style="width:220px">${canAdd ? `<button class="btn primary" id="ogNew">${I('plus')} Thêm đơn vị</button>` : ''}`, { noNote: true })
      + `<div id="ogKpi" class="kpis"></div><style>.og-wrap{display:grid;grid-template-columns:minmax(280px,360px) 1fr;gap:14px;align-items:start}@media (max-width:900px){.og-wrap{grid-template-columns:1fr}}.og-node{display:flex;align-items:center;gap:6px;padding:5px 8px;border-radius:8px;cursor:pointer}.og-node:hover{background:#f4f6f2}.og-node.on{background:#eef6e8;font-weight:700}</style>
        <div class="og-wrap"><div class="card panel" id="ogTree"><div class="small muted">Đang tải…</div></div><div id="ogDetail"><div class="card panel small muted">Chọn công ty hoặc đơn vị bên trái.</div></div></div>`;
    let tree = [], flat = [];
    Promise.all([Store.query('OrganizationUnit/GetOrgTree'), Store.query('OrganizationUnit/GetSummary'), Store.ensure('Employee'), Store.ensure('Company')]).then(([t, s]) => {
      if (!el.isConnected) return;
      tree = (t.ok && t.data) || []; flat = [];
      const walk = (n, parents) => { flat.push({ n, parents }); (n.children || []).forEach(c => walk(c, parents.concat([n]))); };
      tree.forEach(n => walk(n, []));
      if (st.sel === 'COMPANY_FIRST') st.sel = (tree.find(n => n.isCompany) || {}).id || null;
      const d = (s.ok && s.data) || {};
      el.querySelector('#ogKpi').innerHTML = UI.kpi('Đơn vị đang hoạt động', U.num(d.activeUnits || 0), `${U.num(d.companies || 0)} công ty · ${U.num(d.kitchens || 0)} bếp · ${U.num(d.teams || 0)} tổ / phòng`, 'flow', 'green')
        + UI.kpi('Nhân viên', U.num(d.employees || 0), 'đã gắn đơn vị', 'users', 'blue')
        + UI.kpi('Chưa có trưởng đơn vị', U.num(d.noManager || 0), d.noManager ? 'bấm để lọc' : '', 'alert', d.noManager ? 'red' : 'green').replace('<article ', '<article data-k="nomgr" style="cursor:pointer" ')
        + UI.kpi('Nhân viên chưa gắn đơn vị', U.num(d.employeesNoUnit || 0), d.employeesNoUnit ? 'bấm để xem' : '', 'user', d.employeesNoUnit ? 'orange' : 'green').replace('<article ', '<article data-k="nounit" style="cursor:pointer" ');
      el.querySelector('[data-k="nomgr"]').onclick = () => { st.noMgr = !st.noMgr; drawTree(); };
      el.querySelector('[data-k="nounit"]').onclick = () => APP.go('adm-employee');
      drawTree(); drawDetail();
    });
    const q = el.querySelector('#ogQ'); let tq; q.oninput = () => { clearTimeout(tq); tq = setTimeout(() => { st.q = q.value; drawTree(); }, 250); };
    const nb = el.querySelector('#ogNew'); if (nb) nb.onclick = () => unitForm(null, selected() && !selected().n.isCompany ? selected().n.id : selected() ? 'c:' + selected().n.id : null, re);
    const selected = () => flat.find(x => x.n.id === st.sel);

    function drawTree() {
      const box = el.querySelector('#ogTree'), k = st.q.trim().toLowerCase();
      const match = n => (!k || String(n.name || '').toLowerCase().includes(k) || String(n.code || '').toLowerCase().includes(k)) && (!st.noMgr || n.noManager);
      const keep = n => match(n) || (n.children || []).some(keep);
      const node = (n, depth) => keep(n) ? `<div class="og-node ${n.id === st.sel ? 'on' : ''}" data-n="${n.id}" style="padding-left:${8 + depth * 16}px">${I(ICON[n.unitType] || 'panel')}<span style="flex:1">${esc(n.name || '')}${n.status === 'Inactive' ? ' <span class="small muted">(ngừng)</span>' : ''}</span>${n.noManager ? '<span class="tag red" style="font-size:10px">chưa có trưởng</span>' : ''}<span class="small muted">${U.num(n.totalEmployees || 0)}</span></div>${(n.children || []).map(c => node(c, depth + 1)).join('')}` : '';
      box.innerHTML = `${st.noMgr ? '<div class="notice warn mb">Đang lọc đơn vị chưa có trưởng <button class="btn xs" id="ogNm">Bỏ lọc</button></div>' : ''}${tree.map(n => node(n, 0)).join('') || '<div class="empty">Chưa có đơn vị.</div>'}`;
      box.querySelectorAll('[data-n]').forEach(d => d.onclick = () => { st.sel = d.dataset.n; drawTree(); drawDetail(); if (window.innerWidth < 900) el.querySelector('#ogDetail').scrollIntoView({ behavior: 'smooth' }); });
      const nm = box.querySelector('#ogNm'); if (nm) nm.onclick = () => { st.noMgr = false; drawTree(); };
    }
    function drawDetail() {
      const box = el.querySelector('#ogDetail'), s = selected();
      if (!s) { box.innerHTML = '<div class="card panel small muted">Chọn công ty hoặc đơn vị bên trái.</div>'; return; }
      const n = s.n, kids = n.children || [];
      const kidTable = `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Đơn vị</th><th>Loại</th><th>Trưởng đơn vị</th><th class="num">Nhân viên</th></tr></thead><tbody>${kids.map(c => `<tr data-go="${c.id}" style="cursor:pointer"><td><b>${esc(c.name || '')}</b><div class="small muted">${esc(c.code || '')}</div></td><td>${esc(c.unitTypeName || '')}</td><td>${c.managerName ? esc(c.managerName) : '<span class="bad small">chưa có</span>'}</td><td class="num">${U.num(c.totalEmployees || 0)}</td></tr>`).join('') || '<tr><td colspan="4" class="muted">Chưa có đơn vị trực thuộc</td></tr>'}</tbody></table></div>`;
      if (n.isCompany) {
        const c = Store.get('Company', n.id) || {}, canCo = Store.perm('adm-company').edit !== false;
        box.innerHTML = `<div class="card panel"><div class="spread"><div><div class="small muted">Công ty</div><h2 style="margin:2px 0">${esc(n.name || '')}</h2></div>${canCo ? `<button class="btn" id="coEdit">${I('settings')} Sửa</button>` : ''}</div>
          <div class="kv mt"><div><label>Mã</label><span>${esc(c.Code || n.code || '')}</span></div><div><label>Mã số thuế</label><span>${esc(c.TaxCode || '–')}</span></div><div><label>Địa chỉ</label><span>${esc(c.Address || '–')}</span></div><div><label>Điện thoại</label><span>${esc(c.Phone || '–')}</span></div><div><label>Email</label><span>${esc(c.Email || '–')}</span></div></div>
          <div class="small muted mt">Tên, địa chỉ, mã số thuế, điện thoại in ở đầu mọi phiếu in và tệp Excel.</div></div><div class="card panel"><div class="panel-title mb">Đơn vị trực thuộc</div>${kidTable}</div>`;
        const ce = box.querySelector('#coEdit'); if (ce) ce.onclick = () => companyForm(c, re);
      } else {
        const path = s.parents.map(x => esc(x.name || '')).join(' › ');
        box.innerHTML = `<div class="card panel"><div class="small muted">${path}</div><div class="spread"><h2 style="margin:2px 0">${esc(n.name || '')} ${UI.tag(n.unitTypeName || '', 'blue')} ${n.status === 'Inactive' ? UI.tag('Ngừng hoạt động', '') : UI.tag('Hoạt động', 'green')}</h2></div>
          <div class="kv mt"><div><label>Mã</label><span>${esc(n.code || '')}</span></div><div><label>Trưởng đơn vị</label><span>${n.managerName ? esc(n.managerName) : '<span class="bad">Chưa có</span>'}${canEdit ? ` <a href="javascript:void 0" id="ogMgr">${n.managerName ? 'đổi' : 'chọn ngay'}</a>` : ''}</span></div><div><label>Nhân viên (gồm đơn vị con)</label><span>${U.num(n.totalEmployees || 0)}</span></div><div><label>Kho</label><span>${U.num(n.warehouseCount || 0)}</span></div></div>
          ${canEdit || canAdd ? `<div class="row-flex mt" style="gap:6px;flex-wrap:wrap">${canAdd ? `<button class="btn sm" id="ogChild">${I('plus')} Đơn vị con</button>` : ''}${canEdit ? `<button class="btn sm" id="ogEdit">Sửa</button><button class="btn sm" id="ogMove">Chuyển sang đơn vị khác</button><button class="btn sm ${n.status === 'Inactive' ? '' : 'danger'}" id="ogSt">${n.status === 'Inactive' ? 'Hoạt động lại' : 'Ngừng hoạt động'}</button>` : ''}</div>` : ''}</div>
          <div class="tabs c360-tabs">${[['emp', 'Nhân viên'], ['kids', 'Đơn vị con'], ['wh', 'Kho'], ['his', 'Lịch sử']].map(([k, l]) => `<button class="tab ${st.tab === k ? 'active' : ''}" data-ot="${k}">${l}</button>`).join('')}</div><div class="card panel" id="ogPane"></div>`;
        const pane = box.querySelector('#ogPane');
        box.querySelectorAll('[data-ot]').forEach(b => b.onclick = () => { st.tab = b.dataset.ot; drawDetail(); });
        const ids = new Set(); const col = x => { ids.add(x.id); (x.children || []).forEach(col); }; col(n);
        if (st.tab === 'emp') {
          const emps = Store.rows('Employee').filter(e => !e.IsDeleted && ids.has(e.OrganizationUnitId) && e.Status !== 'Resigned');
          pane.innerHTML = emps.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Nhân viên</th><th>Đơn vị</th><th>Chức vụ</th></tr></thead><tbody>${emps.map(e => `<tr data-emp="${e.Id}" style="cursor:pointer"><td><b>${esc(e.FullName || '')}</b><div class="small muted">${esc(e.Code || '')}</div></td><td>${esc(Store.label('OrganizationUnit', e.OrganizationUnitId))}</td><td>${esc(Store.label('Position', e.PositionId))}</td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Chưa có nhân viên.</div>';
          pane.querySelectorAll('[data-emp]').forEach(r => r.onclick = () => APP.go('adm-employee-360?id=' + r.dataset.emp));
        } else if (st.tab === 'kids') { pane.innerHTML = kidTable; }
        else if (st.tab === 'wh') { Store.ensure('Warehouse').then(() => { const wh = Store.rows('Warehouse').filter(w => !w.IsDeleted && ids.has(w.OrganizationUnitId)); pane.innerHTML = wh.length ? wh.map(w => `<div>${esc(w.Name || '')} <span class="small muted">${esc(w.Code || '')}</span></div>`).join('') : '<div class="empty">Chưa có kho.</div>'; }); }
        else if (UI.history) UI.history('OrganizationUnit', n.id, pane);
        const b = id => box.querySelector('#' + id);
        if (b('ogChild')) b('ogChild').onclick = () => unitForm(null, n.id, re);
        if (b('ogEdit')) b('ogEdit').onclick = () => unitForm(n, null, re);
        if (b('ogMove')) b('ogMove').onclick = () => moveDlg(n, re);
        if (b('ogSt')) b('ogSt').onclick = () => UI.confirm(n.status === 'Inactive' ? `Cho <b>${esc(n.name || '')}</b> hoạt động lại?` : `Ngừng hoạt động <b>${esc(n.name || '')}</b>? Chỉ được khi không còn nhân viên đang làm và đơn vị con đang hoạt động.`,
          () => Store.act('OrganizationUnit/SetStatus', { json: { id: n.id, status: n.status === 'Inactive' ? 'Active' : 'Inactive' }, tables: ['OrganizationUnit'] }).then(r => { if (r.ok) { UI.toast(esc(r.res && r.res.responseMessage || 'Đã cập nhật'), 'ok'); re(); } }), { ok: n.status === 'Inactive' ? 'Hoạt động lại' : 'Ngừng hoạt động', danger: n.status !== 'Inactive' });
        if (b('ogMgr')) b('ogMgr').onclick = () => managerDlg(n, re);
      }
      box.querySelectorAll('[data-go]').forEach(r => r.onclick = () => { st.sel = r.dataset.go; drawTree(); drawDetail(); });
    }
    // ---------- hộp thoại ----------
    const parentOptions = (cur, exclude) => {
      const ex = new Set(); if (exclude) { const f2 = flat.find(x => x.n.id === exclude); const col = x => { ex.add(x.id); (x.children || []).forEach(col); }; if (f2) col(f2.n); }
      return flat.filter(x => !ex.has(x.n.id)).map(x => { const v = x.n.isCompany ? 'c:' + x.n.id : x.n.id; return `<option value="${v}" ${v === cur ? 'selected' : ''}>${'— '.repeat(x.parents.length)}${esc(x.n.name || '')}${x.n.isCompany ? ' (công ty)' : ''}</option>`; }).join('');
    };
    const empOptions = cur => Store.rows('Employee').filter(e => !e.IsDeleted && (e.Status || 'Active') === 'Active').sort((a, b) => String(a.FullName).localeCompare(String(b.FullName), 'vi')).map(e => `<option value="${e.Id}" ${e.Id === cur ? 'selected' : ''}>${esc((e.FullName || '') + ' · ' + (e.Code || ''))}</option>`).join('');
    function unitForm(n, parent, done) {
      const edit = !!n, rec = edit ? (Store.get('OrganizationUnit', n.id) || {}) : {};
      const curParent = edit ? (n.parentId || (n.companyId ? 'c:' + n.companyId : '')) : parent || '';
      const types = ref('OrganizationUnitType'), oldType = edit && n.unitType && !types.some(t => t.code === n.unitType);
      UI.modal({ title: edit ? 'Sửa đơn vị' : 'Thêm đơn vị', size: 'md',
        body: `<div class="form-grid"><div class="field full"><label>Thuộc *</label><select class="select" id="ufP">${parentOptions(curParent, edit ? n.id : null)}</select></div>
          <div class="field"><label>Tên *</label><input class="input" id="ufN" value="${esc(n ? n.name || '' : '')}"></div><div class="field"><label>Mã *</label><input class="input" id="ufC" value="${esc(n ? n.code || '' : '')}" placeholder="CHỮ HOA KHÔNG DẤU"></div>
          <div class="field"><label>Loại *</label><select class="select" id="ufT">${oldType ? `<option value="${esc(n.unitType)}" selected>${esc(n.unitType)} (giá trị cũ)</option>` : ''}${types.map(t => `<option value="${esc(t.code)}" ${n && t.code === n.unitType ? 'selected' : ''}>${esc(t.name)}</option>`).join('')}</select></div>
          <div class="field"><label>Trưởng đơn vị</label><select class="select" id="ufM"><option value="">— Chưa có —</option>${empOptions(n && n.managerId)}</select></div>
          <div class="field"><label>Điện thoại</label><input class="input" id="ufPh" value="${esc(rec.Phone || '')}"></div><div class="field"><label>Địa chỉ</label><input class="input" id="ufA" value="${esc(rec.Address || '')}"></div>
          <div class="field full"><label>Ghi chú</label><input class="input" id="ufNo" value="${esc(rec.Note || '')}"></div></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="ufGo">Lưu</button>',
        onMount: (m, close) => m.querySelector('#ufGo').onclick = () => {
          const v = id => m.querySelector('#' + id).value.trim(), par = v('ufP');
          if (!v('ufN')) return UI.toast('Nhập tên đơn vị', 'err'); if (!v('ufC')) return UI.toast('Nhập mã đơn vị', 'err');
          const form = { Name: v('ufN'), Code: v('ufC'), UnitType: v('ufT'), ManagerId: v('ufM'), Phone: v('ufPh'), Address: v('ufA'), Note: v('ufNo') };
          if (par.startsWith('c:')) form.CompanyId = par.slice(2); else form.ParentId = par;
          if (edit) form.Id = n.id;
          Store.act(edit ? 'OrganizationUnit/Update' : 'OrganizationUnit/Insert', { form, method: edit ? 'PUT' : 'POST', tables: ['OrganizationUnit'] }).then(r => { if (r.ok) { close(); UI.toast('Đã lưu đơn vị', 'ok'); done(); } });
        } });
    }
    function moveDlg(n, done) {
      UI.modal({ title: 'Chuyển ' + esc(n.name || ''), size: 'sm', body: `<div class="field"><label>Sang (đơn vị cha mới)</label><select class="select" id="mvP">${parentOptions(n.parentId || ('c:' + n.companyId), n.id)}</select></div><div class="small muted mt">Chuyển sang công ty khác thì cả nhánh đổi công ty.</div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="mvGo">Chuyển</button>',
        onMount: (m, close) => m.querySelector('#mvGo').onclick = () => { const v = m.querySelector('#mvP').value; const body = v.startsWith('c:') ? { id: n.id, newParentId: null, companyId: v.slice(2) } : { id: n.id, newParentId: v };
          Store.act('OrganizationUnit/Move', { json: body, tables: ['OrganizationUnit'] }).then(r => { if (r.ok) { close(); UI.toast('Đã chuyển đơn vị', 'ok'); done(); } }); } });
    }
    function managerDlg(n, done) {
      UI.modal({ title: 'Trưởng đơn vị – ' + esc(n.name || ''), size: 'sm', body: `<div class="field"><label>Nhân viên đang làm</label><input class="input mb" id="mgQ" placeholder="Gõ tên để lọc"><select class="select" id="mgE" size="8"><option value="">— Bỏ trưởng —</option>${empOptions(n.managerId)}</select></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="mgGo">Lưu</button>',
        onMount: (m, close) => {
          m.querySelector('#mgQ').oninput = e => { const k = e.target.value.toLowerCase(); m.querySelectorAll('#mgE option').forEach(o => { o.hidden = !!o.value && !o.textContent.toLowerCase().includes(k); }); };
          m.querySelector('#mgGo').onclick = () => Store.act('OrganizationUnit/SetManager', { json: { id: n.id, employeeId: m.querySelector('#mgE').value || null }, tables: ['OrganizationUnit'] }).then(r => { if (r.ok) { close(); UI.toast('Đã cập nhật trưởng đơn vị', 'ok'); done(); } });
        } });
    }
    function companyForm(c, done) {
      UI.modal({ title: 'Sửa công ty', size: 'md',
        body: `<div class="form-grid"><div class="field full"><label>Tên *</label><input class="input" id="cfN" value="${esc(c.Name || '')}"></div><div class="field"><label>Mã số thuế</label><input class="input" id="cfT" value="${esc(c.TaxCode || '')}" placeholder="10 hoặc 13 số"></div><div class="field"><label>Điện thoại</label><input class="input" id="cfP" value="${esc(c.Phone || '')}"></div><div class="field full"><label>Địa chỉ</label><input class="input" id="cfA" value="${esc(c.Address || '')}"></div><div class="field"><label>Email</label><input class="input" id="cfE" value="${esc(c.Email || '')}"></div><div class="field"><label>Ghi chú</label><input class="input" id="cfNo" value="${esc(c.Note || '')}"></div></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="cfGo">Lưu</button>',
        onMount: (m, close) => m.querySelector('#cfGo').onclick = () => { const v = id => m.querySelector('#' + id).value.trim(); if (!v('cfN')) return UI.toast('Nhập tên công ty', 'err'); const t = v('cfT').replace(/\D/g, ''); if (v('cfT') && t.length !== 10 && t.length !== 13) return UI.toast('Mã số thuế phải gồm 10 hoặc 13 chữ số', 'err');
          Store.act('Company/Update', { form: { Id: c.Id, Code: c.Code, Name: v('cfN'), TaxCode: v('cfT'), Phone: v('cfP'), Address: v('cfA'), Email: v('cfE'), Note: v('cfNo'), Status: c.Status || 'Active' }, method: 'PUT', tables: ['Company'] }).then(r => { if (r.ok) { close(); UI.toast('Đã lưu công ty', 'ok'); done(); } }); } });
    }
  });
})();
