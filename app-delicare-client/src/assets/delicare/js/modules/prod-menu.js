/* UI-6.24 (D-105): menu Sản xuất 7 → 4 mục.
 * - Mẻ nấu: 2 tab "Điều độ mẻ" (bảng ca × dây chuyền cho bếp – giữ nguyên) · "Sản lượng" (danh sách chỉ xem – thay mục Sản lượng đã ẩn);
 *   link "Bảng sản xuất ngày" (trang kế hoạch của ngày đang xem) thay link "Lệnh sản xuất".
 * - Dây chuyền & trạm bếp: 2 tab "Dây chuyền" · "Trạm bếp" (giữ 2 bảng, quyền theo mục cũ – Trạm bếp dùng quyền Dây chuyền; nút In QR của ghep.js giữ nguyên).
 * Tải SAU ghep.js / group-panel.js (bọc màn đã được mở rộng). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { batch: 'board', line: 'line' };
  const tabsHtml = (key, list) => `<div class="tabs mb" data-pmtabs>${list.map(([k, l]) => `<button class="tab ${st[key] === k ? 'active' : ''}" data-pm="${k}">${l}</button>`).join('')}</div>`;
  const placeTabs = (el, html) => { const h = el.querySelector('.page-heading'); if (h && !el.querySelector('[data-pmtabs]')) h.insertAdjacentHTML('afterend', html); };
  const bind = (el, key, rerender) => el.querySelectorAll('[data-pm]').forEach(b => b.onclick = () => { st[key] = b.dataset.pm; rerender(); });
  const asList = (el, f, id, top) => {
    const meta = Object.assign({}, APP.FN[id], { name: f.name });
    const draw = () => { (APP.SCREENS[id] || ((e, ff) => APP.genericList(e, ff, { noAdd: UI.viewOnly(meta.table) })))(el, meta, {}); placeTabs(el, top); if (APP.afterRender) APP.afterRender(el, id); };
    return meta.table ? Store.ensure(meta.table).then(draw, draw) : draw();
  };

  const baseBatch = APP.SCREENS['prod-batch'];
  APP.register('prod-batch', (el, f, p) => {
    if (!Store.isLive) return baseBatch(el, f, p);
    if (p && p.tab === 'output') st.batch = 'output';
    const tabs = tabsHtml('batch', [['board', 'Điều độ mẻ'], ['output', 'Sản lượng']]);
    const rerender = () => APP.SCREENS['prod-batch'](el, f, {});
    if (st.batch === 'output') return Promise.resolve(asList(el, f, 'prod-output', tabs)).then(() => bind(el, 'batch', rerender));
    baseBatch(el, f, p);
    placeTabs(el, tabs); bind(el, 'batch', rerender);
    // Link "Lệnh sản xuất" → "Bảng sản xuất ngày" của ngày đang xem (một kế hoạch / ngày).
    const a = el.querySelector('.heading-actions a[href="#/prod-order"]');
    if (a) {
      const chip = el.querySelector('[data-d].active'), dp = el.querySelector('[data-dp]');
      const day = (dp && dp.value) || (chip && chip.dataset.d) || Store.ctx.date;
      const plan = Store.rows('ProductionPlan').find(x => !x.IsDeleted && U.day(x.ProductionDate) === day);
      a.innerHTML = `${I('calendar')} Bảng sản xuất ngày`;
      a.setAttribute('href', plan ? '#/prod-plan-detail?id=' + encodeURIComponent(plan.Id) : '#/prod-plan');
      a.title = plan ? 'Mở kế hoạch ' + (plan.PlanNumber || '') + ' – ma trận món × ca' : 'Ngày này chưa có kế hoạch – mở Kế hoạch sản xuất';
    }
  });

  // UI-6.26 (D-107): "Bàn nấu" (= KitchenWorkstation) chỉ còn Tên, Khu vực, Ghi chú (+ mã, trạng thái) – bỏ tọa độ / bán kính khỏi form và danh sách.
  const WS_FIELDS = ['Code', 'Name', 'AreaName', 'Status', 'Note'];
  const baseForm = UI.form;
  UI.form = (t, row, opt = {}) => baseForm(t, row, t === 'KitchenWorkstation' && Store.isLive && !opt.fields ? Object.assign({}, opt, { fields: WS_FIELDS }) : opt);

  // UI-6.26 (D-107): tab Dây chuyền – công suất, phút / mẻ, nhóm món đang gán (ra lệnh chia mẻ theo dây chuyền của nhóm món); cảnh báo nhóm chưa gán + Gán nhanh.
  const perm = () => Store.perm('prod-line');
  const lineForm = (line, ov, done) => {
    const cats = ov.listCategory || [], mine = new Set((line && line.listCategory || []).map(c => c.id));
    UI.modal({ title: line ? 'Sửa dây chuyền ' + esc(line.name || '') : 'Thêm dây chuyền', size: 'md',
      body: `<div class="form-grid"><div class="field" style="grid-column:1/-1"><label>Tên dây chuyền <span class="req">*</span></label><input class="input" id="plN" maxlength="200" value="${esc(line ? line.name || '' : '')}" placeholder="vd Bếp món mặn"></div>
        <div class="field"><label>Công suất (suất / mẻ) <span class="req">*</span></label><input class="input" type="number" min="1" step="1" id="plC" value="${line ? esc(line.capacityMealPerBatch ?? '') : ''}"></div>
        <div class="field"><label>Thời gian 1 mẻ (phút) <span class="req">*</span></label><input class="input" type="number" min="1" step="1" id="plM" value="${line ? esc(line.minutesPerBatch ?? '') : ''}"></div>
        <div class="field"><label>Trạng thái</label><select class="select" id="plS"><option value="Active">Đang hoạt động</option><option value="Inactive" ${line && line.status === 'Inactive' ? 'selected' : ''}>Ngừng hoạt động</option></select></div>
        <div class="field"><label>Mã</label><input class="input" value="${esc(line ? line.code || '' : '')}" placeholder="(tự sinh khi lưu)" readonly></div></div>
        <div class="field mt"><label>Nhóm món nấu ở dây chuyền này</label><div class="pl-cats">${cats.map(c => `<label class="pl-cat"><input type="checkbox" value="${esc(c.id)}" ${mine.has(c.id) ? 'checked' : ''}> <span>${esc(c.name || '')} <span class="muted small">· ${U.num(c.dishCount)} món${c.productionLineId && !mine.has(c.id) ? ' · đang ở ' + esc(c.productionLineName || '') : ''}</span></span></label>`).join('') || '<span class="muted small">Chưa có nhóm món</span>'}</div>
          <div class="small muted">Chọn nhóm đang ở dây chuyền khác = chuyển nhóm sang dây chuyền này.</div></div>
        <div class="field mt"><label>Ghi chú</label><textarea class="input" rows="2" id="plNote" maxlength="2000">${esc(line ? line.note || '' : '')}</textarea></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="plOk">Lưu</button>',
      onMount: (m, close) => m.querySelector('#plOk').onclick = () => {
        const v = id => m.querySelector(id).value.trim();
        if (!v('#plN')) return UI.toast('Nhập tên dây chuyền', 'err');
        if (!(+v('#plC') > 0) || !(+v('#plM') > 0)) return UI.toast('Công suất và thời gian 1 mẻ phải lớn hơn 0', 'err');
        Store.act('ProductionLine/SaveWithCategories', { json: { id: line ? line.id : null, name: v('#plN'), capacityMealPerBatch: +v('#plC'), minutesPerBatch: +v('#plM'), status: v('#plS'), note: v('#plNote'),
          listDishCategoryId: [...m.querySelectorAll('.pl-cats input:checked')].map(x => x.value) }, tables: ['ProductionLine', 'DishCategorie'] })
          .then(r => { if (!r.ok) return; UI.toast('Đã lưu dây chuyền ' + esc((r.data && r.data.name) || ''), 'ok'); close(); done(); });
      } });
  };
  const quickAssign = (ov, done) => {
    const lines = (ov.listLine || []).filter(l => l.status !== 'Inactive'), cats = ov.listCategory || [];
    const opts = cur => `<option value="">— Chưa gán (nấu 1 mẻ) —</option>${lines.map(l => `<option value="${esc(l.id)}" ${l.id === cur ? 'selected' : ''}>${esc(l.name || l.code)}</option>`).join('')}`;
    UI.modal({ title: 'Gán nhanh dây chuyền cho nhóm món', size: 'md',
      body: `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Nhóm món</th><th class="num">Số món</th><th>Dây chuyền</th></tr></thead><tbody>${cats.map(c => `<tr><td><b>${esc(c.name || '')}</b>${c.lineInactive ? '<span class="sub bad">Dây chuyền đang gán đã ngừng</span>' : ''}</td><td class="num">${U.num(c.dishCount)}</td><td><select class="select" data-cat="${esc(c.id)}" data-cur="${esc(c.lineInactive ? '' : c.productionLineId || '')}">${opts(c.lineInactive ? '' : c.productionLineId)}</select></td></tr>`).join('')}</tbody></table></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="qaOk">Lưu</button>',
      onMount: (m, close) => m.querySelector('#qaOk').onclick = () => {
        const items = [...m.querySelectorAll('[data-cat]')].filter(s => s.value !== s.dataset.cur).map(s => ({ dishCategoryId: s.dataset.cat, productionLineId: s.value || null }));
        if (!items.length) return close();
        Store.act('DishCategory/AssignProductionLine', { json: { listItem: items }, tables: ['DishCategorie'] }).then(r => { if (!r.ok) return; UI.toast(esc((r.res && r.res.responseMessage) || 'Đã gán'), 'ok'); close(); done(); });
      } });
  };
  function lineTab(el, f, tabs) {
    const p = perm();
    el.innerHTML = APP.header(f, p.add !== false && p.edit !== false ? `<button class="btn primary" id="plAdd">${I('plus')} Thêm dây chuyền</button>` : '', { noNote: true }) + tabs + '<div id="plBody"><div class="muted small">Đang tải…</div></div>';
    const body = el.querySelector('#plBody');
    const load = () => Store.query('ProductionLine/GetOverview').then(x => {
      if (!body.isConnected) return;
      if (!x.ok || !x.data) { body.innerHTML = `<div class="muted small">${esc((x.res && x.res.responseMessage) || 'Không tải được dây chuyền')}</div>`; return; }
      const ov = x.data, lines = ov.listLine || [], edit = p.edit !== false;
      body.innerHTML = (ov.unassignedCategoryCount ? `<div class="notice warn mb">${I('alert')}<div><b>${U.num(ov.unassignedCategoryCount)} nhóm món chưa gán dây chuyền</b> – ${U.num(ov.unassignedDishCount)} món của các nhóm này sẽ nấu 1 mẻ khi ra lệnh sản xuất.${edit ? ' <button class="btn xs" id="plQa">Gán nhanh</button>' : ''}</div></div>` : '<div class="notice ok mb">' + I('check') + '<div>Mọi nhóm món đã gán dây chuyền – ra lệnh sản xuất chia mẻ theo công suất của dây chuyền.</div></div>')
        + `<div class="card panel"><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Dây chuyền</th><th class="num">Công suất (suất / mẻ)</th><th class="num">Phút / mẻ</th><th>Nhóm món đang gán</th><th class="num">Số món</th><th>Trạng thái</th><th></th></tr></thead><tbody>${lines.map(l => `<tr><td><b>${esc(l.name || '')}</b><br><span class="small muted">${esc(l.code || '')}</span></td><td class="num">${U.num(l.capacityMealPerBatch)}</td><td class="num">${U.num(l.minutesPerBatch)}</td>
          <td>${(l.listCategory || []).map(c => UI.tag(c.name || '', l.status === 'Inactive' ? 'orange' : 'green')).join(' ') || '<span class="muted small">Chưa có nhóm</span>'}</td><td class="num">${U.num(l.dishCount)}</td><td>${l.status === 'Inactive' ? UI.tag('Ngừng hoạt động', 'orange') : UI.tag('Đang hoạt động', 'green')}</td>
          <td class="actions">${edit ? `<button class="btn xs" data-pl="${esc(l.id)}">Sửa</button>` : ''}</td></tr>`).join('') || '<tr><td colspan="7" class="muted small">Chưa có dây chuyền</td></tr>'}</tbody></table></div></div>`;
      const again = () => load();
      body.querySelectorAll('[data-pl]').forEach(b => b.onclick = () => lineForm(lines.find(l => l.id === b.dataset.pl), ov, again));
      const qa = body.querySelector('#plQa'); if (qa) qa.onclick = () => quickAssign(ov, again);
      const add = el.querySelector('#plAdd'); if (add) add.onclick = () => lineForm(null, ov, again);
      if (APP.afterRender) APP.afterRender(el, 'prod-line');
    });
    load();
  }

  const baseLine = APP.SCREENS['prod-line'];
  APP.register('prod-line', (el, f, p) => {
    if (!Store.isLive) return baseLine ? baseLine(el, f, p) : APP.genericList(el, f);
    if (p && p.tab === 'ws') st.line = 'ws';
    const tabs = tabsHtml('line', [['line', 'Dây chuyền'], ['ws', 'Bàn nấu']]);
    const rerender = () => APP.SCREENS['prod-line'](el, f, {});
    if (st.line === 'ws') {
      const meta = Object.assign({}, APP.FN['prod-workstation'], { name: f.name });
      const draw = () => { APP.genericList(el, meta, { cols: [{ k: 'Code', label: 'Mã' }, { k: 'Name', label: 'Tên bàn nấu' }, { k: 'AreaName', label: 'Khu vực' }, { k: 'Note', label: 'Ghi chú' }, { k: 'Status', label: 'Trạng thái' }] }); placeTabs(el, tabs); bind(el, 'line', rerender); if (APP.afterRender) APP.afterRender(el, 'prod-workstation'); };
      return Store.ensure('KitchenWorkstation').then(draw, draw);
    }
    lineTab(el, f, tabs); bind(el, 'line', rerender);
  });
})();
