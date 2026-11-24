/* UI-6.21 (D-102): nhóm (nguyên liệu / món ăn) quản lý ngay ở cột trái của màn Nguyên liệu / Món ăn – mục menu nhóm riêng đã ẩn.
 * UI.groupPanel(box, opts): danh sách nhóm + số bản ghi đang dùng, "Tất cả" / "Chưa phân nhóm", bấm = lọc bảng (giữ khi quay lại), ⋯ Đổi tên (tại chỗ,
 * Enter lưu / Esc hủy) · Ngừng dùng (khóa khi còn bản ghi đang dùng) · Dùng lại, "+ Thêm nhóm" (dòng mới gõ tên). Bảng nhóm không có cột thứ tự → sắp theo tên,
 * không kéo thả (không thêm cột). Điện thoại: dải chip cuộn ngang trên bảng. Dùng lại được cho danh mục nhóm khác.
 * APP.groupList(el, f, listOpt, groupOpt): APP.genericList + cột nhóm bên trái + lọc theo nhóm + tiêu đề bảng theo nhóm. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const sel = {};   // nhóm đang chọn theo màn (giữ khi quay lại)
  APP.setGroup = (screen, id) => { sel[screen] = id; };
  UI.groupPanel = (box, o) => {
    const can = a => { const p = Store.perm(o.screen); return p.access !== false && p[a] !== false; };
    const draw = () => Store.query(o.api).then(x => {
      if (!box.isConnected) return;
      if (!x.ok || !x.data) { box.innerHTML = `<div class="small muted">${esc(x.message || 'Không tải được nhóm')}</div>`; return; }
      const d = x.data, groups = d.listGroup || [];
      o.onData && o.onData(d);
      const cur = o.selected();
      const lineOf = g => !o.lineAssign || !g ? '' : `<span class="grp-line ${!g.productionLineId || g.productionLineInactive ? 'warn' : ''}" title="Dây chuyền nấu các món của nhóm (UI-6.26)">${g.productionLineId ? esc(g.productionLineName || '') + (g.productionLineInactive ? ' (đã ngừng)' : '') : 'Chưa gán dây chuyền'}</span>`;
      const item = (id, name, n, extra, off, g) => `<div class="grp-it ${cur === id ? 'on' : ''} ${off ? 'off' : ''}" data-g="${esc(id)}"><span class="nm" title="${esc(name)}">${esc(name)}${lineOf(g)}</span><span class="n">${U.num(n)}</span>${extra || ''}</div>`;
      box.innerHTML = `<div class="spread grp-head"><b>${esc(o.label)}</b>${can('add') ? `<button class="btn xs" data-gadd>${I('plus')} Thêm nhóm</button>` : ''}</div>
        <div class="grp-list">${item('', 'Tất cả nhóm', d.totalActive)}${groups.map(g => item(g.id, g.name || g.code || '', g.activeCount, can('edit') ? `<button class="grp-more" data-gm="${esc(g.id)}" aria-label="Thao tác nhóm">⋯</button>` : '', g.status === 'Inactive', g)).join('')}${d.ungrouped ? item('__none', 'Chưa phân nhóm', d.ungrouped) : ''}</div>
        <div class="small muted mt grp-hint">Bấm nhóm để lọc. ${can('edit') ? '⋯ để đổi tên / ngừng dùng – nhóm còn ' + esc(o.itemLabel) + ' đang dùng thì không ngừng được.' : ''}</div>`;
      box.querySelectorAll('[data-g]').forEach(r => r.onclick = e => { if (e.target.closest('button,input')) return; o.onSelect(r.dataset.g); });
      const save = (g, patch) => Store.act(o.ctl + '/Update', { method: 'PUT', form: Object.assign({ Id: g.id, Code: g.code, Name: g.name, Status: g.status }, patch), tables: [o.table] }).then(r => { if (r.ok) { UI.toast('Đã lưu nhóm', 'ok'); draw(); o.onChanged && o.onChanged(); } });
      box.querySelectorAll('[data-gm]').forEach(b => b.onclick = e => {
        e.stopPropagation();
        const g = groups.find(x => x.id === b.dataset.gm), row = b.closest('[data-g]');
        box.querySelectorAll('.grp-menu').forEach(m => m.remove());
        row.insertAdjacentHTML('beforeend', `<div class="grp-menu"><button data-ga="rename">Đổi tên</button>${o.lineAssign && g.status !== 'Inactive' ? '<button data-ga="line">Gán dây chuyền</button>' : ''}${g.status === 'Inactive' ? '<button data-ga="on">Dùng lại</button>' : g.activeCount ? `<button disabled title="Chuyển ${esc(o.itemLabel)} sang nhóm khác trước">Ngừng dùng (còn ${U.num(g.activeCount)} ${esc(o.itemLabel)})</button>` : '<button data-ga="off">Ngừng dùng</button>'}</div>`);
        row.querySelectorAll('[data-ga]').forEach(a => a.onclick = ev => {
          ev.stopPropagation(); const act = a.dataset.ga; row.querySelector('.grp-menu').remove();
          if (act === 'off') return save(g, { Status: 'Inactive' });
          if (act === 'on') return save(g, { Status: 'Active' });
          if (act === 'line') return Store.query('ProductionLine/GetOverview').then(x => {
            const lines = ((x.ok && x.data && x.data.listLine) || []).filter(l => l.status !== 'Inactive');
            UI.modal({ title: 'Gán dây chuyền cho nhóm ' + esc(g.name || ''), size: 'sm',
              body: `<p class="small muted" style="margin-top:0">Món của nhóm sẽ được chia mẻ theo công suất và thời gian mỗi mẻ của dây chuyền khi ra lệnh sản xuất. Chưa gán → nấu 1 mẻ.</p>
                <div class="field"><label>Dây chuyền</label><select class="select" id="gpLine"><option value="00000000-0000-0000-0000-000000000000">— Chưa gán —</option>${lines.map(l => `<option value="${esc(l.id)}" ${l.id === g.productionLineId ? 'selected' : ''}>${esc(l.name || l.code)} · ${U.num(l.capacityMealPerBatch)} suất / mẻ · ${U.num(l.minutesPerBatch)} phút</option>`).join('')}</select></div>`,
              foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="gpOk">Lưu</button>',
              onMount: (m, close) => { m.querySelector('#gpOk').onclick = () => { close(); save(g, { ProductionLineId: m.querySelector('#gpLine').value }); }; } });
          });
          const nm = row.querySelector('.nm'); nm.innerHTML = `<input class="input grp-in" value="${esc(g.name || '')}">`;
          const inp = nm.querySelector('input'); inp.focus(); inp.select();
          inp.onkeydown = k => { if (k.key === 'Escape') draw(); if (k.key === 'Enter') { const v = inp.value.trim(); if (!v) return UI.toast('Nhập tên nhóm', 'err'); if (v === g.name) return draw(); save(g, { Name: v }); } };
        });
        document.addEventListener('click', () => box.querySelectorAll('.grp-menu').forEach(m => m.remove()), { once: true });
      });
      const add = box.querySelector('[data-gadd]');
      if (add) add.onclick = () => {
        const list = box.querySelector('.grp-list');
        if (list.querySelector('.grp-new')) return;
        list.insertAdjacentHTML('beforeend', `<div class="grp-it grp-new"><input class="input grp-in" placeholder="Tên nhóm mới – Enter để lưu"></div>`);
        const inp = list.querySelector('.grp-new input'); inp.focus();
        inp.onkeydown = k => {
          if (k.key === 'Escape') return draw();
          if (k.key !== 'Enter') return; const v = inp.value.trim(); if (!v) return;
          Store.act(o.ctl + '/Insert', { form: { Name: v, Status: 'Active' }, tables: [o.table] }).then(r => { if (r.ok) { UI.toast('Đã thêm nhóm ' + esc(v), 'ok'); draw(); o.onChanged && o.onChanged(); } });
        };
      };
    });
    draw();
    return { reload: draw };
  };
  // Danh sách + cột nhóm bên trái. groupOpt: { fields: [cột nhóm, cột cũ], api, ctl, table, screen, label, itemLabel, title(id, name, count) → Promise<string> }
  APP.groupList = (el, f, opt, g) => {
    const groupOf = r => r[g.fields[0]] || (g.fields[1] ? r[g.fields[1]] : null);
    const cur = () => sel[f.id] || '';
    const base = opt.rows || (() => Store.rows(f.table));
    const rows = () => { const s = cur(); return base().filter(r => !s || (s === '__none' ? !groupOf(r) : groupOf(r) === s)); };
    const defaults = cur() && cur() !== '__none' ? Object.assign({}, opt.defaults || {}, { [g.fields[0]]: cur() }) : opt.defaults;
    const tbl = APP.genericList(el, f, Object.assign({}, opt, { rows, defaults }));
    const card = el.querySelector('#gt') && el.querySelector('#gt').closest('.card');
    if (!card) return tbl;
    const wrap = document.createElement('div'); wrap.className = 'grp-layout';
    card.parentNode.insertBefore(wrap, card);
    const side = document.createElement('div'); side.className = 'card panel grp-side';
    wrap.appendChild(side); wrap.appendChild(card);
    const head = document.createElement('div'); head.className = 'grp-title'; card.insertBefore(head, card.firstChild);
    let names = {};
    UI.groupPanel(side, Object.assign({}, g, {
      selected: cur,
      onSelect: id => { sel[f.id] = id; APP.refresh(); },
      onChanged: () => Store.reload([g.table]).then(() => APP.refresh()),
      onData: d => {
        names = {}; (d.listGroup || []).forEach(x => { names[x.id] = x; });
        const s = cur(), n = rows().length;
        if (!s) { head.innerHTML = ''; return; }
        if (s === '__none') { head.innerHTML = `<div class="spread mb"><b>Chưa phân nhóm · ${U.num(n)} ${esc(g.itemLabel)}</b></div>`; return; }
        const x = names[s]; if (!x) { sel[f.id] = ''; return; }
        head.innerHTML = `<div class="spread mb"><b>${esc(x.name || '')} · ${U.num(x.activeCount)} ${esc(g.itemLabel)}</b><span class="small muted" data-gt></span></div>${!n && Store.perm(f.id).add !== false ? `<div class="notice info mb">${I('help')}<div>Nhóm chưa có ${esc(g.itemLabel)}. <button class="btn xs primary" data-gfirst>${I('plus')} Thêm ${esc(g.itemLabel)} vào nhóm này</button></div></div>` : ''}`;
        const fb = head.querySelector('[data-gfirst]'); if (fb) fb.onclick = () => UI.form(f.table, null, { title: f.name, defaults, onSave: () => APP.refresh() });
        if (g.title) g.title(s).then(t => { const sp = head.querySelector('[data-gt]'); if (sp && t) sp.textContent = t; });
      },
    }));
    return tbl;
  };

  // ---------- Màn Nguyên liệu (scm-ingredient) ----------
  const ST_TAG = { CHILLED: ['Mát', 'blue'], FROZEN: ['Đông lạnh', 'purple'], DRY: ['Khô / thường', 'gray'], AMBIENT: ['Khô / thường', 'gray'] };
  const base = APP.SCREENS['scm-ingredient'];
  APP.register('scm-ingredient', (el, f, p) => {
    if (!Store.isLive) return base ? base(el, f, p) : APP.genericList(el, f);
    const pref = {}; Store.rows('SupplierMaterial').filter(m => m.IsPreferred && m.Status !== 'Inactive').forEach(m => { pref[m.IngredientId] = m.SupplierId; });
    const priced = new Set(Store.rows('SupplierMaterial').filter(m => m.Status !== 'Inactive' && +m.UnitPrice > 0).map(m => m.IngredientId));
    const all = Store.rows('Ingredient'), act = all.filter(r => r.Status !== 'Inactive');
    const kpis = UI.kpis([
      UI.kpi('Nguyên liệu đang dùng', U.num(act.length), '', 'package', 'green'),
      UI.kpi('Chưa có giá NCC', U.num(act.filter(r => !priced.has(r.Id)).length), 'bảng giá nhà cung cấp', 'calc', 'orange'),
      UI.kpi('Chưa phân nhóm', U.num(act.filter(r => !r.IngredientCategoryId && !r.CategoryId).length), '', 'layers', 'blue'),
      UI.kpi('Nhóm nguyên liệu', U.num(Store.rows('IngredientCategorie').filter(c => c.Status !== 'Inactive').length), 'đang dùng', 'clipboard', 'blue'),
    ]);
    APP.groupList(el, f, {
      top: '<div class="notice info mb">' + I('help') + '<div>Danh mục nguyên liệu theo nhóm. Nhóm quản lý ngay ở cột trái – không còn mục menu "Nhóm nguyên liệu" riêng.</div></div>' + kpis,
      kpis: '',
      cols: [{ k: 'Code', label: 'Mã' }, { k: 'Name', label: 'Nguyên liệu' }, { k: 'IngredientCategoryId', label: 'Nhóm' }, { k: 'BaseUnitId', label: 'ĐVT kho' },
        { k: 'StorageType', label: 'Bảo quản', render: r => { const t = ST_TAG[String(r.StorageType || '').toUpperCase()]; return t ? UI.tag(t[0], t[1]) : '<span class="muted">–</span>'; } },
        { k: 'ShelfLifeDays', label: 'Hạn dùng', num: true, render: r => (r.ShelfLifeDays ? U.num(r.ShelfLifeDays) + ' ngày' : '–') },
        { k: 'x', label: 'NCC ưu tiên', col: false, nosort: true, render: r => (pref[r.Id] ? esc(Store.label('Supplier', pref[r.Id])) : '<span class="muted">–</span>') }, { k: 'Status', label: 'Trạng thái' }],
    }, { fields: ['IngredientCategoryId', 'CategoryId'], api: 'IngredientCategory/GetWithCount', ctl: 'IngredientCategory', table: 'IngredientCategorie', screen: 'scm-ingredient', label: 'Nhóm nguyên liệu', itemLabel: 'nguyên liệu',
      title: id => Store.query('Ingredient/GetCategoryDefaults', { categoryId: id }).then(x => { const d = x.ok && x.data; if (!d || !d.sampleCount) return ''; const t = ST_TAG[d.storageType]; return 'thường bảo quản ' + (t ? t[0] : '–') + (d.baseUnitId ? ', đơn vị ' + Store.label('UnitMeasure', d.baseUnitId) : ''); }) });
    if (!APP._smLoaded) { APP._smLoaded = true; Promise.all(['SupplierMaterial', 'Supplier', 'IngredientCategorie'].map(t => Store.ensure(t))).then(() => { if (APP.current === 'scm-ingredient') APP.refresh(); }); }
  });

  // ---------- Màn Món ăn (menu-dish): cột nhóm món, không gợi ý thuộc tính ----------
  const baseDish = APP.SCREENS['menu-dish'];
  APP.dishListOpt = APP.dishListOpt || null;
  APP.register('menu-dish', (el, f, p) => {
    if (!Store.isLive || !APP.dishListOpt) return baseDish(el, f, p);
    const all = Store.rows('Dishe'), act = all.filter(r => r.Status !== 'Inactive');
    const withRecipe = new Set(Store.rows('Recipe').filter(r => r.IsActive).map(r => r.DishId));
    const kpis = UI.kpis([
      UI.kpi('Món đang dùng', U.num(act.length), '', 'utensils', 'green'),
      UI.kpi('Chưa có công thức hiệu lực', U.num(act.filter(r => !withRecipe.has(r.Id)).length), '', 'clipboard', 'orange'),
      UI.kpi('Chưa phân nhóm', U.num(act.filter(r => !r.DishCategoryId && !r.CategoryId).length), '', 'layers', 'blue'),
      UI.kpi('Nhóm món', U.num(Store.rows('DishCategorie').filter(c => c.Status !== 'Inactive').length), 'đang dùng', 'clipboard', 'blue'),
    ]);
    APP.groupList(el, f, Object.assign({ kpis: '', top: '<div class="notice info mb">' + I('help') + '<div>Danh mục món ăn theo nhóm. Nhóm món quản lý ngay ở cột trái – không còn mục menu "Nhóm món ăn" riêng.</div></div>' + kpis }, APP.dishListOpt),
      { fields: ['DishCategoryId', 'CategoryId'], api: 'DishCategory/GetWithCount', ctl: 'DishCategory', table: 'DishCategorie', screen: 'menu-dish', label: 'Nhóm món', itemLabel: 'món', lineAssign: true });
  });
})();
