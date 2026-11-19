/* P2/P3 – Món ăn, Công thức (version trên bảng Recipe), Thực đơn dạng lịch, Phân công thực đơn */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ingPrice = id => { const sm = Store.rows('SupplierMaterial').filter(s => s.IngredientId === id && s.IsPreferred); return sm.length ? sm[0].UnitPrice : 0; };
  const recipeCost = rid => U.sum(Store.by('RecipeLine', 'RecipeId', rid), l => l.Quantity * ingPrice(l.IngredientId) / 100);
  // ---------- Chế độ API: số liệu do server tính (giá vốn công thức, danh sách + lịch thực đơn) ----------
  // Lưu tạm theo khóa; bảng liên quan tải lại từ server (sau khi ghi) thì bỏ để hỏi lại.
  const live = { ver: {}, list: null, cal: {} };
  const overlayOpen = () => !!document.querySelector('.overlay, .drawer-wrap');
  const redraw = id => { if (APP.current === id && !overlayOpen()) APP.refresh(); };
  Store.onLoaded(t => {
    if (!t || t === 'Recipe' || t === 'RecipeLine') live.ver = {};
    if (!t || t === 'Menu' || t === 'MenuLine' || t === 'MenuAssignment') { live.list = null; live.cal = {}; }
  });
  // Recipe/GetByDishId: các phiên bản của món kèm giá vốn nguyên liệu / suất. Trả { [recipeId]: { costPerMeal, costComplete } } hoặc null khi chưa có.
  const rs = { dish: null };
  const versionCosts = dishId => {
    if (!Store.isLive || !dishId) return null;
    let c = live.ver[dishId];
    if (!c) {
      c = live.ver[dishId] = { map: null };
      Store.query('Recipe/GetByDishId', { dishId }).then(x => {
        c.map = {}; if (x.ok) x.list.forEach(v => { if (v.recipe && v.recipe.id) c.map[v.recipe.id] = v; });
        if (live.ver[dishId] === c && rs.dish === dishId) redraw('menu-recipe');
      });
    }
    return c.map;
  };
  // Menu/GetList: số dòng món, số khách được gán, số suất đã đặt của từng thực đơn.
  const menuList = () => {
    if (!Store.isLive) return null;
    let c = live.list;
    if (!c) {
      c = live.list = { map: null };
      Store.query('Menu/GetList').then(x => {
        c.map = {}; if (x.ok) x.list.forEach(it => { if (it.menu && it.menu.id) c.map[it.menu.id] = it; });
        if (live.list === c) redraw('menu-menu');
      });
    }
    return c;
  };
  // Menu/GetCalendar: lịch tuần theo loại suất kèm giá vốn / suất từng món và từng ngày.
  const menuCalendar = (menuId, mealType) => {
    if (!Store.isLive || !menuId || !mealType) return null;
    const key = menuId + '|' + mealType; let c = live.cal[key];
    if (!c) {
      c = live.cal[key] = { data: null };
      Store.query('Menu/GetCalendar', { id: menuId, mealType }).then(x => {
        c.data = x.ok ? x.data : null;
        if (live.cal[key] === c && x.ok) redraw('menu-menu');
      });
    }
    return c.data;
  };
  // Recipe/CalculateCost: bảng giá vốn nguyên liệu của một phiên bản cho số suất nhập vào.
  const costModal = r => UI.modal({
    title: 'Giá vốn nguyên liệu – ' + esc(r.VersionCode || 'V' + r.VersionNo), sub: esc(Store.label('Dishe', r.DishId)), size: 'lg',
    body: `<div class="row-flex mb"><span class="small muted">Số suất cần tính:</span><input type="number" min="1" step="1" class="input" id="ccN" value="100" style="width:120px;text-align:right"><button class="btn primary" id="ccRun">${I('calc')} Tính</button></div><div id="ccOut"></div>`,
    onMount: m => {
      const out = m.querySelector('#ccOut');
      const run = () => {
        const n = Number(m.querySelector('#ccN').value);
        if (!(n > 0)) return UI.toast('Số suất phải lớn hơn 0', 'err');
        out.innerHTML = '<div class="empty">Đang tính…</div>';
        Store.query('Recipe/CalculateCost', { id: r.Id, mealCount: n }).then(x => {
          if (!out.isConnected) return;
          if (!x.ok || !x.data) { out.innerHTML = `<div class="notice warn">${I('alert')}<div>${esc(x.message || 'Không tính được giá vốn')}</div></div>`; return; }
          const d = x.data, ls = d.listLine || [];
          out.innerHTML = `<div class="kv mb"><div><label>Giá vốn nguyên liệu / suất</label><span><b>${U.money(d.costPerMeal)}</b></span></div><div><label>Tổng cho ${U.num(d.mealCount)} suất</label><span><b>${U.money(d.totalMaterialCost)}</b></span></div></div>
            ${d.complete === false ? `<div class="notice warn mb">${I('alert')}<div>Có nguyên liệu chưa tính được chi phí – giá vốn đang thấp hơn thực tế.</div></div>` : ''}
            <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>#</th><th>Nguyên liệu</th><th class="num">Định lượng / ${U.num(d.standardServings)} suất</th><th>ĐVT</th><th class="num">Đơn giá</th><th>ĐVT giá</th><th class="num">Thành tiền / ${U.num(d.standardServings)} suất</th><th>Ghi chú</th></tr></thead>
            <tbody>${ls.map((l, i) => `<tr><td>${i + 1}</td><td>${esc(l.ingredientName || Store.label('Ingredient', l.ingredientId))}</td><td class="num">${U.num(l.quantity, 3)}</td><td>${esc(Store.label('UnitMeasure', l.unitId))}</td><td class="num">${l.unitPrice == null ? '–' : U.money(l.unitPrice)}</td><td>${l.priceUnitId ? esc(Store.label('UnitMeasure', l.priceUnitId)) : '–'}</td><td class="num">${l.amount == null ? '–' : U.money(l.amount)}</td><td>${l.warning ? `<span class="warn small">${esc(l.warning)}</span>` : ''}</td></tr>`).join('') || '<tr><td colspan="8" class="empty">Công thức chưa có nguyên liệu</td></tr>'}</tbody></table></div>`;
        });
      };
      m.querySelector('#ccRun').onclick = run; run();
    }
  });
  // ---------- Công thức & phiên bản ----------
  APP.register('menu-recipe', (el, f) => {
    const perm = Store.perm(f.id);
    const dishes = Store.rows('Dishe');
    if (!rs.dish) rs.dish = dishes[0].Id;
    const recs = Store.by('Recipe', 'DishId', rs.dish).sort((a, b) => b.VersionNo - a.VersionNo);
    const d = Store.get('Dishe', rs.dish);
    const vc = versionCosts(rs.dish);   // chế độ API: giá vốn / suất do server tính
    el.innerHTML = APP.header(f, `<button class="btn primary" id="rNew" ${perm.add === false ? 'disabled' : ''}>${I('plus')} Tạo phiên bản mới</button>`) + `
    <div class="grid" style="grid-template-columns:300px minmax(0,1fr)">
      <div class="card panel"><input class="input" style="width:100%" placeholder="Tìm món…" id="rQ"><div class="tlist mt" id="rList">${dishes.map(x => { const n = Store.by('Recipe', 'DishId', x.Id); const pend = n.some(r => ['Draft', 'PendingApproval'].includes(r.Status)); return `<div class="titem ${x.Id === rs.dish ? 'active' : ''}" data-dish="${x.Id}" data-s="${esc(x.Name.toLowerCase())}"><div><b style="font-family:inherit">${esc(x.Name)}</b><small>${esc(x.Code)} · ${n.length} phiên bản</small></div>${pend ? '<span class="n tag orange">chờ</span>' : ''}</div>`; }).join('')}</div></div>
      <div>
        <div class="card panel mb"><div class="spread"><div><div class="eyebrow">dbo.Dishe · ${esc(d.Code)}</div><h2>${esc(d.Name)}</h2><div class="small muted">${esc(d.Description || '')}</div></div><div class="row-flex">${UI.tag(Store.label('DishCategorie', d.DishCategoryId))} ${UI.badge(d.Status, 'Dishe', 'Status')}</div></div>
          ${Store.isLive ? '' : `<div class="notice info mt small">${I('database')}<div><b>Không tạo bảng RecipeVersion.</b> Mỗi phiên bản là một dòng <code>Recipe</code> cùng <code>DishId</code>, phân biệt bằng <code>VersionNo</code>/<code>VersionCode</code>; hiệu lực <code>EffectiveFrom/To</code>; duyệt <code>ApprovedBy/At</code>; khóa <code>LockedAt/By</code> + <code>SnapshotJson</code>.</div></div>`}</div>
        ${recs.map(r => { const lines = Store.by('RecipeLine', 'RecipeId', r.Id); const cost = recipeCost(r.Id); return `<div class="card panel mb"><div class="spread"><div><b>${esc(r.VersionCode || 'V' + r.VersionNo)}</b> · VersionNo ${r.VersionNo} ${UI.badge(r.Status, 'Recipe', 'Status')} ${r.IsActive ? '<span class="tag green">IsActive</span>' : ''}<div class="small muted">Hiệu lực ${U.date(r.EffectiveFrom) || '–'} → ${U.date(r.EffectiveTo) || 'nay'} · Yield ${U.num(r.YieldPercent)}% · ${r.ApprovedAt ? 'Duyệt ' + U.dt(r.ApprovedAt) : 'Chưa duyệt'}${r.LockedAt ? ' · Khóa ' + U.dt(r.LockedAt) : ''}</div></div>
          <div class="row-flex">${r.Status === 'Draft' ? `<button class="btn sm orange" data-sub="${r.Id}">Gửi duyệt</button><button class="btn sm" data-addl="${r.Id}">${I('plus')} Thêm nguyên liệu</button>` : ''}${r.Status === 'Approved' ? `<button class="btn sm primary" data-lock="${r.Id}">${I('key')} Khóa & áp dụng</button>` : ''}${r.Status === 'PendingApproval' ? `<a class="btn sm" href="${APP.href('wf-inbox')}">Xem hộp phê duyệt →</a>` : ''}${Store.isLive ? `<button class="btn sm" data-cost="${r.Id}">${I('calc')} Tính giá vốn</button>` : ''}<button class="btn sm" data-open="Recipe" data-id="${r.Id}">${I('eye')}</button></div></div>
          <table class="table dc-table dt mt"><thead><tr><th>#</th><th>Nguyên liệu<small>IngredientId</small></th><th class="num">Định lượng /100 suất<small>Quantity</small></th><th>ĐVT<small>UnitId</small></th><th class="num">Hao hụt %<small>WastePercent</small></th><th class="num">Giá NCC ưu tiên</th><th class="num">Chi phí /100 suất</th>${r.Status === 'Draft' ? '<th></th>' : ''}</tr></thead><tbody>${lines.map(l => `<tr><td>${l.SequenceNo}</td><td>${esc(Store.label('Ingredient', l.IngredientId))}</td><td class="num">${r.Status === 'Draft' ? `<input type="number" step="any" class="input" style="width:90px;text-align:right" data-rl="${l.Id}" value="${l.Quantity}">` : U.num(l.Quantity, 3)}</td><td>${esc(Store.label('UnitMeasure', l.UnitId))}</td><td class="num">${U.num(l.WastePercent)}</td><td class="num">${U.money(ingPrice(l.IngredientId))}</td><td class="num">${U.money(l.Quantity * ingPrice(l.IngredientId))}</td>${r.Status === 'Draft' ? `<td><button class="btn xs danger" data-rmv="${l.Id}">${I('trash')}</button></td>` : ''}</tr>`).join('')}</tbody></table>
          ${Store.isLive ? (() => { const v = vc && vc[r.Id]; return `<div class="spread mt small"><span class="muted">Giá vốn nguyên liệu / suất theo giá nhà cung cấp ưu tiên${v && v.costComplete === false ? ' · <span class="warn">còn nguyên liệu thiếu giá hoặc thiếu quy đổi đơn vị</span>' : ''}</span><b>${v ? U.money(v.costPerMeal) : vc ? U.money(cost) : '…'} / suất</b></div>`; })() : `<div class="spread mt small"><span class="muted">Chi phí NVL ước tính / suất = Σ(Quantity × giá) ÷ 100</span><b>${U.money(cost)} / suất</b></div>`}</div>`; }).join('') || '<div class="card panel empty">Món chưa có công thức</div>'}
      </div></div>`;
    el.querySelector('#rQ').oninput = e => el.querySelectorAll('[data-dish]').forEach(x => x.style.display = x.dataset.s.includes(e.target.value.toLowerCase()) ? '' : 'none');
    el.querySelectorAll('[data-dish]').forEach(x => x.onclick = () => { rs.dish = x.dataset.dish; APP.refresh(); });
    el.querySelectorAll('[data-rl]').forEach(inp => inp.onchange = () => { Store.update('RecipeLine', inp.dataset.rl, { Quantity: Number(inp.value) }); UI.toast('Đã cập nhật định lượng (RecipeLine.Quantity)', 'ok'); APP.refresh(); });
    el.querySelectorAll('[data-rmv]').forEach(b => b.onclick = () => { Store.remove('RecipeLine', b.dataset.rmv); APP.refresh(); });
    el.querySelectorAll('[data-addl]').forEach(b => b.onclick = () => UI.form('RecipeLine', null, { title: 'Thêm nguyên liệu vào công thức', fields: ['IngredientId', 'Quantity', 'UnitId', 'WastePercent', 'SequenceNo', 'PreparationNote'], defaults: { RecipeId: b.dataset.addl, SortOrder: 99 }, onSave: () => APP.refresh() }));
    el.querySelectorAll('[data-cost]').forEach(b => b.onclick = () => costModal(Store.get('Recipe', b.dataset.cost)));
    // Gửi duyệt – chế độ API: Recipe/Submit (Nháp → Chờ duyệt, backend tạo yêu cầu duyệt phiên bản công thức).
    el.querySelectorAll('[data-sub]').forEach(b => b.onclick = () => {
      const id = b.dataset.sub, r = Store.get('Recipe', id);
      if (!Store.by('RecipeLine', 'RecipeId', id).length) return UI.toast('Công thức phải có ít nhất 1 nguyên liệu', 'err');
      UI.confirm(`Gửi duyệt phiên bản <b>${esc(r.VersionCode || 'V' + r.VersionNo)}</b>? Sau khi gửi không sửa được định lượng cho tới khi có kết quả duyệt.`, () =>
        Store.act('Recipe/Submit', { form: { id }, tables: ['Recipe', 'ApprovalRequest', 'ApprovalStep', 'ApprovalItem'], demo: () => { Store.update('Recipe', id, { Status: 'PendingApproval' }); APP.submitApproval('RECIPE_VERSION', 'Recipe', id, [['Quản lý bếp']], 'Duyệt phiên bản công thức'); } })
          .then(x => { if (!x.ok) return; if (!x.demo) UI.toast('Đã gửi duyệt phiên bản công thức', 'ok'); APP.refresh(); }), { ok: 'Gửi duyệt' });
    });
    // Khóa & áp dụng – chế độ API: Recipe/Lock (chỉ bản Đã duyệt; bản đang dùng ngừng áp dụng, tính lại giá thành chuẩn của món).
    el.querySelectorAll('[data-lock]').forEach(b => b.onclick = () => {
      const r = Store.get('Recipe', b.dataset.lock);
      UI.modal({
        title: 'Khóa & áp dụng ' + esc(r.VersionCode || 'V' + r.VersionNo), size: 'sm',
        body: `<p style="margin-top:0">${Store.isLive ? 'Phiên bản đang dùng của món sẽ ngừng áp dụng từ ngày trước đó và giá thành chuẩn được tính lại. Sau khi khóa không sửa được công thức.' : 'Khóa phiên bản này và áp dụng? Phiên bản đang dùng sẽ chuyển Obsolete và EffectiveTo = hôm trước ngày áp dụng.'}</p><div class="field"><label>Áp dụng từ ngày <span class="req">*</span></label><input type="date" class="input" id="lkFrom" value="${esc(Store.ctx.date)}"></div>`,
        foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="lkOk">${I('key')} Khóa & áp dụng</button>`,
        onMount: (m, close) => m.querySelector('#lkOk').onclick = () => {
          const from = m.querySelector('#lkFrom').value; if (!from) return UI.toast('Chọn ngày áp dụng', 'err');
          Store.act('Recipe/Lock', { form: { Id: r.Id, EffectiveFrom: from }, tables: ['Recipe', 'StandardCost', 'Dishe'], demo: () => {
            Store.by('Recipe', 'DishId', r.DishId).filter(x => x.IsActive && x.Id !== r.Id).forEach(x => Store.update('Recipe', x.Id, { IsActive: 0, Status: 'Obsolete', EffectiveTo: U.addDays(from, -1) }));
            Store.update('Recipe', r.Id, { Status: 'Locked', IsActive: 1, LockedAt: U.iso(new Date()), LockedBy: Store.user.id, EffectiveFrom: from, SnapshotJson: JSON.stringify(Store.by('RecipeLine', 'RecipeId', r.Id).map(l => ({ i: l.IngredientId, q: l.Quantity }))) });
          } }).then(x => { if (!x.ok) return; close(); UI.toast('Đã khóa & áp dụng phiên bản ' + esc(r.VersionCode || ''), 'ok'); APP.refresh(); });
        }
      });
    });
    // Tạo phiên bản mới – chế độ API: Recipe/CreateVersion (bản nháp, chép định lượng từ phiên bản mới nhất; backend chặn khi món còn bản nháp / chờ duyệt).
    el.querySelector('#rNew').onclick = () => {
      const last = recs[0];
      Store.act('Recipe/CreateVersion', { form: { DishId: rs.dish, CopyFromRecipeId: last ? last.Id : null }, tables: ['Recipe', 'RecipeLine'], demo: () => {
        const v = (last ? last.VersionNo : 0) + 1;
        const r = Store.add('Recipe', { DishId: rs.dish, VersionNo: v, VersionCode: d.Code + '-V' + v, YieldPercent: last ? last.YieldPercent : 92, YieldQuantity: 100, StandardOutputQty: 100, YieldUnitId: last && last.YieldUnitId, Status: 'Draft', IsActive: 0, Instructions: last && last.Instructions, Note: 'Sao chép từ ' + (last ? last.VersionCode : '') });
        if (last) Store.by('RecipeLine', 'RecipeId', last.Id).forEach(l => Store.add('RecipeLine', { RecipeId: r.Id, IngredientId: l.IngredientId, Quantity: l.Quantity, UnitId: l.UnitId, WastePercent: l.WastePercent, YieldPercent: l.YieldPercent, SequenceNo: l.SequenceNo, SortOrder: l.SortOrder }));
        UI.toast('Đã tạo phiên bản nháp ' + esc(r.VersionCode) + ' (sao chép định lượng)', 'ok');
      } }).then(x => { if (!x.ok) return; if (!x.demo) UI.toast('Đã tạo phiên bản nháp ' + esc((x.data && x.data.versionCode) || '') + (last ? ' (sao chép định lượng)' : ''), 'ok'); APP.refresh(); });
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
    if (mts.length && !mts.includes(ms.mt)) ms.mt = mts[0];
    // Chế độ API: số liệu thẻ lấy từ Menu/GetList, lịch tuần + giá vốn lấy từ Menu/GetCalendar; chưa có / lỗi thì tính cục bộ như cũ.
    const ml = menuList(), info = ml && ml.map && ml.map[mn.Id], waiting = !!ml && !ml.map;
    const cal = menuCalendar(mn.Id, ms.mt);
    const useLocal = !info && !waiting;
    const meals = info ? info.orderedMeals : useLocal ? U.sum(Store.rows('OrderLine').filter(l => l.MenuId === mn.Id), 'Quantity') : null;
    const assigned = info ? info.assignedCustomerCount : useLocal ? new Set(Store.by('MenuAssignment', 'MenuId', mn.Id).map(a => a.CustomerId)).size : null;
    const calRows = cal ? (cal.listRow || []).filter(r => r.mealType === ms.mt) : [];
    const calOpts = [...new Set(calRows.map(r => r.optionNo))].sort((a, b) => a - b);
    const optLabel = o => calOpts.length > 1 ? ` · Lựa chọn ${o}` : '';
    const calHtml = () => calRows.map(r => `<div class="rowh">${esc(r.dishCategoryName || 'Khác')}${esc(optLabel(r.optionNo))}</div>${DOW.map((_, i) => `<div class="c">${((r.cells || {})[i + 1] || []).map(x => `<div><a href="javascript:void 0" data-open="MenuLine" data-id="${esc(x.menuLineId)}">${esc(x.dishName || x.dishCode || '')}</a> ${x.costPerMeal == null ? '<small class="warn" title="Món chưa có công thức đang áp dụng">chưa có giá vốn</small>' : `<small class="muted">${U.money(x.costPerMeal)}</small>`}</div>`).join('') || '<span class="muted">–</span>'}</div>`).join('')}`).join('') +
      calOpts.map(o => `<div class="rowh">Giá vốn / suất${esc(optLabel(o))}</div>${DOW.map((_, i) => { const t = (cal.listTotal || []).find(x => x.mealType === ms.mt && x.optionNo === o && x.dayOfWeek === i + 1); return `<div class="c">${t ? `<div><b>${U.money(t.costPerMeal)}</b>${t.costComplete === false ? ' <small class="warn">thiếu giá</small>' : ''}</div>${t.foodCostPercent == null ? '' : `<div class="${t.foodCostOverTarget ? 'bad' : 'muted'}">${U.num(t.foodCostPercent, 1)}% giá bán</div>`}` : '<span class="muted">–</span>'}</div>`; }).join('')}`).join('');
    const next = APP.nextStatus('Menu', 'Status', mn.Status);
    el.innerHTML = APP.header(f, `${next ? `<button class="btn orange" id="mNext">${I('approve')} Chuyển sang ${UI.statusVi('Menu', 'Status', next)}</button>` : ''}<button class="btn primary" id="mAdd">${I('plus')} Thêm món</button>`) + `
    <div class="row-flex mb">${menus.map(m => `<button class="chip ${m.Id === mn.Id ? 'active' : ''}" data-m="${m.Id}">${esc(m.Code)} ${UI.badge(m.Status, 'Menu', 'Status')}</button>`).join('')}</div>
    <div class="kpis">${UI.kpi('Thực đơn', esc(mn.Name), `${U.date(mn.EffectiveFrom)} – ${U.date(mn.EffectiveTo)}`, 'calendar', 'green')}${UI.kpi('Số dòng món', info ? U.num(info.lineCount) : lines.length, info ? U.num(info.dishCount) + ' món' : '', 'utensils', 'blue')}${UI.kpi('Khách hàng được gán', assigned === null ? '…' : U.num(assigned), '', 'building', 'orange', 'menu-assignment')}${UI.kpi('Suất đã đặt theo thực đơn', meals === null ? '…' : U.num(meals), '', 'clipboard', 'purple')}</div>
    <div class="card panel"><div class="row-flex mb"><span class="small muted">Loại suất (MenuLine.MealType):</span>${mts.map(m => `<button class="chip ${m === ms.mt ? 'active' : ''}" data-mt="${m}">${m}</button>`).join('')}</div>
      <div class="cal"><div class="h">Nhóm món</div>${DOW.map((d, i) => `<div class="h">${d} · ${U.date(U.addDays(U.day(mn.EffectiveFrom), i)).slice(0, 5)}</div>`).join('')}
      ${cal && calRows.length ? calHtml() : Store.rows('DishCategorie').map(cat => { const any = lines.some(l => l.MealType === ms.mt && (Store.get('Dishe', l.DishId) || {}).DishCategoryId === cat.Id); if (!any) return ''; return `<div class="rowh">${esc(cat.Name)}</div>${DOW.map((_, i) => `<div class="c">${lines.filter(l => l.MealType === ms.mt && l.DayOfWeek === i + 1 && (Store.get('Dishe', l.DishId) || {}).DishCategoryId === cat.Id).map(l => `<div><a href="javascript:void 0" data-open="MenuLine" data-id="${l.Id}">${esc(Store.label('Dishe', l.DishId))}</a></div>`).join('') || '<span class="muted">–</span>'}</div>`).join('')}`; }).join('')}</div>
      <div class="small muted mt">${Store.isLive ? 'Bấm vào món để xem/sửa chi tiết.' + (cal && calRows.length ? ' Giá vốn / suất tính theo công thức đang áp dụng và giá nhà cung cấp ưu tiên.' : '') : 'Ô = MenuLine (MenuId, DishId, MealType, DayOfWeek). Bấm vào món để xem/sửa chi tiết.'}</div></div>`;
    el.querySelectorAll('[data-m]').forEach(b => b.onclick = () => { ms.menu = b.dataset.m; APP.refresh(); });
    el.querySelectorAll('[data-mt]').forEach(b => b.onclick = () => { ms.mt = b.dataset.mt; APP.refresh(); });
    el.querySelector('#mAdd').onclick = () => UI.form('MenuLine', null, { title: 'Thêm món vào thực đơn', fields: ['DishId', 'MealType', 'DayOfWeek', 'SequenceNo', 'PortionQty', 'IsRequired', 'Note'], defaults: { MenuId: mn.Id, MealType: ms.mt, SortOrder: 99 }, validate: o => (o.DayOfWeek < 1 || o.DayOfWeek > 7) ? 'DayOfWeek phải từ 1 (T2) đến 7 (CN)' : null, onSave: () => APP.refresh() });
    const nb = el.querySelector('#mNext'); if (nb) nb.onclick = () => UI.confirm(`Chuyển thực đơn sang <b>${UI.statusVi('Menu', 'Status', next)}</b>?`, () => {
      // Chế độ API: Menu/Approve · Publish · Expire (backend kiểm tra công thức hiệu lực, giá vốn, trùng kỳ); trạng thái khác dùng Update chung.
      const action = { Approved: 'Approve', Published: 'Publish', Expired: 'Expire' }[next];
      const local = () => Store.update('Menu', mn.Id, { Status: next });
      if (!action) { local(); UI.toast('Đã cập nhật Menu.Status', 'ok'); return APP.refresh(); }
      Store.act('Menu/' + action, { form: { Id: mn.Id }, tables: ['Menu', 'MenuLine', 'MenuAssignment'], demo: local }).then(r => { if (r.ok) { UI.toast('Đã cập nhật Menu.Status', 'ok'); APP.refresh(); } });
    });
    // Chế độ API: xuất thực đơn gửi khách + nhân bản thực đơn sang kỳ mới.
    if (Store.isLive) {
      const bar = el.querySelector('#mAdd');
      bar.insertAdjacentHTML('beforebegin', `<button class="btn" id="mExp">${I('export')} Xuất gửi khách</button><button class="btn" id="mClone">${I('layers')} Nhân bản</button>`);
      el.querySelector('#mExp').onclick = () => Store.download('Menu/ExportForCustomer', { id: mn.Id });
      el.querySelector('#mClone').onclick = () => UI.modal({ title: 'Nhân bản thực đơn ' + esc(mn.Code), sub: 'Menu/Clone · chép toàn bộ MenuLine sang kỳ mới (Nháp)', size: 'sm',
        body: `<div class="field"><label>Mã</label><input class="input mono" id="cCode" value="" placeholder="(tự sinh khi lưu)" readonly title="MA-1 (D-63): mã NU-YYMMDD-NNN theo ngày hiệu lực"></div><div class="field mt"><label>Tên <code>Name</code></label><input class="input" id="cName" value="${esc(mn.Name)}"></div><div class="field mt"><label>Hiệu lực từ <span class="req">*</span> <code>EffectiveFrom</code></label><input type="date" class="input" id="cFrom" value="${U.addDays(U.day(mn.EffectiveTo || Store.ctx.date), 1)}"></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="cOk">Nhân bản</button>',
        onMount: (m, close) => m.querySelector('#cOk').onclick = () => { const from = m.querySelector('#cFrom').value; if (!from) return UI.toast('Chọn ngày hiệu lực', 'err'); Store.act('Menu/Clone', { form: { Id: mn.Id, Name: m.querySelector('#cName').value.trim(), EffectiveFrom: from }, tables: ['Menu', 'MenuLine'] }).then(r => { if (!r.ok) return; close(); const nid = r.data && (r.data.id || (r.data.menu && r.data.menu.id)); if (nid) ms.menu = nid; UI.toast(esc((r.res && r.res.responseMessage) || 'Đã nhân bản thực đơn'), 'ok'); APP.refresh(); }); } });
    }
  });
  // ---------- Món ăn: thêm cột giá thành ----------
  APP.register('menu-dish', (el, f) => APP.genericList(el, f, {
    cols: [{ k: 'Code', label: 'Mã' }, { k: 'Name', label: 'Món ăn' }, { k: 'DishCategoryId', label: 'Nhóm món' }, { k: 'StandardCost', label: 'Giá thành chuẩn', num: true },
      { k: 'x', label: 'Công thức đang dùng', col: false, nosort: true, render: r => { const rc = Store.by('Recipe', 'DishId', r.Id).find(x => x.IsActive); return rc ? `${esc(rc.VersionCode)} ${UI.badge(rc.Status, 'Recipe', 'Status')}` : '<span class="bad small">Chưa có công thức hiệu lực</span>'; } },
      { k: 'y', label: 'CP NVL ước tính', col: false, num: true, nosort: true, render: r => { const rc = Store.by('Recipe', 'DishId', r.Id).find(x => x.IsActive) || Store.by('Recipe', 'DishId', r.Id)[0]; return rc ? U.money(recipeCost(rc.Id)) : '–'; } }, { k: 'Status', label: 'Trạng thái' }]
  }));
  // ---------- Dùng chung cho các hộp thoại chế độ API ----------
  const opts = (rows, label, sel) => rows.map(r => `<option value="${esc(r.Id)}" ${r.Id === sel ? 'selected' : ''}>${esc(label(r))}</option>`).join('');
  const hasTable = t => !!Store.schema.tables[t];
  const mealTypes = () => [...new Set(['ContractMeal', 'MenuLine', 'MenuAssignment'].filter(hasTable).flatMap(t => Store.rows(t).map(r => r.MealType)).filter(Boolean))].sort();
  // ---------- Giá thành chuẩn: tính lại theo giá nhà cung cấp hiện hành (StandardCost/Recalculate) ----------
  APP.register('cost-standard', (el, f) => {
    APP.genericList(el, f);
    if (!Store.isLive || Store.perm(f.id).edit === false) return;
    const recalc = (dishId, msg) => UI.confirm(msg, () => Store.act('StandardCost/Recalculate', { form: { ListDishId: dishId || '' }, tables: ['StandardCost', 'Dishe'] }).then(x => {
      if (!x.ok) return;
      const n = Number(x.data) || 0;
      UI.toast(n ? `Đã tính lại giá thành chuẩn cho ${U.num(n)} món` : 'Không có món nào có công thức đang áp dụng để tính lại', n ? 'ok' : 'warn'); APP.refresh();
    }), { ok: 'Tính lại' });
    const bar = el.querySelector('.heading-actions');
    if (bar) {
      bar.insertAdjacentHTML('afterbegin', `<button class="btn orange" id="scAll">${I('calc')} Tính lại giá thành</button>`);
      el.querySelector('#scAll').onclick = () => recalc(null, 'Tính lại giá thành chuẩn của <b>mọi món có công thức đang áp dụng</b> theo giá nhà cung cấp hiện hành? Bản giá thành đang dùng sẽ ngừng hiệu lực và được thay bằng bản mới.');
    }
    APP.rowExtra(el, tr => {
      const r = Store.get('StandardCost', tr.dataset.row), td = tr.querySelector('.actions');
      if (!r || !td || r.Status !== 'Active' || !r.DishId || td.querySelector('[data-rc]')) return;
      td.insertAdjacentHTML('afterbegin', '<button class="btn xs" data-rc title="Tính lại theo giá nhà cung cấp hiện hành">Tính lại</button> ');
      td.querySelector('[data-rc]').onclick = () => recalc(r.DishId, `Tính lại giá thành chuẩn của món <b>${esc(Store.label('Dishe', r.DishId))}</b> theo giá nhà cung cấp hiện hành?`);
    });
  });
  // ---------- Phân công thực đơn: tra thực đơn đang áp dụng (MenuAssignment/Resolve) ----------
  APP.register('menu-assignment', (el, f) => {
    APP.genericList(el, f);
    if (!Store.isLive) return;
    const bar = el.querySelector('.heading-actions'); if (!bar) return;
    bar.insertAdjacentHTML('afterbegin', `<button class="btn" id="maRes">${I('link')} Tra thực đơn áp dụng</button>`);
    const LOC = 'CustomerDeliveryLocation';
    el.querySelector('#maRes').onclick = () => Promise.all(['Customer', LOC, 'ContractMeal', 'MenuLine'].filter(hasTable).map(t => Store.ensure(t))).then(() => UI.modal({
      title: 'Tra thực đơn áp dụng', sub: 'Thực đơn mà khách / điểm giao đang được gán cho một loại suất tại một ngày', size: 'sm',
      body: `<div class="field"><label>Khách hàng <span class="req">*</span></label><select class="select" id="rvCus"><option value="">— Chọn khách hàng —</option>${opts(Store.rows('Customer'), r => Store.label('Customer', r))}</select></div>
        <div class="field mt"><label>Điểm giao</label><select class="select" id="rvLoc"><option value="">— Mọi điểm giao của khách —</option></select></div>
        <div class="field mt"><label>Loại suất <span class="req">*</span></label><input class="input" id="rvMt" list="rvMtList" value="${esc(ms.mt || '')}"><datalist id="rvMtList">${mealTypes().map(x => `<option value="${esc(x)}">`).join('')}</datalist></div>
        <div class="field mt"><label>Ngày ăn <span class="req">*</span></label><input type="date" class="input" id="rvDate" value="${esc(Store.ctx.date)}"></div>
        <div class="mt" id="rvOut"></div>`,
      foot: '<button class="btn" data-close>Đóng</button><button class="btn primary" id="rvOk">Tra cứu</button>',
      onMount: (m, close) => {
        const cus = m.querySelector('#rvCus'), loc = m.querySelector('#rvLoc'), out = m.querySelector('#rvOut');
        cus.onchange = () => { loc.innerHTML = '<option value="">— Mọi điểm giao của khách —</option>' + (hasTable(LOC) && cus.value ? opts(Store.by(LOC, 'CustomerId', cus.value), r => Store.label(LOC, r)) : ''); };
        m.querySelector('#rvOk').onclick = () => {
          const mt = m.querySelector('#rvMt').value.trim(), date = m.querySelector('#rvDate').value;
          if (!cus.value || !mt || !date) return UI.toast('Chọn khách hàng, loại suất và ngày ăn', 'err');
          out.innerHTML = '<div class="empty">Đang tra cứu…</div>';
          Store.query('MenuAssignment/Resolve', { customerId: cus.value, deliveryLocationId: loc.value, mealType: mt, date }).then(x => {
            if (!out.isConnected) return;
            if (!x.ok || !x.data) { out.innerHTML = `<div class="notice warn">${I('alert')}<div><b>Chưa có thực đơn áp dụng</b>${esc(x.message || 'Khách hàng chưa được gán thực đơn cho loại suất và ngày này.')}</div></div>`; return; }
            const d = x.data;
            out.innerHTML = `<div class="kv"><div><label>Thực đơn</label><span><a href="javascript:void 0" id="rvGo"><b>${esc(d.menuCode || '')}</b></a> · ${esc(d.menuName || '')}</span></div><div><label>Cấp phân công</label><span>${d.level === 'LOCATION' ? UI.tag('Gán theo điểm giao', 'green') : UI.tag('Gán theo khách hàng', 'blue')}</span></div></div>`;
            out.querySelector('#rvGo').onclick = () => { ms.menu = d.menuId; ms.mt = mt; close(); APP.go('menu-menu'); };
          });
        };
      }
    }));
  });
  // ---------- Yêu cầu đổi món: tạo + gửi duyệt (MenuChangeRequest/Create) ----------
  APP.register('menu-change-request', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    const perm = Store.perm(f.id);
    APP.genericList(el, f, { noAdd: true });
    const bar = el.querySelector('.heading-actions'); if (!bar) return;
    bar.insertAdjacentHTML('beforeend', `<button class="btn primary" id="mcNew" ${perm.add === false ? 'disabled title="Tài khoản không có quyền Thêm"' : ''}>${I('plus')} Tạo yêu cầu đổi món</button>`);
    const RT = ['MenuChangeRequest', 'ApprovalRequest', 'ApprovalStep', 'ApprovalItem'];
    el.querySelector('#mcNew').onclick = () => Promise.all(['Order', 'Menu', 'Dishe', 'Shift', 'Customer'].map(t => Store.ensure(t))).then(() => {
      const orders = Store.rows('Order').filter(o => !['Cancelled', 'Delivered', 'Completed', 'Closed'].includes(o.Status)).sort((a, b) => String(b.MealDate || '').localeCompare(String(a.MealDate || ''))).slice(0, 300);
      const dishes = Store.rows('Dishe').slice().sort((a, b) => String(a.Name || '').localeCompare(String(b.Name || '')));
      const dishLabel = r => (r.Name || '') + (r.Code ? ' · ' + r.Code : '');
      UI.modal({
        title: 'Tạo yêu cầu đổi món', sub: 'Yêu cầu được gửi duyệt ngay sau khi tạo', size: 'lg',
        body: `<form class="form-grid" novalidate>
          <div class="field full"><label>Đơn hàng</label><select class="select" id="mcOrd"><option value="">— Không theo đơn: áp dụng cho mọi đơn của ngày / ca —</option>${opts(orders, o => (o.OrderNumber || Store.label('Order', o)) + ' · ' + U.date(o.MealDate) + (o.CustomerId ? ' · ' + Store.label('Customer', o.CustomerId) : ''))}</select></div>
          <div class="field"><label>Món hiện tại <span class="req">*</span></label><select class="select" id="mcFrom"><option value="">— Chọn món —</option>${opts(dishes, dishLabel)}</select></div>
          <div class="field"><label>Món thay thế <span class="req">*</span></label><select class="select" id="mcTo"><option value="">— Chọn món —</option>${opts(dishes, dishLabel)}</select></div>
          <div class="field"><label>Ngày ăn <span class="req" id="mcDateReq">*</span></label><input type="date" class="input" id="mcDate"></div>
          <div class="field"><label>Ca</label><select class="select" id="mcShift"><option value="">— Theo đơn / mọi ca —</option>${opts(Store.rows('Shift'), r => Store.label('Shift', r))}</select></div>
          <div class="field"><label>Số suất đổi <span class="req" id="mcQtyReq">*</span></label><input type="number" min="0" step="any" class="input" id="mcQty" placeholder=""></div>
          <div class="field"><label>Thực đơn</label><select class="select" id="mcMenu"><option value="">— Theo thực đơn của đơn —</option>${opts(Store.rows('Menu'), r => (r.Code || '') + ' · ' + (r.Name || ''))}</select></div>
          <div class="field full"><label>Lý do <span class="req">*</span></label><textarea class="input" rows="2" id="mcReason" maxlength="1000"></textarea></div>
        </form>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="mcOk">Tạo & gửi duyệt</button>',
        onMount: (m, close) => {
          const q = s => m.querySelector(s), ord = q('#mcOrd'), date = q('#mcDate'), qty = q('#mcQty');
          // Có đơn: ngày ăn lấy theo đơn; số suất để trống = toàn bộ suất của đơn theo thực đơn đó.
          ord.onchange = () => {
            const o = ord.value && Store.get('Order', ord.value);
            date.value = o && o.MealDate ? U.day(o.MealDate) : date.value; date.disabled = !!o;
            q('#mcDateReq').classList.toggle('hide', !!o); q('#mcQtyReq').classList.toggle('hide', !!o);
            qty.placeholder = o ? 'Để trống = toàn bộ suất của đơn' : '';
          };
          q('#mcOk').onclick = () => {
            const from = q('#mcFrom').value, to = q('#mcTo').value, reason = q('#mcReason').value.trim(), n = qty.value === '' ? null : Number(qty.value);
            const err = !from || !to ? 'Chọn món hiện tại và món thay thế'
              : from === to ? 'Món thay thế phải khác món hiện tại'
              : reason.length < 3 ? 'Nhập lý do đổi món (ít nhất 3 ký tự)'
              : n !== null && !(n > 0) ? 'Số suất đổi phải lớn hơn 0'
              : !ord.value && (!date.value || n === null) ? 'Yêu cầu không theo đơn cần có ngày ăn và số suất đổi'
              : null;
            if (err) return UI.toast(err, 'err');
            Store.act('MenuChangeRequest/Create', { form: { OrderId: ord.value, MenuId: q('#mcMenu').value, OriginalDishId: from, RequestedDishId: to, MealDate: date.value, ShiftId: q('#mcShift').value, Quantity: n, Reason: reason }, tables: RT })
              .then(x => { if (!x.ok) return; close(); UI.toast('Đã tạo yêu cầu đổi món và gửi duyệt', 'ok'); APP.refresh(); });
          };
        }
      });
    });
  });
})();
