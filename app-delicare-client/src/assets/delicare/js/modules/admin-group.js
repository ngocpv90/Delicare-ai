/* UI-6.43 (D-124): "Nhóm quyền" (adm-usergroup) – Phân quyền thành tab của trang nhóm (#/adm-permission ẩn, link cũ mở trang này).
 * - Danh sách (UserGroup/GetList): 4 chỉ số · tìm · theo khối (Ban điều hành, Vận hành, Kinh doanh, Tài chính – Hỗ trợ, QA / ATTP); mỗi nhóm: tên tiếng Việt, mã, mô tả,
 *   số tài khoản / màn hình / loại duyệt, sửa gần nhất. Bấm → trang nhóm.
 * - Trang nhóm (UserGroup/GetDetail): Sửa (tên, mã – khóa khi đang dùng ở luồng duyệt / cấu hình, khối, mô tả) · Ngừng dùng (UserGroup/Retire) ·
 *   tab Phân quyền (UserRole/GetLevels – mức Không / Xem / Xem, in & xuất / Xem & sửa / Toàn quyền; đổi nhiều → "Xem lại & lưu" → UserRole/SaveMany),
 *   tab Tài khoản (thêm / bỏ – không để tài khoản hết nhóm), Xem trước menu (UserRole/GetPreviewDraft – gồm thay đổi chưa lưu), So sánh (UserRole/Compare), Lịch sử.
 * Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const base = APP.SCREENS['adm-usergroup'], baseMx = APP.SCREENS['adm-permission'];
  const st = { id: null, tab: 'role', q: '', empty: false, draft: new Map(), rq: '', onlyOn: false, cmp: '' };
  const LV = ['NONE', 'X', 'IX', 'S', 'Q'];
  const LV_COLOR = { NONE: '', X: 'blue', IX: 'blue', S: 'green', Q: 'purple', D: 'green', CUSTOM: 'orange' };
  const BLOCKS = () => (Store.refList('UserGroupBlock') || []).map(x => ({ code: x.code || x.Code, name: x.name || x.label || x.Label }));
  const when = d => d ? U.date(d) : '–';

  APP.register('adm-permission', (el, f, p) => {
    if (!Store.isLive) return baseMx(el, f, p);
    location.replace(APP.href('adm-usergroup' + (p && p.g ? '?id=' + encodeURIComponent(p.g) : '')));
  });

  APP.register('adm-usergroup', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    // Mở từ menu / link: theo ?id= (link cũ #/adm-permission?g= cũng về đây); vẽ lại trong trang (re) giữ trạng thái.
    if (!p || !p._re) { const id = (p && p.id) || null; if (id !== st.id) { st.id = id; st.tab = (p && p.tab) || 'role'; st.draft.clear(); } }
    const re = () => APP.SCREENS['adm-usergroup'](el, f, { _re: 1 });
    const perm = Store.perm('adm-usergroup'), canEdit = perm.edit !== false, canAdd = perm.add !== false, canDel = perm.delete !== false;
    Promise.all([Store.ensure('UserGroup'), Store.loadRefLists()]).then(() => st.id ? detail(el, f, re, canEdit, canDel) : list(el, f, re, canAdd));
  });

  function list(el, f, re, canAdd) {
    el.innerHTML = APP.header(f, canAdd ? `<button class="btn primary" id="ugNew">${I('plus')} Tạo nhóm</button>` : '', { noNote: true, sub: 'Mỗi nhóm là một vai trò: quyền xem / sửa các màn hình và quyền duyệt. Tài khoản nhận quyền theo nhóm.' })
      + '<div id="ugKpi" class="kpis"></div><div id="ugBody"><div class="card panel small muted">Đang tải…</div></div>';
    const nb = el.querySelector('#ugNew'); if (nb) nb.onclick = () => form(null, re, false);
    Store.query('UserGroup/GetList', { keyword: st.q || null, emptyOnly: st.empty }).then(x => {
      if (!el.isConnected) return;
      const d = (x.ok && x.data) || {}, rows = d.listItem || [];
      const kp = (l, v, s, i, c, k) => UI.kpi(l, v, s, i, c).replace('<article ', k ? `<article data-k="${k}" style="cursor:pointer" ` : '<article ');
      el.querySelector('#ugKpi').innerHTML = kp('Nhóm quyền', U.num(d.groups || 0), `${U.num(d.blocks || 0)} khối`, 'layers', 'blue')
        + kp('Tài khoản có nhóm', U.num(d.accountsWithGroup || 0), d.accountsNoGroup ? `${U.num(d.accountsNoGroup)} tài khoản chưa có nhóm` : 'mọi tài khoản đều có nhóm', 'users', d.accountsNoGroup ? 'orange' : 'green', 'acc')
        + kp('Nhóm chưa có tài khoản', U.num(d.emptyGroups || 0), st.empty ? 'đang lọc – bấm để bỏ' : 'bấm để lọc', 'alert', d.emptyGroups ? 'orange' : 'green', 'empty')
        + kp('Đổi trong 7 ngày', U.num(d.changedLast7Days || 0), 'nhóm có sửa tên / quyền', 'history', 'purple');
      el.querySelector('[data-k="acc"]').onclick = () => APP.go('adm-user');
      el.querySelector('[data-k="empty"]').onclick = () => { st.empty = !st.empty; re(); };
      const byBlock = {};
      rows.forEach(r => { (byBlock[r.groupBlockName || 'Khác'] = byBlock[r.groupBlockName || 'Khác'] || []).push(r); });
      const card = r => `<div class="card panel" data-g="${r.id}" style="cursor:pointer;margin:0">
          <div class="spread"><b>${esc(r.name || '')}</b><span class="mono small muted">${esc(r.code || '')}</span></div>
          <div class="small muted" style="min-height:18px">${esc(r.description || '')}</div>
          <div class="row-flex small mt" style="gap:12px;flex-wrap:wrap"><span>${I('users')} ${U.num(r.accounts)} tài khoản</span><span>${I('panel')} ${U.num(r.screens)} màn hình</span><span>${I('check')} ${U.num(r.approvalTypes)} loại duyệt</span></div>
          <div class="small muted mt">Sửa gần nhất: ${when(r.lastModified)}${r.accounts ? '' : ' · ' + UI.tag('chưa có tài khoản', 'orange')}</div></div>`;
      el.querySelector('#ugBody').innerHTML = `<div class="card panel mb"><input class="input" id="ugQ" placeholder="Tìm theo tên hoặc mã nhóm" value="${esc(st.q)}" style="max-width:320px"></div>`
        + (rows.length ? Object.entries(byBlock).map(([b, list]) => `<div class="panel-title mt">${esc(b)} <span class="small muted">· ${list.length} nhóm</span></div><div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:10px">${list.map(card).join('')}</div>`).join('')
          : '<div class="card panel muted">Không có nhóm nào khớp.</div>');
      const q = el.querySelector('#ugQ'); let t; q.oninput = () => { clearTimeout(t); t = setTimeout(() => { st.q = q.value.trim(); re(); }, 400); };
      el.querySelectorAll('[data-g]').forEach(c => c.onclick = () => { st.id = c.dataset.g; st.tab = 'role'; st.draft.clear(); re(); });
    });
  }

  function form(g, re, codeLocked) {
    const blocks = BLOCKS();
    UI.modal({ title: g ? 'Sửa nhóm quyền' : 'Tạo nhóm quyền', size: 'md',
      body: `<div class="form-grid"><div class="field"><label>Tên nhóm <span class="req">*</span></label><input class="input" id="gfN" value="${esc(g ? g.name || '' : '')}" placeholder="Ví dụ: Thủ kho"></div>
        <div class="field"><label>Mã nhóm <span class="req">*</span></label><input class="input mono" id="gfC" value="${esc(g ? g.code || '' : '')}" placeholder="Ví dụ: OPS-WH" ${codeLocked ? 'disabled' : ''}>
          <span class="small muted">${codeLocked ? 'Mã đang dùng ở luồng duyệt / cấu hình – không đổi được.' : 'Chữ hoa không dấu, dùng cho luồng duyệt và bot.'}</span></div>
        <div class="field"><label>Khối</label><select class="select" id="gfB"><option value="">– Chọn khối –</option>${blocks.map(b => `<option value="${esc(b.code)}" ${g && g.groupBlock === b.code ? 'selected' : ''}>${esc(b.name)}</option>`).join('')}</select></div>
        <div class="field" style="grid-column:1/-1"><label>Mô tả vai trò</label><textarea class="input" id="gfD" rows="2">${esc(g ? g.description || '' : '')}</textarea></div></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="gfGo">Lưu</button>',
      onMount: (m, close) => m.querySelector('#gfGo').onclick = () => {
        const v = id => m.querySelector(id).value.trim();
        if (!v('#gfN')) return UI.toast('Nhập tên nhóm', 'err');
        const form = { Name: v('#gfN'), Code: codeLocked ? '' : v('#gfC'), GroupBlock: v('#gfB'), Description: v('#gfD') };
        if (g) form.Id = g.id;
        Store.act(g ? 'UserGroup/Update' : 'UserGroup/Insert', { method: g ? 'PUT' : 'POST', form, tables: ['UserGroup'] }).then(r => { if (r.ok) { close(); UI.toast('Đã lưu nhóm', 'ok'); re(); } });
      } });
  }

  function detail(el, f, re, canEdit, canDel) {
    el.innerHTML = '<div class="card panel small muted">Đang tải…</div>';
    Store.query('UserGroup/GetDetail', { id: st.id }).then(x => {
      if (!el.isConnected) return;
      if (!x.ok || !x.data) { st.id = null; return re(); }
      const d = x.data, g = d.group || {};
      const back = `<button class="btn" id="ugBack">‹ Danh sách nhóm</button>`;
      el.innerHTML = APP.header(f, back + (canEdit ? ` <button class="btn" id="ugEdit">${I('edit')} Sửa</button>` : '') + (canDel ? ` <button class="btn danger" id="ugRet">Ngừng dùng</button>` : ''),
        { noNote: true, title: g.name || '', sub: `<span class="mono">${esc(g.code || '')}</span> · ${esc(g.groupBlockName || '')}${g.description ? ' · ' + esc(g.description) : ''}` })
        + `<div class="kpis">${UI.kpi('Tài khoản', U.num(g.accounts), '', 'users', 'blue')}${UI.kpi('Màn hình được xem', U.num(g.screens), '', 'panel', 'green')}${UI.kpi('Loại duyệt', U.num(g.approvalTypes), '', 'check', 'purple')}${UI.kpi('Lần đổi', U.num((d.listHistory || []).length), 'gần nhất ' + when(g.lastModified), 'history', '')}</div>`
        + `<div class="tabs c360-tabs mb">${[['role', 'Phân quyền'], ['users', 'Tài khoản'], ['preview', 'Xem trước menu'], ['cmp', 'So sánh'], ['hist', 'Lịch sử']].map(([k, l]) => `<button class="tab ${st.tab === k ? 'active' : ''}" data-t="${k}">${l}${k === 'role' && st.draft.size ? ` <span class="tag orange">${st.draft.size}</span>` : ''}</button>`).join('')}</div><div id="ugTab"></div>`;
      el.querySelector('#ugBack').onclick = () => {
        const go = () => { st.id = null; st.draft.clear(); re(); };
        st.draft.size ? UI.confirm(`Bỏ <b>${st.draft.size}</b> thay đổi quyền chưa lưu?`, go, { ok: 'Bỏ thay đổi', danger: true }) : go();
      };
      const eb = el.querySelector('#ugEdit'); if (eb) eb.onclick = () => form(g, re, d.codeLocked);
      const rb = el.querySelector('#ugRet'); if (rb) rb.onclick = () => UI.confirm(`Ngừng dùng nhóm <b>${esc(g.name || '')}</b>? Chỉ được khi nhóm không còn tài khoản và không là người duyệt.`,
        () => Store.act('UserGroup/Retire', { json: { id: g.id }, tables: ['UserGroup'] }).then(r => { if (r.ok) { UI.toast('Đã ngừng dùng nhóm', 'ok'); st.id = null; re(); } }), { ok: 'Ngừng dùng', danger: true });
      el.querySelectorAll('[data-t]').forEach(b => b.onclick = () => { st.tab = b.dataset.t; re(); });
      const host = el.querySelector('#ugTab');
      ({ role: roleTab, users: usersTab, preview: previewTab, cmp: cmpTab, hist: histTab })[st.tab](host, d, re, canEdit);
    });
  }

  const draftItems = () => [...st.draft.entries()].map(([objectMenuId, level]) => ({ objectMenuId, level }));

  function roleTab(host, d, re, canEdit) {
    host.innerHTML = '<div class="card panel small muted">Đang tải quyền…</div>';
    Store.query('UserRole/GetLevels', { userGroupId: st.id }).then(x => {
      if (!host.isConnected) return;
      const m = (x.ok && x.data) || {}, items = m.listItem || [], L = m.levels || {};
      const screens = items.filter(i => i.levelShow === 2);
      const parents = items.filter(i => i.levelShow === 1);
      const cur = i => st.draft.get(i.objectMenuId) || i.level;
      // D-31: mục "Phê duyệt › loại" – được duyệt = Truy cập + Sửa (mức D) → chỉ 2 lựa chọn Không / Được duyệt (mức khác đang có vẫn hiện).
      const APR = { NONE: 'Không', D: 'Được duyệt', X: 'Chỉ xem' };
      const isApr = i => /^#\/wf-inbox\?type=/.test(i.metaLink || '');
      const lab = (i, v) => isApr(i) && APR[v] ? APR[v] : (L[v] || v);
      const sel = i => {
        const c = cur(i), opts = isApr(i) ? ['NONE', 'D'].concat(['NONE', 'D'].includes(c) ? [] : [c]) : LV.concat(c === 'CUSTOM' ? ['CUSTOM'] : []);
        if (!canEdit) return UI.tag(lab(i, c), LV_COLOR[c]);
        return `<select class="select" data-m="${i.objectMenuId}" style="width:auto;min-width:150px${st.draft.has(i.objectMenuId) ? ';border-color:#E8A33D;background:#FFF8EC' : ''}">${opts.map(v => `<option value="${v}" ${v === c ? 'selected' : ''} ${v === 'CUSTOM' ? 'disabled' : ''}>${esc(lab(i, v))}</option>`).join('')}</select>${c === 'CUSTOM' ? ` <span class="small muted" title="Quyền lẻ từ ma trận cũ">${flags(i)}</span>` : ''}`;
      };
      const q = st.rq.toLowerCase();
      const show = i => (!q || String(i.name || '').toLowerCase().includes(q) || String(i.parentName || '').toLowerCase().includes(q)) && (!st.onlyOn || cur(i) !== 'NONE');
      // MENU-TAB: tab của 1 màn (Trung tâm Agent, Bảng điều hành) lồng ngay dưới màn cha – không tính là màn.
      const groups = parents.map(p => ({ p, kids: Store.nestTabs(screens.filter(s => s.parentId === p.objectMenuId && show(s)), i => i.metaLink) })).filter(x => x.kids.length);
      const orphan = screens.filter(s => !parents.some(p => p.objectMenuId === s.parentId) && show(s));
      if (orphan.length) groups.push({ p: { name: 'Khác' }, kids: orphan });
      host.innerHTML = `<div class="card panel"><div class="row-flex mb" style="gap:8px;flex-wrap:wrap"><input class="input" id="rq" placeholder="Tìm màn hình" value="${esc(st.rq)}" style="width:220px">
          <label class="small"><input type="checkbox" id="rOn" ${st.onlyOn ? 'checked' : ''}> Chỉ màn có quyền</label>
          <span class="small muted">Mức: Xem · Xem, in & xuất · Xem & sửa (thêm, sửa) · Toàn quyền (gồm xóa, nhập file). Quyền duyệt ở nhóm "Phê duyệt": Được duyệt / Không ("Tất cả yêu cầu duyệt" chỉ để xem).</span></div>
        <div id="rBar"></div>
        ${groups.map(({ p, kids }) => `<div class="panel-title mt">${esc(p.name || '')} <span class="small muted">· ${kids.filter(k => !k._tab && cur(k) !== 'NONE').length}/${kids.filter(k => !k._tab).length} màn có quyền</span>${canEdit ? ` <span class="small">${kids.every(isApr) ? '' : `<a href="javascript:void 0" data-all="${p.objectMenuId || ''}" data-v="X">tất cả Xem</a> · `}<a href="javascript:void 0" data-all="${p.objectMenuId || ''}" data-v="NONE">bỏ hết</a></span>` : ''}</div>
          <div class="table-wrap"><table class="table dc-table"><tbody>${kids.map(k => `<tr><td${k._tab ? ' style="padding-left:28px"' : ''}>${k._tab ? '<span class="tag" style="font-size:10px;padding:0 6px;margin-right:4px">tab</span>' : ''}${esc(k.name || '')}</td><td style="width:260px">${sel(k)}</td></tr>`).join('')}</tbody></table></div>`).join('') || '<div class="muted">Không có màn nào khớp.</div>'}</div>`;
      const bar = () => {
        const b = host.querySelector('#rBar'), n = st.draft.size;
        b.innerHTML = n ? `<div class="notice warn mb" style="display:flex;gap:8px;align-items:center;flex-wrap:wrap"><b>${n} thay đổi chưa lưu</b><button class="btn primary sm" id="rSave">Xem lại & lưu</button><button class="btn sm" id="rPrev">Xem trước menu</button><button class="btn sm" id="rDrop">Bỏ thay đổi</button></div>` : '';
        if (!n) return;
        b.querySelector('#rDrop').onclick = () => { st.draft.clear(); re(); };
        b.querySelector('#rPrev').onclick = () => { st.tab = 'preview'; re(); };
        b.querySelector('#rSave').onclick = () => {
          const ch = [...st.draft.entries()].map(([id, v]) => { const i = items.find(z => z.objectMenuId === id) || {}; return `<tr><td>${esc(i.parentName || '')} › ${esc(i.name || '')}</td><td>${UI.tag(lab(i, i.level), LV_COLOR[i.level])}</td><td>→</td><td>${UI.tag(lab(i, v), LV_COLOR[v])}</td></tr>`; }).join('');
          UI.modal({ title: `Lưu ${n} thay đổi quyền – ${esc(d.group.name || '')}`, sub: `Có hiệu lực ngay với ${U.num(d.group.accounts)} tài khoản trong nhóm.`, size: 'md',
            body: `<div class="table-wrap" style="max-height:360px;overflow:auto"><table class="table dc-table"><thead><tr><th>Màn hình</th><th>Trước</th><th></th><th>Sau</th></tr></thead><tbody>${ch}</tbody></table></div>`,
            foot: '<button class="btn" data-close>Quay lại</button><button class="btn primary" id="rGo">Lưu thay đổi</button>',
            onMount: (mm, close) => mm.querySelector('#rGo').onclick = () => Store.act('UserRole/SaveMany', { json: { userGroupId: st.id, items: draftItems() }, tables: ['UserRole'] }).then(r => {
              if (!r.ok) return; close(); st.draft.clear(); UI.toast(esc(r.res && r.res.responseMessage || 'Đã lưu'), 'ok'); re();
            }) });
        };
      };
      bar();
      const mark = (id, v) => { const i = items.find(z => z.objectMenuId === id); if (!i) return; v === i.level ? st.draft.delete(id) : st.draft.set(id, v); };
      host.querySelectorAll('[data-m]').forEach(s => s.onchange = () => { mark(s.dataset.m, s.value); s.style.borderColor = st.draft.has(s.dataset.m) ? '#E8A33D' : ''; s.style.background = st.draft.has(s.dataset.m) ? '#FFF8EC' : ''; bar(); tabBadge(); });
      host.querySelectorAll('[data-all]').forEach(a => a.onclick = () => { screens.filter(s => (s.parentId || '') === a.dataset.all || (!a.dataset.all && !parents.some(p => p.objectMenuId === s.parentId))).filter(show).forEach(s => mark(s.objectMenuId, a.dataset.v)); re(); });
      const rq = host.querySelector('#rq'); let t; rq.oninput = () => { clearTimeout(t); t = setTimeout(() => { st.rq = rq.value.trim(); roleTab(host, d, re, canEdit); }, 300); };
      host.querySelector('#rOn').onchange = e => { st.onlyOn = e.target.checked; roleTab(host, d, re, canEdit); };
      function tabBadge() { const tb = document.querySelector('[data-t="role"]'); if (tb) tb.innerHTML = 'Phân quyền' + (st.draft.size ? ` <span class="tag orange">${st.draft.size}</span>` : ''); }
    });
  }
  const flags = i => [['allowAdd', 'thêm'], ['allowEdit', 'sửa'], ['allowDelete', 'xóa'], ['allowPrint', 'in'], ['allowExport', 'xuất'], ['allowImport', 'nhập']].filter(([k]) => i[k]).map(([, l]) => l).join(', ') || 'chỉ xem';

  function usersTab(host, d, re, canEdit) {
    const rows = d.listUser || [];
    host.innerHTML = `<div class="card panel">${canEdit ? `<div class="row-flex mb"><button class="btn primary sm" id="uAdd">${I('plus')} Thêm tài khoản</button></div>` : ''}
      ${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Tên đăng nhập</th><th>Người dùng</th><th>Số nhóm</th><th>Trạng thái</th><th></th></tr></thead><tbody>${rows.map(u => `<tr><td class="mono">${esc(u.userName || '')}</td><td>${esc(u.fullName || '')}</td><td>${U.num(u.groupCount)}</td><td>${u.isActive ? UI.tag('Hoạt động', 'green') : UI.tag('Đã khóa', '')}</td>
        <td>${canEdit ? (u.groupCount > 1 ? `<button class="btn xs" data-rm="${u.id}">Bỏ khỏi nhóm</button>` : '<span class="small muted" title="Tài khoản phải còn ít nhất 1 nhóm">nhóm duy nhất</span>') : ''}</td></tr>`).join('')}</tbody></table></div>` : '<div class="muted">Nhóm chưa có tài khoản.</div>'}</div>`;
    host.querySelectorAll('[data-rm]').forEach(b => b.onclick = () => UI.confirm('Bỏ tài khoản khỏi nhóm này?', () => Store.act('UserGroup/RemoveUsers', { json: { userGroupId: st.id, userIds: [b.dataset.rm] }, tables: ['UserClaim'] }).then(r => { if (r.ok) { UI.toast(esc(r.res && r.res.responseMessage || 'Đã bỏ'), 'ok'); re(); } }), { ok: 'Bỏ khỏi nhóm', danger: true }));
    const ab = host.querySelector('#uAdd'); if (!ab) return;
    ab.onclick = () => UI.modal({ title: 'Thêm tài khoản vào nhóm ' + esc(d.group.name || ''), size: 'md', body: '<input class="input mb" id="uQ" placeholder="Tìm tên đăng nhập, họ tên, mã nhân viên"><div id="uL" style="max-height:340px;overflow:auto" class="small muted">Gõ để tìm…</div>',
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="uGo">Thêm</button>',
      onMount: (m, close) => {
        const pick = new Set(), have = new Set(rows.map(r => r.id)), q = m.querySelector('#uQ'), L = m.querySelector('#uL'); let t;
        const load = () => Store.query('User/GetList', { keyword: q.value.trim() || null }).then(x => {
          const list = ((x.ok && x.data && x.data.listItem) || []).filter(u => !have.has(u.id)).slice(0, 50);
          L.innerHTML = list.length ? list.map(u => `<label style="display:block;padding:3px 0"><input type="checkbox" data-u="${u.id}" ${pick.has(u.id) ? 'checked' : ''}> <span class="mono">${esc(u.userName || '')}</span> ${esc(u.fullName || '')}</label>`).join('') : 'Không tìm thấy tài khoản';
          L.querySelectorAll('[data-u]').forEach(c => c.onchange = () => c.checked ? pick.add(c.dataset.u) : pick.delete(c.dataset.u));
        });
        q.oninput = () => { clearTimeout(t); t = setTimeout(load, 300); }; load();
        m.querySelector('#uGo').onclick = () => { if (!pick.size) return UI.toast('Chọn ít nhất 1 tài khoản', 'err'); Store.act('UserGroup/AddUsers', { json: { userGroupId: st.id, userIds: [...pick] }, tables: ['UserClaim'] }).then(r => { if (r.ok) { close(); UI.toast(esc(r.res && r.res.responseMessage || 'Đã thêm'), 'ok'); re(); } }); };
      } });
  }

  function previewTab(host, d) {
    host.innerHTML = '<div class="card panel small muted">Đang dựng menu…</div>';
    Store.act('UserRole/GetPreviewDraft', { json: { userGroupId: st.id, items: draftItems() }, quiet: true }).then(r => {
      if (!host.isConnected) return;
      const m = (r.ok && r.data) || {}, tree = m.listMenu || [];
      const fl = n => [n.allowAdd || n.allowEdit ? 'sửa' : '', n.allowDelete ? 'xóa' : '', n.allowPrint || n.allowExport ? 'in / xuất' : '', n.allowImport ? 'nhập file' : ''].filter(Boolean).join(' · ');
      host.innerHTML = `<div class="card panel"><div class="small muted mb">${st.draft.size ? `Đã tính cả <b>${st.draft.size}</b> thay đổi chưa lưu. ` : ''}Người trong nhóm thấy <b>${U.num(m.screenCount || 0)}</b> màn hình (chưa tính quyền từ nhóm khác của họ).</div>
        <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:10px">${tree.map(p => `<div class="card panel" style="margin:0"><b>${esc(p.objectMenuName || '')}</b>${Store.nestTabs((p.children || []).slice(), c => c.metaLink).map(c => `<div class="small" style="padding:2px 0${c._tab ? ';padding-left:18px' : ''}">${c._tab ? '<span class="tag" style="font-size:10px;padding:0 6px;margin-right:4px">tab</span>' : ''}${esc(c.objectMenuName || '')}${fl(c) ? ` <span class="muted">– ${fl(c)}</span>` : ' <span class="muted">– xem</span>'}</div>`).join('') || '<div class="small muted">–</div>'}</div>`).join('') || '<div class="muted">Nhóm chưa thấy màn nào.</div>'}</div></div>`;
    });
  }

  function cmpTab(host, d) {
    const groups = Store.rows('UserGroup').filter(g => !g.IsDeleted && g.Id !== st.id).sort((a, b) => String(a.Name).localeCompare(String(b.Name)));
    host.innerHTML = `<div class="card panel"><div class="row-flex mb" style="gap:8px"><span>So sánh <b>${esc(d.group.name || '')}</b> với</span><select class="select" id="cG" style="width:auto"><option value="">– Chọn nhóm –</option>${groups.map(g => `<option value="${g.Id}" ${st.cmp === g.Id ? 'selected' : ''}>${esc(g.Name || '')}</option>`).join('')}</select></div><div id="cR"></div></div>`;
    const s = host.querySelector('#cG'); s.onchange = () => { st.cmp = s.value; cmpTab(host, d); };
    if (!st.cmp) return;
    Store.query('UserRole/Compare', { groupA: st.id, groupB: st.cmp }).then(x => {
      const m = (x.ok && x.data) || {}, rows = m.listItem || [], L = { NONE: 'Không', X: 'Xem', IX: 'Xem, in & xuất', S: 'Xem & sửa', Q: 'Toàn quyền', D: 'Được duyệt', CUSTOM: 'Tùy chỉnh' };
      host.querySelector('#cR').innerHTML = `<div class="small muted mb">${U.num(m.same || 0)} màn giống nhau · ${U.num(rows.length)} màn khác nhau</div>${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Màn hình</th><th>${esc(m.groupA || '')}</th><th>${esc(m.groupB || '')}</th></tr></thead><tbody>${rows.map(r => `<tr><td>${esc(r.parentName || '')} › ${esc(r.name || '')}</td><td>${UI.tag(L[r.levelA] || r.levelA, LV_COLOR[r.levelA])}</td><td>${UI.tag(L[r.levelB] || r.levelB, LV_COLOR[r.levelB])}</td></tr>`).join('')}</tbody></table></div>` : '<div class="muted">Hai nhóm có quyền giống nhau.</div>'}`;
    });
  }

  function histTab(host, d) {
    const rows = d.listHistory || [];
    host.innerHTML = `<div class="card panel">${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Thời gian</th><th>Người sửa</th><th>Việc</th><th>Chi tiết</th></tr></thead><tbody>${rows.map(h => `<tr><td>${esc(U.dt(h.at))}</td><td>${esc(h.by || 'hệ thống')}</td><td>${esc(h.action || '')}</td><td class="small" style="white-space:pre-line">${esc(String(h.changes || '').replace(/^- /gm, '• '))}</td></tr>`).join('')}</tbody></table></div>` : '<div class="muted">Chưa có lịch sử.</div>'}</div>`;
  }
})();
