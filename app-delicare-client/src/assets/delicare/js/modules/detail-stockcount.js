/* UI-6 (D-80) – trang chi tiết toàn màn: PHIẾU KIỂM KÊ (#/scm-stockcount-detail?id=) – trang MẪU của khuôn chứng từ (APP.D360.page, modules/detail-page.js).
 * BE: StockCount/GetOverview = GetDetail cũ (dòng, % lệch, vượt ngưỡng, bút toán, thao tác được phép) + yêu cầu duyệt mới nhất + số dòng lệch.
 * Nút phần đầu gọi lại đúng thao tác đang có ở danh sách (modules/scm.js – APP.scmStockCount): Kiểm đếm / Gửi duyệt / Ghi sổ (hộp kiểm đếm), In. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, D = APP.D360;
  const FLOW = ['Draft', 'Counting', 'PendingApproval', 'Posted'];
  const st = s => UI.statusVi('StockCount', 'Status', s) || s;
  const vr = l => `${l.varianceQuantity > 0 ? '+' : ''}${U.num(l.varianceQuantity, 3)}${l.varianceQuantity ? ` (${U.num(l.variancePercent, 1)}%)` : ''}`;
  const lineTable = lines => D.table(lines, [
    { label: 'Nguyên liệu', html: l => `<b>${esc(l.ingredientName || '')}</b><span class="sub">${esc(l.ingredientCode || '')}</span>` },
    { label: 'Lô', html: l => esc(l.lotNumber || '–') + (l.expiryDate ? `<span class="sub">HSD ${D.dmy(l.expiryDate)}</span>` : '') },
    { label: 'Hệ thống', num: true, html: l => U.num(l.systemQuantity, 3) + ' ' + esc(l.unitName || '') },
    { label: 'Thực tế', num: true, html: l => U.num(l.physicalQuantity, 3) },
    { label: 'Lệch', num: true, html: l => `<span class="${l.overThreshold ? 'bad' : ''}">${vr(l)}</span>` },
    { label: 'Giá trị lệch', num: true, html: l => (l.varianceValue ? U.money(l.varianceValue) : '–') },
    { label: 'Lý do', html: l => esc(l.varianceReason || '') },
  ]);

  D.page({
    id: 'scm-stockcount-detail', screen: 'scm-stockcount', table: 'StockCount', kind: 'doc', api: 'StockCount/GetOverview', resource: 'StockCount',
    head: o => {
      const d = o.detail || {}, sc = d.stockCount || {}, acts = d.allowedActions || [];
      return {
        title: 'Phiếu kiểm kê ' + (sc.countNumber || ''), no: sc.countNumber, avatar: 'KK', avatarCls: 'biz',
        tags: [sc.status ? UI.badge(sc.status, 'StockCount', 'Status') : '', d.needsApproval && !d.isApproved ? UI.tag('Cần duyệt (lệch > ' + U.num(d.varianceThresholdPercent || 0, 1) + '%)', 'orange') : '', d.isApproved ? UI.tag('Đã duyệt', 'green') : ''],
        meta: [esc(d.warehouseName || ''), sc.countType && esc(UI.statusVi('StockCount', 'CountType', sc.countType) || sc.countType), sc.countDate && 'Ngày đếm ' + D.dmy(sc.countDate), o.responsibleName && esc('Phụ trách: ' + o.responsibleName)],
        steps: D.stepsOf(FLOW, sc.status, ['Cancelled'], st),
        buttons: [
          { act: 'count', icon: 'check', label: 'Kiểm đếm', ok: D.can('scm-stockcount', 'edit') && (acts.includes('recordCounts') || acts.includes('submit') || acts.includes('post')), primary: true },
          { act: 'print', icon: 'export', label: 'In biên bản', ok: true },
        ],
        more: [d.pendingApproval ? `<button data-h="inbox">Mở màn Phê duyệt</button>` : ''],
      };
    },
    kpis: o => {
      const d = o.detail || {};
      return UI.kpi('Số dòng', U.num(o.lineCount || 0), '', 'clipboard', 'blue')
        + UI.kpi('Dòng lệch', U.num(o.varianceLineCount || 0), o.overThresholdCount ? `· ${U.num(o.overThresholdCount)} vượt ngưỡng` : '', 'alert', o.overThresholdCount ? 'orange' : 'green')
        + UI.kpi('Lệch % lớn nhất', U.num(d.maxVariancePercent || 0, 1) + '%', `ngưỡng ${U.num(d.varianceThresholdPercent || 0, 1)}%`, 'chart', (d.maxVariancePercent || 0) > (d.varianceThresholdPercent || 0) ? 'red' : 'green')
        + UI.kpi('Giá trị chênh', U.moneyShort(d.totalVarianceValue || 0), '', 'calc', (d.totalVarianceValue || 0) < 0 ? 'red' : 'blue');
    },
    overview: (box, o) => {
      const d = o.detail || {}, sc = d.stockCount || {}, lines = d.listLine || [];
      const top = lines.filter(l => l.varianceQuantity).sort((a, b) => Math.abs(b.variancePercent) - Math.abs(a.variancePercent)).slice(0, 10);
      box.innerHTML = D.split(
        D.block('Thông tin phiếu', D.kv([['Số phiếu', esc(sc.countNumber || '')], ['Kho', esc(d.warehouseName || '')], ['Ngày đếm', D.dmy(sc.countDate)], ['Phụ trách', esc(o.responsibleName || '')],
          ['Trạng thái', esc(d.statusName || st(sc.status))], ['Duyệt', d.isApproved ? 'Đã duyệt' : d.needsApproval ? 'Cần duyệt 2 cấp (lệch vượt ngưỡng)' : 'Không cần duyệt'],
          ['Ghi chú', esc(sc.note || '')]])),
        D.block('Dòng lệch nhiều nhất', top.length ? lineTable(top) : '<div class="muted small">Không có dòng lệch.</div>')
        + (d.pendingApproval ? D.block('Đang chờ duyệt', D.kv([['Cấp', `${d.pendingApproval.stepNo}/${d.pendingApproval.stepCount} – ${esc(d.pendingApproval.stepName || '')}`], ['Hạn', d.pendingApproval.dueAt ? esc(U.dt(d.pendingApproval.dueAt)) : '']])) : ''));
    },
    tabs: [
      { label: 'Chi tiết dòng', render: (box, o) => { const lines = (o.detail || {}).listLine || []; box.innerHTML = lines.length ? lineTable(lines) : D.none; } },
      { label: 'Bút toán điều chỉnh', render: (box, o) => {
        const tx = (o.detail || {}).listTransaction || [];
        box.innerHTML = tx.length ? D.table(tx, [{ label: 'Số bút toán', html: t => esc(t.transactionNo || t.transactionNumber || '') }, { label: 'Loại', html: t => esc(UI.statusVi('InventoryTransaction', 'TransactionType', t.transactionType) || t.transactionType || '') },
          { label: 'Số lượng', num: true, html: t => U.num(t.quantity, 3) }, { label: 'Ngày', html: t => esc(U.dt(t.transactionDate || t.createdDate)) }]) : '<div class="muted small">Chưa ghi sổ.</div>';
      } },
    ],
    onAct: (act, o, id) => {
      if (act === 'count' && APP.scmStockCount) APP.scmStockCount.countModal(id);
      else if (act === 'print') Store.download('StockCount/Print', { id }).then(x => { if (x.ok) UI.toast('Đã tải ' + esc(x.fileName || 'biên bản kiểm kê'), 'ok'); });
      else if (act === 'inbox') APP.go('wf-inbox');
    },
  });
})();
