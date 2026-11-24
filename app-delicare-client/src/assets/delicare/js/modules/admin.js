/* P0 – Ma trận phân quyền UserGroup × ObjectMenu, Cây đơn vị tổ chức */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ps = { g: null };
  const FLAGS = [['AllowAccess', 'Truy cập'], ['AllowAdd', 'Thêm'], ['AllowEdit', 'Sửa'], ['AllowDelete', 'Xóa'], ['AllowPrint', 'In'], ['AllowExport', 'Xuất'], ['AllowImport', 'Nhập']];
  const LIVE = () => Store.isLive;
  // Tài khoản đang đăng nhập có cờ quản trị (User.IsAdmin) – một số action backend chỉ cho quản trị gọi.
  const isAdmin = () => !!(window.DC_HOST && window.DC_HOST.user && window.DC_HOST.user.isAdmin);
  // Action ghi dùng HTTP GET của khung cũ (User/SaveIsActive, User/ResetPassword): Store.act chỉ gửi POST / PUT / DELETE nên đi qua Store.query.
  // opt: { tables: bảng tải lại khi thành công, demo: thao tác thay thế ở chế độ DEMO }. Lỗi đã hiện toast.
  const getAct = (path, params, opt = {}) => {
    if (!Store.isLive) { if (opt.demo) opt.demo(); return Promise.resolve({ ok: true, demo: true }); }
    return Store.query(path, params).then(x => {
      if (!x.ok) { UI.toast(`<b>${esc(path)}</b>: ${esc(x.message || 'Lỗi API')}`, 'err'); return x; }
      return Store.reload(opt.tables).then(() => x);
    });
  };
  // Kết quả đọc qua action riêng giữ 60 giây theo khóa + dấu dữ liệu (màn hình vẽ lại nhiều lần khi các bảng tải xong).
  const cache = {};
  const cached = (key, sig, load) => {
    const c = cache[key]; if (c && c.sig === sig && Date.now() - c.at < 60000) return c.p;
    const p = load(); cache[key] = { sig, at: Date.now(), p };
    p.then(x => { if (!x.ok && cache[key] && cache[key].p === p) delete cache[key]; });
    return p;
  };
  // Bảng quyền chỉ đọc từ cây menu của API (UserRole/GetPreview, UserRole/GetByUserId): [{ objectMenuName, allowAccess…, children }]
  const flagKey = k => k.charAt(0).toLowerCase() + k.slice(1);
  const permTable = list => {
    const row = (m, lvl) => `<tr><td class="l" style="${lvl ? 'padding-left:24px' : 'font-weight:800;background:#F3F6EF'}">${esc(m.objectMenuName || m.shortName || '')}</td>${FLAGS.map(fl => `<td ${lvl ? '' : 'style="background:#F3F6EF"'}>${m[flagKey(fl[0])] ? '<b>✓</b>' : '<span class="muted">–</span>'}</td>`).join('')}</tr>` + (m.children || []).map(c => row(c, 1)).join('');
    return list && list.length ? `<div class="table-wrap" style="max-height:460px"><table class="matrix" style="width:100%"><thead><tr><th class="l">Chức năng</th>${FLAGS.map(x => `<th>${x[1]}</th>`).join('')}</tr></thead><tbody>${list.map(m => row(m, 0)).join('')}</tbody></table></div>` : '<div class="empty">Chưa được cấp quyền truy cập chức năng nào</div>';
  };
  // Xem trước menu + quyền một nhóm nhận được và thành viên của nhóm (chỉ quản trị).
  const openGroupPreview = g => Store.query('UserRole/GetPreview', { userGroupId: g.Id }).then(x => {
    if (!x.ok) { UI.toast(esc(x.message || 'Không tải được quyền của nhóm'), 'err'); return; }
    const d = x.data || {}, us = d.listUser || [];
    UI.modal({ title: 'Xem trước quyền – ' + esc(d.userGroupName || g.Name || ''), sub: `${d.screenCount || 0} màn hình được truy cập · ${us.length} tài khoản`, size: 'lg', body: `<div class="small muted mb">Tài khoản thuộc nhóm: ${us.map(u => esc(u.userName || '')).join(', ') || '—'}</div>${permTable(d.listMenu)}` });
  });
  // Quyền hiệu lực của một tài khoản (cộng dồn mọi nhóm; quản trị = mọi quyền).
  const openUserPermission = u => Store.query('UserRole/GetByUserId', { userId: u.Id }).then(x => {
    if (!x.ok) { UI.toast(esc(x.message || 'Không tải được quyền của tài khoản'), 'err'); return; }
    UI.modal({ title: 'Quyền hiệu lực – ' + esc(u.UserName || ''), sub: u.IsAdmin ? 'Tài khoản quản trị: có mọi quyền' : 'Cộng dồn quyền của mọi nhóm tài khoản thuộc về', size: 'lg', body: permTable((x.data || {}).listMenu) });
  });
  APP.register('adm-permission', (el, f) => {
    const groups = Store.rows('UserGroup'); if (!ps.g || !Store.get('UserGroup', ps.g)) ps.g = (Store.one('UserGroup', 'Name', 'Bếp trưởng') || groups[0] || {}).Id;
    if (!ps.g) { el.innerHTML = APP.header(f, '') + UI.emptyData(['UserGroup'], 'Chưa có nhóm người dùng nào – tạo nhóm ở màn hình Nhóm người dùng trước khi phân quyền.'); return; }
    const g = Store.get('UserGroup', ps.g);
    // MENU-4.3 (c): ma trận không hiện mục menu đã ẩn (IsActive = 0) – quyền của bảng con theo màn cha.
    const menus = Store.rows('ObjectMenu').filter(m => m.IsActive !== false && m.IsActive !== 0);
    // APR-1.1 (D-81 a): mục "Phê duyệt › …" (#/wf-inbox?type=…) là QUYỀN DUYỆT (DB giữ IsActive = 1) – gom vào khối "Quyền duyệt" riêng, không nằm trong cây menu.
    const isApr = m => String(m.MetaLink || '').startsWith('#/wf-inbox?type=');
    const aprItems = menus.filter(isApr).sort((a, b) => a.OrderShow - b.OrderShow);
    const roots = menus.filter(m => !m.ParentId && m.MetaLink !== '#/group/approve').sort((a, b) => a.OrderShow - b.OrderShow);
    const aprLevel = m => { const x = /cấp\s*(\d)/i.exec(m.Name || ''); return x ? 'Cấp ' + x[1] : m.MetaLink === '#/wf-inbox?type=wf-inbox' ? 'Chỉ xem' : '1 cấp'; };
    const ur = Store.by('UserRole', 'UserGroupId', g.Id);
    const users = Store.by('UserClaim', 'UserGroupId', g.Id).map(c => Store.get('User', c.UserId)).filter(Boolean);
    const canEdit = Store.perm(f.id).edit !== false;
    el.innerHTML = APP.header(f, LIVE() ? `${canEdit && groups.length > 1 ? `<button class="btn" id="pCopy">${I('layers')} Sao chép quyền từ nhóm khác</button>` : ''}${isAdmin() ? `<button class="btn" id="pPreview">${I('eye')} Xem trước quyền của nhóm</button>` : ''}` : `<button class="btn" id="pView">${I('eye')} Xem mockup với vai trò này</button>`, { sub: 'Mỗi nhóm quyền có quyền Xem / Thêm / Sửa / Xóa / In / Xuất / Nhập trên từng mục menu. Tích ô để cấp / thu hồi quyền – có hiệu lực ngay cho menu và nút bấm.' }) + `
    <div class="grid" style="grid-template-columns:260px minmax(0,1fr)"><div class="card panel"><div class="panel-title mb">Nhóm quyền</div>${groups.map(x => `<div class="titem ${x.Id === g.Id ? 'active' : ''}" data-g="${x.Id}"><div><b style="font-family:inherit">${esc(x.Name)}</b><small>${Store.by('UserRole', 'UserGroupId', x.Id).filter(r => r.AllowAccess).length} chức năng · ${Store.by('UserClaim', 'UserGroupId', x.Id).length} tài khoản</small></div></div>`).join('')}</div>
      <div class="card panel"><div class="spread mb"><div><h2>${esc(g.Name)}</h2><div class="small muted">Tài khoản: ${users.map(u => esc(Store.label('User', u))).join(', ') || '—'}</div></div><div class="row-flex"><button class="btn sm" data-all="1">Cấp toàn bộ truy cập</button><button class="btn sm danger" data-all="0">Thu hồi toàn bộ</button></div></div>
        <div class="table-wrap" style="max-height:600px"><table class="matrix" style="width:100%"><thead><tr><th class="l">Chức năng</th><th class="l">Đường dẫn</th>${FLAGS.map(x => `<th>${x[1]}</th>`).join('')}</tr></thead><tbody>${roots.map(r => `<tr><td class="l" colspan="${FLAGS.length + 2}" style="background:#F3F6EF;font-weight:800">${esc(r.Name)}</td></tr>${menus.filter(m => m.ParentId === r.Id && !isApr(m)).sort((a, b) => a.OrderShow - b.OrderShow).map(m => { const x = ur.find(u => u.ObjectMenuId === m.Id) || {}; const dup = menus.filter(o => o.ParentId && o.Name === m.Name).length > 1; return `<tr><td class="l">${esc(m.Name)}${dup ? ` <span class="small muted">· ${esc(r.Name)}</span>` : ""}</td><td class="l mono small">${esc(m.MetaLink)}</td>${FLAGS.map(fl => `<td><input type="checkbox" data-m="${m.Id}" data-fl="${fl[0]}" ${x[fl[0]] ? 'checked' : ''} aria-label="${fl[1]} ${esc(m.Name)} – ${esc(r.Name)}"></td>`).join('')}</tr>`; }).join('')}`).join('')}</tbody></table></div>
        ${aprItems.length ? `<h3 class="mt">Quyền duyệt</h3><div class="small muted mb">Mỗi loại 1 ô: tick = được duyệt / từ chối loại đó ở màn Phê duyệt (quyền Truy cập + Sửa trên mục quyền duyệt). "Tất cả yêu cầu duyệt" = chỉ xem mọi yêu cầu, không duyệt được.</div>
        <div class="table-wrap"><table class="matrix" style="width:100%"><thead><tr><th class="l">Loại duyệt</th><th class="l">Cấp</th><th>Được duyệt</th></tr></thead><tbody>${aprItems.map(m => { const x = ur.find(u => u.ObjectMenuId === m.Id) || {}; const viewOnly = m.MetaLink === '#/wf-inbox?type=wf-inbox'; const on = viewOnly ? !!x.AllowAccess : !!(x.AllowAccess && x.AllowEdit); return `<tr><td class="l">${esc(m.Name)}</td><td class="l small">${aprLevel(m)}</td><td><input type="checkbox" data-apr="${m.Id}" data-view="${viewOnly ? 1 : 0}" ${on ? 'checked' : ''} ${canEdit ? '' : 'disabled'} aria-label="${viewOnly ? 'Xem' : 'Duyệt'} ${esc(m.Name)}"></td></tr>`; }).join('')}</tbody></table></div>` : ''}</div></div>`;
    el.querySelectorAll('[data-g]').forEach(b => b.onclick = () => { ps.g = b.dataset.g; APP.refresh(); });
    const setFlag = (mid, fl, v) => { let x = Store.rows('UserRole').find(u => u.UserGroupId === g.Id && u.ObjectMenuId === mid); if (!x) x = Store.add('UserRole', { UserGroupId: g.Id, ObjectMenuId: mid, AllowAccess: 0, AllowAdd: 0, AllowEdit: 0, AllowDelete: 0, AllowPrint: 0, AllowExport: 0, AllowImport: 0 }); const patch = { [fl]: v }; if (fl === 'AllowAccess' && !v) FLAGS.forEach(f2 => patch[f2[0]] = 0); if (fl !== 'AllowAccess' && v) patch.AllowAccess = 1; Store.update('UserRole', x.Id, patch, 'Phân quyền ' + g.Name); };
    // APR-1.1: 1 ô quyền duyệt = Truy cập + Sửa (bỏ tick → thu cả 2); "Tất cả yêu cầu duyệt" = chỉ Truy cập.
    el.querySelectorAll('input[data-apr]').forEach(c => c.onchange = () => { const view = c.dataset.view === '1'; if (c.checked) setFlag(c.dataset.apr, view ? 'AllowAccess' : 'AllowEdit', 1); else setFlag(c.dataset.apr, 'AllowAccess', 0); UI.toast(`${c.checked ? 'Cấp' : 'Thu hồi'} quyền ${view ? 'xem' : 'duyệt'} – UserRole (ghi AuditChange)`, 'ok'); APP.refresh(); });
    el.querySelectorAll('input[data-m]').forEach(c => c.onchange = () => { setFlag(c.dataset.m, c.dataset.fl, c.checked ? 1 : 0); UI.toast(`${c.checked ? 'Cấp' : 'Thu hồi'} ${c.dataset.fl} – UserRole (ghi AuditChange)`, 'ok'); APP.refresh(); });
    el.querySelectorAll('[data-all]').forEach(b => b.onclick = () => UI.confirm(`${b.dataset.all === '1' ? 'Cấp' : 'Thu hồi'} quyền truy cập toàn bộ ${menus.filter(m => m.ParentId).length} chức năng cho "${esc(g.Name)}"?${b.dataset.all === '0' ? '<div class="small muted mt">Mọi quyền Thêm / Sửa / Xóa / In / Xuất / Nhập của nhóm cũng bị tắt.</div>' : ''}`, () => Store.act('UserRole/SaveAccessAll', { form: { UserGroupId: g.Id, AllowAccess: b.dataset.all === '1' }, tables: ['UserRole'], demo: () => menus.filter(m => m.ParentId).forEach(m => setFlag(m.Id, 'AllowAccess', +b.dataset.all)) }).then(x => { if (!x.ok) return; UI.toast('Đã cập nhật ma trận quyền', 'ok'); APP.refresh(); }), { danger: b.dataset.all === '0' }));
    const pPreview = el.querySelector('#pPreview'); if (pPreview) pPreview.onclick = () => openGroupPreview(g);
    const pCopy = el.querySelector('#pCopy'); if (pCopy) pCopy.onclick = () => UI.modal({
      title: 'Sao chép quyền sang nhóm ' + esc(g.Name), size: 'sm',
      body: `<div class="field"><label>Lấy quyền từ nhóm</label><select class="select" id="cpSrc">${groups.filter(x => x.Id !== g.Id).map(x => `<option value="${esc(x.Id)}">${esc(x.Name)}</option>`).join('')}</select></div><div class="small muted mt">Toàn bộ quyền hiện tại của nhóm "${esc(g.Name)}" sẽ được thay bằng quyền của nhóm nguồn.</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="cpOk">Sao chép</button>',
      onMount: (m, close) => m.querySelector('#cpOk').onclick = () => {
        const src = Store.get('UserGroup', m.querySelector('#cpSrc').value); if (!src) return; close();
        UI.confirm(`Thay toàn bộ quyền của nhóm <b>${esc(g.Name)}</b> bằng quyền của nhóm <b>${esc(src.Name)}</b>?`, () => Store.act('UserRole/CopyFromUserGroup', { form: { SourceUserGroupId: src.Id, TargetUserGroupId: g.Id }, tables: ['UserRole'] }).then(x => { if (!x.ok) return; UI.toast('Đã sao chép quyền từ nhóm ' + esc(src.Name), 'ok'); APP.refresh(); }), { danger: true, ok: 'Sao chép' });
      }
    });
    const pView = el.querySelector('#pView'); if (pView) pView.onclick = () => { Store.user.group = g.Name; const uc = Store.one('UserClaim', 'UserGroupId', g.Id); Store.user.id = uc ? uc.UserId : null; Store.user.label = uc && Store.get('User', uc.UserId) ? Store.get('User', uc.UserId).UserName : 'demo'; APP._setUserUi(); APP._buildNav(); APP.go('dash-exec'); UI.toast('Đang xem với vai trò ' + esc(g.Name) + ' – menu đã lọc theo UserRole. Bấm avatar để đổi lại.', 'ok'); };
  });
  // ---------- Cây đơn vị tổ chức ----------
  APP.register('adm-orgunit', (el, f) => {
    const ous = Store.rows('OrganizationUnit');
    const node = p => ous.filter(o => (o.ParentId || null) === p).map(o => `<div class="tl ${o.UnitType === 'BRANCH' ? 'blue' : o.UnitType === 'WAREHOUSE' ? 'orange' : ''}"><b><a href="javascript:void 0" data-open="OrganizationUnit" data-id="${o.Id}">${esc(o.Name)}</a> ${UI.tag(o.UnitType)} <span class="mono small muted">${esc(o.Code)}</span></b><small>Quản lý: ${esc(o.ManagerId ? Store.label('Employee', o.ManagerId) : '—')} · ${Store.by('Employee', 'OrganizationUnitId', o.Id).length} nhân viên · ${Store.by('Warehouse', 'OrganizationUnitId', o.Id).length} kho</small>${ous.some(c => c.ParentId === o.Id) ? `<div class="timeline" style="margin-top:8px">${node(o.Id)}</div>` : ''}</div>`).join('');
    if (LIVE()) {
      // Chế độ dữ liệu thật: cây + tên quản lý + số nhân viên / số kho do server tính (không kéo bảng Employee, Warehouse về trình duyệt).
      const color = t => t === 'BRANCH' ? 'blue' : t === 'WAREHOUSE' ? 'orange' : '';
      const liveNode = list => (list || []).map(o => `<div class="tl ${color(o.unitType)}"><b><a href="javascript:void 0" data-open="OrganizationUnit" data-id="${esc(o.id)}">${esc(o.name || '')}</a> ${o.unitType ? UI.tag(o.unitType) : ''} <span class="mono small muted">${esc(o.code || '')}</span></b><small>Quản lý: ${esc(o.managerName || '—')} · ${o.employeeCount || 0} nhân viên · ${o.warehouseCount || 0} kho</small>${o.children && o.children.length ? `<div class="timeline" style="margin-top:8px">${liveNode(o.children)}</div>` : ''}</div>`).join('');
      APP.genericList(el, f, { top: `<div class="card panel mb"><div class="panel-title mb">${esc((Store.rows('Company')[0] || {}).Name || 'Công ty')} – cây đơn vị</div><div class="timeline" id="ouTree"><div class="muted small">Đang tải cây đơn vị…</div></div></div>` });
      const sig = ous.map(o => o.Id + '|' + (o.ParentId || '') + '|' + (o.ManagerId || '') + '|' + (o.ModifiedDate || '')).join(',');
      cached('orgTree', sig, () => Store.query('OrganizationUnit/GetTree')).then(x => {
        const host = el.querySelector('#ouTree'); if (!host || !host.isConnected) return;
        const tree = x.ok ? (Array.isArray(x.data) ? x.data : x.list) : null;
        host.innerHTML = (tree ? liveNode(tree) : node(null)) || '<div class="muted small">Chưa có đơn vị nào</div>';
      });
      return;
    }
    APP.genericList(el, f, { top: `<div class="card panel mb"><div class="panel-title mb">${esc((Store.rows('Company')[0] || {}).Name || 'Công ty')} – cây đơn vị (OrganizationUnit.ParentId)</div><div class="notice warn small">${I('alert')}<div>ParentId → OrganizationUnit và ManagerId → Employee là quan hệ ĐỀ XUẤT (chưa có trong danh sách quan hệ xác nhận). PO xác nhận OrganizationUnit là nguồn Chi nhánh / Phòng ban.</div></div><div class="timeline">${node(null)}</div></div>` });
  });
  // ---------- Tài khoản người dùng: khóa / mở, đặt lại mật khẩu, quyền hiệu lực ----------
  // UG-1 C-06: mật khẩu tạm hiện MỘT lần (không lưu, không ghi log) + nút sao chép – dùng cho Tạo người dùng và Đặt lại mật khẩu.
  function showTempPassword(name, pw) {
    UI.modal({
      title: 'Mật khẩu tạm', size: 'sm',
      body: `<p style="margin-top:0">Mật khẩu tạm của <b>${name}</b> – hết hạn sau 24 giờ, người dùng phải đổi khi đăng nhập. Hộp này chỉ hiện một lần:</p><div class="row-flex"><input class="input mono" id="tmpPw" readonly value="${esc(pw)}" style="flex:1"><button class="btn primary" id="tmpCopy">Sao chép</button></div>`,
      onMount: m => m.querySelector('#tmpCopy').onclick = () => {
        const inp = m.querySelector('#tmpPw'); inp.select();
        const done = () => UI.toast('Đã sao chép mật khẩu tạm', 'ok');
        const legacy = () => { try { if (document.execCommand('copy')) done(); } catch (e) { /* người dùng tự chép từ ô đã bôi đen */ } };
        if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(inp.value).then(done, legacy); else legacy();
      }
    });
  }
  // UG-1 C-06: chọn nhiều nhóm quyền (UserGroup) – ô tick.
  const groupPicker = selected => {
    const sel = new Set((selected || []).map(String));
    const groups = Store.rows('UserGroup').slice().sort((a, b) => String(a.Name || '').localeCompare(String(b.Name || '')));
    return `<div class="field full"><label>Nhóm quyền</label><div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:6px;max-height:260px;overflow:auto;border:1px solid var(--line);border-radius:10px;padding:10px">${groups.map(g => `<label class="row-flex small" style="gap:6px;font-weight:400"><input type="checkbox" data-grp value="${esc(g.Id)}" ${sel.has(String(g.Id)) ? 'checked' : ''}> ${esc(g.Name || '')}${g.Description ? ` <span class="muted">· ${esc(g.Description)}</span>` : ''}</label>`).join('') || '<span class="muted small">Chưa có nhóm người dùng</span>'}</div></div>`;
  };
  const pickedGroups = m => [...m.querySelectorAll('[data-grp]:checked')].map(i => i.value).join(',');
  function createUser() {
    const persons = Store.rows('Person').slice(0, 1000);
    UI.modal({
      title: 'Tạo người dùng', size: 'md',
      body: `<div class="form-grid">
        <div class="field"><label>Tên đăng nhập <span class="req">*</span></label><input class="input" id="nuName" maxlength="50" placeholder="vd. an.nguyen"><span class="hint">Chữ thường, không dấu; trùng sẽ báo lỗi.</span></div>
        <div class="field"><label>Hồ sơ người</label><select class="select" id="nuPerson"><option value="">— Không gắn —</option>${persons.map(x => `<option value="${esc(x.Id)}">${esc(x.FullName || x.Id)}</option>`).join('')}</select>${LIVE() ? `<button type="button" class="btn sm mt" id="nuFromEmp">${I('users')} Lấy từ nhân viên</button>` : ''}</div>
        <div class="field full hide" id="nuEmpBox"></div>
        ${groupPicker([])}
        <div class="field"><label class="row-flex" style="gap:6px"><input type="checkbox" id="nuActive" checked> Hoạt động</label></div></div><div id="nuErr" class="notice warn hide"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="nuOk">Tạo người dùng</button>',
      onMount: (m, close) => {
        // T45R.8 (D-38): "Lấy từ nhân viên" – chọn Employee để tạo hồ sơ người mới (Person/Insert tạo kèm đúng 1 tài khoản);
        // chỉ điền dữ liệu, không lưu liên kết Employee ↔ Person (DB không có cột – GAP-ADM-03).
        let emp = null;
        const box = m.querySelector('#nuEmpBox'), selPerson = m.querySelector('#nuPerson');
        const showEmp = () => {
          if (!emp) { box.classList.add('hide'); box.innerHTML = ''; selPerson.disabled = false; return; }
          selPerson.value = ''; selPerson.disabled = true;
          box.classList.remove('hide');
          box.innerHTML = `<div class="notice info small">${I('users')}<div><b>Tạo hồ sơ mới từ nhân viên ${esc(emp.code || '')} – ${esc(emp.fullName || '')}</b>SĐT: ${esc(emp.phone || '–')} · Ngày sinh: ${emp.dateOfBirth ? esc(String(emp.dateOfBirth).slice(0, 10)) : '–'} · Giới tính: ${esc(emp.gender || '–')} · Địa chỉ: ${esc(emp.address || '–')} <button type="button" class="btn sm" id="nuEmpClr">Bỏ chọn</button></div></div>`;
          box.querySelector('#nuEmpClr').onclick = () => { emp = null; showEmp(); };
        };
        const suggest = full => { const w = String(full || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[đĐ]/g, 'd').toLowerCase().split(/\s+/).filter(Boolean).map(x => x.replace(/[^a-z0-9]/g, '')).filter(Boolean); return w.length > 1 ? w[w.length - 1] + '.' + w[0] : (w[0] || ''); };
        const fromEmp = m.querySelector('#nuFromEmp');
        if (fromEmp) fromEmp.onclick = () => UI.modal({
          title: 'Lấy từ nhân viên', size: 'md',
          body: `<div class="row-flex"><input class="input" id="feQ" placeholder="Gõ mã hoặc tên nhân viên (từ 2 ký tự)" style="flex:1"><button class="btn primary" id="feGo">Tìm</button></div><div id="feList" class="mt"></div>`,
          onMount: (mm, close2) => {
            const go = () => {
              const q = mm.querySelector('#feQ').value.trim(), list = mm.querySelector('#feList');
              if (q.length < 2) { list.innerHTML = '<div class="muted small">Nhập ít nhất 2 ký tự</div>'; return; }
              list.innerHTML = '<div class="muted small">Đang tìm…</div>';
              Store.query('Employee/GetAll', { SearchText: q, PageSize: 20, PageIndex: 1 }).then(r => {
                const rows = r.ok ? (r.list || []) : [];
                if (!r.ok) { list.innerHTML = `<div class="notice warn small">${esc(r.message || 'Không tìm được nhân viên')}</div>`; return; }
                list.innerHTML = rows.length ? `<div class="grid">${rows.map((e, i) => `<button class="btn" data-ei="${i}" style="justify-content:space-between">${esc(e.fullName || '')}<span class="small muted">${esc(e.code || '')}${e.phone ? ' · ' + esc(e.phone) : ''}</span></button>`).join('')}</div>` : '<div class="muted small">Không có nhân viên phù hợp</div>';
                list.querySelectorAll('[data-ei]').forEach(b => b.onclick = () => {
                  emp = rows[+b.dataset.ei]; close2(); showEmp();
                  const un = m.querySelector('#nuName'); if (!un.value.trim()) un.value = suggest(emp.fullName);
                });
              });
            };
            mm.querySelector('#feGo').onclick = go;
            mm.querySelector('#feQ').onkeydown = e => { if (e.key === 'Enter') go(); };
            mm.querySelector('#feQ').focus();
          }
        });
        m.querySelector('#nuOk').onclick = () => {
        const name = m.querySelector('#nuName').value.trim(), err = m.querySelector('#nuErr');
        if (!name) { err.textContent = 'Nhập tên đăng nhập'; err.classList.remove('hide'); return; }
        const btn = m.querySelector('#nuOk'); btn.disabled = true;
        const common = { UserName: name, IsActive: m.querySelector('#nuActive').checked, ListUserGroupId: pickedGroups(m) };
        const call = emp
          ? Store.act('Person/Insert', { form: Object.assign({ FullName: emp.fullName || '', PhoneNumber: emp.phone || '', DateOfBirth: emp.dateOfBirth ? String(emp.dateOfBirth).slice(0, 10) : '', Sex: emp.gender || '', Address: emp.address || '' }, common), tables: ['Person', 'User', 'UserClaim'], quiet: true })
          : Store.act('User/Insert', { form: Object.assign({ PersonId: selPerson.value }, common), tables: ['User', 'UserClaim'], quiet: true });
        call.then(r => {
          btn.disabled = false;
          if (!r.ok) { err.textContent = r.message || 'Không tạo được người dùng'; err.classList.remove('hide'); return; }
          close(); APP.refresh();
          const d = r.data || {};
          if (d.tempPassword) showTempPassword(esc(d.userName || name), d.tempPassword); else UI.toast('Đã tạo người dùng ' + esc(name), 'ok');
        });
        };
      }
    });
  }
  function assignGroups(r) {
    Store.query('User/GetById', { id: r.Id }).then(x => {
      if (!x.ok || !x.data) { UI.toast(esc(x.message || 'Không đọc được tài khoản'), 'err'); return; }
      const u = x.data, cur = String(u.listUserGroupId || '').split(',').filter(Boolean);
      UI.modal({
        title: 'Gán nhóm – ' + esc(r.UserName || ''), size: 'md', body: groupPicker(cur) + '<div id="agErr" class="notice warn hide"></div>',
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="agOk">Lưu nhóm</button>',
        onMount: (m, close) => m.querySelector('#agOk').onclick = () => {
          const ids = pickedGroups(m), err = m.querySelector('#agErr');
          if (!ids) { err.textContent = 'Chọn ít nhất 1 nhóm (muốn chặn đăng nhập thì dùng Khóa).'; err.classList.remove('hide'); return; }
          const fd = new FormData();
          [['Id', r.Id], ['UserName', u.userName || r.UserName], ['PersonId', u.personId || ''], ['IsActive', u.isActive ? 'true' : 'false'], ['ListUserGroupId', ids]].forEach(([k, v]) => { if (v !== '' && v !== null && v !== undefined) fd.append(k, v); });
          Store.act('User/Update', { method: 'PUT', json: fd, tables: ['UserClaim'], quiet: true }).then(res => {
            if (!res.ok) { err.textContent = res.message || 'Không lưu được'; err.classList.remove('hide'); return; }
            close(); UI.toast('Đã cập nhật nhóm quyền của ' + esc(r.UserName || ''), 'ok'); APP.refresh();
          });
        }
      });
    });
  }
  // HR-ACC-1.8 (D-82 j): User/GetAccountLinks – cột Nhân viên + nhãn loại (Nhân viên / Hệ thống / Khách hàng), cảnh báo cố định
  // "Nhân viên đã nghỉ nhưng tài khoản còn mở" (chỉ tiêu = 0), lọc "Chưa gắn". Tải 1 lần / lần mở màn, rồi vẽ lại danh sách.
  const accState = { data: null, loading: false, only: false };
  const accTop = () => {
    const d = accState.data; if (!LIVE()) return '';
    if (!d) return `<div class="small muted mb" data-acctop>Đang tải liên kết tài khoản – nhân viên…</div>`;
    return `<div data-acctop>${d.resignedButActiveCount ? `<div class="notice warn mb">${I('alert')}<div><b>${U.num(d.resignedButActiveCount)} nhân viên đã nghỉ nhưng tài khoản còn mở</b> – khóa ngay (job hằng ngày cũng tự khóa và báo FIN-IT).</div></div>` : ''}
      <div class="row-flex mb"><button class="chip ${accState.only ? 'active' : ''}" data-acconly>Chưa gắn nhân viên · ${U.num(d.unlinkedCount || 0)}</button>
      <span class="small muted">Nhân viên đang làm chưa có tài khoản: ${U.num(d.employeeWithoutAccountCount || 0)}${Store.perm('adm-employee').access !== false ? ' – xem tab <a href="#/adm-employee?tab=link">Đối chiếu</a> ở màn Nhân viên' : ''}</span></div></div>`;
  };
  const accRow = id => accState.data && accState.data.by[id];
  APP.register('adm-user', (el, f) => {
    const canEdit = Store.perm(f.id).edit !== false;
    if (LIVE() && !accState.data && !accState.loading) {
      accState.loading = true;
      Store.query('User/GetAccountLinks').then(x => {
        accState.loading = false;
        if (!x.ok || !x.data) return;
        const d = x.data; d.by = {}; (d.listRow || []).forEach(r => { d.by[r.userId] = r; });
        accState.data = d;
        if (APP.current === 'adm-user') APP.refresh();
      });
    }
    const onlyRows = LIVE() && accState.only && accState.data ? () => Store.rows('User').filter(u => { const a = accRow(u.Id); return !a || (!a.linked && a.accountType === 'INTERNAL'); }) : undefined;
    APP.genericList(el, f, { noAdd: LIVE(), top: accTop(), rows: onlyRows });
    const onlyBtn = el.querySelector('[data-acconly]'); if (onlyBtn) onlyBtn.onclick = () => { accState.only = !accState.only; APP.refresh(); };
    // UG-1 C-06: chế độ thật – tạo người dùng qua User/Insert (gán nhóm + mật khẩu tạm), không nhập tay dòng User.
    if (LIVE() && Store.perm(f.id).add !== false) {
      const bar = el.querySelector('.heading-actions');
      if (bar) { bar.insertAdjacentHTML('beforeend', `<button class="btn primary" data-newuser>${I('plus')} Tạo người dùng</button>`); bar.querySelector('[data-newuser]').onclick = () => Store.reload(['UserGroup', 'Person']).then(createUser); }
    }
    APP.rowExtra(el, tr => {
      const r = Store.get('User', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td) return;
      const name = esc(r.UserName || ''), active = !!r.IsActive;
      const acc = accRow(r.Id);
      if (acc && !tr.querySelector('[data-accinfo]')) {
        const cell = [...tr.children].find(c => c.textContent.trim() === String(r.UserName || '').trim()) || tr.children[0];
        const kindColor = acc.accountType === 'INTERNAL' ? (acc.employeeId ? 'blue' : 'gray') : 'purple';
        cell.insertAdjacentHTML('beforeend', ` <span data-accinfo>${UI.tag(acc.kindName || '', kindColor)}${acc.resignedButActive ? ' ' + UI.tag('Đã nghỉ – còn mở', 'red') : ''}${acc.employeeId && !acc.linked ? ' ' + UI.tag('Đoán theo mã', 'orange') : ''}
          ${acc.employeeId ? `<span class="sub">${esc([acc.employeeCode, acc.employeeName, acc.organizationUnitName, acc.employeeStatusName].filter(Boolean).join(' · '))}</span>` : ''}</span>`);
      }
      let h = '';
      if (LIVE()) h += `<button class="btn xs" data-eff title="Quyền hiệu lực của tài khoản">${I('shield')}</button> `;
      // Không tự khóa tài khoản đang đăng nhập (backend cũng chặn).
      if (canEdit && r.Id !== Store.user.id) h += active ? '<button class="btn xs danger" data-lock>Khóa</button> ' : '<button class="btn xs" data-lock>Mở khóa</button> ';
      if (LIVE() && isAdmin()) h += `<button class="btn xs orange" data-reset title="Đặt lại mật khẩu">${I('key')}</button> `;
      if (LIVE() && canEdit) h += `<button class="btn xs" data-groups title="Gán nhóm quyền">${I('users')}</button> `;
      td.insertAdjacentHTML('afterbegin', h);
      const eff = td.querySelector('[data-eff]'); if (eff) eff.onclick = () => openUserPermission(r);
      const grp = td.querySelector('[data-groups]'); if (grp) grp.onclick = e => { e.stopPropagation(); Store.reload(['UserGroup']).then(() => assignGroups(r)); };
      const lock = td.querySelector('[data-lock]'); if (lock) lock.onclick = () => UI.confirm(active ? `Khóa tài khoản <b>${name}</b>? Người dùng sẽ không đăng nhập được cho tới khi mở lại.` : `Mở khóa tài khoản <b>${name}</b>?`,
        () => getAct('User/SaveIsActive', { id: r.Id }, { tables: ['User'], demo: () => Store.update('User', r.Id, { IsActive: active ? 0 : 1 }, active ? 'Khóa tài khoản' : 'Mở khóa tài khoản') }).then(x => { if (!x.ok) return; UI.toast((active ? 'Đã khóa tài khoản ' : 'Đã mở khóa tài khoản ') + name, 'ok'); APP.refresh(); }),
        { danger: active, ok: active ? 'Khóa' : 'Mở khóa' });
      const reset = td.querySelector('[data-reset]'); if (reset) reset.onclick = () => UI.confirm(`Đặt lại mật khẩu của tài khoản <b>${name}</b>? Mật khẩu hiện tại sẽ không dùng được nữa.`,
        () => getAct('User/ResetPassword', { id: r.Id }).then(x => {
          if (!x.ok) return;
          // Mật khẩu tạm chỉ hiện đúng một lần trong hộp thoại này – không lưu, không ghi log.
          const pw = typeof x.data === 'string' ? x.data : '';
          if (!pw) { UI.toast('Đã đặt lại mật khẩu tài khoản ' + name, 'ok'); return; }
          showTempPassword(name, pw);
        }), { danger: true, ok: 'Đặt lại mật khẩu' });
    });
  });
  // ---------- Nhóm người dùng: sang ma trận quyền, xem trước quyền của nhóm ----------
  APP.register('adm-usergroup', (el, f) => {
    APP.genericList(el, f);
    APP.rowExtra(el, tr => {
      const r = Store.get('UserGroup', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td) return;
      td.insertAdjacentHTML('afterbegin', `${Store.perm('adm-permission').access !== false ? '<button class="btn xs" data-mx>Phân quyền</button> ' : ''}${LIVE() && isAdmin() ? `<button class="btn xs" data-pv title="Xem trước quyền của nhóm">${I('shield')}</button> ` : ''}`);
      const mx = td.querySelector('[data-mx]'); if (mx) mx.onclick = () => { ps.g = r.Id; APP.go('adm-permission'); };
      const pv = td.querySelector('[data-pv]'); if (pv) pv.onclick = () => openGroupPreview(r);
    });
  });
  // ---------- Menu chức năng: cây 2 cấp, sắp xếp thứ tự, chuyển nhóm ----------
  APP.register('adm-objectmenu', (el, f) => {
    const canEdit = Store.perm(f.id).edit !== false;
    const ms = Store.rows('ObjectMenu');
    const byOrder = (a, b) => (a.OrderShow || 0) - (b.OrderShow || 0) || String(a.Name || '').localeCompare(String(b.Name || ''));
    const localTree = () => { const mk = m => ({ id: m.Id, name: m.Name, parentId: m.ParentId || null, orderShow: m.OrderShow, isActive: m.IsActive !== 0 && m.IsActive !== false, children: ms.filter(c => c.ParentId === m.Id).sort(byOrder).map(mk) }); return ms.filter(m => !m.ParentId).sort(byOrder).map(mk); };
    APP.genericList(el, f, { top: `<div class="card panel mb"><div class="panel-title">Cây menu chức năng</div><div class="small muted mb">Thứ tự hiển thị trên thanh menu. Thay đổi có hiệu lực với người dùng từ lần đăng nhập sau.</div><div id="omTree" style="max-height:420px;overflow:auto"><div class="muted small">Đang tải cây menu…</div></div></div>` });
    const draw = tree => {
      const host = el.querySelector('#omTree'); if (!host || !host.isConnected) return;
      const pos = {};                                   // id -> { node, sibs, i }
      const line = (m, sibs, i, lvl) => {
        pos[m.id] = { node: m, sibs, i };
        const kids = m.children || [];
        return `<div class="spread" style="padding:5px 0 5px ${lvl * 24}px;border-bottom:1px solid #F0F2ED${lvl ? '' : ';background:#F3F6EF'}"><div>${lvl ? esc(m.name || '') : '<b>' + esc(m.name || '') + '</b>'}${m.isActive === false ? ' <span class="tag">Đang tắt</span>' : ''}${lvl ? '' : ` <span class="small muted">${kids.length} mục</span>`}</div>${canEdit ? `<div class="row-flex" style="flex-wrap:nowrap;padding-right:6px">${lvl && tree.length > 1 ? `<button class="btn xs" data-par="${esc(m.id)}" title="Chuyển sang nhóm menu khác">Chuyển nhóm</button>` : ''}<button class="btn xs" data-mv="-1" data-id="${esc(m.id)}" ${i === 0 ? 'disabled' : ''} title="Lên trên">${I('up')}</button><button class="btn xs" data-mv="1" data-id="${esc(m.id)}" ${i === sibs.length - 1 ? 'disabled' : ''} title="Xuống dưới">${I('down')}</button></div>` : ''}</div>${kids.map((c, k) => line(c, kids, k, 1)).join('')}`;
      };
      host.innerHTML = tree.length ? tree.map((m, i) => line(m, tree, i, 0)).join('') : '<div class="muted small">Chưa có menu nào</div>';
      // Gửi danh sách [{ id, parentId, orderShow }] – body JSON vì là danh sách.
      const save = (payload, msg) => Store.act('ObjectMenu/UpdateOrder', { method: 'PUT', json: payload, tables: ['ObjectMenu'], demo: () => payload.forEach(p => Store.update('ObjectMenu', p.id, { ParentId: p.parentId, OrderShow: p.orderShow }, 'Sắp xếp menu')) }).then(x => { if (!x.ok) return; delete cache.menuTree; UI.toast(msg, 'ok'); APP.refresh(); });
      host.querySelectorAll('[data-mv]').forEach(b => b.onclick = () => {
        const p = pos[b.dataset.id]; if (!p) return;
        const j = p.i + (+b.dataset.mv); if (j < 0 || j >= p.sibs.length) return;
        const list = p.sibs.slice(); list[p.i] = p.sibs[j]; list[j] = p.node;
        save(list.map((s, k) => ({ id: s.id, parentId: s.parentId || null, orderShow: k + 1 })), 'Đã đổi thứ tự menu');
      });
      host.querySelectorAll('[data-par]').forEach(b => b.onclick = () => {
        const p = pos[b.dataset.par]; if (!p) return; const m = p.node;
        UI.modal({
          title: 'Chuyển nhóm menu', size: 'sm',
          body: `<div class="field"><label>Chuyển "${esc(m.name || '')}" sang nhóm</label><select class="select" id="omPar">${tree.filter(r => r.id !== m.parentId).map(r => `<option value="${esc(r.id)}">${esc(r.name || '')}</option>`).join('')}</select></div>`,
          foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="omOk">Chuyển</button>',
          onMount: (md, close) => md.querySelector('#omOk').onclick = () => {
            const to = tree.find(r => r.id === md.querySelector('#omPar').value); if (!to) return; close();
            const last = (to.children || []).reduce((mx, c) => Math.max(mx, c.orderShow || 0), 0);
            save([{ id: m.id, parentId: to.id, orderShow: last + 1 }], 'Đã chuyển menu sang nhóm ' + esc(to.name || ''));
          }
        });
      });
    };
    if (!LIVE()) { draw(localTree()); return; }
    const sig = ms.map(m => m.Id + '|' + (m.ParentId || '') + '|' + (m.OrderShow || 0) + '|' + (m.IsActive ? 1 : 0) + '|' + (m.Name || '')).join(',');
    cached('menuTree', sig, () => Store.query('ObjectMenu/GetTree', { includeInactive: true })).then(x => draw(x.ok ? (Array.isArray(x.data) ? x.data : x.list) : localTree()));
  });
  // ---------- Xung đột chuẩn hóa dữ liệu lõi: đánh dấu đã xử lý ----------
  APP.register('adm-merge-conflict', (el, f) => {
    const canEdit = Store.perm(f.id).edit !== false;
    // Backend chỉ có đọc + Resolve cho bảng này → chế độ dữ liệu thật không có Tạo mới / Sửa / Xóa.
    APP.genericList(el, f, { noAdd: LIVE(), noTransit: true });
    APP.rowExtra(el, tr => {
      const r = Store.get('CoreDataMergeConflict', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td) return;
      if (LIVE()) td.querySelectorAll('[data-edit],[data-del]').forEach(b => b.remove());
      if (r.ResolvedAt || !canEdit) return;
      td.insertAdjacentHTML('afterbegin', '<button class="btn xs orange" data-res>Xử lý</button> ');
      td.querySelector('[data-res]').onclick = () => UI.confirm(`Đánh dấu đã xử lý xung đột <b>${esc(r.EntityName || '')}</b> · ${esc(r.FieldName || '')}? Sau khi lưu không sửa lại được.`, note => {
        if (note.length > 1000) { UI.toast('Cách xử lý tối đa 1000 ký tự', 'err'); return; }
        Store.act('CoreDataMergeConflict/Resolve', { form: { Id: r.Id, ResolutionNote: note }, tables: ['CoreDataMergeConflict'], demo: () => Store.update('CoreDataMergeConflict', r.Id, { ResolvedAt: U.iso(new Date()), ResolutionNote: note }, 'Xử lý xung đột') }).then(x => { if (!x.ok) return; UI.toast('Đã đánh dấu xử lý xung đột', 'ok'); APP.refresh(); });
      }, { input: 'Cách xử lý', required: true, ok: 'Đã xử lý' });
    });
  });

  // ---------- MENU-4.6 (D-75 f2): Danh mục dùng chung – chỉ xem giá trị thật từ máy chủ (MetaData/GetReferenceLists) ----------
  // Thêm / sửa giá trị = sửa cấu hình máy chủ (ReferenceLists) – liên hệ quản trị hệ thống. Người dùng tự sửa = GAP-CFG-01 (chờ duyệt).
  const rs = { sel: null, data: null, busy: false };
  APP.register('adm-reference', (el, f) => {
    if (!Store.isLive) return APP.gapScreen(el, f);
    el.innerHTML = APP.header(f, '', { noNote: true, sub: 'Các giá trị chọn sẵn dùng chung trên nhiều màn (loại suất, loại kho, kênh phản ánh…).' })
      + `<div class="notice info">${I('key')}<div><b>Chỉ xem</b>Muốn thêm / sửa giá trị – liên hệ quản trị hệ thống (giá trị nằm trong cấu hình máy chủ).</div></div><div id="refBody"><div class="card panel empty">${I('clipboard')}<h2 class="mt">Đang tải…</h2></div></div>`;
    const body = el.querySelector('#refBody');
    // Bảng con đã ẩn khỏi menu (MENU-4) → ghi theo màn cha (nơi nhập).
    const screenOf = ent => { const s = Object.values(APP.FN).find(x => x.table && (x.table === ent || Store.controllerOf(x.table) === ent)); const up = s && (window.DC_PARENT_OF || {})[s.id]; return up ? APP.FN[up[0]] || s : s; };
    const usedIn = col => { const [ent, c] = col.split('.'); const s = screenOf(ent); return (s ? s.fn || s.name : ent) + ' › ' + U.colLabel(c); };
    const draw = () => {
      if (!body.isConnected) return;
      const x = rs.data; if (!x.ok) { body.innerHTML = `<div class="notice gap">${I('alert')}<div><b>Không tải được danh mục</b>${esc(x.message || '')}</div></div>`; return; }
      const lists = (Array.isArray(x.data) ? x.data : x.list) || [];
      if (!rs.sel || !lists.some(l => l.name === rs.sel)) rs.sel = (lists[0] || {}).name;
      const cur = lists.find(l => l.name === rs.sel) || { listItem: [], listColumn: [] };
      body.innerHTML = `<div class="grid g2">
        <div class="card panel"><div class="panel-title mb">${lists.length} danh mục</div>${lists.map(l => `<div class="titem ${l.name === rs.sel ? 'active' : ''}" data-ref="${esc(l.name)}" style="cursor:pointer"><div><b>${esc(l.title || l.name)}</b><div class="small muted">${(l.listColumn || []).length ? esc((l.listColumn || []).map(usedIn).join(' · ')) : 'Cấu hình hệ thống'}</div></div><span class="badge badge-sm badge-secondary">${(l.listItem || []).length}</span></div>`).join('')}</div>
        <div class="card panel"><div class="panel-title">${esc(cur.title || cur.name || '')}</div><div class="panel-desc mb">${(cur.listColumn || []).length ? 'Đang dùng ở: ' + esc(cur.listColumn.map(usedIn).join(' · ')) : ''}</div>
          <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th class="num">#</th><th>Mã</th><th>Tên hiển thị</th></tr></thead><tbody>${(cur.listItem || []).map((v, i) => `<tr><td class="num">${i + 1}</td><td class="mono">${esc(v.code)}</td><td>${esc(v.label)}</td></tr>`).join('') || '<tr><td colspan="3" class="empty">Chưa có giá trị</td></tr>'}</tbody></table></div></div></div>`;
      body.querySelectorAll('[data-ref]').forEach(d => d.onclick = () => { rs.sel = d.dataset.ref; draw(); });
    };
    if (rs.data) return draw();
    if (rs.busy) return; rs.busy = true;
    Store.query('MetaData/GetReferenceLists').then(x => { rs.busy = false; rs.data = x; draw(); });
  });
})();
