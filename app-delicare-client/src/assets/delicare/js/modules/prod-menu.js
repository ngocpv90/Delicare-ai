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

  const baseLine = APP.SCREENS['prod-line'];
  APP.register('prod-line', (el, f, p) => {
    if (!Store.isLive) return baseLine ? baseLine(el, f, p) : APP.genericList(el, f);
    if (p && p.tab === 'ws') st.line = 'ws';
    const tabs = tabsHtml('line', [['line', 'Dây chuyền'], ['ws', 'Trạm bếp']]);
    const rerender = () => APP.SCREENS['prod-line'](el, f, {});
    if (st.line === 'ws') return Promise.resolve(asList(el, f, 'prod-workstation', tabs)).then(() => bind(el, 'line', rerender));
    if (baseLine) baseLine(el, f, p); else APP.genericList(el, f);
    placeTabs(el, tabs); bind(el, 'line', rerender);
  });
})();
