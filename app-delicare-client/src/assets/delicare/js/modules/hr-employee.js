/* UI-6.36 (D-117): "Nhân viên" theo nghiệp vụ nhân sự.
 * - Danh sách (adm-employee): 4 chỉ số bấm để lọc, chip trạng thái có số đếm (mặc định Đang làm việc), đơn vị dạng cây có tìm (gồm đơn vị con), chức vụ, tìm;
 *   cột Mã · Họ tên (SĐT) · Đơn vị · Chức vụ · Ngày vào làm · Tài khoản · Bot · Trạng thái; chọn nhiều → Đề nghị cấp tài khoản / Tạo mã liên kết bot / Xuất Excel;
 *   Nhập từ Excel; tab Đối chiếu tài khoản chỉ khi còn người chưa gắn – Employee/GetList, RequestAccountMany, CreateBotCodes.
 * - Form "Thêm nhân viên" riêng (APP.employeeForm) 3 nhóm: Thông tin cá nhân (SĐT báo trùng khi gõ – Employee/CheckPhone) · Công việc (đơn vị cây có tìm) ·
 *   Tài khoản & bot (nhóm quyền gợi ý theo chức vụ – Employee/GetPositionGroups). Sửa dùng cùng form, không có nhóm Tài khoản & bot. Lưu → mở hồ sơ.
 * - Hồ sơ: hộp Cho nghỉ việc (APP.employeeResign – PreviewResign / Resign), Tạm nghỉ / Quay lại làm (SetStatus), dải việc cần làm cho người mới (employee360.js).
 * Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { status: 'Active', flag: '', unit: '', pos: '', q: '', sel: new Set() };
  const ref = n => (Store.refList ? Store.refList(n) : []) || [];
  const ACC = { ACTIVE: ['Có', 'green'], LOCKED: ['Đã khóa', 'gray'], PENDING: ['Chờ IT duyệt', 'orange'], NONE: ['Chưa có', ''] };
  const STATUS = [['Active', 'Đang làm việc'], ['Inactive', 'Tạm nghỉ'], ['Resigned', 'Đã nghỉ việc'], ['ALL', 'Tất cả']];
  const TABLES = ['Employee', 'ShiftAssignment', 'User', 'ApprovalRequest'];

  // ---------- Đơn vị dạng cây có tìm (dùng chung: lọc danh sách, form; UI-6.37 Phân ca) ----------
  const unitTree = () => {
    const rows = Store.rows('OrganizationUnit').filter(o => !o.IsDeleted), by = {}, out = [];
    rows.forEach(o => { (by[o.ParentId || ''] = by[o.ParentId || ''] || []).push(o); });
    const walk = (pid, depth, seen) => (by[pid] || []).sort((a, b) => String(a.Name || '').localeCompare(String(b.Name || ''), 'vi')).forEach(o => { if (seen.has(o.Id)) return; seen.add(o.Id); out.push({ id: o.Id, name: o.Name || o.Code || '', depth }); walk(o.Id, depth + 1, seen); });
    const seen = new Set(); walk('', 0, seen);
    rows.filter(o => !seen.has(o.Id)).forEach(o => { out.push({ id: o.Id, name: o.Name || '', depth: 0 }); walk(o.Id, 1, seen); });
    return out;
  };
  APP.unitPicker = (host, { value, onChange, placeholder = 'Mọi đơn vị', allowEmpty = true, id = 'upk' } = {}) => {
    const list = unitTree(), cur = list.find(u => u.id === value);
    host.innerHTML = `<div class="up-wrap" style="position:relative;min-width:220px"><input class="input" id="${id}" autocomplete="off" placeholder="${esc(placeholder)}" value="${esc(cur ? cur.name : '')}"><div class="up-list card" style="display:none;position:absolute;z-index:30;left:0;right:0;top:100%;max-height:300px;overflow:auto;padding:4px"></div></div>`;
    const inp = host.querySelector('input'), box = host.querySelector('.up-list');
    const draw = q => { const k = (q || '').trim().toLowerCase(); const items = list.filter(u => !k || u.name.toLowerCase().includes(k));
      box.innerHTML = (allowEmpty ? `<div class="up-it" data-u="" style="padding:6px 8px;cursor:pointer" class="muted">${esc(placeholder)}</div>` : '') + items.map(u => `<div class="up-it" data-u="${u.id}" style="padding:6px 8px;padding-left:${8 + (k ? 0 : u.depth * 16)}px;cursor:pointer;${u.id === value ? 'font-weight:700' : ''}">${u.depth && !k ? '└ ' : ''}${esc(u.name)}</div>`).join('') || '<div class="small muted" style="padding:6px">Không có đơn vị khớp</div>';
      box.querySelectorAll('[data-u]').forEach(d => d.onmousedown = e => { e.preventDefault(); value = d.dataset.u; const u = list.find(x => x.id === value); inp.value = u ? u.name : ''; box.style.display = 'none'; onChange && onChange(value || ''); }); };
    inp.onfocus = () => { draw(''); box.style.display = 'block'; inp.select(); };
    inp.oninput = () => { draw(inp.value); box.style.display = 'block'; };
    inp.onblur = () => setTimeout(() => { box.style.display = 'none'; const u = list.find(x => x.id === value); inp.value = u ? u.name : ''; }, 150);
    return { get: () => value || '' };
  };

  // ================= Danh sách =================
  const base = APP.SCREENS['adm-employee'];
  APP.register('adm-employee', (el, f, params) => {
    if (!Store.isLive) return base(el, f, params);
    const re = () => APP.SCREENS['adm-employee'](el, f, {});
    const canEdit = Store.perm('adm-employee').edit !== false, canImp = Store.perm('adm-employee').imp !== false && Store.perm('sys-import').access !== false;
    Promise.all([Store.ensure('OrganizationUnit'), Store.ensure('Position')]).then(() => {
      if (!el.isConnected) return;
      if (params && params.tab === 'link') {
        el.innerHTML = APP.header(f, '', { noNote: true }) + `<div class="tabs c360-tabs mb"><button class="tab" data-et="list">Danh sách</button><button class="tab active">Đối chiếu tài khoản</button></div><div data-epane></div>`;
        el.querySelector('[data-et]').onclick = () => APP.go('adm-employee');
        return APP.linkReview(el.querySelector('[data-epane]'), Object.assign({}, f, { name: 'Đối chiếu tài khoản' }));
      }
      el.innerHTML = APP.header(f, `${canImp ? `<button class="btn" id="emImp">${I('file')} Nhập từ Excel</button>` : ''}${canEdit ? `<button class="btn primary" id="emNew">${I('plus')} Thêm nhân viên</button>` : ''}`, { noNote: true })
        + `<div id="emTabs"></div><div id="emKpi" class="kpis"></div>
        <div class="card panel"><div class="row-flex mb" style="gap:8px;flex-wrap:wrap" id="emFilter"></div><div id="emBar"></div><div id="emBody"><div class="small muted">Đang tải…</div></div></div>`;
      const ni = el.querySelector('#emNew'); if (ni) ni.onclick = () => APP.employeeForm(null, {});
      const ii = el.querySelector('#emImp'); if (ii) ii.onclick = importDlg;
      const filter = el.querySelector('#emFilter');
      filter.innerHTML = `<span id="emSt"></span><span id="emUnit"></span><select class="select" id="emPos" style="width:auto"><option value="">Mọi chức vụ</option>${Store.rows('Position').filter(p => !p.IsDeleted).map(p => `<option value="${p.Id}" ${p.Id === st.pos ? 'selected' : ''}>${esc(p.Name || p.Code || '')}</option>`).join('')}</select><input class="input" id="emQ" placeholder="Tìm tên, mã, số điện thoại" value="${esc(st.q)}" style="width:240px">${st.flag ? `<button class="chip active" id="emFlagX">${esc({ NEW_MONTH: 'Vào làm tháng này', NO_ACCOUNT: 'Chưa có tài khoản', NO_BOT: 'Chưa liên kết bot' }[st.flag])} ✕</button>` : ''}`;
      APP.unitPicker(filter.querySelector('#emUnit'), { value: st.unit, onChange: v => { st.unit = v; load(); } });
      filter.querySelector('#emPos').onchange = e => { st.pos = e.target.value; load(); };
      let tq; filter.querySelector('#emQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { st.q = e.target.value; load(); }, 300); };
      const fx = filter.querySelector('#emFlagX'); if (fx) fx.onclick = () => { st.flag = ''; re(); };
      let rows = [];
      const bar = () => {
        const b = el.querySelector('#emBar'), n = st.sel.size;
        b.innerHTML = n ? `<div class="notice info mb" style="display:flex;gap:8px;align-items:center;flex-wrap:wrap"><span>Đã chọn ${U.num(n)} người</span>${canEdit ? `<button class="btn sm primary" data-bk="acc">Đề nghị cấp tài khoản</button><button class="btn sm" data-bk="bot">Tạo mã liên kết bot</button>` : ''}<button class="btn sm" data-bk="xls">Xuất Excel</button><button class="btn sm" data-bk="clr">Bỏ chọn</button></div>` : '';
        b.querySelectorAll('[data-bk]').forEach(x => x.onclick = () => bulk(x.dataset.bk));
      };
      const bulk = k => {
        const ids = [...st.sel];
        if (k === 'clr') { st.sel.clear(); draw(); return; }
        if (k === 'xls') { const sel = rows.filter(r => st.sel.has(r.id)); U.csv(sel, [['code', 'Mã'], ['fullName', 'Họ tên'], ['phone', 'Điện thoại'], ['organizationUnitName', 'Đơn vị'], ['positionName', 'Chức vụ'], ['hireDate', 'Ngày vào làm'], ['accountStatusName', 'Tài khoản'], ['bot', 'Bot'], ['statusName', 'Trạng thái']].map(([c, l]) => ({ k: c, label: l, text: r => c === 'hireDate' ? U.date(r.hireDate) : c === 'bot' ? (r.botLinked ? 'Đã liên kết' : 'Chưa') : (r[c] || '') })), `Nhan_vien_${Store.ctx.date}.csv`); UI.toast('Đã xuất ' + sel.length + ' dòng', 'ok'); return; }
        if (k === 'acc') return UI.confirm(`Đề nghị cấp tài khoản web cho <b>${U.num(ids.length)}</b> người? Mỗi người một yêu cầu cho IT, nhóm quyền gợi ý theo chức vụ; người đã có / đang chờ tài khoản được bỏ qua.`, () => Store.act('Employee/RequestAccountMany', { json: { listId: ids }, tables: TABLES }).then(r => { if (!r.ok) return; const d = r.data || {}; UI.toast(esc(r.res && r.res.responseMessage || ''), d.skipped ? 'warn' : 'ok'); if ((d.listSkipped || []).length) UI.modal({ title: 'Người được bỏ qua', size: 'md', body: `<ul>${d.listSkipped.map(x => `<li>${esc(x)}</li>`).join('')}</ul>` }); st.sel.clear(); load(); }), { ok: 'Gửi đề nghị' });
        if (k === 'bot') return Store.act('Employee/CreateBotCodes', { json: { listId: ids } }).then(r => { if (!r.ok) return; const d = r.data || {}; codesDlg(d); st.sel.clear(); draw(); });
      };
      const draw = () => {
        const body = el.querySelector('#emBody'), all = rows.length && rows.every(r => st.sel.has(r.id));
        // Điện thoại: dòng gọn (CSS chọn bảng / dòng gọn theo bề rộng).
        const narrowHtml = rows.length ? rows.map(r => { const a = ACC[r.accountStatus] || ACC.NONE; return `<div data-emr="${r.id}" style="display:flex;gap:10px;align-items:flex-start;padding:10px 0;border-bottom:1px solid #eef0ec;cursor:pointer"><input type="checkbox" data-ck="${r.id}" ${st.sel.has(r.id) ? 'checked' : ''} aria-label="Chọn"><div style="flex:1;min-width:0"><div class="spread"><b>${esc(r.fullName || '')}</b>${UI.tag(a[0], a[1])}</div><div class="small">${esc(r.phone || '')}</div><div class="small muted">${esc([r.code, r.positionName, r.organizationUnitName].filter(Boolean).join(' · '))}</div></div></div>`; }).join('') : '';
        body.innerHTML = '<style>.em-n{display:none}@media (max-width:700px){.em-w{display:none}.em-n{display:block}}</style>' + (rows.length ? '<div class="em-n">' + narrowHtml + '</div><div class="em-w">' : '') + (rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th style="width:28px"><input type="checkbox" id="emAll" ${all ? 'checked' : ''} aria-label="Chọn tất cả"></th><th>Mã</th><th>Họ tên</th><th>Đơn vị</th><th>Chức vụ</th><th>Ngày vào làm</th><th>Tài khoản</th><th>Bot</th><th>Trạng thái</th></tr></thead><tbody>${rows.map(r => { const a = ACC[r.accountStatus] || ACC.NONE; return `<tr data-emr="${r.id}" style="cursor:pointer"><td><input type="checkbox" data-ck="${r.id}" ${st.sel.has(r.id) ? 'checked' : ''} aria-label="Chọn"></td><td>${esc(r.code || '')}</td><td><b>${esc(r.fullName || '')}</b><div class="small muted">${esc(r.phone || '')}</div></td><td>${esc(r.organizationUnitName || '')}</td><td>${esc(r.positionName || '')}</td><td>${r.hireDate ? U.date(r.hireDate) : ''}</td><td>${UI.tag(a[0], a[1])}${r.userName ? `<div class="small muted">${esc(r.userName)}</div>` : ''}</td><td>${r.botLinked ? UI.tag('Đã liên kết', 'green') : UI.tag('Chưa', '')}</td><td>${UI.tag(r.statusName || '', r.status === 'Active' ? 'green' : '')}${r.scheduledResignDate ? `<div class="small warn">Nghỉ từ ${U.date(r.scheduledResignDate)}</div>` : ''}</td></tr>`; }).join('')}</tbody></table></div></div>` : '<div class="empty">Không có nhân viên khớp bộ lọc.</div>');
        const ca = body.querySelector('#emAll'); if (ca) ca.onchange = () => { rows.forEach(r => ca.checked ? st.sel.add(r.id) : st.sel.delete(r.id)); draw(); };
        body.querySelectorAll('[data-ck]').forEach(c => { c.onclick = e => e.stopPropagation(); c.onchange = () => { c.checked ? st.sel.add(c.dataset.ck) : st.sel.delete(c.dataset.ck); bar(); const a2 = body.querySelector('#emAll'); if (a2) a2.checked = rows.every(r => st.sel.has(r.id)); }; });
        body.querySelectorAll('[data-emr]').forEach(tr => tr.onclick = () => APP.go('adm-employee-360?id=' + encodeURIComponent(tr.dataset.emr)));
        bar();
      };
      const load = () => Store.query('Employee/GetList', { status: st.status, organizationUnitId: st.unit || null, positionId: st.pos || null, keyword: st.q || null, flag: st.flag || null }).then(x => {
        if (!el.isConnected) return;
        if (!x.ok) { el.querySelector('#emBody').innerHTML = `<div class="empty">${esc(x.message || 'Không tải được danh sách')}</div>`; return; }
        const d = x.data || {}; rows = d.listItem || [];
        const sc = d.statusCounts || {}, cnt = k => k === 'ALL' ? d.totalCount : sc[k] || 0;
        el.querySelector('#emSt').innerHTML = STATUS.map(([k, l]) => `<button class="chip ${st.status === k ? 'active' : ''}" data-es="${k}">${l} · ${U.num(cnt(k))}</button>`).join(' ');
        el.querySelectorAll('[data-es]').forEach(b => b.onclick = () => { st.status = b.dataset.es; st.sel.clear(); load(); });
        const kp = (label, v, sub, icon, color, flag) => UI.kpi(label, v, sub, icon, color).replace("<article ", `<article data-fl="${flag}" style="cursor:pointer" `);   // thẻ con trực tiếp của .kpis
        el.querySelector('#emKpi').innerHTML = kp('Đang làm việc', U.num(d.workingCount || 0), `trên ${U.num(d.totalCount || 0)} hồ sơ`, 'users', 'green', '')
          + kp('Vào làm tháng này', U.num(d.newMonthCount || 0), '', 'calendar', 'blue', 'NEW_MONTH')
          + kp('Chưa có tài khoản', U.num(d.noAccountCount || 0), d.pendingAccountCount ? `${U.num(d.pendingAccountCount)} chờ IT duyệt` : '', 'key', 'orange', 'NO_ACCOUNT')
          + kp('Chưa liên kết bot', U.num(d.noBotCount || 0), '', 'msg', 'red', 'NO_BOT');
        el.querySelectorAll('[data-fl]').forEach(k => k.onclick = () => { st.flag = k.dataset.fl; if (st.flag) st.status = 'Active'; st.sel.clear(); re(); });
        el.querySelector('#emTabs').innerHTML = d.unlinkedCount ? `<div class="tabs c360-tabs mb"><button class="tab active">Danh sách</button><button class="tab" data-et="link">Đối chiếu tài khoản · ${U.num(d.unlinkedCount)}</button></div>` : '';
        const lt = el.querySelector('[data-et="link"]'); if (lt) lt.onclick = () => APP.go('adm-employee?tab=link');
        draw();
      });
      load();
    });
  });
  function codesDlg(d) {
    const list = d.listCode || [];
    UI.modal({ title: 'Mã liên kết bot (hạn 72 giờ)', size: 'lg', onMount: m => UI.fillQr && UI.fillQr(m),
      body: `${d.skipped ? `<div class="notice warn mb">${I('alert')}<div>Bỏ qua ${U.num(d.skipped)} người: ${(d.listSkipped || []).map(esc).join('; ')}</div></div>` : ''}${list.length ? `<div class="small muted mb">Đưa mã / QR cho từng người: mở Telegram → bấm liên kết (hoặc gửi mã cho bot). In trang này để phát tay.</div><div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:12px">${list.map(c => `<div class="card" style="padding:10px;text-align:center"><b>${esc(c.fullName || '')}</b><div class="small muted">${esc(c.code || '')}</div>${c.deepLink && UI.qrSlot ? `<div class="tg-qr">${UI.qrSlot(c.deepLink, 140)}</div>` : ''}<div style="font-size:18px;font-weight:800;letter-spacing:2px">${esc(c.linkCode || '')}</div><div class="small muted">hết hạn ${esc(U.dt ? U.dt(c.expiresAt) : c.expiresAt)}</div></div>`).join('')}</div>` : '<div class="empty">Không tạo mã nào.</div>'}`,
      foot: `<button class="btn" data-close>Đóng</button>${list.length ? '<button class="btn primary" id="cdPrint">In danh sách mã</button>' : ''}`,
      onMount2: null });
    const p = document.querySelector('#cdPrint'); if (p) p.onclick = () => window.print();
  }
  function importDlg() {
    UI.modal({ title: 'Nhập nhân viên từ Excel', size: 'md',
      body: `<ol class="small"><li>Tải file mẫu, điền mỗi dòng một người: Họ tên*, Mã đơn vị, Mã chức vụ, Điện thoại, Email, Ngày vào làm (yyyy-MM-dd). Mã nhân viên trống → hệ thống tự cấp.</li><li>Nạp file. Dòng lỗi (thiếu họ tên, số điện thoại sai / trùng người đang làm…) được bỏ qua và liệt kê sau khi nạp.</li></ol><input type="file" id="ixF" hidden accept=".xlsx,.xlsm">`,
      foot: '<button class="btn" id="ixT">Tải file mẫu</button><button class="btn primary" id="ixGo">Chọn file và nạp</button>',
      onMount: (m, close) => {
        const inp = m.querySelector('#ixF');
        m.querySelector('#ixT').onclick = () => Store.download('ImportExcel/GetTemplate', { tableName: 'Employee' });
        m.querySelector('#ixGo').onclick = () => inp.click();
        inp.onchange = () => { const file = inp.files && inp.files[0]; inp.value = ''; if (!file) return; if (!/\.(xlsx|xlsm)$/i.test(file.name)) return UI.toast('Chỉ nhận tệp .xlsx / .xlsm', 'err');
          const fd = new FormData(); fd.append('TableName', 'Employee'); fd.append('File', file);
          Store.act('ImportExcel/Import', { json: fd, tables: ['Employee'] }).then(r => { if (!r.ok) return; close(); if (APP.importResult) APP.importResult('Nhân viên', r.data || {}); APP.refresh(); }); };
      } });
  }

  // ================= Form Thêm / Sửa nhân viên =================
  let posGroups = null;
  const groupsFor = posId => { const p = Store.get('Position', posId); if (!p || !posGroups) return []; return posGroups[p.Code] || posGroups[String(p.Code || '').toUpperCase()] || []; };
  APP.employeeForm = (row, { onSaved } = {}) => {
    const edit = !!(row && row.Id);
    Promise.all([Store.ensure('OrganizationUnit'), Store.ensure('Position'), posGroups ? null : Store.query('Employee/GetPositionGroups').then(x => { posGroups = (x.ok && x.data) || {}; })]).then(() => {
      const r = row || {}, g = ref('Gender'), today = U.day(Store.ctx.date || new Date());
      let unit = r.OrganizationUnitId || '', phoneOk = true, tp;
      UI.modal({ title: edit ? 'Sửa nhân viên' : 'Thêm nhân viên', sub: edit ? esc((r.Code || '') + ' · ' + (r.FullName || '')) : 'Mã nhân viên do hệ thống cấp', size: 'lg',
        body: `<div class="panel-title">Thông tin cá nhân</div><div class="form-grid">
            <div class="field"><label>Họ tên *</label><input class="input" id="efN" maxlength="500" value="${esc(r.FullName || '')}"></div>
            <div class="field"><label>Điện thoại *</label><input class="input" id="efP" maxlength="50" inputmode="tel" value="${esc(r.Phone || '')}"><div class="small" id="efPm"></div></div>
            <div class="field"><label>Giới tính</label><select class="select" id="efG"><option value="">—</option>${g.map(x => `<option value="${esc(x.code)}" ${String(r.Gender || '').toUpperCase() === x.code ? 'selected' : ''}>${esc(x.name)}</option>`).join('')}</select></div>
            <div class="field"><label>Ngày sinh</label><input class="input" type="date" id="efB" value="${r.DateOfBirth ? U.day(r.DateOfBirth) : ''}"></div>
            <div class="field"><label>Email</label><input class="input" type="email" id="efE" maxlength="200" value="${esc(r.Email || '')}"></div>
            <div class="field"><label>Địa chỉ</label><input class="input" id="efA" maxlength="1000" value="${esc(r.Address || '')}"></div></div>
          <div class="panel-title mt">Công việc</div><div class="form-grid">
            <div class="field"><label>Đơn vị *</label><span id="efU"></span></div>
            <div class="field"><label>Chức vụ *</label><select class="select" id="efPo"><option value="">— Chọn chức vụ —</option>${Store.rows('Position').filter(p => !p.IsDeleted).map(p => `<option value="${p.Id}" ${p.Id === r.PositionId ? 'selected' : ''}>${esc(p.Name || p.Code || '')}</option>`).join('')}</select></div>
            <div class="field"><label>Ngày vào làm *</label><input class="input" type="date" id="efH" value="${r.HireDate ? U.day(r.HireDate) : today}"></div></div>
          ${edit ? '<div class="small muted mt">Đổi trạng thái (Tạm nghỉ / Quay lại làm / Cho nghỉ việc) bằng nút trên hồ sơ.</div>' : `<div class="panel-title mt">Tài khoản & bot</div>
            <label class="row-flex" style="gap:6px"><input type="checkbox" id="efAcc"> Cấp tài khoản web</label><div class="small muted" id="efGr" style="margin-left:22px"></div>
            <label class="row-flex mt" style="gap:6px"><input type="checkbox" id="efBot" checked> Tạo mã liên kết bot (hiện mã / QR sau khi lưu)</label>`}`,
        foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="efGo">${edit ? 'Lưu' : 'Lưu & mở hồ sơ'}</button>`,
        onMount: (m, close) => {
          APP.unitPicker(m.querySelector('#efU'), { value: unit, allowEmpty: false, placeholder: 'Tìm đơn vị…', id: 'efUi', onChange: v => { unit = v; } });
          const v = id => m.querySelector('#' + id).value.trim(), pm = m.querySelector('#efPm'), go = m.querySelector('#efGo');
          const check = () => { const p = v('efP'); if (!p) { phoneOk = false; pm.innerHTML = ''; return; }
            Store.query('Employee/CheckPhone', { phone: p, exceptId: edit ? r.Id : null }).then(x => { if (!m.isConnected || v('efP') !== p) return; const d = x.data || {}; phoneOk = !!d.valid; pm.className = 'small ' + (d.valid ? 'muted' : 'bad'); pm.textContent = d.valid ? 'Số hợp lệ' : (d.message || ''); go.disabled = !phoneOk; }); };
          m.querySelector('#efP').oninput = () => { clearTimeout(tp); tp = setTimeout(check, 350); };
          if (r.Phone) check(); else phoneOk = false;
          const gr = () => { const box = m.querySelector('#efGr'); if (!box) return; const on = m.querySelector('#efAcc').checked, list = groupsFor(v('efPo')); box.innerHTML = on ? (list.length ? `Nhóm quyền gợi ý: <b>${esc(list.join(', '))}</b> (IT đổi được khi duyệt)` : 'Chức vụ này chưa có nhóm quyền gợi ý – IT chọn khi duyệt') : ''; };
          if (!edit) { m.querySelector('#efAcc').onchange = gr; m.querySelector('#efPo').addEventListener('change', gr); }
          go.onclick = () => {
            if (!v('efN')) return UI.toast('Nhập họ tên', 'err'); if (!v('efP')) return UI.toast('Nhập số điện thoại', 'err'); if (!phoneOk) return UI.toast(pm.textContent || 'Số điện thoại chưa hợp lệ', 'err');
            if (!unit) return UI.toast('Chọn đơn vị', 'err'); if (!v('efPo')) return UI.toast('Chọn chức vụ', 'err'); if (!v('efH')) return UI.toast('Chọn ngày vào làm', 'err');
            const form = { FullName: v('efN'), Phone: v('efP'), Gender: v('efG'), DateOfBirth: v('efB'), Email: v('efE'), Address: v('efA'), OrganizationUnitId: unit, PositionId: v('efPo'), HireDate: v('efH') };
            if (edit) form.Id = r.Id;
            const acc = !edit && m.querySelector('#efAcc').checked, bot = !edit && m.querySelector('#efBot').checked;
            if (acc) { form.RequestWebAccount = true; form.AccountGroups = groupsFor(v('efPo')).join(','); }
            go.disabled = true;
            Store.act(edit ? 'Employee/Update' : 'Employee/Insert', { form, method: edit ? 'PUT' : 'POST', tables: ['Employee', 'ApprovalRequest'], quiet: true }).then(res => {
              go.disabled = false;
              if (!res.ok) return UI.toast(esc(res.message || 'Không lưu được'), 'err');
              close(); UI.toast(esc((res.res && (res.res.responseMessage || res.res.message)) || 'Đã lưu'), 'ok');
              if (edit) { onSaved ? onSaved() : APP.refresh(); return; }
              Store.query('Employee/GetList', { status: 'Active', keyword: form.Phone }).then(x => {
                const hit = ((x.data && x.data.listItem) || []).find(e => e.fullName === form.FullName) || ((x.data && x.data.listItem) || [])[0];
                if (!hit) return APP.refresh();
                const open = () => APP.go('adm-employee-360?id=' + hit.id);
                if (!bot) return open();
                Store.act('TelegramLink/CreateCodeForEmployee', { form: { EmployeeId: hit.id } }).then(t => { open(); if (t.ok && !t.demo && APP.showTelegramCode) setTimeout(() => APP.showTelegramCode(t.data), 400); });
              });
            });
          };
        } });
    });
  };

  // ================= Hồ sơ: cho nghỉ việc, tạm nghỉ, quay lại =================
  APP.employeeResign = (emp, done) => {
    // Mặc định hôm nay, không sớm hơn ngày vào làm (giờ máy có thể lệch ngày với máy chủ).
    const hire = emp.HireDate ? U.day(emp.HireDate) : '', t0 = U.day(Store.ctx.date || new Date()), today = hire && hire > t0 ? hire : t0;
    UI.modal({ title: 'Cho nghỉ việc – ' + esc(emp.FullName || ''), size: 'md',
      body: `<div class="form-grid"><div class="field"><label>Ngày nghỉ việc *</label><input class="input" type="date" id="rsD" value="${today}"></div>
        <div class="field"><label>Lý do *</label><select class="select" id="rsR"><option value="">— Chọn lý do —</option>${ref('EmployeeResignReason').map(x => `<option value="${esc(x.code)}">${esc(x.name)}</option>`).join('')}</select></div>
        <div class="field full"><label>Ghi chú</label><input class="input" id="rsN" maxlength="1000"></div></div>
        <div class="panel-title mt">Hệ thống sẽ</div><div id="rsP" class="small muted">Đang tính…</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn danger" id="rsGo">Xác nhận nghỉ việc</button>',
      onMount: (m, close) => {
        const pv = () => Store.query('Employee/PreviewResign', { id: emp.Id, resignDate: m.querySelector('#rsD').value }).then(x => {
          const box = m.querySelector('#rsP'); if (!box) return;
          box.innerHTML = x.ok ? `<ul style="margin:0;padding-left:18px">${((x.data && x.data.listMessage) || []).map(t => `<li>${esc(t)}</li>`).join('')}</ul>` : `<span class="bad">${esc(x.message || '')}</span>`;
        });
        m.querySelector('#rsD').onchange = pv; pv();
        m.querySelector('#rsGo').onclick = () => { const d = m.querySelector('#rsD').value, r = m.querySelector('#rsR').value; if (!d) return UI.toast('Chọn ngày nghỉ việc', 'err'); if (!r) return UI.toast('Chọn lý do nghỉ việc', 'err');
          Store.act('Employee/Resign', { json: { id: emp.Id, resignDate: d, reasonCode: r, note: m.querySelector('#rsN').value.trim() }, tables: TABLES }).then(x => { if (!x.ok) return; close(); UI.toast(esc(x.res && x.res.responseMessage || 'Đã cho nghỉ việc'), 'ok'); done && done(); }); };
      } });
  };
  APP.employeeSetStatus = (emp, status, done) => UI.confirm(status === 'Inactive'
    ? `Chuyển <b>${esc(emp.FullName || '')}</b> sang Tạm nghỉ? Tài khoản web và bot tạm khóa cho tới khi quay lại làm.`
    : `Cho <b>${esc(emp.FullName || '')}</b> quay lại làm? Nếu có tài khoản đang khóa, hệ thống gửi yêu cầu mở lại cho IT.`, note =>
    Store.act('Employee/SetStatus', { json: { id: emp.Id, status, note }, tables: TABLES }).then(x => { if (!x.ok) return; UI.toast(esc(x.res && x.res.responseMessage || 'Đã cập nhật'), 'ok'); done && done(); }),
    { ok: status === 'Inactive' ? 'Tạm nghỉ' : 'Quay lại làm', input: 'Ghi chú (không bắt buộc)' });
})();
