/* P2/P3 – Món ăn, Công thức (version trên bảng Recipe), Thực đơn dạng lịch, Phân công thực đơn */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ingPrice = id => { const sm = Store.rows('SupplierMaterial').filter(s => s.IngredientId === id && s.IsPreferred); return sm.length ? sm[0].UnitPrice : 0; };
  const recipeCost = rid => U.sum(Store.by('RecipeLine', 'RecipeId', rid), l => l.Quantity * ingPrice(l.IngredientId) / 100);
  // ---------- Công thức & phiên bản ----------
  const rs = { dish: null };
  APP.register('menu-recipe', (el, f) => {
    const perm = Store.perm(f.id);
    const dishes = Store.rows('Dishe');
    if (!rs.dish) rs.dish = dishes[0].Id;
    const recs = Store.by('Recipe', 'DishId', rs.dish).sort((a, b) => b.VersionNo - a.VersionNo);
    const d = Store.get('Dishe', rs.dish);
    el.innerHTML = APP.header(f, `<button class="btn primary" id="rNew" ${perm.add === false ? 'disabled' : ''}>${I('plus')} Tạo phiên bản mới</button>`) + `
    <div class="grid" style="grid-template-columns:300px minmax(0,1fr)">
      <div class="card panel"><input class="input" style="width:100%" placeholder="Tìm món…" id="rQ"><div class="tlist mt" id="rList">${dishes.map(x => { const n = Store.by('Recipe', 'DishId', x.Id); const pend = n.some(r => ['Draft', 'PendingApproval'].includes(r.Status)); return `<div class="titem ${x.Id === rs.dish ? 'active' : ''}" data-dish="${x.Id}" data-s="${esc(x.Name.toLowerCase())}"><div><b style="font-family:inherit">${esc(x.Name)}</b><small>${esc(x.Code)} · ${n.length} phiên bản</small></div>${pend ? '<span class="n tag orange">chờ</span>' : ''}</div>`; }).join('')}</div></div>
      <div>
        <div class="card panel mb"><div class="spread"><div><div class="eyebrow">dbo.Dishe · ${esc(d.Code)}</div><h2>${esc(d.Name)}</h2><div class="small muted">${esc(d.Description || '')}</div></div><div class="row-flex">${UI.tag(Store.label('DishCategorie', d.DishCategoryId))} ${UI.badge(d.Status, 'Dishe', 'Status')}</div></div>
          <div class="notice info mt small">${I('database')}<div><b>Không tạo bảng RecipeVersion.</b> Mỗi phiên bản là một dòng <code>Recipe</code> cùng <code>DishId</code>, phân biệt bằng <code>VersionNo</code>/<code>VersionCode</code>; hiệu lực <code>EffectiveFrom/To</code>; duyệt <code>ApprovedBy/At</code>; khóa <code>LockedAt/By</code> + <code>SnapshotJson</code>.</div></div></div>
        ${recs.map(r => { const lines = Store.by('RecipeLine', 'RecipeId', r.Id); const cost = recipeCost(r.Id); return `<div class="card panel mb"><div class="spread"><div><b>${esc(r.VersionCode || 'V' + r.VersionNo)}</b> · VersionNo ${r.VersionNo} ${UI.badge(r.Status, 'Recipe', 'Status')} ${r.IsActive ? '<span class="tag green">IsActive</span>' : ''}<div class="small muted">Hiệu lực ${U.date(r.EffectiveFrom) || '–'} → ${U.date(r.EffectiveTo) || 'nay'} · Yield ${U.num(r.YieldPercent)}% · ${r.ApprovedAt ? 'Duyệt ' + U.dt(r.ApprovedAt) : 'Chưa duyệt'}${r.LockedAt ? ' · Khóa ' + U.dt(r.LockedAt) : ''}</div></div>
          <div class="row-flex">${r.Status === 'Draft' ? `<button class="btn sm orange" data-sub="${r.Id}">Gửi duyệt</button><button class="btn sm" data-addl="${r.Id}">${I('plus')} Thêm nguyên liệu</button>` : ''}${r.Status === 'Approved' ? `<button class="btn sm primary" data-lock="${r.Id}">${I('key')} Khóa & áp dụng</button>` : ''}${r.Status === 'PendingApproval' ? `<a class="btn sm" href="#/wf-inbox">Xem hộp phê duyệt →</a>` : ''}<button class="btn sm" data-open="Recipe" data-id="${r.Id}">${I('eye')}</button></div></div>
          <table class="dt mt"><thead><tr><th>#</th><th>Nguyên liệu<small>IngredientId</small></th><th class="num">Định lượng /100 suất<small>Quantity</small></th><th>ĐVT<small>UnitId</small></th><th class="num">Hao hụt %<small>WastePercent</small></th><th class="num">Giá NCC ưu tiên</th><th class="num">Chi phí /100 suất</th>${r.Status === 'Draft' ? '<th></th>' : ''}</tr></thead><tbody>${lines.map(l => `<tr><td>${l.SequenceNo}</td><td>${esc(Store.label('Ingredient', l.IngredientId))}</td><td class="num">${r.Status === 'Draft' ? `<input type="number" step="any" class="input" style="width:90px;text-align:right" data-rl="${l.Id}" value="${l.Quantity}">` : U.num(l.Quantity, 3)}</td><td>${esc(Store.label('UnitMeasure', l.UnitId))}</td><td class="num">${U.num(l.WastePercent)}</td><td class="num">${U.money(ingPrice(l.IngredientId))}</td><td class="num">${U.money(l.Quantity * ingPrice(l.IngredientId))}</td>${r.Status === 'Draft' ? `<td><button class="btn xs danger" data-rmv="${l.Id}">${I('trash')}</button></td>` : ''}</tr>`).join('')}</tbody></table>
          <div class="spread mt small"><span class="muted">Chi phí NVL ước tính / suất = Σ(Quantity × giá) ÷ 100</span><b>${U.money(cost)} / suất</b></div></div>`; }).join('') || '<div class="card panel empty">Món chưa có công thức</div>'}
      </div></div>`;
    el.querySelector('#rQ').oninput = e => el.querySelectorAll('[data-dish]').forEach(x => x.style.display = x.dataset.s.includes(e.target.value.toLowerCase()) ? '' : 'none');
    el.querySelectorAll('[data-dish]').forEach(x => x.onclick = () => { rs.dish = x.dataset.dish; APP.refresh(); });
    el.querySelectorAll('[data-rl]').forEach(inp => inp.onchange = () => { Store.update('RecipeLine', inp.dataset.rl, { Quantity: Number(inp.value) }); UI.toast('Đã cập nhật định lượng (RecipeLine.Quantity)', 'ok'); APP.refresh(); });
    el.querySelectorAll('[data-rmv]').forEach(b => b.onclick = () => { Store.remove('RecipeLine', b.dataset.rmv); APP.refresh(); });
    el.querySelectorAll('[data-addl]').forEach(b => b.onclick = () => UI.form('RecipeLine', null, { title: 'Thêm nguyên liệu vào công thức', fields: ['IngredientId', 'Quantity', 'UnitId', 'WastePercent', 'SequenceNo', 'PreparationNote'], defaults: { RecipeId: b.dataset.addl, SortOrder: 99 }, onSave: () => APP.refresh() }));
    el.querySelectorAll('[data-sub]').forEach(b => b.onclick = () => { if (!Store.by('RecipeLine', 'RecipeId', b.dataset.sub).length) return UI.toast('Công thức phải có ít nhất 1 nguyên liệu', 'err'); Store.update('Recipe', b.dataset.sub, { Status: 'PendingApproval' }); APP.submitApproval('RECIPE_VERSION', 'Recipe', b.dataset.sub, [['Quản lý bếp']], 'Duyệt phiên bản công thức'); APP.refresh(); });
    el.querySelectorAll('[data-lock]').forEach(b => b.onclick = () => UI.confirm('Khóa phiên bản này và áp dụng? Phiên bản đang dùng sẽ chuyển Obsolete và EffectiveTo = hôm qua.', () => {
      const r = Store.get('Recipe', b.dataset.lock);
      Store.by('Recipe', 'DishId', r.DishId).filter(x => x.IsActive && x.Id !== r.Id).forEach(x => Store.update('Recipe', x.Id, { IsActive: 0, Status: 'Obsolete', EffectiveTo: U.addDays(Store.ctx.date, -1) }));
      Store.update('Recipe', r.Id, { Status: 'Locked', IsActive: 1, LockedAt: U.iso(new Date()), LockedBy: Store.user.id, EffectiveFrom: Store.ctx.date, SnapshotJson: JSON.stringify(Store.by('RecipeLine', 'RecipeId', r.Id).map(l => ({ i: l.IngredientId, q: l.Quantity }))) });
      UI.toast('Đã khóa & áp dụng phiên bản ' + esc(r.VersionCode), 'ok'); APP.refresh();
    }));
    el.querySelector('#rNew').onclick = () => {
      const last = recs[0]; const v = (last ? last.VersionNo : 0) + 1;
      const r = Store.add('Recipe', { DishId: rs.dish, VersionNo: v, VersionCode: d.Code + '-V' + v, YieldPercent: last ? last.YieldPercent : 92, YieldQuantity: 100, StandardOutputQty: 100, YieldUnitId: last && last.YieldUnitId, Status: 'Draft', IsActive: 0, Instructions: last && last.Instructions, Note: 'Sao chép từ ' + (last ? last.VersionCode : '') });
      if (last) Store.by('RecipeLine', 'RecipeId', last.Id).forEach(l => Store.add('RecipeLine', { RecipeId: r.Id, IngredientId: l.IngredientId, Quantity: l.Quantity, UnitId: l.UnitId, WastePercent: l.WastePercent, YieldPercent: l.YieldPercent, SequenceNo: l.SequenceNo, SortOrder: l.SortOrder }));
      UI.toast('Đã tạo phiên bản nháp ' + esc(r.VersionCode) + ' (sao chép định lượng)', 'ok'); APP.refresh();
    };
  });
  // ---------- Thực đơn dạng lịch tuần ----------
  const ms = { menu: null, mt: 'LUNCH' };
  APP.register('menu-menu', (el, f) => {
    const menus = Store.rows('Menu'); if (!ms.menu || !Store.get('Menu', ms.menu)) ms.menu = (menus[0] || {}).Id;
    if (!ms.menu) return APP.genericList(el, f);
    const mn = Store.get('Menu', ms.menu);
    const lines = Store.by('MenuLine', 'MenuId', mn.Id);
    const mts = [...new Set(lines.map(l => l.MealType))];
    const DOW = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
    const meals = U.sum(Store.rows('OrderLine').filter(l => l.MenuId === mn.Id), 'Quantity');
    const next = APP.nextStatus('Menu', 'Status', mn.Status);
    el.innerHTML = APP.header(f, `${next ? `<button class="btn orange" id="mNext">${I('approve')} Chuyển sang ${UI.statusVi('Menu', 'Status', next)}</button>` : ''}<button class="btn primary" id="mAdd">${I('plus')} Thêm món</button>`) + `
    <div class="row-flex mb">${menus.map(m => `<button class="chip ${m.Id === mn.Id ? 'active' : ''}" data-m="${m.Id}">${esc(m.Code)} ${UI.badge(m.Status, 'Menu', 'Status')}</button>`).join('')}</div>
    <div class="kpis">${UI.kpi('Thực đơn', esc(mn.Name), `${U.date(mn.EffectiveFrom)} – ${U.date(mn.EffectiveTo)} · ${esc(mn.MenuType || '')}`, 'calendar', 'green')}${UI.kpi('Số dòng món', lines.length, 'dbo.MenuLine', 'utensils', 'blue')}${UI.kpi('Khách hàng được gán', new Set(Store.by('MenuAssignment', 'MenuId', mn.Id).map(a => a.CustomerId)).size, 'MenuAssignment (BRD: không tự áp dụng)', 'building', 'orange', 'menu-assignment')}${UI.kpi('Suất đã đặt theo thực đơn', U.num(meals), 'Σ OrderLine.Quantity', 'clipboard', 'purple')}</div>
    <div class="card panel"><div class="row-flex mb"><span class="small muted">Loại suất (MenuLine.MealType):</span>${mts.map(m => `<button class="chip ${m === ms.mt ? 'active' : ''}" data-mt="${m}">${m}</button>`).join('')}</div>
      <div class="cal"><div class="h">Nhóm món</div>${DOW.map((d, i) => `<div class="h">${d} · ${U.date(U.addDays(U.day(mn.EffectiveFrom), i)).slice(0, 5)}</div>`).join('')}
      ${Store.rows('DishCategorie').map(cat => { const any = lines.some(l => l.MealType === ms.mt && (Store.get('Dishe', l.DishId) || {}).DishCategoryId === cat.Id); if (!any) return ''; return `<div class="rowh">${esc(cat.Name)}</div>${DOW.map((_, i) => `<div class="c">${lines.filter(l => l.MealType === ms.mt && l.DayOfWeek === i + 1 && (Store.get('Dishe', l.DishId) || {}).DishCategoryId === cat.Id).map(l => `<div><a href="javascript:void 0" data-open="MenuLine" data-id="${l.Id}">${esc(Store.label('Dishe', l.DishId))}</a></div>`).join('') || '<span class="muted">–</span>'}</div>`).join('')}`; }).join('')}</div>
      <div class="small muted mt">Ô = MenuLine (MenuId, DishId, MealType, DayOfWeek). Bấm vào món để xem/sửa chi tiết.</div></div>`;
    el.querySelectorAll('[data-m]').forEach(b => b.onclick = () => { ms.menu = b.dataset.m; APP.refresh(); });
    el.querySelectorAll('[data-mt]').forEach(b => b.onclick = () => { ms.mt = b.dataset.mt; APP.refresh(); });
    el.querySelector('#mAdd').onclick = () => UI.form('MenuLine', null, { title: 'Thêm món vào thực đơn', fields: ['DishId', 'MealType', 'DayOfWeek', 'SequenceNo', 'PortionQty', 'IsRequired', 'Note'], defaults: { MenuId: mn.Id, MealType: ms.mt, SortOrder: 99 }, validate: o => (o.DayOfWeek < 1 || o.DayOfWeek > 7) ? 'DayOfWeek phải từ 1 (T2) đến 7 (CN)' : null, onSave: () => APP.refresh() });
    const nb = el.querySelector('#mNext'); if (nb) nb.onclick = () => UI.confirm(`Chuyển thực đơn sang <b>${UI.statusVi('Menu', 'Status', next)}</b>?`, () => { Store.update('Menu', mn.Id, { Status: next }); UI.toast('Đã cập nhật Menu.Status', 'ok'); APP.refresh(); });
  });
  // ---------- Món ăn: thêm cột giá thành ----------
  APP.register('menu-dish', (el, f) => APP.genericList(el, f, {
    cols: [{ k: 'Code', label: 'Mã' }, { k: 'Name', label: 'Món ăn' }, { k: 'DishCategoryId', label: 'Nhóm món' }, { k: 'StandardCost', label: 'Giá thành chuẩn', num: true },
      { k: 'x', label: 'Công thức đang dùng', col: false, nosort: true, render: r => { const rc = Store.by('Recipe', 'DishId', r.Id).find(x => x.IsActive); return rc ? `${esc(rc.VersionCode)} ${UI.badge(rc.Status, 'Recipe', 'Status')}` : '<span class="bad small">Chưa có công thức hiệu lực</span>'; } },
      { k: 'y', label: 'CP NVL ước tính', col: false, num: true, nosort: true, render: r => { const rc = Store.by('Recipe', 'DishId', r.Id).find(x => x.IsActive) || Store.by('Recipe', 'DishId', r.Id)[0]; return rc ? U.money(recipeCost(rc.Id)) : '–'; } }, { k: 'Status', label: 'Trạng thái' }]
  }));
})();
