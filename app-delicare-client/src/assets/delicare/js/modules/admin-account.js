/* UI-6.42 (D-123): "Tài khoản" (adm-user).
 * - 4 chỉ số (User/GetList): Đang hoạt động (nhân viên · hệ thống) · Chờ IT xử lý (bấm → tab) · Nghỉ việc nhưng còn mở (Khóa ngay – LockMany) · Không đăng nhập N ngày (bấm lọc).
 * - Tab Tài khoản: chip loại + lọc trạng thái / nhóm / đơn vị + tìm; cột Tên đăng nhập · Người dùng · Nhóm quyền · Truy cập gần nhất · Bot · Trạng thái · "Thao tác ▾"
 *   (Xem quyền hiệu lực · Đổi nhóm quyền · Đặt lại mật khẩu · Khóa / Mở khóa); chọn nhiều → Thêm nhóm · Bỏ nhóm · Khóa (SetGroupsMany / LockMany). Không Sửa / Xóa.
 * - Tab Chờ IT xử lý (User/GetPendingRequests): thẻ yêu cầu cấp / đổi nhóm / mở lại – Đổi nhóm & duyệt · Từ chối · Duyệt (ApprovalRequest/Decide – cùng engine).
 * - Tạo tài khoản: Cho nhân viên (User/CreateForEmployee – tên = mã NV) · Tài khoản hệ thống (quản trị) · Khách hàng (giai đoạn sau).
 * Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { tab: 'list', type: '', status: '', group: '', unit: '', q: '', flag: '', sel: new Set() };
  const base = APP.SCREENS['adm-user'];
  const A = () => APP.adminAcc || {};
  const ST = { ACTIVE: ['Hoạt động', 'green'], MUST_CHANGE: ['Phải đổi mật khẩu', 'orange'], LOCKED: ['Đã khóa', ''], RESIGNED_OPEN: ['Nghỉ việc – còn mở', 'red'] };

  APP.register('adm-user', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && p.tab === 'pending') st.tab = 'pending';
    const re = () => APP.SCREENS['adm-user'](el, f, {});
    const canEdit = Store.perm('adm-user').edit !== false, canAdd = Store.perm('adm-user').add !== false, admin = A().isAdmin && A().isAdmin();
    Promise.all([Store.ensure('UserGroup'), Store.ensure('OrganizationUnit'), Store.ensure('Employee')]).then(() => {
      if (!el.isConnected) return;
      el.innerHTML = APP.header(f, canAdd ? `<button class="btn primary" id="acNew">${I('plus')} Tạo tài khoản</button>` : '', { noNote: true })
        + `<div id="acKpi" class="kpis"></div><div class="tabs c360-tabs mb"><button class="tab ${st.tab === 'list' ? 'active' : ''}" data-at="list">Tài khoản</button><button class="tab ${st.tab === 'pending' ? 'active' : ''}" data-at="pending">Chờ IT xử lý <span id="acPend"></span></button></div><div id="acBody"><div class="card panel small muted">Đang tải…</div></div>`;
      el.querySelectorAll('[data-at]').forEach(b => b.onclick = () => { st.tab = b.dataset.at; re(); });
      const nb = el.querySelector('#acNew'); if (nb) nb.onclick = () => createDlg(admin, re);
      Store.query('User/GetList', { type: st.type || null, status: st.status || null, groupId: st.group || null, organizationUnitId: st.unit || null, keyword: st.q || null, flag: st.flag || null }).then(x => {
        if (!el.isConnected) return;
        const d = (x.ok && x.data) || {};
        const kp = (l, v, s, i, c, k) => UI.kpi(l, v, s, i, c).replace('<article ', k ? `<article data-k="${k}" style="cursor:pointer" ` : '<article ');
        el.querySelector('#acKpi').innerHTML = kp('Đang hoạt động', U.num(d.active || 0), `${U.num(d.activeEmployee || 0)} nhân viên · ${U.num(d.activeSystem || 0)} hệ thống`, 'users', 'green')
          + kp('Chờ IT xử lý', U.num(d.pendingIt || 0), '', 'clock', d.pendingIt ? 'orange' : 'green', 'pending')
          + kp('Nghỉ việc nhưng còn mở', U.num(d.resignedOpen || 0), d.resignedOpen ? 'bấm để khóa ngay' : '', 'alert', d.resignedOpen ? 'red' : 'green', 'resigned')
          + kp(`Không đăng nhập ${U.num(d.inactiveWarnDays || 60)} ngày`, U.num(d.inactiveLong || 0), 'bấm để lọc', 'history', 'purple', 'inactive');
        el.querySelector('#acPend').textContent = d.pendingIt ? '· ' + U.num(d.pendingIt) : '';
        el.querySelector('[data-k="pending"]').onclick = () => { st.tab = 'pending'; re(); };
        el.querySelector('[data-k="inactive"]').onclick = () => { st.flag = st.flag === 'INACTIVE_LONG' ? '' : 'INACTIVE_LONG'; st.tab = 'list'; re(); };
        el.querySelector('[data-k="resigned"]').onclick = () => {
          const ids = (d.listItem || []).filter(r => r.status === 'RESIGNED_OPEN').map(r => r.id);
          if (!d.resignedOpen) return;
          if (!canEdit || !ids.length) { st.flag = 'RESIGNED_ACTIVE'; st.tab = 'list'; return re(); }
          UI.confirm(`Khóa ngay <b>${U.num(ids.length)}</b> tài khoản của nhân viên đã nghỉ việc?`, () => Store.act('User/LockMany', { json: { userIds: ids }, tables: ['User'] }).then(r => { if (r.ok) { UI.toast(esc(r.res && r.res.responseMessage || 'Đã khóa'), 'ok'); re(); } }), { ok: 'Khóa ngay', danger: true });
        };
        if (st.tab === 'pending') return pending(el.querySelector('#acBody'), re);
        list(el.querySelector('#acBody'), d, re, canEdit, admin);
      });
    });
  });

  function list(body, d, re, canEdit, admin) {
    const rows = d.listItem || [], groups = Store.rows('UserGroup').filter(g => !g.IsDeleted).sort((a, b) => String(a.Name).localeCompare(String(b.Name)));
    const chip = (k, l) => `<button class="chip ${st.type === k ? 'active' : ''}" data-ty="${k}">${l}</button>`;
    body.innerHTML = `<div class="card panel"><div class="row-flex mb" style="gap:8px;flex-wrap:wrap">${chip('', 'Tất cả')}${chip('EMPLOYEE', 'Nhân viên')}${chip('SYSTEM', 'Hệ thống')}
      <select class="select" id="acSt" style="width:auto"><option value="">Mọi trạng thái</option>${Object.entries(ST).map(([k, v]) => `<option value="${k}" ${st.status === k ? 'selected' : ''}>${v[0]}</option>`).join('')}</select>
      <select class="select" id="acGr" style="width:auto"><option value="">Mọi nhóm quyền</option>${groups.map(g => `<option value="${g.Id}" ${st.group === g.Id ? 'selected' : ''}>${esc(g.Name || '')}</option>`).join('')}</select>
      <span id="acUnit"></span><input class="input" id="acQ" placeholder="Tên đăng nhập, họ tên, mã NV" value="${esc(st.q)}" style="width:220px">${st.flag ? `<button class="chip active" id="acFx">${st.flag === 'INACTIVE_LONG' ? 'Lâu không đăng nhập' : 'Nghỉ việc còn mở'} ✕</button>` : ''}</div><div id="acBar"></div>
      <style>.ac-n{display:none}@media (max-width:700px){.ac-w{display:none}.ac-n{display:block}}</style><div class="ac-w table-wrap"><table class="table dc-table"><thead><tr><th style="width:28px"></th><th>Tên đăng nhập</th><th>Người dùng</th><th>Nhóm quyền</th><th>Truy cập gần nhất</th><th>Bot</th><th>Trạng thái</th><th></th></tr></thead><tbody>${rows.map(r => {
        const s = ST[r.status] || ST.ACTIVE, warn = r.isActive && (r.inactiveDays == null || r.inactiveDays >= (d.inactiveWarnDays || 60));
        const who = r.kind === 'EMPLOYEE' ? `<b>${esc(r.fullName || '')}</b><div class="small muted">${esc([r.employeeCode, r.positionName, r.unitName].filter(Boolean).join(' · '))}</div>${r.employeeStatus === 'Resigned' ? '<div class="small bad">đã nghỉ việc</div>' : r.employeeStatus === 'Inactive' ? '<div class="small warn">tạm nghỉ</div>' : ''}`
          : r.kind === 'CUSTOMER' ? '<span class="muted">Tài khoản khách hàng</span>' : `<span class="muted">Tài khoản hệ thống</span>${r.fullName ? `<div class="small">${esc(r.fullName)}</div>` : ''}${r.isAdmin ? ' ' + UI.tag('quản trị', 'purple') : ''}`;
        return `<tr data-row="${r.id}" style="cursor:pointer"><td><input type="checkbox" data-ck="${r.id}" ${st.sel.has(r.id) ? 'checked' : ''} aria-label="Chọn"></td><td class="mono">${esc(r.userName || '')}</td><td>${who}</td><td>${(r.listGroup || []).map(g => `<span class="tag" style="margin:1px">${esc(g)}</span>`).join('') || '<span class="bad small">chưa có nhóm</span>'}</td>
          <td>${r.lastAccess ? U.dt(r.lastAccess) : '<span class="muted">chưa đăng nhập</span>'}${warn && r.inactiveDays != null ? `<div class="small warn">${U.num(r.inactiveDays)} ngày</div>` : ''}</td><td>${r.botLinked ? UI.tag('Đã liên kết', 'green') : '<span class="muted small">Chưa</span>'}</td><td>${UI.tag(s[0], s[1])}</td><td><button class="btn xs" data-op="${r.id}">Thao tác ▾</button></td></tr>`;
      }).join('') || '<tr><td colspan="8" class="empty">Không có tài khoản khớp bộ lọc.</td></tr>'}</tbody></table></div>
      <div class="ac-n">${rows.map(r => `<div class="card" style="padding:10px;margin-bottom:8px" data-row="${r.id}"><div class="spread"><b>${esc(r.fullName || r.userName || '')}</b>${UI.tag((ST[r.status] || ST.ACTIVE)[0], (ST[r.status] || ST.ACTIVE)[1])}</div><div class="small muted">${esc(r.userName || '')} · ${r.lastAccess ? U.dt(r.lastAccess) : 'chưa đăng nhập'}</div><button class="btn xs mt" data-op="${r.id}">Thao tác ▾</button></div>`).join('')}</div></div>`;
    body.querySelectorAll('[data-ty]').forEach(b => b.onclick = () => { st.type = b.dataset.ty; APP.refresh(); });
    body.querySelector('#acSt').onchange = e => { st.status = e.target.value; APP.refresh(); };
    body.querySelector('#acGr').onchange = e => { st.group = e.target.value; APP.refresh(); };
    if (APP.unitPicker) APP.unitPicker(body.querySelector('#acUnit'), { value: st.unit, placeholder: 'Mọi đơn vị', onChange: v => { st.unit = v; APP.refresh(); } });
    let tq; body.querySelector('#acQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { st.q = e.target.value; APP.refresh(); }, 350); };
    const fx = body.querySelector('#acFx'); if (fx) fx.onclick = () => { st.flag = ''; APP.refresh(); };
    const bar = () => { const b = body.querySelector('#acBar'), n = st.sel.size; b.innerHTML = n && canEdit ? `<div class="notice info mb" style="display:flex;gap:8px;align-items:center;flex-wrap:wrap"><span>Đã chọn ${U.num(n)} tài khoản</span><button class="btn sm" data-bk="add">Thêm nhóm quyền</button><button class="btn sm" data-bk="rm">Bỏ nhóm quyền</button><button class="btn sm danger" data-bk="lock">Khóa</button><button class="btn sm" data-bk="clr">Bỏ chọn</button></div>` : '';
      b.querySelectorAll('[data-bk]').forEach(x => x.onclick = () => bulk(x.dataset.bk)); };
    const bulk = k => {
      const ids = [...st.sel];
      if (k === 'clr') { st.sel.clear(); body.querySelectorAll('[data-ck]').forEach(c => { c.checked = false; }); return bar(); }
      if (k === 'lock') return UI.confirm(`Khóa <b>${U.num(ids.length)}</b> tài khoản? (bỏ qua tài khoản của bạn và quản trị cuối cùng)`, () => Store.act('User/LockMany', { json: { userIds: ids }, tables: ['User'] }).then(r => { if (r.ok) { done(r); } }), { ok: 'Khóa', danger: true });
      UI.modal({ title: k === 'add' ? 'Thêm nhóm quyền' : 'Bỏ nhóm quyền', sub: `${U.num(ids.length)} tài khoản`, size: 'md',
        body: `<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:6px">${groups.map(g => `<label class="small"><input type="checkbox" data-g="${g.Id}"> ${esc(g.Name || '')}</label>`).join('')}</div>${k === 'rm' ? '<div class="small muted mt">Tài khoản sẽ hết nhóm nếu bỏ thì được giữ nguyên và liệt kê.</div>' : ''}`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="bgGo">Lưu</button>',
        onMount: (m, close) => m.querySelector('#bgGo').onclick = () => { const g = [...m.querySelectorAll('[data-g]:checked')].map(x => x.dataset.g); if (!g.length) return UI.toast('Chọn nhóm quyền', 'err');
          Store.act('User/SetGroupsMany', { json: k === 'add' ? { userIds: ids, addGroupIds: g } : { userIds: ids, removeGroupIds: g }, tables: ['UserClaim'] }).then(r => { if (r.ok) { close(); done(r); } }); } });
    };
    const done = r => { const sk = (r.data && r.data.listSkipped) || []; UI.toast(esc(r.res && r.res.responseMessage || 'Đã cập nhật') + (sk.length ? '<br><span class="small">' + sk.slice(0, 4).map(esc).join('<br>') + '</span>' : ''), sk.length ? 'warn' : 'ok'); st.sel.clear(); re(); };
    body.querySelectorAll('[data-ck]').forEach(c => { c.onclick = e => e.stopPropagation(); c.onchange = () => { c.checked ? st.sel.add(c.dataset.ck) : st.sel.delete(c.dataset.ck); bar(); }; });
    body.querySelectorAll('[data-op]').forEach(b => b.onclick = e => { e.stopPropagation(); ops(rows.find(r => r.id === b.dataset.op), canEdit, admin, re); });
    body.querySelectorAll('[data-row]').forEach(tr => tr.onclick = () => detail(rows.find(r => r.id === tr.dataset.row), canEdit, admin, re));
    bar();
  }
  function ops(r, canEdit, admin, re) {
    const me = Store.user && Store.user.id === r.id;
    const acts = [['eff', 'Xem quyền hiệu lực', true], ['grp', 'Đổi nhóm quyền', canEdit], ['reset', 'Đặt lại mật khẩu', admin], ['log', 'Lịch sử truy cập', true], ['lock', r.isActive ? 'Khóa' : 'Mở khóa', canEdit && !me]].filter(a => a[2]);
    UI.modal({ title: esc(r.userName || ''), sub: esc(r.fullName || ''), size: 'sm', body: `<div class="grid">${acts.map(([k, l]) => `<button class="btn ${k === 'lock' && r.isActive ? 'danger' : ''}" data-a="${k}">${l}</button>`).join('')}</div>`,
      onMount: (m, close) => m.querySelectorAll('[data-a]').forEach(b => b.onclick = () => { close(); run(b.dataset.a, r, re); }) });
  }
  function run(k, r, re) {
    const name = esc(r.userName || ''), row = { Id: r.id, UserName: r.userName };
    if (k === 'eff' && A().openUserPermission) return A().openUserPermission(row);
    if (k === 'grp' && A().assignGroups) return Store.reload(['UserGroup']).then(() => A().assignGroups(row));
    if (k === 'log') return APP.go('adm-access-log?user=' + r.id);
    if (k === 'reset') return UI.confirm(`Đặt lại mật khẩu của <b>${name}</b>? Mật khẩu hiện tại sẽ không dùng được nữa.`, () => A().getAct('User/ResetPassword', { id: r.id }).then(x => { if (!x.ok) return; const pw = typeof x.data === 'string' ? x.data : ''; if (pw && A().showTempPassword) A().showTempPassword(name, pw); else UI.toast('Đã đặt lại mật khẩu', 'ok'); }), { danger: true, ok: 'Đặt lại mật khẩu' });
    if (k === 'lock') return UI.confirm(r.isActive ? `Khóa tài khoản <b>${name}</b>?` : `Mở khóa tài khoản <b>${name}</b>?`, () => A().getAct('User/SaveIsActive', { id: r.id }, { tables: ['User'] }).then(x => { if (x.ok) { UI.toast(r.isActive ? 'Đã khóa' : 'Đã mở khóa', 'ok'); re(); } }), { danger: r.isActive, ok: r.isActive ? 'Khóa' : 'Mở khóa' });
  }
  function detail(r, canEdit, admin, re) {
    UI.drawer({ title: esc(r.userName || ''), sub: esc(r.fullName || (r.kind === 'SYSTEM' ? 'Tài khoản hệ thống' : '')), width: 640, tabs: [
      { label: 'Thông tin', html: `<div class="kv"><div><label>Loại</label><span>${r.kind === 'EMPLOYEE' ? 'Nhân viên' : r.kind === 'SYSTEM' ? 'Hệ thống' : 'Khách hàng'}${r.isAdmin ? ' · quản trị' : ''}</span></div><div><label>Trạng thái</label><span>${esc((ST[r.status] || ST.ACTIVE)[0])}</span></div><div><label>Nhân viên</label><span>${esc([r.employeeCode, r.fullName].filter(Boolean).join(' · ') || '–')}</span></div><div><label>Chức vụ · đơn vị</label><span>${esc([r.positionName, r.unitName].filter(Boolean).join(' · ') || '–')}</span></div><div><label>Truy cập gần nhất</label><span>${r.lastAccess ? U.dt(r.lastAccess) : 'chưa đăng nhập'}</span></div><div><label>Bot</label><span>${r.botLinked ? 'Đã liên kết' : 'Chưa liên kết'}</span></div></div><div class="row-flex mt" style="gap:6px"><a class="btn sm" href="${APP.href('adm-access-log?user=' + r.id)}">Lịch sử truy cập</a></div>` },
      { label: 'Nhóm & quyền', html: p => { setTimeout(() => { const b = p.querySelector('#dEff'); if (b) b.onclick = () => run('eff', r, re); }); return `<div>${(r.listGroup || []).map(g => UI.tag(g, 'blue')).join(' ') || 'Chưa có nhóm'}</div><button class="btn sm mt" id="dEff">Xem quyền hiệu lực</button>`; } },
      { label: 'Lịch sử', html: p => (UI.history ? UI.history('User', r.id, p) : '') },
    ] });
  }
  function pending(body, re) {
    Store.query('User/GetPendingRequests').then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được')}</div>`; return; }
      const rows = x.list || [];
      body.innerHTML = rows.length ? `<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:12px">${rows.map(r => `<div class="card panel" style="margin:0"><div class="spread"><b>${esc(r.employeeName || '')}</b>${UI.tag(r.typeName || '', r.businessType === 'USER_ACCOUNT_CREATE' ? 'blue' : r.businessType === 'USER_ACCOUNT_REOPEN' ? 'green' : 'orange')}</div>
        <div class="small muted">${esc([r.employeeCode, r.positionName, r.unitName].filter(Boolean).join(' · '))}</div><div class="small mt">Người gửi ${esc(r.requestedByName || '')} · ${U.dt(r.requestedAt)} · ${esc(r.requestNo || '')}</div>
        <div class="small mt">${r.businessType === 'USER_ACCOUNT_GROUP' ? `Nhóm mới: ${(r.listGroup || []).map(g => UI.tag(g, 'blue')).join(' ')}<br>Nhóm cũ: ${(r.listPreviousGroup || []).map(g => UI.tag(g, '')).join(' ') || '–'}` : `Nhóm gợi ý: ${(r.listGroup || []).map(g => UI.tag(g, 'blue')).join(' ') || '<span class="muted">chưa cấu hình</span>'}`}</div>
        ${r.businessType === 'USER_ACCOUNT_CREATE' && !r.botLinked ? '<div class="small warn mt">Chưa liên kết bot – IT xem mật khẩu tạm 1 lần sau khi duyệt.</div>' : ''}
        ${r.canApprove ? `<div class="row-flex mt" style="gap:6px"><button class="btn sm" data-chg="${r.approvalId}">Đổi nhóm & duyệt</button><button class="btn sm danger" data-rj="${r.approvalId}">Từ chối</button><button class="btn sm primary" data-ok="${r.approvalId}">${r.businessType === 'USER_ACCOUNT_CREATE' ? 'Duyệt & tạo' : 'Duyệt'}</button></div>` : '<div class="small muted mt">Bạn chỉ xem – không có quyền duyệt tài khoản.</div>'}</div>`).join('')}</div>` : '<div class="card panel empty">Không có yêu cầu tài khoản nào đang chờ.</div>';
      const decide = (id, approve, note, proposal) => Store.act('ApprovalRequest/Decide', { form: Object.assign({ Id: id, Approve: approve, Note: note || '' }, proposal ? { Proposal: proposal } : {}), tables: ['User', 'ApprovalRequest'] }).then(r => {
        if (!r.ok) return; const d = r.data || {}; UI.toast(esc(r.res && r.res.responseMessage || 'Đã xử lý'), 'ok');
        const pw = d.temporaryPassword || (d.result && d.result.temporaryPassword); if (pw && A().showTempPassword) A().showTempPassword(esc(d.userName || ''), pw); re(); });
      body.querySelectorAll('[data-ok]').forEach(b => b.onclick = () => decide(b.dataset.ok, true));
      body.querySelectorAll('[data-rj]').forEach(b => b.onclick = () => UI.confirm('Từ chối yêu cầu tài khoản?', note => decide(b.dataset.rj, false, note), { ok: 'Từ chối', danger: true, input: 'Lý do', required: true }));
      body.querySelectorAll('[data-chg]').forEach(b => b.onclick = () => { const r = rows.find(z => z.approvalId === b.dataset.chg), sel = new Set(r.listGroup || []), groups = Store.rows('UserGroup').filter(g => !g.IsDeleted);
        UI.modal({ title: 'Đổi nhóm & duyệt – ' + esc(r.employeeName || ''), size: 'md', body: `<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:6px">${groups.map(g => `<label class="small"><input type="checkbox" data-g="${esc(g.Name || '')}" ${sel.has(g.Name) ? 'checked' : ''}> ${esc(g.Name || '')}</label>`).join('')}</div>`,
          foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="cgGo">Duyệt với nhóm đã chọn</button>',
          onMount: (m, close) => m.querySelector('#cgGo').onclick = () => { const g = [...m.querySelectorAll('[data-g]:checked')].map(x => x.dataset.g); if (!g.length) return UI.toast('Chọn ít nhất 1 nhóm', 'err'); close(); decide(r.approvalId, true, '', JSON.stringify({ Groups: g })); } }); });
    });
  }
  function createDlg(admin, re) {
    let kind = 'EMP';
    UI.modal({ title: 'Tạo tài khoản', size: 'md', body: `<div class="seg mb" style="display:inline-flex">${[['EMP', 'Cho nhân viên'], ['SYS', 'Tài khoản hệ thống'], ['CUS', 'Khách hàng']].map(([k, l]) => `<button class="seg-b ${k === kind ? 'on' : ''}" data-k="${k}">${l}</button>`).join('')}</div><div id="cdB"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="cdGo">Tạo & gửi mật khẩu</button>',
      onMount: (m, close) => {
        const body = m.querySelector('#cdB'), go = m.querySelector('#cdGo'); let submit = null;
        const groups = Store.rows('UserGroup').filter(g => !g.IsDeleted).sort((a, b) => String(a.Name).localeCompare(String(b.Name)));
        const chips = sel => `<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:6px;max-height:220px;overflow:auto">${groups.map(g => `<label class="small"><input type="checkbox" data-g="${esc(g.Name || '')}" ${sel.includes(g.Name) ? 'checked' : ''}> ${esc(g.Name || '')}</label>`).join('')}</div>`;
        const picked = () => [...body.querySelectorAll('[data-g]:checked')].map(x => x.dataset.g);
        const draw = () => {
          m.querySelectorAll('[data-k]').forEach(b => b.classList.toggle('on', b.dataset.k === kind)); go.disabled = false;
          if (kind === 'CUS') { body.innerHTML = '<div class="notice info">Tài khoản cho khách hàng (cổng khách hàng) – giai đoạn sau.</div>'; go.disabled = true; return; }
          if (kind === 'SYS') {
            if (!admin) { body.innerHTML = '<div class="notice warn">Chỉ quản trị tạo được tài khoản hệ thống.</div>'; go.disabled = true; return; }
            body.innerHTML = `<div class="field"><label>Tên đăng nhập *</label><input class="input" id="sysU" placeholder="chữ thường, số, . _ -"></div><div class="field mt"><label>Mục đích *</label><input class="input" id="sysP" placeholder="vd. tích hợp máy chấm công"></div><div class="field mt"><label>Nhóm quyền *</label>${chips([])}</div>`;
            submit = () => { const u = body.querySelector('#sysU').value.trim(), pp = body.querySelector('#sysP').value.trim(), g = picked(); if (!u) return UI.toast('Nhập tên đăng nhập', 'err'); if (pp.length < 3) return UI.toast('Nhập mục đích', 'err'); if (!g.length) return UI.toast('Chọn nhóm quyền', 'err');
              const ids = groups.filter(x => g.includes(x.Name)).map(x => x.Id).join(',');
              Store.act('Person/Insert', { form: { FullName: pp, UserName: u, ListUserGroupId: ids, IsActive: true }, tables: ['User', 'Person'], quiet: true }).then(r => { if (!r.ok) return UI.toast(esc(r.message || 'Không tạo được'), 'err'); close(); const pw = r.data && (r.data.tempPassword || r.data.temporaryPassword); if (pw && A().showTempPassword) A().showTempPassword(esc(u), pw); else UI.toast('Đã tạo tài khoản', 'ok'); re(); }); };
            return;
          }
          const emps = Store.rows('Employee').filter(e => !e.IsDeleted && (e.Status || 'Active') === 'Active');
          body.innerHTML = `<div class="field"><label>Nhân viên đang làm, chưa có tài khoản *</label><input class="input mb" id="emQ" placeholder="Gõ tên / mã để lọc"><select class="select" id="emS" size="6">${emps.map(e => `<option value="${e.Id}" data-code="${esc(e.Code || '')}" data-pos="${e.PositionId || ''}">${esc((e.FullName || '') + ' · ' + (e.Code || ''))}</option>`).join('')}</select></div><div class="field mt"><label>Tên đăng nhập</label><input class="input mono" id="emU" readonly placeholder="= mã nhân viên"></div><div class="field mt"><label>Nhóm quyền (gợi ý theo chức vụ)</label><div id="emG">${chips([])}</div></div><div class="small muted mt">Mật khẩu tạm gửi qua bot nếu nhân viên đã liên kết; chưa liên kết thì hiện cho bạn xem một lần.</div>`;
          body.querySelector('#emQ').oninput = e => { const k = e.target.value.toLowerCase(); body.querySelectorAll('#emS option').forEach(o => { o.hidden = !o.textContent.toLowerCase().includes(k); }); };
          body.querySelector('#emS').onchange = e => { const o = e.target.selectedOptions[0]; body.querySelector('#emU').value = (o.dataset.code || '').toLowerCase();
            Store.query('Employee/GetPositionGroups').then(x => { const pos = Store.get('Position', o.dataset.pos), map = (x.ok && x.data) || {}, sug = pos ? (map[pos.Code] || map[String(pos.Code || '').toUpperCase()] || []) : []; body.querySelector('#emG').innerHTML = chips(sug); }); };
          submit = () => { const id = body.querySelector('#emS').value; if (!id) return UI.toast('Chọn nhân viên', 'err'); const g = picked();
            Store.act('User/CreateForEmployee', { json: { employeeId: id, groups: g.join(',') }, tables: ['User', 'Employee'], quiet: true }).then(r => { if (!r.ok) return UI.toast(esc(r.message || 'Không tạo được'), 'err'); close(); const d = r.data || {}; if (d.temporaryPassword && A().showTempPassword) A().showTempPassword(esc(d.userName || ''), d.temporaryPassword); else UI.toast(esc(r.res && r.res.responseMessage || 'Đã tạo tài khoản'), 'ok'); re(); }); };
        };
        m.querySelectorAll('[data-k]').forEach(b => b.onclick = () => { kind = b.dataset.k; draw(); });
        go.onclick = () => submit && submit();
        draw();
      } });
  }
})();
