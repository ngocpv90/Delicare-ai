/* P2/P3 – Món ăn, Công thức (version trên bảng Recipe), Thực đơn dạng lịch, Phân công thực đơn */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const ingPrice = id => { const sm = Store.rows('SupplierMaterial').filter(s => s.IngredientId === id && s.IsPreferred); return sm.length ? sm[0].UnitPrice : 0; };
  // BIZ-1.1 (D-77 a): hao hụt 2 tầng, công thức chia như file thật – sau sơ chế = tinh ÷ (1 − % chế biến); lượng mua = sau sơ chế ÷ (1 − % sơ chế).
  // % sơ chế = RecipeLine.WastePercent (trống = Ingredient.StandardWastePercent), % chế biến = RecipeLine.ExpectedWasteRate. Khớp WasteRules máy chủ.
  const pct = v => { const n = Number(v); return n > 0 && n < 100 ? n : 0; };
  const prepPct = l => l.WastePercent !== null && l.WastePercent !== undefined && l.WastePercent !== '' ? l.WastePercent : (Store.get('Ingredient', l.IngredientId) || {}).StandardWastePercent;
  const afterPrep = l => Number(l.Quantity || 0) / (1 - pct(l.ExpectedWasteRate) / 100);
  const grossQty = l => afterPrep(l) / (1 - pct(prepPct(l)) / 100);
  const recipeCost = rid => U.sum(Store.by('RecipeLine', 'RecipeId', rid), l => grossQty(l) * ingPrice(l.IngredientId) / 100);
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
        <div class="card panel mb"><div class="spread"><div><h2>${esc(d.Name)}${Store.isLive && APP.detailHref ? ` <a class="small" href="${APP.detailHref('Dishe', d.Id)}">Mở trang món</a>` : ''}</h2><div class="small muted">${esc(d.Description || '')}</div></div><div class="row-flex">${UI.tag(Store.label('DishCategorie', d.DishCategoryId))} ${UI.badge(d.Status, 'Dishe', 'Status')}</div></div>
          ${Store.isLive ? '' : `<div class="notice info mt small">${I('database')}<div><b>Không tạo bảng RecipeVersion.</b> Mỗi phiên bản là một dòng <code>Recipe</code> cùng <code>DishId</code>, phân biệt bằng <code>VersionNo</code>/<code>VersionCode</code>; hiệu lực <code>EffectiveFrom/To</code>; duyệt <code>ApprovedBy/At</code>; khóa <code>LockedAt/By</code> + <code>SnapshotJson</code>.</div></div>`}</div>
        ${recs.map(r => { const lines = Store.by('RecipeLine', 'RecipeId', r.Id); const cost = recipeCost(r.Id); return `<div class="card panel mb"><div class="spread"><div><b>${esc(r.VersionCode || 'V' + r.VersionNo)}</b> · VersionNo ${r.VersionNo} ${UI.badge(r.Status, 'Recipe', 'Status')} ${r.IsActive ? '<span class="tag green">IsActive</span>' : ''}<div class="small muted">Hiệu lực ${U.date(r.EffectiveFrom) || '–'} → ${U.date(r.EffectiveTo) || 'nay'} · Yield ${U.num(r.YieldPercent)}% · ${r.ApprovedAt ? 'Duyệt ' + U.dt(r.ApprovedAt) : 'Chưa duyệt'}${r.LockedAt ? ' · Khóa ' + U.dt(r.LockedAt) : ''}</div></div>
          <div class="row-flex">${r.Status === 'Draft' ? `<button class="btn sm orange" data-sub="${r.Id}">Gửi duyệt</button><button class="btn sm" data-addl="${r.Id}">${I('plus')} Thêm nguyên liệu</button>` : ''}${r.Status === 'Approved' && Store.canApprove('menu-recipe') ? `<button class="btn sm primary" data-lock="${r.Id}">${I('key')} Áp dụng</button>` : ''}${r.Status === 'PendingApproval' ? `<a class="btn sm" href="${APP.href('wf-inbox')}">Xem hộp phê duyệt →</a>` : ''}${Store.isLive ? `<button class="btn sm" data-cost="${r.Id}">${I('calc')} Tính giá vốn</button>` : ''}<button class="btn sm" data-open="Recipe" data-id="${r.Id}">${I('eye')}</button></div></div>
          <table class="table dc-table dt mt"><thead><tr><th>#</th><th>Nguyên liệu<small>IngredientId</small></th><th class="num">Định lượng tinh /100 suất<small>Quantity</small></th><th>ĐVT<small>UnitId</small></th><th class="num">Hao hụt chế biến %<small>ExpectedWasteRate</small></th><th class="num">Sau sơ chế</th><th class="num">Hao hụt sơ chế %<small>WastePercent</small></th><th class="num">Lượng mua (trước sơ chế)</th><th class="num">Giá NCC ưu tiên</th><th class="num">Chi phí /100 suất</th>${r.Status === 'Draft' ? '<th></th>' : ''}</tr></thead><tbody>${lines.map(l => `<tr><td>${l.SequenceNo}</td><td>${esc(Store.label('Ingredient', l.IngredientId))}</td><td class="num">${r.Status === 'Draft' ? `<input type="number" step="any" class="input" style="width:90px;text-align:right" data-rl="${l.Id}" value="${l.Quantity}">` : U.num(l.Quantity, 3)}</td><td>${esc(Store.label('UnitMeasure', l.UnitId))}</td><td class="num">${U.num(l.ExpectedWasteRate)}</td><td class="num">${U.num(afterPrep(l), 3)}</td><td class="num">${U.num(prepPct(l))}${(l.WastePercent === null || l.WastePercent === undefined || l.WastePercent === '') && prepPct(l) ? '<span class="sub">theo NL</span>' : ''}</td><td class="num"><b>${U.num(grossQty(l), 3)}</b></td><td class="num">${U.money(ingPrice(l.IngredientId))}</td><td class="num">${U.money(grossQty(l) * ingPrice(l.IngredientId))}</td>${r.Status === 'Draft' ? `<td><button class="btn xs danger" data-rmv="${l.Id}">${I('trash')}</button></td>` : ''}</tr>`).join('')}</tbody></table>
          ${Store.isLive ? (() => { const v = vc && vc[r.Id]; return `<div class="spread mt small"><span class="muted">Giá vốn nguyên liệu / suất theo giá nhà cung cấp ưu tiên${v && v.costComplete === false ? ' · <span class="warn">còn nguyên liệu thiếu giá hoặc thiếu quy đổi đơn vị</span>' : ''}</span><b>${v ? U.money(v.costPerMeal) : vc ? U.money(cost) : '…'} / suất</b></div>`; })() : `<div class="spread mt small"><span class="muted">Chi phí NVL ước tính / suất = Σ(Quantity × giá) ÷ 100</span><b>${U.money(cost)} / suất</b></div>`}</div>`; }).join('') || '<div class="card panel empty">Món chưa có công thức</div>'}
      </div></div>`;
    el.querySelector('#rQ').oninput = e => el.querySelectorAll('[data-dish]').forEach(x => x.style.display = x.dataset.s.includes(e.target.value.toLowerCase()) ? '' : 'none');
    el.querySelectorAll('[data-dish]').forEach(x => x.onclick = () => { rs.dish = x.dataset.dish; APP.refresh(); });
    el.querySelectorAll('[data-rl]').forEach(inp => inp.onchange = () => { Store.update('RecipeLine', inp.dataset.rl, { Quantity: Number(inp.value) }); UI.toast('Đã cập nhật định lượng (RecipeLine.Quantity)', 'ok'); APP.refresh(); });
    el.querySelectorAll('[data-rmv]').forEach(b => b.onclick = () => { Store.remove('RecipeLine', b.dataset.rmv); APP.refresh(); });
    el.querySelectorAll('[data-addl]').forEach(b => b.onclick = () => UI.form('RecipeLine', null, { title: 'Thêm nguyên liệu vào công thức', fields: ['IngredientId', 'Quantity', 'UnitId', 'ExpectedWasteRate', 'WastePercent', 'SequenceNo', 'PreparationNote'], defaults: { RecipeId: b.dataset.addl, SortOrder: 99 }, onSave: () => APP.refresh() }));
    el.querySelectorAll('[data-cost]').forEach(b => b.onclick = () => costModal(Store.get('Recipe', b.dataset.cost)));
    // Gửi duyệt – chế độ API: Recipe/Submit (Nháp → Chờ duyệt, backend tạo yêu cầu duyệt phiên bản công thức).
    el.querySelectorAll('[data-sub]').forEach(b => b.onclick = () => {
      const id = b.dataset.sub, r = Store.get('Recipe', id);
      if (!Store.by('RecipeLine', 'RecipeId', id).length) return UI.toast('Công thức phải có ít nhất 1 nguyên liệu', 'err');
      // BIZ-2.3: chọn ngày áp dụng khi gửi – duyệt cấp cuối là khóa & áp dụng luôn.
      UI.modal({ title: 'Gửi duyệt phiên bản ' + esc(r.VersionCode || 'V' + r.VersionNo), size: 'sm',
        body: `<p style="margin-top:0">Sau khi gửi không sửa được định lượng cho tới khi có kết quả duyệt. <b>Duyệt xong là khóa và áp dụng ngay</b> từ ngày dưới đây (phiên bản đang dùng ngừng áp dụng, giá thành chuẩn tính lại).</p><div class="field"><label>Áp dụng từ ngày <span class="req">*</span></label><input type="date" class="input" id="sbFrom" value="${esc(U.addDays(Store.ctx.date, 1))}"></div>`,
        foot: `<button class="btn" data-close>Hủy</button><button class="btn orange" id="sbOk">Gửi duyệt</button>`,
        onMount: (m, close) => m.querySelector('#sbOk').onclick = () => { const from = m.querySelector('#sbFrom').value; if (!from) return UI.toast('Chọn ngày áp dụng', 'err'); close();
        Store.act('Recipe/Submit', { form: { id, effectiveFrom: from }, tables: ['Recipe', 'ApprovalRequest', 'ApprovalStep', 'ApprovalItem'], demo: () => { Store.update('Recipe', id, { Status: 'PendingApproval' }); APP.submitApproval('RECIPE_VERSION', 'Recipe', id, [['Quản lý bếp']], 'Duyệt phiên bản công thức'); } })
          .then(x => { if (!x.ok) return; if (!x.demo) UI.toast('Đã gửi duyệt phiên bản công thức – duyệt xong tự áp dụng', 'ok'); APP.refresh(); }); } });
    });
    // Khóa & áp dụng – chế độ API: Recipe/Lock (chỉ bản Đã duyệt; bản đang dùng ngừng áp dụng, tính lại giá thành chuẩn của món).
    el.querySelectorAll('[data-lock]').forEach(b => b.onclick = () => {
      const r = Store.get('Recipe', b.dataset.lock);
      UI.modal({
        title: 'Áp dụng ' + esc(r.VersionCode || 'V' + r.VersionNo), size: 'sm',
        body: `<p style="margin-top:0">${Store.isLive ? 'Phiên bản đang dùng của món sẽ ngừng áp dụng từ ngày trước đó và giá thành chuẩn được tính lại. Sau khi khóa không sửa được công thức.' : 'Khóa phiên bản này và áp dụng? Phiên bản đang dùng sẽ chuyển Obsolete và EffectiveTo = hôm trước ngày áp dụng.'}</p><div class="field"><label>Áp dụng từ ngày <span class="req">*</span></label><input type="date" class="input" id="lkFrom" value="${esc(Store.ctx.date)}"></div>`,
        foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="lkOk">${I('key')} Áp dụng</button>`,
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
  // ---------- UI-6.11 (D-89): Thực đơn tuần – hệ thống tự lập nháp, bếp chỉ sửa chỗ được đánh dấu (mô phỏng menu-week-mockup.html) ----------
  // Tuần ◀ ▶ (mặc định tuần sau) · 1 thực đơn chung / tuần (D-90) · 4 bước · 4 chỉ số · lưới buổi → lựa chọn (giá vốn + FC%) → nhóm món × 7 ngày ·
  // bấm ô → 5 gợi ý (Enter lưu + sang ngày kế, Delete xóa, gõ để tìm) · ⋯ Điền cả hàng · Chép ngày · Gửi duyệt khóa khi còn đánh dấu.
  const mw = { week: '', data: null, key: '', tab: 'cal', closed: new Set(), focus: -1, hist: null };
  const MW_DOW = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
  const MW_FLAG = { EMPTY: ['mw-empty', 'Ô trống'], NO_RECIPE: ['mw-norec', 'Món chưa có công thức khóa'], DUPLICATE: ['mw-dup', 'Món chính trùng trong 7 ngày'] };
  const mwMonday = d => { const x = new Date(d + 'T00:00'); return U.addDays(d, -((x.getDay() + 6) % 7)); };
  const mwNum = v => v === null || v === undefined ? '–' : U.num(v);
  function mwLoad(el, f, force) {
    const key = mw.week;
    if (!force && mw.key === key && mw.data) return Promise.resolve(mw.data);
    mw.key = key;
    return Store.query('Menu/GetWeek', { WeekStart: mw.week }).then(x => { if (mw.key !== key) return null; mw.data = x.ok ? x.data : null; if (!x.ok) UI.toast(esc(x.message || 'Không tải được thực đơn tuần'), 'err'); return mw.data; });
  }
  function menuWeek(el, f) {
    if (!mw.week) mw.week = U.addDays(mwMonday(Store.ctx.date), 7);
    el.innerHTML = APP.header(f, '', { noNote: true }) + `
      <div class="notice info mb">${I('help')}<div>Mỗi tuần một thực đơn chung cho mọi khách. Thứ Hai 06:00 hệ thống tự lập bản nháp tuần sau (nhân bản tuần cùng vị trí trong chu kỳ 4 tuần) – bếp chỉ sửa các ô được đánh dấu, gửi duyệt, công bố trước thứ Năm.</div></div>
      <div class="card panel mb"><div class="spread" style="flex-wrap:wrap;gap:8px"><div class="fw-week"><button class="btn sm" id="mwPrev" aria-label="Tuần trước">◀</button><span id="mwLbl"></span><button class="btn sm" id="mwNext" aria-label="Tuần sau">▶</button><button class="btn sm" id="mwNow">Tuần sau</button></div><div id="mwStatus"></div></div>
        <div class="fw-steps ow-steps" id="mwSteps"></div><div id="mwKpis"></div></div>
      <div class="card panel"><div class="tabs"><button class="tab" data-mwt="cal">Lịch tuần</button><button class="tab" data-mwt="chg">Đổi món</button><button class="tab" data-mwt="hist">Duyệt &amp; lịch sử</button></div><div id="mwBody"><div class="muted small">Đang tải…</div></div></div>`;
    const go = w => { mw.week = w; mw.data = null; mw.focus = -1; mw.hist = null; menuWeek(el, f); };
    el.querySelector('#mwPrev').onclick = () => go(U.addDays(mw.week, -7));
    el.querySelector('#mwNext').onclick = () => go(U.addDays(mw.week, 7));
    el.querySelector('#mwNow').onclick = () => go(U.addDays(mwMonday(Store.ctx.date), 7));
    el.querySelectorAll('[data-mwt]').forEach(t => t.onclick = () => { mw.tab = t.dataset.mwt; mwDraw(el, f); });
    mwLoad(el, f).then(() => el.isConnected && mwDraw(el, f));
  }
  function mwDraw(el, f) {
    const d = mw.data, perm = Store.perm(f.id), canEdit = perm.edit !== false, canApprove = Store.canApprove(f.id);
    el.querySelectorAll('[data-mwt]').forEach(t => t.classList.toggle('active', t.dataset.mwt === mw.tab));
    const nm = U.addDays(mwMonday(Store.ctx.date), 7);
    el.querySelector('#mwLbl').innerHTML = `Tuần ${U.date(mw.week).slice(0, 5)} – ${U.date(U.addDays(mw.week, 6))} <span class="small muted" style="font-weight:400">${mw.week === nm ? 'tuần sau' : ''}</span>`;
    if (!d) { el.querySelector('#mwBody').innerHTML = '<div class="empty">Không tải được thực đơn tuần.</div>'; return; }
    const st = { Draft: 'orange', Approved: 'blue', Published: 'green', Expired: 'gray' }[d.status] || 'gray';
    el.querySelector('#mwStatus').innerHTML = d.menuId ? `<span class="badge ${st}">${esc(d.statusName || '')}</span> <b>${esc(d.menuCode || '')}</b> <span class="small muted">· hạn công bố thứ Năm ${U.date(U.day(d.publishDeadline)).slice(0, 5)}</span>` : `<span class="small muted">Chưa có thực đơn · hạn công bố thứ Năm ${U.date(U.day(d.publishDeadline)).slice(0, 5)}</span>`;
    const need = d.emptyCells + d.missingRecipe + d.fcOver;
    const s1 = !!d.menuId, s2 = s1 && need === 0, s3 = d.status === 'Approved' || d.status === 'Published', s4 = d.status === 'Published';
    const dot = ok => ok ? '<span class="mw-ok">✓</span>' : '';
    el.querySelector('#mwSteps').innerHTML = `
      <div class="fw-step"><span class="n s1">1</span><div><b>Lập thực đơn ${dot(s1)}</b><span class="small muted">${s1 ? esc(d.sourceNote || 'Đã có thực đơn tuần') : 'Tự lập 06:00 thứ Hai – hoặc tạo tay'}</span></div>${!s1 && perm.add !== false ? `<button class="btn sm primary" id="mwCreate">${I('plus')} Tạo thực đơn tuần</button>` : ''}</div>
      <div class="fw-step"><span class="n s2">2</span><div><b>Hoàn thiện món ${dot(s2)}</b><span class="small muted">${d.emptyCells} ô trống · ${d.missingRecipe} thiếu công thức · ${d.fcOver} suất FC vượt · ${d.duplicateMain} trùng món</span></div>${s1 && (need + d.duplicateMain) > 0 ? `<button class="btn sm" id="mwNextFlag">${I('search')} Đến ô cần xử lý</button>` : ''}</div>
      <div class="fw-step"><span class="n s3">3</span><div><b>Gửi duyệt ${dot(s3)}</b><span class="small muted">${d.status === 'Draft' ? (d.canSubmit ? 'Đủ điều kiện – COO duyệt' : 'Khóa tới khi hết ô trống / thiếu công thức / FC vượt') : esc(d.statusName || '')}</span></div>${d.status === 'Draft' && canEdit ? `<button class="btn sm primary" id="mwSubmit" ${d.canSubmit ? '' : 'disabled'}>${I('approve')} Gửi duyệt</button>` : ''}${d.status === 'Draft' && canApprove && d.canSubmit ? `<button class="btn sm orange" id="mwApprove">${I('check')} Duyệt</button>` : ''}</div>
      <div class="fw-step"><span class="n s4">4</span><div><b>Công bố &amp; gửi phiếu ${dot(s4)}</b><span class="small muted">Công bố trước thứ Năm → gửi phiếu đăng ký cho khách</span></div>${d.status === 'Approved' && canApprove ? `<button class="btn sm orange" id="mwPublish">${I('approve')} Công bố</button>` : ''}${s4 ? `<button class="btn sm" id="mwSlip">${I('down')} Gửi phiếu đăng ký</button>` : ''}</div>`;
    el.querySelector('#mwKpis').innerHTML = UI.kpis([
      UI.kpi('Ô còn trống', U.num(d.emptyCells), 'cần chọn món', 'alert', d.emptyCells ? 'orange' : 'green'),
      UI.kpi('Món thiếu công thức', U.num(d.missingRecipe), 'chưa khóa công thức', 'utensils', d.missingRecipe ? 'red' : 'green'),
      UI.kpi('FC% TB tuần', d.avgFcPct != null ? U.num(d.avgFcPct) + '%' : '–', 'mục tiêu ≤ ' + U.num(d.fcTargetPct) + '%', 'chart', d.avgFcPct != null && d.avgFcPct > d.fcTargetPct ? 'red' : 'green'),
      UI.kpi('Khách áp dụng', U.num(d.customerCount), 'hợp đồng hiệu lực', 'users', 'blue')], 'compact');
    const b = id => el.querySelector(id);
    if (b('#mwCreate')) b('#mwCreate').onclick = () => mwCreate(el, f);
    if (b('#mwSubmit')) b('#mwSubmit').onclick = () => Store.act('Menu/SubmitWeek', { form: { Id: d.menuId } }).then(x => { if (x.ok) { UI.toast(esc(x.res.responseMessage || 'Đã gửi duyệt'), 'ok'); } });
    if (b('#mwApprove')) b('#mwApprove').onclick = () => UI.confirm(`Duyệt thực đơn <b>${esc(d.menuCode)}</b>?`, () => Store.act('Menu/Approve', { form: { Id: d.menuId }, tables: ['Menu'] }).then(x => { if (x.ok) { UI.toast('Đã duyệt', 'ok'); mwLoad(el, f, true).then(() => mwDraw(el, f)); } }), { ok: 'Duyệt' });
    if (b('#mwPublish')) b('#mwPublish').onclick = () => UI.confirm(`Công bố thực đơn <b>${esc(d.menuCode)}</b>? Khách đăng ký suất theo thực đơn này.`, () => Store.act('Menu/Publish', { form: { Id: d.menuId }, tables: ['Menu'] }).then(x => { if (x.ok) { UI.toast('Đã công bố', 'ok'); mwLoad(el, f, true).then(() => mwDraw(el, f)); } }), { ok: 'Công bố' });
    if (b('#mwSlip')) b('#mwSlip').onclick = () => APP.go('crm-order-week');
    if (b('#mwNextFlag')) b('#mwNextFlag').onclick = () => { if (mw.tab !== 'cal') { mw.tab = 'cal'; mwDraw(el, f); } const cs = [...el.querySelectorAll('.mw-cell.mw-empty, .mw-cell.mw-norec, .mw-cell.mw-dup, .mw-fc.bad')]; if (!cs.length) return; mw.focus = (mw.focus + 1) % cs.length; const c = cs[mw.focus]; c.scrollIntoView({ behavior: 'smooth', block: 'center' }); c.classList.add('mw-pulse'); setTimeout(() => c.classList.remove('mw-pulse'), 1200); };
    if (mw.tab === 'hist') return mwHist(el, f);
    if (mw.tab === 'chg') return mwChanges(el, f);
    mwGrid(el, f, canEdit);
  }
  function mwGrid(el, f, canEdit) {
    const d = mw.data, box = el.querySelector('#mwBody'), edit = canEdit && d.editable;
    // UI-6.13 (D-91): thực đơn đã công bố / đã duyệt → bấm ô món để "Đổi món ngày này" (quyền Thêm của màn Thực đơn tuần).
    const changeable = !d.editable && (d.status === 'Published' || d.status === 'Approved') && Store.perm(f.id).add !== false;
    if (!d.menuId) { box.innerHTML = `<div class="empty">Tuần này chưa có thực đơn chung. ${Store.perm(f.id).add !== false ? 'Bấm <b>Tạo thực đơn tuần</b> ở bước 1.' : ''}</div>`; return; }
    const days = d.listDate.map(U.day);
    let h = `<div class="fw-legend"><span><i class="mw-sw mw-empty"></i>Ô trống</span><span><i class="mw-sw mw-norec"></i>Món chưa có công thức khóa</span><span><i class="mw-sw mw-fcbad"></i>Suất FC vượt mục tiêu</span><span><i class="mw-sw mw-dup"></i>Món chính trùng trong 7 ngày</span><span><i class="mw-sw mw-chg"></i>Ô vừa sửa</span>${d.editable ? '' : '<span><b>Đã duyệt / công bố</b> – bấm ô món để <b>đổi món ngày này</b> (mọi khách)</span><span><span class="mw-tag">đã đổi</span> món đã đổi</span>'}</div>
      <div class="table-wrap"><table class="mw-table"><colgroup><col class="name">${days.map(() => '<col>').join('')}<col style="width:40px"></colgroup><thead><tr><th class="l">Buổi · lựa chọn · nhóm món</th>${days.map((k, i) => `<th>${MW_DOW[i]} ${U.date(k).slice(0, 5)}${edit ? ` <button class="btn xs mw-copy" data-mwcd="${i + 1}" title="Chép cả ngày này sang ngày khác" aria-label="Chép ngày">⧉</button>` : ''}</th>`).join('')}<th></th></tr></thead><tbody>`;
    d.listMeal.forEach((m, mi) => {
      const closed = mw.closed.has(m.mealType);
      h += `<tr class="mw-meal ${closed ? 'closed' : ''}" data-mwm="${esc(m.mealType)}"><td class="l" colspan="9"><span class="fw-chev">▾</span> <b>${esc(m.mealName || m.mealType)}</b> <span class="small muted">· ${m.price != null ? 'giá bán ' + U.money(m.price) : 'chưa có giá bán'} · ${m.listOption.length} lựa chọn</span></td></tr>`;
      if (closed) return;
      m.listOption.forEach((o, oi) => {
        h += `<tr class="mw-opt"><td class="l">${m.listOption.length > 1 ? 'Lựa chọn ' + o.optionNo : 'Giá vốn / FC%'}</td>${o.listCost.map((c, i) => { const fc = o.listFcPct[i], bad = fc != null && fc > d.fcTargetPct, miss = (o.listMissingCost || [])[i] || 0; return `<td class="mw-fc ${bad ? 'bad' : fc != null ? 'good' : ''}" ${bad && edit ? `data-mwfc="${mi}|${oi}|${i}" title="Vượt ngưỡng – bấm để đổi món đắt nhất của ngày (gợi ý món rẻ hơn cùng nhóm trước)" style="cursor:pointer"` : ''}>${miss ? '–' : c ? U.num(c) : '–'}<div class="small">${miss ? `<span class="muted">thiếu giá ${miss} món</span>` : fc != null ? U.num(fc) + '%' : ''}</div></td>`; }).join('')}<td></td></tr>`;
        o.listRow.forEach((r, ri) => {
          h += `<tr><td class="l mw-cat">${esc(r.categoryName || 'Món')}</td>${r.listCell.map((c, di) => { const fl = MW_FLAG[c.flag]; const canChg = changeable && c.dishId && !c.changePending; const tip = c.changedDishName ? c.changeNote || '' : (c.dishName || '') + (c.cost != null ? ' · giá vốn ' + U.num(c.cost) : '') + (fl ? ' · ' + fl[1] : '') + (canChg ? ' · bấm để đổi món ngày này' : '');
            return `<td><button class="mw-cell ${fl && !c.changedDishName ? fl[0] : ''} ${c.changedDishName ? 'mw-changed' : ''}" data-mwc="${mi}|${oi}|${ri}|${di}" title="${esc(tip)}" ${edit || canChg ? '' : 'disabled'}>${c.changedDishName ? `${esc(c.changedDishName)}<span class="mw-tag">đã đổi</span>` : c.dishName ? esc(c.dishName) : '<span class="muted">+ chọn món</span>'}${c.changePending ? '<span class="mw-tag pend">chờ duyệt đổi</span>' : ''}</button></td>`; }).join('')}<td>${edit ? `<button class="btn xs" data-mwf="${mi}|${oi}|${ri}" title="Điền cả hàng (7 ngày cùng món)" aria-label="Điền cả hàng">⋯</button>` : ''}</td></tr>`;
        });
      });
    });
    const xl = edit ? `<div class="row-flex mb"><button class="btn sm" id="mwXlsOut">${I('down')} Tải mẫu Excel</button><button class="btn sm" id="mwXlsIn">${I('file')} Tải Excel lên</button><input type="file" id="mwXlsFile" accept=".xlsx" hidden><span class="small muted">Mẫu sheet "1. Menu": Buổi · Lựa chọn · Nhóm món · T2…CN – ghi tên món, hệ thống tự khớp danh mục</span></div>` : '';
    box.innerHTML = xl + h + '</tbody></table></div><div class="small muted mt">Bấm ô → 5 gợi ý (cùng nhóm → có công thức → chưa dùng 14 ngày → giữ FC); gõ để tìm món khác; <b>Enter</b> lưu và sang ngày kế; phím <b>xóa</b> để bỏ món; <b>Esc</b> đóng. ⧉ ở đầu cột = chép cả ngày.</div>';
    box.querySelectorAll('[data-mwm]').forEach(tr => tr.onclick = () => { const k = tr.dataset.mwm; mw.closed.has(k) ? mw.closed.delete(k) : mw.closed.add(k); mwGrid(el, f, canEdit); });
    if (changeable) box.querySelectorAll('[data-mwc]:not([disabled])').forEach(btn => btn.onclick = () => mwChange(el, f, btn));
    if (!edit) return;
    box.querySelector('#mwXlsOut').onclick = () => Store.download('Menu/GetWeekTemplate', { WeekStart: mw.week });
    const fileIn = box.querySelector('#mwXlsFile');
    box.querySelector('#mwXlsIn').onclick = () => fileIn.click();
    fileIn.onchange = () => { const fl = fileIn.files[0]; fileIn.value = ''; if (fl) mwImport(el, f, fl); };
    box.querySelectorAll('[data-mwc]').forEach(btn => btn.onclick = () => mwPick(el, f, canEdit, btn, false));
    // UI-6.27 (D-108): ngày vượt ngưỡng FC → mở chọn món cho món đắt nhất của ngày, gợi ý món rẻ hơn cùng nhóm trước.
    box.querySelectorAll('[data-mwfc]').forEach(td => td.onclick = () => {
      const [mi, oi, di] = td.dataset.mwfc.split('|').map(Number), o = mw.data.listMeal[mi].listOption[oi];
      let best = -1, cost = -1; o.listRow.forEach((r, ri) => { const c = r.listCell[di]; if (c && c.dishId && (c.cost || 0) > cost) { cost = c.cost || 0; best = ri; } });
      const btn = best >= 0 && box.querySelector(`[data-mwc="${mi}|${oi}|${best}|${di}"]`);
      if (btn) { mw.cheaper = true; btn.click(); }
    });
    box.querySelectorAll('[data-mwf]').forEach(btn => btn.onclick = () => mwPick(el, f, canEdit, btn, true));
    box.querySelectorAll('[data-mwcd]').forEach(btn => btn.onclick = () => {
      const from = +btn.dataset.mwcd;
      UI.modal({ title: 'Chép ngày ' + MW_DOW[from - 1], size: 'sm', body: `<div class="field"><label>Chép mọi buổi của ${MW_DOW[from - 1]} sang</label><select class="select" id="mwTo">${MW_DOW.map((x, i) => i + 1 === from ? '' : `<option value="${i + 1}">${x} ${U.date(days[i]).slice(0, 5)}</option>`).join('')}</select></div><div class="small muted">Món của ngày đích bị thay toàn bộ.</div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="mwCdOk">Chép</button>',
        onMount: (mm, close) => mm.querySelector('#mwCdOk').onclick = () => Store.act('Menu/CopyDay', { json: { menuId: mw.data.menuId, fromDay: from, toDay: +mm.querySelector('#mwTo').value }, tables: ['MenuLine'] }).then(x => { if (!x.ok) return; close(); mw.data = x.data; UI.toast('Đã chép ngày', 'ok'); mwDraw(el, f); }) });
    });
  }
  // Hộp chọn món ngay dưới ô (không hộp thoại): 5 gợi ý + tìm; Enter lưu + sang ngày kế; Delete xóa.
  function mwPick(el, f, canEdit, anchor, fillRow) {
    document.querySelectorAll('.mw-pop').forEach(p => p.remove());
    const cheaper = !!mw.cheaper; mw.cheaper = false;
    const [mi, oi, ri, di0] = (anchor.dataset.mwc || anchor.dataset.mwf + '|0').split('|').map(Number);
    const d = mw.data, meal = d.listMeal[mi], opt = meal.listOption[oi], row = opt.listRow[ri];
    let di = di0;
    const pop = document.createElement('div'); pop.className = 'mw-pop';
    const r = anchor.getBoundingClientRect();
    pop.style.top = (r.bottom + window.scrollY + 4) + 'px'; pop.style.left = Math.min(r.left + window.scrollX, window.innerWidth - 340) + 'px';
    pop.innerHTML = `<div class="small muted mb">${fillRow ? 'Điền cả hàng – ' : ''}${esc(meal.mealName)} · ${opt.optionNo > 1 || meal.listOption.length > 1 ? 'LC ' + opt.optionNo + ' · ' : ''}${esc(row.categoryName || '')}${fillRow ? '' : ' · ' + MW_DOW[di]}</div><input class="input" id="mwQ" placeholder="Gõ để tìm món khác…" autocomplete="off"><div id="mwList" class="mw-list"><div class="muted small">Đang gợi ý…</div></div>`;
    document.body.appendChild(pop);
    const q = pop.querySelector('#mwQ'); q.focus();
    let items = [], sel = 0, t = null;
    const close = () => { pop.remove(); document.removeEventListener('mousedown', outside, true); };
    const outside = e => { if (!pop.contains(e.target) && e.target !== anchor) close(); };
    document.addEventListener('mousedown', outside, true);
    const paint = () => { pop.querySelector('#mwList').innerHTML = items.length ? items.map((s, i) => `<button class="mw-opt-btn ${i === sel ? 'on' : ''}" data-i="${i}"><b>${esc(s.dishName)}</b><span class="small muted">${s.hasRecipe ? '' : '⚠ chưa có công thức · '}giá vốn ${mwNum(s.cost)}${s.newFcPct != null ? ` · FC <span class="${s.newFcPct > d.fcTargetPct ? 'bad' : 'up'}">${U.num(s.newFcPct)}%</span>` : ''}${s.daysSinceUsed != null ? ' · dùng ' + s.daysSinceUsed + ' ngày trước' : ''}</span></button>`).join('') : '<div class="muted small">Không có món phù hợp</div>'; pop.querySelectorAll('[data-i]').forEach(b => b.onmousedown = e => { e.preventDefault(); sel = +b.dataset.i; choose(); }); };
    const load = () => Store.query('Menu/SuggestDish', { MenuId: d.menuId, WeekStart: mw.week, MealType: meal.mealType, OptionNo: opt.optionNo, CategoryId: row.categoryId, Day: di + 1, Search: q.value.trim() || null, CheaperFirst: cheaper || null }).then(x => { items = x.ok ? x.list || [] : []; sel = 0; paint(); });
    const after = x => { if (!x.ok) return; mw.data = x.data; mwGrid(el, f, canEdit); el.querySelector('#mwKpis') && mwDraw(el, f); };
    const save = dishId => Store.act('Menu/SaveCell', { json: { menuId: d.menuId, mealType: meal.mealType, optionNo: opt.optionNo, categoryId: row.categoryId, day: di + 1, dishId }, tables: ['MenuLine'] });
    function choose() {
      const s = items[sel]; if (!s) return;
      if (fillRow) return Store.act('Menu/FillRow', { json: { menuId: d.menuId, mealType: meal.mealType, optionNo: opt.optionNo, categoryId: row.categoryId, dishId: s.dishId }, tables: ['MenuLine'] }).then(x => { close(); after(x); });
      save(s.dishId).then(x => { close(); after(x); if (x.ok && di < 6) { const next = el.querySelector(`[data-mwc="${mi}|${oi}|${ri}|${di + 1}"]`); if (next) { next.classList.add('mw-chgd'); mwPick(el, f, canEdit, next, false); } } });
    }
    q.oninput = () => { clearTimeout(t); t = setTimeout(load, 250); };
    q.onkeydown = e => {
      if (e.key === 'ArrowDown') { e.preventDefault(); sel = Math.min(items.length - 1, sel + 1); paint(); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); sel = Math.max(0, sel - 1); paint(); }
      else if (e.key === 'Enter') { e.preventDefault(); choose(); }
      else if (e.key === 'Escape') close();
      else if ((e.key === 'Delete' || e.key === 'Backspace') && !q.value && !fillRow) { e.preventDefault(); save(null).then(x => { close(); after(x); }); }
    };
    load();
  }
  const MW_REASON = [['SHORTAGE', 'Thiếu nguyên liệu'], ['PRICE', 'Giá tăng'], ['SUPPLIER', 'NCC giao lỗi'], ['QUALITY', 'Chất lượng nguyên liệu'], ['OTHER', 'Khác']];
  // Hộp "Đổi món ngày này" ngay dưới ô: 5 gợi ý (như lập thực đơn) + lý do + ảnh hưởng (N khách · M suất) + tự duyệt hay cần COO → Gửi.
  function mwChange(el, f, anchor) {
    const cheaper = !!mw.cheaper; mw.cheaper = false;
    document.querySelectorAll('.mw-pop').forEach(p => p.remove());
    const [mi, oi, ri, di] = anchor.dataset.mwc.split('|').map(Number), d = mw.data, meal = d.listMeal[mi], opt = meal.listOption[oi], row = opt.listRow[ri], cell = row.listCell[di];
    const pop = document.createElement('div'); pop.className = 'mw-pop';
    const r = anchor.getBoundingClientRect();
    pop.style.top = (r.bottom + window.scrollY + 4) + 'px'; pop.style.left = Math.min(r.left + window.scrollX, window.innerWidth - 360) + 'px';
    pop.innerHTML = `<div class="mb"><b>Đổi món ngày này</b><div class="small muted">${esc(meal.mealName)} · ${MW_DOW[di]} ${U.date(U.day(d.listDate[di])).slice(0, 5)} · đang là <b>${esc(cell.dishName || '')}</b> · áp cho mọi khách</div></div>
      <input class="input" id="mcQ" placeholder="Gõ để tìm món khác…" autocomplete="off"><div id="mcList" class="mw-list"><div class="muted small">Đang gợi ý…</div></div>
      <div class="field mt"><label>Lý do <span class="req">*</span></label><select class="select" id="mcR"><option value="">— Chọn —</option>${MW_REASON.map(([k, l]) => `<option value="${k}">${l}</option>`).join('')}</select></div>
      <input class="input mt" id="mcN" placeholder="Ghi chú (tùy chọn)" maxlength="300"><div id="mcImpact" class="small mt muted">Chọn món thay để xem ảnh hưởng</div>
      <div class="row-flex mt" style="justify-content:flex-end"><button class="btn sm" id="mcX">Hủy</button><button class="btn sm primary" id="mcGo" disabled>Gửi</button></div>`;
    document.body.appendChild(pop);
    let items = [], sel = -1, t = null;
    const close = () => { pop.remove(); document.removeEventListener('mousedown', outside, true); };
    const outside = e => { if (!pop.contains(e.target) && e.target !== anchor) close(); };
    document.addEventListener('mousedown', outside, true);
    const g = q => pop.querySelector(q);
    const ready = () => { g('#mcGo').disabled = !(sel >= 0 && g('#mcR').value); };
    const impact = () => {
      const s2 = items[sel]; if (!s2) return;
      g('#mcImpact').innerHTML = 'Đang tính ảnh hưởng…';
      Store.query('MenuChangeRequest/GetCellPreview', { MenuId: d.menuId, WeekStart: mw.week, MealType: meal.mealType, OptionNo: opt.optionNo, Day: di + 1, OriginalDishId: cell.dishId, RequestedDishId: s2.dishId }).then(x => {
        if (!pop.isConnected) return; if (!x.ok || !x.data) { g('#mcImpact').innerHTML = '<span class="bad">' + esc(x.message || 'Không tính được ảnh hưởng') + '</span>'; return; }
        const p2 = x.data;
        g('#mcImpact').innerHTML = `Ảnh hưởng <b>${U.num(p2.customers)} khách · ${U.num(p2.quantity)} suất</b> (${U.num(p2.orders)} đơn)${p2.fcPct != null ? ' · FC suất ' + U.num(p2.fcPct) + '%' : ''}<br>${p2.autoApprove ? '<span class="up"><b>✓ Hệ thống tự duyệt</b> (cùng nhóm, có công thức, FC đạt, trước giờ chốt)</span>' : '<span class="bad">Cần COO duyệt: ' + esc(p2.listBlock.join('; ')) + '</span>'}`;
      });
    };
    const paint = () => { g('#mcList').innerHTML = items.length ? items.map((s2, i) => `<button class="mw-opt-btn ${i === sel ? 'on' : ''}" data-i="${i}"><b>${esc(s2.dishName)}</b><span class="small muted">${s2.hasRecipe ? '' : '⚠ chưa có công thức · '}giá vốn ${mwNum(s2.cost)}${s2.newFcPct != null ? ' · FC ' + U.num(s2.newFcPct) + '%' : ''}</span></button>`).join('') : '<div class="muted small">Không có món phù hợp</div>';
      pop.querySelectorAll('[data-i]').forEach(b => b.onmousedown = e => { e.preventDefault(); sel = +b.dataset.i; paint(); impact(); ready(); }); };
    const load = () => Store.query('Menu/SuggestDish', { MenuId: d.menuId, WeekStart: mw.week, MealType: meal.mealType, OptionNo: opt.optionNo, CategoryId: row.categoryId, Day: di + 1, Search: g('#mcQ').value.trim() || null, CheaperFirst: cheaper || null })
      .then(x => { items = (x.ok ? x.list || [] : []).filter(s2 => s2.dishId !== cell.dishId); sel = -1; paint(); ready(); });
    g('#mcQ').oninput = () => { clearTimeout(t); t = setTimeout(load, 250); };
    g('#mcR').onchange = ready; g('#mcX').onclick = close;
    g('#mcGo').onclick = () => {
      const s2 = items[sel]; if (!s2) return;
      g('#mcGo').disabled = true;
      Store.act('MenuChangeRequest/CreateForCell', { json: { menuId: d.menuId, weekStart: mw.week, mealType: meal.mealType, optionNo: opt.optionNo, day: di + 1, originalDishId: cell.dishId, requestedDishId: s2.dishId, reasonCode: g('#mcR').value, note: g('#mcN').value.trim() || null }, tables: ['MenuChangeRequest'] })
        .then(x => { if (!x.ok) { g('#mcGo').disabled = false; return; } close(); UI.toast(esc((x.res && x.res.responseMessage) || 'Đã gửi'), 'ok'); mwLoad(el, f, true).then(() => mwDraw(el, f)); });
    };
    load();
  }
  function mwChanges(el, f) {
    const box = el.querySelector('#mwBody');
    box.innerHTML = '<div class="muted small">Đang tải…</div>';
    Store.query('MenuChangeRequest/GetWeekChanges', { WeekStart: mw.week }).then(x => {
      if (!box.isConnected || mw.tab !== 'chg') return;
      const list = x.ok ? x.list || [] : [];
      const cls = { Approved: 'green', Pending: 'orange', Rejected: 'red', Cancelled: 'gray' };
      box.innerHTML = list.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Số</th><th>Ngày ăn</th><th>Món cũ → món mới</th><th>Phạm vi</th><th class="num">Suất</th><th>Lý do</th><th>Trạng thái</th><th>Người duyệt</th></tr></thead><tbody>${list.map(r => `<tr><td>${esc(r.requestNo || '')}</td><td>${U.date(U.day(r.mealDate))}</td><td>${esc(r.originalDish || '')} → <b>${esc(r.requestedDish || '')}</b></td><td>${esc(r.scope || '')}</td><td class="num">${r.quantity != null ? U.num(r.quantity) : '–'}</td><td class="small">${esc(r.reason || '')}</td><td><span class="badge ${cls[r.status] || 'gray'}">${esc(r.statusName || '')}</span></td><td class="small">${esc(r.approvedBy || '')}${r.approvedAt ? ' · ' + U.dt(r.approvedAt) : ''}</td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Tuần này chưa có yêu cầu đổi món. Đổi món cho mọi khách: bấm ô món trên Lịch tuần của thực đơn đã công bố.</div>';
    });
  }
  // UI-6.11: tải Excel – xem trước khớp tên món (xanh = khớp, đỏ = chưa khớp + chọn món gợi ý) → Ghi.
  function mwImport(el, f, file) {
    const send = (apply, picks) => { const fd = new FormData(); fd.append('menuId', mw.data.menuId); fd.append('apply', apply ? 'true' : 'false'); if (picks) fd.append('picks', JSON.stringify(picks)); fd.append('file', file); return Store.act('Menu/ImportWeek', { json: fd, tables: ['MenuLine'], quiet: !apply }); };
    send(false).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không đọc được tệp'), 'err');
      const d = x.data, picks = {};
      UI.modal({ title: 'Tải Excel thực đơn tuần', sub: `Khớp ${d.matched} ô · ${d.unmatched} ô chưa khớp tên món`, size: 'lg',
        body: `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Buổi · LC</th><th>Thứ</th><th>Trong tệp</th><th>Món trong danh mục</th></tr></thead><tbody>${d.listCell.map(c => `<tr class="${c.dishId ? '' : 'mw-miss'}"><td>${esc(c.mealType)} · ${c.optionNo}</td><td>${MW_DOW[c.day - 1]}</td><td>${esc(c.text)}</td><td>${c.dishId ? '<span class="up">✓ ' + esc(c.dishName) + '</span>' : `<select class="select" data-pk="${esc(c.key)}"><option value="">— Bỏ qua (chưa khớp) —</option>${(c.listSuggestion || []).map(s2 => `<option value="${s2.dishId}">${esc(s2.dishName)}</option>`).join('')}</select>`}</td></tr>`).join('')}</tbody></table></div><div class="small muted mt">Ô chưa khớp: chọn món gần giống hoặc bỏ qua; ô trống trong tệp giữ nguyên món đang có.</div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="mwXlsOk">Ghi vào thực đơn</button>',
        onMount: (m, close) => {
          m.querySelectorAll('[data-pk]').forEach(sel => sel.onchange = () => { if (sel.value) picks[sel.dataset.pk] = sel.value; else delete picks[sel.dataset.pk]; });
          m.querySelector('#mwXlsOk').onclick = () => send(true, picks).then(y => { if (!y.ok) return; close(); UI.toast(esc((y.res && y.res.responseMessage) || 'Đã ghi'), 'ok'); mwLoad(el, f, true).then(() => mwDraw(el, f)); });
        } });
    });
  }
  function mwCreate(el, f) {
    const menus = Store.rows('Menu').filter(m => m.MenuType === 'COMMON' && m.Status !== 'Draft').sort((a, b) => String(b.EffectiveFrom || '').localeCompare(String(a.EffectiveFrom || ''))).slice(0, 20);
    UI.modal({ title: 'Tạo thực đơn tuần ' + U.date(mw.week).slice(0, 5), size: 'sm',
      body: `<label class="mw-radio"><input type="radio" name="mwm" value="auto" checked> <b>Nhân bản tuần cùng vị trí chu kỳ 4 tuần</b><div class="small muted">Như hệ thống tự lập thứ Hai</div></label>
        <label class="mw-radio"><input type="radio" name="mwm" value="pick"> <b>Nhân bản tuần chọn</b>${menus.length ? `<select class="select mt" id="mwSrc">${menus.map(m => `<option value="${m.Id}">${esc(m.Code || '')} · ${U.date(U.day(m.EffectiveFrom))}</option>`).join('')}</select>` : '<div class="small muted">Chưa có thực đơn chung nào đã công bố để nhân bản</div>'}</label>
        <label class="mw-radio"><input type="radio" name="mwm" value="blank"> <b>Khung trống mẫu chuẩn</b><div class="small muted">Sáng 3 lựa chọn × Món chính – Ăn kèm – Đồ uống; Trưa / Tối 7 nhóm món; Đêm Món chính – Đồ uống</div></label>
        <div class="small muted">Tải Excel: tạo <b>khung trống</b> (hoặc nhân bản) rồi bấm <b>Tải Excel lên</b> trên Lịch tuần (mẫu sheet "1. Menu").</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="mwCOk">Tạo</button>',
      onMount: (m, close) => m.querySelector('#mwCOk').onclick = e => {
        const btn = e.currentTarget, mode = (m.querySelector('[name=mwm]:checked') || {}).value, form = { weekStart: mw.week };
        if (mode === 'pick') { form.sourceId = (m.querySelector('#mwSrc') || {}).value; if (!form.sourceId) return UI.toast('Chưa có thực đơn nào để nhân bản – chọn cách khác', 'err'); }
        if (mode === 'blank') form.blank = true;
        if (btn.disabled) return; btn.disabled = true;
        Store.act('Menu/CreateWeek', { form, tables: ['Menu', 'MenuLine'] }).then(x => { btn.disabled = false; if (!x.ok) return; close(); UI.toast(esc((x.data && x.data.message) || 'Đã tạo'), 'ok'); mwLoad(el, f, true).then(() => mwDraw(el, f)); });
      } });
  }
  function mwHist(el, f) {
    const box = el.querySelector('#mwBody'), d = mw.data;
    if (!d.menuId) { box.innerHTML = '<div class="empty">Chưa có thực đơn.</div>'; return; }
    box.innerHTML = '<div class="muted small">Đang tải…</div>';
    (mw.hist ? Promise.resolve(mw.hist) : Store.query('Common/GetHistory', { resource: 'Menu', id: d.menuId }).then(x => (mw.hist = x.ok ? x.list : []))).then(list => {
      if (!box.isConnected) return;
      box.innerHTML = list.length ? `<div class="timeline">${list.slice(0, 80).map(h => `<div class="tl ${/REJECT|DELETE/i.test(h.action || '') ? 'red' : 'orange'}"><b>${esc(h.actionName || h.action || '')}${h.entityName === 'MenuLine' ? ' · món' : ''}</b><small>${esc(U.dt(String(h.at || '').replace(/\.\d+$/, '')))} · ${esc(h.byName || '')}${(h.listChange || []).filter(c => c.field === 'Status').map(c => ' · ' + esc((c.from || '') + ' → ' + c.to)).join('')}</small></div>`).join('')}</div>` : '<div class="empty">Chưa có lịch sử.</div>';
    });
  }
  // ---------- Thực đơn dạng lịch tuần ----------
  const ms = { menu: null, mt: 'LUNCH' };
  APP.register('menu-menu', (el, f) => {
    if (Store.isLive) return menuWeek(el, f);
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
    el.innerHTML = APP.header(f, `${next && (next !== 'Approved' || Store.canApprove('menu-menu')) ? `<button class="btn orange" id="mNext">${I('approve')} Chuyển sang ${UI.statusVi('Menu', 'Status', next)}</button>` : ''}<button class="btn primary" id="mAdd">${I('plus')} Thêm món</button>`) + `
    <div class="row-flex mb">${menus.map(m => `<button class="chip ${m.Id === mn.Id ? 'active' : ''}" data-m="${m.Id}">${esc(m.Code)} ${UI.badge(m.Status, 'Menu', 'Status')}</button>`).join('')}</div>
    <div class="small muted mb">${String(mn.MenuType || '').toUpperCase() === 'COMMON' ? UI.tag('Thực đơn chung', 'orange') + ' ' : ''}<b>${esc(mn.Name)}</b> · hiệu lực ${U.date(mn.EffectiveFrom)} – ${U.date(mn.EffectiveTo)}</div>
    <div class="kpis">${UI.kpi('Số ngày hiệu lực', mn.EffectiveFrom && mn.EffectiveTo ? U.num(Math.round((new Date(U.day(mn.EffectiveTo)) - new Date(U.day(mn.EffectiveFrom))) / 86400000) + 1) : '–', '', 'calendar', 'green')}${UI.kpi('Số dòng món', info ? U.num(info.lineCount) : lines.length, info ? U.num(info.dishCount) + ' món' : '', 'utensils', 'blue')}${UI.kpi('Suất đã đặt theo thực đơn', meals === null ? '…' : U.num(meals), '', 'clipboard', 'purple')}</div>
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
      bar.insertAdjacentHTML('beforebegin', `<button class="btn" id="mExp">${I('export')} Xuất gửi khách</button><button class="btn" id="mSlip" title="ORD-OPT-1.5: phiếu theo khách + điểm giao + tuần, có ô Số suất dưới mỗi lựa chọn">${I('print')} In phiếu đăng ký</button><button class="btn" id="mClone">${I('layers')} Nhân bản</button>`);
      el.querySelector('#mExp').onclick = () => Store.download('Menu/ExportForCustomer', { id: mn.Id });
      // ORD-OPT-1.5 (D-74 h): phiếu đăng ký suất (Excel có mã ẩn) – chọn khách + điểm giao; tuần = tuần bắt đầu hiệu lực của thực đơn.
      el.querySelector('#mSlip').onclick = () => {
        const cus = Store.rows('Customer').filter(c => c.Status === 'Active');
        const monday = (() => { const s = U.day(mn.EffectiveFrom) || Store.ctx.date; const d = new Date(s + 'T00:00'); return U.addDays(s, -((d.getDay() + 6) % 7)); })();
        UI.modal({ title: 'In phiếu đăng ký suất', sub: `Tuần ${U.date(monday)} – ${U.date(U.addDays(monday, 6))} · thực đơn áp dụng của từng khách (riêng hoặc chung)`, size: 'sm',
          body: `<div class="field"><label>Khách hàng <span class="req">*</span></label><select class="select" id="sC"><option value="">— Chọn —</option>${cus.map(c => `<option value="${c.Id}">${esc((c.Code ? c.Code + ' · ' : '') + (c.DisplayName || c.Name || ''))}</option>`).join('')}</select></div><div class="field mt"><label>Điểm giao <span class="req">*</span></label><select class="select" id="sL"><option value="">— Chọn khách hàng trước —</option></select></div>`,
          foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="sOk">Tải phiếu (Excel)</button>',
          onMount: (m, close) => {
            m.querySelector('#sC').onchange = e => { const ls = Store.by('CustomerDeliveryLocation', 'CustomerId', e.target.value); m.querySelector('#sL').innerHTML = ls.map(l => `<option value="${l.Id}">${esc((l.Code ? l.Code + ' · ' : '') + (l.Name || ''))}</option>`).join('') || '<option value="">Khách chưa có điểm giao</option>'; };
            m.querySelector('#sOk').onclick = () => { const c = m.querySelector('#sC').value, l = m.querySelector('#sL').value; if (!c || !l) return UI.toast('Chọn khách hàng và điểm giao', 'err'); Store.download('Order/ExportWeekSlip', { customerId: c, deliveryLocationId: l, weekStart: monday }).then(r => { if (r.ok) close(); }); };
          } });
      };
      el.querySelector('#mClone').onclick = () => UI.modal({ title: 'Nhân bản thực đơn ' + esc(mn.Code), sub: 'Menu/Clone · chép toàn bộ MenuLine sang kỳ mới (Nháp)', size: 'sm',
        body: `<div class="field"><label>Mã</label><input class="input mono" id="cCode" value="" placeholder="(tự sinh khi lưu)" readonly title="MA-1 (D-63): mã NU-YYMMDD-NNN theo ngày hiệu lực"></div><div class="field mt"><label>Tên <code>Name</code></label><input class="input" id="cName" value="${esc(mn.Name)}"></div><div class="field mt"><label>Hiệu lực từ <span class="req">*</span> <code>EffectiveFrom</code></label><input type="date" class="input" id="cFrom" value="${U.addDays(U.day(mn.EffectiveTo || Store.ctx.date), 1)}"></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="cOk">Nhân bản</button>',
        onMount: (m, close) => m.querySelector('#cOk').onclick = () => { const from = m.querySelector('#cFrom').value; if (!from) return UI.toast('Chọn ngày hiệu lực', 'err'); Store.act('Menu/Clone', { form: { Id: mn.Id, Name: m.querySelector('#cName').value.trim(), EffectiveFrom: from }, tables: ['Menu', 'MenuLine'] }).then(r => { if (!r.ok) return; close(); const nid = r.data && (r.data.id || (r.data.menu && r.data.menu.id)); if (nid) ms.menu = nid; UI.toast(esc((r.res && r.res.responseMessage) || 'Đã nhân bản thực đơn'), 'ok'); APP.refresh(); }); } });
    }
  });
  // ---------- Món ăn: thêm cột giá thành ----------
  // UI-6.21: tùy chọn danh sách món dùng chung (group-panel.js thêm cột nhóm bên trái ở chế độ API).
  const dishListOpt = {
    cols: [{ k: 'Code', label: 'Mã' }, { k: 'Name', label: 'Món ăn' }, { k: 'DishCategoryId', label: 'Nhóm món' }, { k: 'StandardCost', label: 'Giá thành chuẩn', num: true },
      { k: 'x', label: 'Công thức đang dùng', col: false, nosort: true, render: r => { const rc = Store.by('Recipe', 'DishId', r.Id).find(x => x.IsActive); return rc ? `${esc(rc.VersionCode)} ${UI.badge(rc.Status, 'Recipe', 'Status')}` : '<span class="bad small">Chưa có công thức hiệu lực</span>'; } },
      { k: 'y', label: 'CP NVL ước tính', col: false, num: true, nosort: true, render: r => { const rc = Store.by('Recipe', 'DishId', r.Id).find(x => x.IsActive) || Store.by('Recipe', 'DishId', r.Id)[0]; return rc ? U.money(recipeCost(rc.Id)) : '–'; } }, { k: 'Status', label: 'Trạng thái' }]
  };
  APP.dishListOpt = dishListOpt;
  APP.register('menu-dish', (el, f) => APP.genericList(el, f, dishListOpt));
  // ---------- UI-6.17 (D-98): form Món ăn gọn (5 ô) + "Thêm món ăn" kèm công thức + tab Công thức sửa được trong trang chi tiết món ----------
  // Form dùng cho mọi chỗ gọi UI.form('Dishe') ở chế độ API (danh sách Món ăn, nút Sửa trang chi tiết). Mã tự sinh (MA-1), giá thành chuẩn chỉ đọc.
  // Công thức: cùng API màn "Công thức chế biến" (Recipe/CreateVersion, RecipeLine Insert / Update / Delete, Recipe/Submit / Approve / Reject / Lock, Recipe/CalculateCost).
  const dsh = { mode: 'copy' };
  const nilv = v => v === null || v === undefined || v === '';
  function dishForm(row, opt) {
    if (!row && opt && opt.defaults) row = Object.assign({}, opt.defaults);
    const isNew = !row || !row.Id, cats = () => Store.rows('DishCategorie').filter(c => c.Status !== 'Inactive' || (row && c.Id === row.DishCategoryId));
    const units = Store.rows('UnitMeasure').filter(u => u.Status !== 'Inactive');
    const canCat = Store.perm('menu-dish-cat').add !== false && Store.perm('menu-dish-cat').access !== false;
    const r = row || {};
    const catOpts = sel => `<option value="">— Chọn nhóm món —</option>${cats().map(c => `<option value="${esc(c.Id)}" ${c.Id === sel ? 'selected' : ''}>${esc(c.Name || c.Code || '')}</option>`).join('')}`;
    const srcDishes = () => {
      const cat = document.getElementById('dfCat') ? document.getElementById('dfCat').value : '';
      return Store.rows('Dishe').filter(x => x.Id !== r.Id && Store.by('Recipe', 'DishId', x.Id).some(rc => rc.IsActive || ['Approved', 'Locked'].includes(rc.Status)))
        .sort((a, b) => (b.DishCategoryId === cat) - (a.DishCategoryId === cat) || String(a.Name || '').localeCompare(String(b.Name || '')));
    };
    const srcLabel = x => { const rc = Store.by('Recipe', 'DishId', x.Id).find(y => y.IsActive) || Store.by('Recipe', 'DishId', x.Id).find(y => ['Approved', 'Locked'].includes(y.Status)); const n = rc ? Store.by('RecipeLine', 'RecipeId', rc.Id).length : 0; return `${x.Name || ''} · ${x.Code || ''}${rc ? ' · ' + (rc.VersionCode || 'V' + rc.VersionNo) + ' đang dùng · ' + n + ' nguyên liệu' : ''}`; };
    UI.modal({ title: isNew ? 'Thêm món ăn' : 'Sửa món ăn', sub: isNew ? 'Mã món hệ thống tự sinh khi lưu' : esc(r.Code || ''), size: isNew ? 'lg' : 'md',
      body: `<div class="form-grid">
        <div class="field"><label>Tên món <span class="req">*</span></label><input class="input" id="dfName" value="${esc(r.Name || '')}"></div>
        <div class="field"><label>Nhóm món <span class="req">*</span>${canCat ? ' <a href="javascript:void 0" id="dfCatNew" class="small">+ Thêm nhóm món</a>' : ''}</label><select class="select" id="dfCat">${catOpts(r.DishCategoryId || r.CategoryId)}</select></div>
        <div class="field"><label>Định lượng 1 suất</label><div class="row-flex"><input type="number" min="0" step="any" class="input" id="dfSize" style="max-width:140px" value="${nilv(r.StandardServingSize) ? '' : r.StandardServingSize}"><select class="select" id="dfUnit" style="max-width:140px"><option value="">— Đơn vị —</option>${units.map(u => `<option value="${esc(u.Id)}" ${u.Id === r.DefaultUnitId ? 'selected' : ''}>${esc(u.Code || u.Name || '')}</option>`).join('')}</select></div></div>
        <div class="field"><label>Trạng thái</label><select class="select" id="dfSt"><option value="Active" ${r.Status !== 'Inactive' ? 'selected' : ''}>Đang dùng</option><option value="Inactive" ${r.Status === 'Inactive' ? 'selected' : ''}>Ngừng dùng</option></select></div>
        <div class="field" style="grid-column:1/-1"><label>Mô tả</label><textarea class="input" rows="2" id="dfDesc">${esc(r.Description || r.Note || '')}</textarea></div>
        <div class="field"><label>Mã món</label><input class="input" value="${esc(r.Code || 'Tự sinh khi lưu')}" disabled></div>
        <div class="field"><label>Giá thành chuẩn</label><div class="small muted" style="padding-top:8px">${nilv(r.StandardCost) ? 'Hệ thống tự tính khi công thức được duyệt' : '<b>' + U.money(r.StandardCost) + '</b> · tính từ công thức đang dùng'}</div></div></div>
        ${isNew ? `<h3 class="mt">Công thức chế biến</h3><div class="df-modes">
          <label class="chip-radio"><input type="radio" name="dfM" value="copy" ${dsh.mode === 'copy' ? 'checked' : ''}> Chép từ món có sẵn</label>
          <label class="chip-radio"><input type="radio" name="dfM" value="new" ${dsh.mode === 'new' ? 'checked' : ''}> Nhập mới</label>
          <label class="chip-radio"><input type="radio" name="dfM" value="later" ${dsh.mode === 'later' ? 'checked' : ''}> Để sau</label></div>
          <div class="field mt" id="dfSrcBox"><label>Món nguồn (ưu tiên cùng nhóm)</label><input class="input" id="dfSrcQ" placeholder="Tìm theo tên / mã món"><select class="select mt" id="dfSrc" size="6"></select></div>
          <div class="small muted mt" id="dfHint"></div>` : ''}`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="dfOk">${isNew ? 'Lưu và mở công thức' : 'Lưu'}</button>`,
      onMount: (m, close) => {
        const g = s => m.querySelector(s);
        const drawSrc = () => {
          if (!isNew) return;
          const q = (g('#dfSrcQ').value || '').toLowerCase(), cur = g('#dfSrc').value;
          g('#dfSrc').innerHTML = srcDishes().filter(x => !q || String(x.Name || '').toLowerCase().includes(q) || String(x.Code || '').toLowerCase().includes(q)).slice(0, 80).map(x => `<option value="${esc(x.Id)}" ${x.Id === cur ? 'selected' : ''}>${esc(srcLabel(x))}</option>`).join('');
        };
        const mode = () => (m.querySelector('[name=dfM]:checked') || {}).value || 'later';
        const sync = () => {
          if (!isNew) return;
          dsh.mode = mode(); g('#dfSrcBox').classList.toggle('hide', dsh.mode !== 'copy');
          g('#dfOk').textContent = dsh.mode === 'later' ? 'Lưu' : 'Lưu và mở công thức';
          g('#dfHint').textContent = dsh.mode === 'copy' ? 'Tạo món → tạo phiên bản V1 Nháp chép định lượng từ công thức đang dùng của món nguồn → mở tab Công thức để chỉnh.' : dsh.mode === 'new' ? 'Tạo món → tạo phiên bản V1 Nháp trống → mở tab Công thức để nhập nguyên liệu.' : 'Món gắn nhãn "Chưa có công thức" – Thực đơn tuần chưa chọn được món này cho tới khi có công thức hiệu lực.';
        };
        if (isNew) { m.querySelectorAll('[name=dfM]').forEach(x => x.onchange = sync); g('#dfSrcQ').oninput = drawSrc; g('#dfCat').addEventListener('change', drawSrc); Store.ensure('Recipe').then(() => Store.ensure('RecipeLine')).then(drawSrc); sync(); }
        const cn = g('#dfCatNew');
        if (cn) cn.onclick = () => UI.form('DishCategorie', null, { title: 'Nhóm món', onSave: () => Store.reload(['DishCategorie']).then(() => { const list = Store.rows('DishCategorie'); const last = list[0]; g('#dfCat').innerHTML = catOpts(last && last.Id); }) });
        g('#dfOk').onclick = () => {
          const name = g('#dfName').value.trim(), cat = g('#dfCat').value;
          if (!name) return UI.toast('Nhập tên món', 'err');
          if (!cat) return UI.toast('Chọn nhóm món', 'err');
          const size = g('#dfSize').value;
          if (size !== '' && !(+size > 0)) return UI.toast('Định lượng 1 suất phải lớn hơn 0', 'err');
          const form = { Name: name, DishCategoryId: cat, StandardServingSize: size === '' ? null : +size, DefaultUnitId: g('#dfUnit').value || null, Description: g('#dfDesc').value.trim() || null, Status: g('#dfSt').value };
          if (!isNew) {
            return Store.act('Dish/Update', { method: 'PUT', form: Object.assign({ Id: r.Id, Code: r.Code }, form), tables: ['Dishe'] }).then(x => { if (!x.ok) return; close(); UI.toast('Đã lưu món ăn', 'ok'); if (opt && opt.onSave) opt.onSave(); });
          }
          const md = mode(), src = md === 'copy' ? g('#dfSrc').value : '';
          if (md === 'copy' && !src) return UI.toast('Chọn món nguồn để chép công thức (hoặc chọn Nhập mới / Để sau)', 'err');
          const btn = g('#dfOk'); btn.disabled = true;
          Store.act('Dish/Create', { form, tables: ['Dishe'] }).then(x => {
            if (!x.ok || !x.data) { btn.disabled = false; return; }
            const id = x.data.id;
            const next = md === 'later' ? Promise.resolve(null) : (md === 'copy' ? Store.query('Dish/GetActiveRecipe', { id: src }).then(y => (y.ok && y.data && y.data.recipe ? y.data.recipe.id : null)) : Promise.resolve(null))
              .then(rid => Store.act('Recipe/CreateVersion', { form: { DishId: id, CopyFromRecipeId: rid || null }, tables: ['Recipe', 'RecipeLine'] }));
            next.then(v => {
              close(); UI.toast('Đã tạo món ' + esc(x.data.code || name) + (v && v.ok ? ' + công thức V1 Nháp' : ''), 'ok');
              if (md === 'later') { if (opt && opt.onSave) opt.onSave(); return; }
              APP._wantTab = 'Công thức'; APP._wantTabAt = Date.now(); location.hash = APP.href('menu-dish-detail?id=' + encodeURIComponent(id));
            });
          });
        };
      } });
  }
  APP.dishForm = dishForm;
  const baseForm = UI.form;
  UI.form = (t, row, opt) => (t === 'Dishe' && Store.isLive ? Promise.all(['DishCategorie', 'UnitMeasure', 'Dishe'].map(x => Store.ensure(x))).then(() => dishForm(row, opt || {})) : baseForm(t, row, opt));

  // Tab "Công thức" của trang chi tiết món: chọn phiên bản; bản Nháp sửa ngay trên bảng (định lượng tinh / 100 suất, hao hụt sơ chế %), thêm nguyên liệu bằng ô gõ tên.
  APP.dishRecipeTab = (box, o, dishId, reload) => {
    const st = { ver: null };
    const canAdd = Store.perm('menu-recipe').add !== false && Store.perm('menu-recipe').access !== false;
    const canEdit = Store.perm('menu-recipe').edit !== false && Store.perm('menu-recipe').access !== false;
    const canApr = Store.canApprove && Store.canApprove('menu-recipe');
    // Đọc thẳng Recipe/GetByDishId (bảng Recipe trên máy khách có thể chỉ nạp một phần) → đổi về tên cột PascalCase để dùng chung với màn Công thức.
    const P = o => { const x = {}; Object.keys(o || {}).forEach(k => { x[k.charAt(0).toUpperCase() + k.slice(1)] = o[k]; }); return x; };
    const lineOf = {};
    const draw = () => Promise.all([Store.query('Recipe/GetByDishId', { dishId }), Store.ensure('UnitMeasure'), Store.ensure('Ingredient')]).then(([q]) => {
      const list = q && q.ok ? (q.list || []) : [];
      const recs = list.map(v => { const r = P(v.recipe); lineOf[r.Id] = (v.listLine || []).map(P); return r; }).sort((a, b) => b.VersionNo - a.VersionNo);
      if (!recs.length) {
        box.innerHTML = `<div class="notice warn">${I('alert')}<div>Món chưa có công thức. ${canAdd ? '<button class="btn sm primary" data-rnew>Tạo phiên bản V1 (Nháp)</button>' : ''}</div></div>`;
        const b = box.querySelector('[data-rnew]'); if (b) b.onclick = () => Store.act('Recipe/CreateVersion', { form: { DishId: dishId }, tables: ['Recipe', 'RecipeLine'] }).then(x => { if (x.ok) draw(); });
        return;
      }
      const active = recs.find(x => x.IsActive);
      const cur = recs.find(x => x.Id === st.ver) || recs.find(x => x.Status === 'Draft') || active || recs[0]; st.ver = cur.Id;
      const draft = cur.Status === 'Draft' && canEdit;
      const lines = (lineOf[cur.Id] || []).slice().sort((a, b) => (a.SequenceNo || 0) - (b.SequenceNo || 0));
      const lineById = {}; lines.forEach(l => { lineById[l.Id] = l; });
      const vname = x => (x.VersionCode || 'V' + x.VersionNo) + ' · ' + UI.statusVi('Recipe', 'Status', x.Status) + (x.IsActive ? ' (đang dùng)' : '');
      box.innerHTML = `<div class="spread mb"><div class="row-flex"><select class="select" id="rvSel" style="max-width:280px">${recs.map(x => `<option value="${esc(x.Id)}" ${x.Id === cur.Id ? 'selected' : ''}>${esc(vname(x))}</option>`).join('')}</select>${UI.badge(cur.Status, 'Recipe', 'Status')}</div>
        <div class="row-flex">${draft ? `<button class="btn sm" id="rvSave">${I('ok')} Lưu nháp</button><button class="btn sm orange" id="rvSub">Gửi duyệt</button>` : ''}${cur.Status === 'PendingApproval' && canApr && cur.CreatedBy !== Store.user.id ? `<button class="btn sm primary" id="rvApr">${I('approve')} Duyệt</button><button class="btn sm danger" id="rvRej">Từ chối</button>` : ''}${cur.Status === 'Approved' && canApr ? `<button class="btn sm primary" id="rvLock">${I('key')} Khóa</button>` : ''}${['Locked', 'Approved', 'Obsolete'].includes(cur.Status) && canAdd && !recs.some(x => ['Draft', 'PendingApproval'].includes(x.Status)) ? `<button class="btn sm" id="rvNew">${I('plus')} Tạo phiên bản mới</button>` : ''}${Store.perm('menu-recipe').access !== false ? '<a href="javascript:void 0" class="small" data-go="menu-recipe">Màn Công thức chế biến</a>' : ''}</div></div>
        <div class="table-wrap"><table class="table dc-table rv-table"><thead><tr><th>Nguyên liệu</th><th class="num">Định lượng tinh / 100 suất</th><th>ĐVT</th><th class="num">Hao hụt sơ chế %</th><th class="num">Lượng cần mua</th><th class="num">Giá NCC ưu tiên</th><th class="num">Thành tiền / 100 suất</th>${draft ? '<th></th>' : ''}</tr></thead>
        <tbody>${lines.map(l => { const w = +(l.ExpectedWasteRate ?? l.WastePercent ?? 0) || 0, gross = w < 100 ? (+l.Quantity || 0) * 100 / (100 - w) : 0; return `<tr data-rl="${esc(l.Id)}"><td><b>${esc(Store.label('Ingredient', l.IngredientId))}</b></td>
          <td class="num">${draft ? `<input type="number" min="0" step="any" class="input rv-q" data-q value="${l.Quantity ?? ''}">` : U.num(l.Quantity, 3)}</td><td>${esc(Store.label('UnitMeasure', l.UnitId || l.UomId) || '')}</td>
          <td class="num">${draft ? `<input type="number" min="0" max="99" step="any" class="input rv-q" data-w value="${w}">` : U.num(w, 1)}</td><td class="num" data-gross>${U.num(gross, 3)}</td><td class="num" data-price>…</td><td class="num" data-amt>…</td>${draft ? `<td><button class="btn xs danger" data-del title="Xóa dòng">✕</button></td>` : ''}</tr>`; }).join('')}
        ${draft ? `<tr><td colspan="8"><input class="input" id="rvAdd" list="rvIngList" placeholder="+ Gõ tên nguyên liệu để thêm" style="max-width:360px"><datalist id="rvIngList">${Store.rows('Ingredient').filter(i => i.Status !== 'Inactive').slice(0, 1500).map(i => `<option value="${esc((i.Name || '') + ' · ' + (i.Code || ''))}">`).join('')}</datalist>${Store.perm('scm-ingredient').add !== false && Store.perm('scm-ingredient').access !== false ? ' <a href="javascript:void 0" class="small" id="rvIngNew">+ Tạo nguyên liệu mới</a>' : ''}</td></tr>` : ''}</tbody></table></div>
        <div class="kpis compact mt" id="rvKpi"></div><div class="small mt" id="rvCheck"></div>`;
      box.querySelector('#rvSel').onchange = e => { st.ver = e.target.value; draw(); };
      box.querySelectorAll('[data-go]').forEach(a => a.onclick = () => APP.go(a.dataset.go));
      // Giá vốn theo phiên bản (Recipe/CalculateCost, 100 suất) + so với bản đang dùng.
      const calc = id => Store.query('Recipe/CalculateCost', { id, mealCount: 100 });
      Promise.all([calc(cur.Id), active && active.Id !== cur.Id ? calc(active.Id) : Promise.resolve(null)]).then(([x, y]) => {
        if (!box.isConnected) return;
        const d = x && x.ok ? x.data : null, a = y && y.ok ? y.data : null;
        (d ? d.listLine || [] : []).forEach(l => { const tr = [...box.querySelectorAll('[data-rl]')].find(t => { const rl = lineById[t.dataset.rl]; return rl && rl.IngredientId === l.ingredientId; }); if (!tr) return; if (l.ingredientName) tr.querySelector('b').textContent = l.ingredientName; tr.querySelector('[data-price]').innerHTML = nilv(l.unitPrice) ? '<span class="bad small">thiếu giá</span>' : U.money(l.unitPrice); tr.querySelector('[data-amt]').innerHTML = nilv(l.amount) ? '–' : U.money(l.amount); });
        box.querySelectorAll('[data-price]').forEach(td => { if (td.textContent === '…') td.textContent = '–'; });
        box.querySelectorAll('[data-amt]').forEach(td => { if (td.textContent === '…') td.textContent = '–'; });
        const diff = d && a && a.costPerMeal ? (d.costPerMeal - a.costPerMeal) / a.costPerMeal * 100 : null;
        box.querySelector('#rvKpi').outerHTML = UI.kpis([
          UI.kpi('Giá vốn NL / suất', d ? U.money(d.costPerMeal || 0) : '–', d && d.complete === false ? 'còn dòng thiếu giá' : '', 'calc', d && d.complete === false ? 'orange' : 'green'),
          UI.kpi('Số nguyên liệu', U.num(lines.length), '', 'clipboard', 'blue'),
          UI.kpi('Thành phẩm / suất', nilv((o.dish || {}).standardServingSize) ? '–' : U.num(o.dish.standardServingSize) + ' ' + esc(o.unitName || ''), '', 'utensils', 'blue'),
          UI.kpi('So với bản đang dùng', diff === null ? '–' : (diff > 0 ? '+' : '') + U.num(diff, 1) + '%', !active ? 'chưa có bản đang dùng' : active.Id !== cur.Id ? esc(active.VersionCode || 'V' + active.VersionNo) : 'đây là bản đang dùng', 'chart', diff > 0 ? 'orange' : 'green'),
        ], 'compact') || '<div id="rvKpi"></div>';
        const chk = [];
        chk.push(d && d.complete !== false ? '✓ Đủ giá nhà cung cấp' : '⚠ Có nguyên liệu thiếu giá nhà cung cấp');
        chk.push(lines.every(l => l.UnitId || l.UomId) ? '✓ Đơn vị đủ' : '⚠ Có dòng thiếu đơn vị');
        chk.push(cur.Instructions ? '✓ Có hướng dẫn chế biến' : '· Chưa có hướng dẫn chế biến (không bắt buộc)');
        box.querySelector('#rvCheck').textContent = chk.join('   ');
      });
      if (draft) {
        // Enter xuống dòng cùng cột.
        box.querySelectorAll('.rv-q').forEach(inp => inp.onkeydown = e => { if (e.key !== 'Enter') return; e.preventDefault(); const col = inp.hasAttribute('data-q') ? '[data-q]' : '[data-w]'; const all = [...box.querySelectorAll(col)]; const nx = all[all.indexOf(inp) + 1]; if (nx) nx.focus(); else box.querySelector('#rvAdd').focus(); });
        const save = () => {
          const ops = [...box.querySelectorAll('[data-rl]')].map(tr => { const l = lineById[tr.dataset.rl], q = +tr.querySelector('[data-q]').value, w = +tr.querySelector('[data-w]').value; if (!l || (q === +l.Quantity && w === +(l.ExpectedWasteRate ?? l.WastePercent ?? 0))) return null; return () => Store.act('RecipeLine/Update', { method: 'PUT', form: Object.assign({}, l, { Quantity: q, ExpectedWasteRate: w, WastePercent: w }), quiet: false }); }).filter(Boolean);
          return ops.reduce((p, f) => p.then(f), Promise.resolve());
        };
        box.querySelector('#rvSave').onclick = () => save().then(() => { UI.toast('Đã lưu nháp công thức', 'ok'); draw(); });
        box.querySelector('#rvSub').onclick = () => save().then(() => {
          if (!lines.length) return UI.toast('Công thức phải có ít nhất 1 nguyên liệu', 'err');
          UI.confirm('Gửi duyệt phiên bản ' + esc(cur.VersionCode || 'V' + cur.VersionNo) + '? Sau khi gửi không sửa được định lượng; duyệt cấp cuối là khóa và áp dụng.', () => Store.act('Recipe/Submit', { form: { id: cur.Id, effectiveFrom: U.addDays(Store.ctx.date, 1) }, tables: ['Recipe', 'ApprovalRequest'] }).then(x => { if (x.ok) { UI.toast('Đã gửi duyệt', 'ok'); draw(); } }), { ok: 'Gửi duyệt' });
        });
        box.querySelectorAll('[data-del]').forEach(b => b.onclick = () => { const id = b.closest('[data-rl]').dataset.rl; UI.confirm('Xóa nguyên liệu này khỏi công thức?', () => Store.act('RecipeLine/Delete?id=' + encodeURIComponent(id), { method: 'DELETE', tables: ['RecipeLine'] }).then(x => { if (x.ok) draw(); }), { danger: true, ok: 'Xóa' }); });
        const add = box.querySelector('#rvAdd');
        add.onchange = () => {
          const v = add.value.trim(), code = v.split(' · ').pop();
          const ing = Store.rows('Ingredient').find(i => i.Code === code) || Store.rows('Ingredient').find(i => (i.Name || '').toLowerCase() === v.toLowerCase());
          if (!ing) return UI.toast('Không có nguyên liệu này trong danh mục' + (Store.perm('scm-ingredient').add !== false ? ' – bấm "+ Tạo nguyên liệu mới"' : ''), 'warn');
          if (lines.some(l => l.IngredientId === ing.Id)) return UI.toast('Nguyên liệu đã có trong công thức', 'warn');
          Store.act('RecipeLine/Insert', { form: { RecipeId: cur.Id, IngredientId: ing.Id, Quantity: 0, UnitId: ing.BaseUnitId || ing.UnitId || null, UomId: ing.BaseUnitId || ing.UnitId || null, ExpectedWasteRate: 0, WastePercent: 0, SequenceNo: lines.length + 1, SortOrder: lines.length + 1 }, tables: ['RecipeLine'] })
            .then(x => { if (!x.ok) return; draw().then(() => { const q = [...box.querySelectorAll('[data-q]')].pop(); if (q) q.focus(); }); });
        };
        const ni = box.querySelector('#rvIngNew'); if (ni) ni.onclick = () => UI.form('Ingredient', null, { title: 'Nguyên liệu', onSave: () => Store.reload(['Ingredient']).then(draw) });
      }
      const act = (sel, path, msg, extra) => { const b = box.querySelector(sel); if (b) b.onclick = () => extra ? extra() : Store.act(path, { form: { Id: cur.Id }, tables: ['Recipe', 'ApprovalRequest'] }).then(x => { if (x.ok) { UI.toast(msg, 'ok'); draw(); if (reload) reload(); } }); };
      act('#rvApr', 'Recipe/Approve', 'Đã duyệt phiên bản');
      act('#rvRej', null, null, () => UI.confirm('Từ chối phiên bản này?', reason => Store.act('Recipe/Reject', { form: { Id: cur.Id, Note: reason }, tables: ['Recipe', 'ApprovalRequest'] }).then(x => { if (x.ok) { UI.toast('Đã từ chối', 'ok'); draw(); } }), { input: 'Lý do từ chối', required: true, danger: true, ok: 'Từ chối' }));
      act('#rvLock', null, null, () => UI.confirm('Khóa & áp dụng phiên bản này từ ngày mai? Bản đang dùng ngừng áp dụng, giá thành chuẩn tính lại.', () => Store.act('Recipe/Lock', { form: { Id: cur.Id, EffectiveFrom: U.addDays(Store.ctx.date, 1) }, tables: ['Recipe', 'StandardCost', 'Dishe'] }).then(x => { if (x.ok) { UI.toast('Đã khóa & áp dụng', 'ok'); draw(); if (reload) reload(); } }), { ok: 'Khóa' }));
      act('#rvNew', null, null, () => Store.act('Recipe/CreateVersion', { form: { DishId: dishId, CopyFromRecipeId: cur.Id }, tables: ['Recipe', 'RecipeLine'] }).then(x => { if (x.ok) { st.ver = null; UI.toast('Đã tạo phiên bản mới (Nháp) – chép từ ' + esc(cur.VersionCode || ''), 'ok'); draw(); } }));
    });
    return draw();
  };
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
        <div class="field mt"><label>Loại suất <span class="req">*</span></label>${UI.refSelect('rvMt', 'MealType', { required: true, value: ms.mt || '', placeholder: '— Chọn loại suất —' })}</div>
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
            out.innerHTML = `<div class="kv"><div><label>Thực đơn</label><span><a href="javascript:void 0" id="rvGo"><b>${esc(d.menuCode || '')}</b></a> · ${esc(d.menuName || '')}</span></div><div><label>Cấp phân công</label><span>${d.level === 'LOCATION' ? UI.tag('Gán theo điểm giao', 'green') : d.level === 'COMMON' ? UI.tag('Thực đơn chung', 'orange') : UI.tag('Gán theo khách hàng', 'blue')}</span></div></div>`;
            out.querySelector('#rvGo').onclick = () => { ms.menu = d.menuId; ms.mt = mt; close(); APP.go('menu-menu'); };
          });
        };
      }
    }));
  });
  // ---------- Yêu cầu đổi món: tạo + gửi duyệt (MenuChangeRequest/Create) ----------
  APP.register('menu-change-request', (el, f) => {
    // UI-6.13 (D-91): mục menu đã ẩn – link cũ / thông báo mở màn Thực đơn tuần, tab Đổi món.
    if (Store.isLive) { mw.tab = 'chg'; return APP.go('menu-menu'); }
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
