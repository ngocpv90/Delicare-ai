/* UI-6.40 (D-121): trang tổng "Danh mục hệ thống" (#/sys-catalog – mục đầu nhóm, không có dòng menu riêng).
 * Lưới thẻ theo quyền Xem từng danh mục (Common/GetCatalogSummary): tên + biểu tượng · số bản ghi đang dùng · sửa gần nhất · cảnh báo thiếu dữ liệu (bấm mở đúng màn). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  APP.FN['sys-catalog'] = { ph: 'P0', grp: 'Danh mục hệ thống', fn: 'Tổng quan danh mục', id: 'sys-catalog', name: 'Tổng quan danh mục', types: ['DB'], table: null, rel: [], st: 'MAPPED', note: '' };
  const ICON = { 'adm-orgunit': 'flow', 'adm-position': 'user', 'hr-shift': 'calendar', 'scm-uom': 'calc', 'scm-warehouse': 'warehouse', 'prod-line': 'factory', 'log-vehicle': 'truck', 'adm-reference': 'list', 'adm-admin-unit': 'map', 'kpi-definition': 'chart' };
  APP.register('sys-catalog', (el, f) => {
    el.innerHTML = APP.header(f, '<button class="btn" id="scGaps">' + UTIL.icon('export') + ' Xuất danh sách cần bổ sung</button>', { noNote: true, sub: 'Danh mục ít thay đổi dùng chung cho các phân hệ – số bản ghi, lần sửa gần nhất và dữ liệu còn thiếu.' }) + '<div id="scBody"><div class="card panel small muted">Đang tải…</div></div>';
    // UI-6.49 (9) (D-127 g): Excel danh sách dữ liệu cần bổ sung (mỗi loại 1 sheet, theo quyền xem) – người phụ trách tự bổ sung, hệ thống không sửa hộ.
    el.querySelector('#scGaps').onclick = () => Store.download('Common/ExportDataGaps');
    if (!Store.isLive) { el.querySelector('#scBody').innerHTML = '<div class="card panel muted">Trang tổng chỉ có ở chế độ thật.</div>'; return; }
    Store.query('Common/GetCatalogSummary').then(x => {
      const body = el.querySelector('#scBody'); if (!body) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được')}</div>`; return; }
      const cards = x.list || [], secs = [...new Set(cards.map(c => c.section))];
      body.innerHTML = cards.length ? secs.map(s => `<div class="small muted mb mt">${esc(s)}</div><div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:12px">${cards.filter(c => c.section === s).map(c => `<div class="card panel" style="margin:0"><div class="spread"><a href="${APP.href(c.screen)}" style="display:flex;gap:8px;align-items:center"><span class="kpi-icon green">${I(ICON[c.screen] || 'list')}</span><b>${esc(c.name)}</b></a><span style="font-size:22px;font-weight:800">${U.num(c.count || 0)}</span></div>
        <div class="small muted mt">${c.lastModified ? 'Sửa gần nhất ' + U.dt(c.lastModified) + (c.lastModifiedBy ? ' · ' + esc(c.lastModifiedBy) : '') : 'Chưa có lịch sử sửa'}</div>
        ${(c.listWarning || []).map(w => `<a class="small bad" style="display:block;margin-top:4px" href="${APP.href(w.screen)}">⚠ ${esc(w.text)}</a>`).join('')}</div>`).join('')}</div>`).join('') : '<div class="card panel empty">Bạn chưa có quyền xem danh mục nào.</div>';
    });
  });
})();
