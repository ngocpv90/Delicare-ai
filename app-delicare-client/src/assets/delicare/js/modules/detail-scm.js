/* UI-6 (D-80) nhóm B – trang chi tiết toàn màn MUA HÀNG / KHO (khung APP.D360.page, modules/detail-page.js; mẫu: detail-stockcount.js):
 *   Đơn mua (#/scm-po-detail?id=)       – PurchaseOrder/GetOverview: dòng + VAT, tiến độ nhận, phiếu nhập kèm QC, yêu cầu duyệt mới nhất.
 *   Phiếu nhập (#/scm-gr-detail?id=)    – GoodsReceipt/GetOverview: dòng + QC + nhiệt độ + lô đã tạo.
 *   Đề nghị mua (#/scm-pr-detail?id=)   – PurchaseRequest/GetOverview: dòng + NCC gợi ý + PO đã lập, yêu cầu duyệt mới nhất.
 *   Lô hàng (#/scm-lot-detail?id=)      – MaterialLot/GetOverview: nguồn, tồn theo kho, hạn dùng / chặn xuất, giao dịch kho; tab Truy xuất = MaterialLot/Trace.
 *   Nguyên liệu (#/scm-ingredient-detail?id=) – Ingredient/GetOverview: tồn, lô, giá mua gần nhất, bảng giá NCC, lịch sử giá, giao dịch kho.
 * Nút phần đầu gọi lại đúng thao tác của danh sách (modules/scm.js – APP.scmPo / APP.scmGr / APP.scmPr / APP.scmLot). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, D = APP.D360;
  const has = v => v !== null && v !== undefined;
  const q3 = v => U.num(v || 0, 3);
  // Liên kết sang trang chi tiết khác (nếu bảng có trang + người dùng có quyền xem màn gốc), không thì chữ thường.
  const link = (t, id, text) => {
    const h = id && APP.detailHref(t, id), p = APP.detailPageOf(t), fn = p && APP.FN[p];
    return h && (!fn || !fn.detailOf || D.can(fn.detailOf)) ? `<a href="${h}">${esc(text || '')}</a>` : esc(text || '');
  };
  const st = (t, s) => UI.statusVi(t, 'Status', s) || s || '';
  const badge = (t, s) => (s ? UI.badge(s, t, 'Status') : '');
  const warns = w => (w && w.length ? `<div class="notice warn">${I('alert')}<div>${w.map(x => esc(x)).join('<br>')}</div></div>` : '');
  const inbox = d => (d && (d.pendingApproval || d.pendingApprovalId) ? `<button data-h="inbox">Mở màn Phê duyệt</button>` : '');
  const pendingBlock = p => (p ? D.block('Đang chờ duyệt', D.kv([['Cấp', `${p.stepNo}/${p.stepCount} – ${esc(p.stepName || '')}`], ['Hạn', p.dueAt ? esc(U.dt(p.dueAt)) : '']])) : '');
  const TX_TYPE = t => UI.statusVi('InventoryTransaction', 'TransactionType', t) || t || '';
  const txTable = rows => D.table(rows, [
    { label: 'Số giao dịch', html: t => `<b>${esc(t.transactionNumber || '')}</b>` },
    { label: 'Loại', html: t => esc(TX_TYPE(t.transactionType)) },
    { label: 'Ngày', html: t => esc(U.dt(t.transactionDate)) },
    { label: 'Kho', html: t => esc(t.warehouseName || '') },
    { label: 'Lô', html: t => (t.materialLotId ? link('MaterialLot', t.materialLotId, t.lotNumber || '') : '–') },
    { label: 'Nhập', num: true, html: t => (t.quantityIn ? U.num(t.quantityIn, 3) : '') },
    { label: 'Xuất', num: true, html: t => (t.quantityOut ? U.num(t.quantityOut, 3) : '') },
    { label: 'Lý do / ghi chú', html: t => esc(t.reason || '') },
  ]);
  const balTable = rows => D.table(rows, [
    { label: 'Kho', html: b => esc(b.warehouseName || '') },
    { label: 'Tồn thực', num: true, html: b => q3(b.onHandQuantity) },
    { label: 'Đã giữ', num: true, html: b => q3(b.reservedQuantity) },
    { label: 'Khả dụng', num: true, html: b => `<b>${q3(b.availableQuantity)}</b>` },
  ]);

  // ================= Đơn mua (PO) =================
  const PO_FLOW = ['Draft', 'PendingApproval', 'Approved', 'Sent', 'PartiallyReceived', 'Received', 'Closed'];
  const poRef = d => ({ Id: (d.purchaseOrder || {}).id, PurchaseOrderNumber: (d.purchaseOrder || {}).purchaseOrderNumber, Status: (d.purchaseOrder || {}).status });
  const poLines = lines => D.table(lines, [
    { label: 'Nguyên liệu', html: l => `<b>${link('Ingredient', l.ingredientId, l.ingredientName)}</b><span class="sub">${esc(l.ingredientCode || '')}</span>` },
    { label: 'Đặt', num: true, html: l => q3(l.quantity) + ' ' + esc(l.unitName || '') },
    { label: 'Đơn giá', num: true, html: l => U.money(l.unitPrice) },
    { label: 'Thành tiền', num: true, html: l => U.money(l.lineAmount) },
    { label: 'VAT', num: true, html: l => U.num(l.vatPct, 1) + '%' },
    { label: 'Đã nhận', num: true, html: l => q3(l.receivedQuantity) },
    { label: 'Chờ QC', num: true, html: l => (l.pendingQuantity ? q3(l.pendingQuantity) : '') },
    { label: 'Còn lại', num: true, html: l => (l.remainingQuantity > 0 ? `<b class="warn">${q3(l.remainingQuantity)}</b>` : '<span class="up">0</span>') },
  ]);
  D.page({
    id: 'scm-po-detail', screen: 'scm-po', table: 'PurchaseOrder', kind: 'doc', api: 'PurchaseOrder/GetOverview', resource: 'PurchaseOrder',
    head: o => {
      const d = o.detail || {}, po = d.purchaseOrder || {}, acts = d.allowedActions || [], edit = D.can('scm-po', 'edit');
      return {
        title: 'Đơn mua ' + (po.purchaseOrderNumber || ''), no: po.purchaseOrderNumber, avatar: 'PO', avatarCls: 'biz',
        tags: [badge('PurchaseOrder', po.status), d.needsDirectorApproval ? UI.tag('Giám đốc duyệt', 'orange') : ''],
        meta: [esc(d.supplierName || ''), po.orderDate && 'Đặt ' + D.dmy(po.orderDate), (po.expectedDeliveryDate || po.expectedDate) && 'Giao dự kiến ' + esc(U.dt(po.expectedDeliveryDate || po.expectedDate)),
          d.purchaseRequestNumber && 'Từ đề nghị ' + link('PurchaseRequest', po.purchaseRequestId, d.purchaseRequestNumber)],
        steps: D.stepsOf(PO_FLOW, po.status, ['Cancelled'], s => st('PurchaseOrder', s)),
        buttons: [
          { act: 'receive', icon: 'package', label: 'Nhận hàng', ok: edit && acts.includes('receive'), primary: true },
          { act: 'submit', icon: 'approve', label: 'Gửi duyệt', ok: edit && acts.includes('submit'), primary: true },
          { act: 'send', icon: 'check', label: 'Gửi NCC', ok: edit && acts.includes('send') },
          { act: 'close', icon: 'check', label: 'Đóng PO', ok: edit && acts.includes('close') },
          { act: 'print', icon: 'export', label: 'In đơn', ok: acts.includes('export') },
        ],
        more: [inbox(d), edit && acts.includes('cancel') ? `<button data-h="cancel">Hủy đơn mua</button>` : ''],
      };
    },
    kpis: o => {
      const d = o.detail || {};
      return UI.kpi('Giá trị gồm VAT', U.moneyShort(d.totalAmountInclVat || 0), 'chưa VAT ' + U.moneyShort(d.subtotalAmount || 0), 'calc', 'blue')
        + UI.kpi('Đã nhận', U.num(o.receivedPercent || 0, 1) + '%', o.remainingLineCount ? `${U.num(o.remainingLineCount)} dòng còn chờ` : 'đủ hàng', 'package', o.receivedPercent >= 100 ? 'green' : 'orange')
        + UI.kpi('Phiếu nhập', U.num(o.receiptCount || 0), `${U.num(o.lineCount || 0)} dòng hàng`, 'clipboard', 'blue')
        + UI.kpi('Bị loại QC', q3(o.rejectedQuantity), '', 'alert', o.rejectedQuantity > 0 ? 'red' : 'green');
    },
    overview: (box, o) => {
      const d = o.detail || {}, po = d.purchaseOrder || {};
      box.innerHTML = warns(d.listWarning) + D.split(
        D.block('Thông tin đơn', D.kv([['Số PO', esc(po.purchaseOrderNumber || '')], ['Nhà cung cấp', esc([d.supplierCode, d.supplierName].filter(Boolean).join(' – '))],
          ['Điện thoại', esc(d.supplierPhone || '')], ['Địa chỉ', esc(d.supplierAddress || '')], ['Ngày đặt', D.dmy(po.orderDate)],
          ['Giao dự kiến', esc(U.dt(po.expectedDeliveryDate || po.expectedDate) || '')], ['Đề nghị mua', d.purchaseRequestNumber ? link('PurchaseRequest', po.purchaseRequestId, d.purchaseRequestNumber) : ''],
          ['Trạng thái', esc(d.statusName || st('PurchaseOrder', po.status))], ['Mức duyệt', d.needsDirectorApproval ? 'Giám đốc (vượt ngưỡng)' : 'Quản lý mua hàng'],
          ['Tổng chưa VAT', U.money(d.subtotalAmount || 0)], ['Tiền VAT', U.money(d.vatAmount || 0)], ['Tổng gồm VAT', `<b>${U.money(d.totalAmountInclVat || 0)}</b>`], ['Ghi chú', esc(po.note || '')]])),
        D.block('Dòng hàng', (d.listLine || []).length ? poLines(d.listLine.slice(0, 8)) + (d.listLine.length > 8 ? `<div class="small muted mt">… và ${d.listLine.length - 8} dòng khác – xem tab Dòng hàng.</div>` : '') : D.none)
        + pendingBlock(d.pendingApproval));
    },
    tabs: [
      { label: 'Dòng hàng', render: (box, o) => { box.innerHTML = poLines((o.detail || {}).listLine || []); } },
      { label: 'Phiếu nhập & QC', render: (box, o) => {
        box.innerHTML = D.table(o.listReceiptQc || [], [
          { label: 'Số phiếu', html: r => `<b>${link('GoodsReceipt', r.id, r.receiptNumber)}</b>` },
          { label: 'Ngày nhận', html: r => D.dmy(r.receiptDate) },
          { label: 'Kho', html: r => esc(r.warehouseName || '') },
          { label: 'Phiếu QC', html: r => esc(r.inspectionNumber || '–') },
          { label: 'Kết quả QC', html: r => (r.qcResult ? UI.tag(r.qcResultName || r.qcResult, r.qcResult === 'DAT' ? 'green' : r.qcResult === 'KHONG_DAT' ? 'red' : 'orange') : '<span class="muted">Chưa kết luận</span>') },
          { label: 'Nhận', num: true, html: r => q3(r.receivedQuantity) },
          { label: 'Đạt', num: true, html: r => q3(r.acceptedQuantity) },
          { label: 'Loại', num: true, html: r => (r.rejectedQuantity ? `<span class="bad">${q3(r.rejectedQuantity)}</span>` : '0') },
          { label: 'Trạng thái', html: r => badge('GoodsReceipt', r.status) },
        ]);
      } },
    ],
    onAct: (act, o) => {
      const r = poRef(o.detail || {}), A = APP.scmPo;
      if (act === 'inbox') return APP.go('wf-inbox');
      if (A && A[act]) A[act](r);
    },
  });

  // ================= Phiếu nhập kho =================
  const grRef = d => ({ Id: (d.goodsReceipt || {}).id, ReceiptNumber: (d.goodsReceipt || {}).receiptNumber, Status: (d.goodsReceipt || {}).status });
  const grSteps = s => {
    const qcDone = ['Accepted', 'PartiallyAccepted', 'Rejected', 'Posted'].includes(s);
    if (s === 'Cancelled') return [{ label: 'Chờ QC', state: 'done' }, { label: st('GoodsReceipt', s), state: 'bad' }];
    return [{ label: st('GoodsReceipt', 'QCPending'), state: s === 'QCPending' ? 'cur' : qcDone ? 'done' : '' },
      { label: qcDone && s !== 'Posted' ? st('GoodsReceipt', s) : 'QC kết luận', state: s === 'Posted' ? 'done' : qcDone ? (s === 'Rejected' ? 'bad' : 'cur') : '' },
      { label: st('GoodsReceipt', 'Posted'), state: s === 'Posted' ? 'cur' : '' }];
  };
  const tempCell = l => (has(l.temperature) ? `<span class="${l.temperatureZone === 'REJECT' ? 'bad' : l.temperatureZone === 'CONDITIONAL' ? 'warn' : ''}">${U.num(l.temperature, 1)} °C</span>${l.temperatureZoneName ? `<span class="sub">${esc(l.temperatureZoneName)}</span>` : ''}` : (l.temperatureZone === 'MISSING' ? '<span class="warn">Thiếu số đo</span>' : '–'));
  const grLines = lines => D.table(lines, [
    { label: 'Nguyên liệu', html: l => `<b>${link('Ingredient', l.ingredientId, l.ingredientName)}</b><span class="sub">${esc(l.ingredientCode || '')}</span>` },
    { label: 'Đặt', num: true, html: l => (has(l.orderedQuantity) ? q3(l.orderedQuantity) : '–') },
    { label: 'Nhận', num: true, html: l => q3(l.receivedQuantity) + ' ' + esc(l.unitName || '') },
    { label: 'Đạt', num: true, html: l => q3(l.acceptedQuantity) },
    { label: 'Loại', num: true, html: l => (l.rejectedQuantity ? `<span class="bad">${q3(l.rejectedQuantity)}</span>` : '0') },
    { label: 'Nhiệt độ', html: tempCell },
    { label: 'Lô NCC / HSD', html: l => esc(l.supplierLotNumber || '–') + (l.expiryDate ? `<span class="sub">HSD ${D.dmy(l.expiryDate)}</span>` : '') },
    { label: 'QC', html: l => (l.qualityStatus ? esc(l.qualityStatusName || l.qualityStatus) : '<span class="muted">–</span>') + (l.useFirst ? ' <span class="tag orange">Dùng trước</span>' : '') },
    { label: 'Lô nội bộ', html: l => (l.materialLotId ? link('MaterialLot', l.materialLotId, l.lotNumber) : '–') },
  ]);
  D.page({
    id: 'scm-gr-detail', screen: 'scm-gr', table: 'GoodsReceipt', kind: 'doc', api: 'GoodsReceipt/GetOverview', resource: 'GoodsReceipt',
    head: o => {
      const d = o.detail || {}, gr = d.goodsReceipt || {}, acts = d.allowedActions || [], edit = D.can('scm-gr', 'edit');
      return {
        title: 'Phiếu nhập ' + (gr.receiptNumber || ''), no: gr.receiptNumber, avatar: 'PN', avatarCls: 'biz',
        tags: [badge('GoodsReceipt', gr.status), d.qcResult ? UI.tag('QC: ' + (d.qcResultName || d.qcResult), d.qcResult === 'DAT' ? 'green' : d.qcResult === 'KHONG_DAT' ? 'red' : 'orange') : ''],
        meta: [esc(d.supplierName || ''), esc(d.warehouseName || ''), gr.receiptDate && 'Nhận ' + D.dmy(gr.receiptDate), d.purchaseOrderNumber && 'PO ' + link('PurchaseOrder', gr.purchaseOrderId, d.purchaseOrderNumber)],
        steps: grSteps(gr.status),
        buttons: [
          { act: 'post', icon: 'check', label: 'Ghi sổ nhập kho', ok: edit && acts.includes('post'), primary: true },
          { act: 'qc', icon: 'shield', label: 'Mở phiếu QC', ok: gr.status === 'QCPending' && D.can('qc-inspection'), primary: true },
          { act: 'print', icon: 'export', label: 'In phiếu', ok: ['Accepted', 'PartiallyAccepted', 'Rejected', 'Posted'].includes(gr.status) },
        ],
        more: [edit && acts.includes('cancel') ? `<button data-h="cancel">Hủy phiếu nhập</button>` : ''],
      };
    },
    kpis: o => UI.kpi('Số dòng', U.num(o.lineCount || 0), `${U.num(o.lotCount || 0)} lô đã tạo`, 'clipboard', 'blue')
      + UI.kpi('Dòng bị loại', U.num(o.rejectedLineCount || 0), '', 'alert', o.rejectedLineCount ? 'red' : 'green')
      + UI.kpi('Lệch nhiệt độ', U.num(o.temperatureIssueCount || 0), 'có điều kiện / loại / thiếu số đo', 'alert', o.temperatureIssueCount ? 'orange' : 'green')
      + UI.kpi('Giá trị đạt', U.moneyShort(o.acceptedValue || 0), 'nhận ' + U.moneyShort(o.receivedValue || 0), 'calc', 'blue'),
    overview: (box, o) => {
      const d = o.detail || {}, gr = d.goodsReceipt || {}, lines = d.listLine || [];
      const lots = lines.filter(l => l.materialLotId);
      box.innerHTML = warns(d.listWarning) + D.split(
        D.block('Thông tin phiếu', D.kv([['Số phiếu', esc(gr.receiptNumber || '')], ['Nhà cung cấp', esc([d.supplierCode, d.supplierName].filter(Boolean).join(' – '))],
          ['Kho nhận', esc(d.warehouseName || '')], ['Ngày nhận', D.dmy(gr.receiptDate)], ['Đơn mua', d.purchaseOrderNumber ? link('PurchaseOrder', gr.purchaseOrderId, d.purchaseOrderNumber) : ''],
          ['Phiếu QC', esc(d.inspectionNumber || '')], ['Kết luận QC', esc(d.qcResultName || (d.qcInspectionId ? 'Chưa kết luận' : ''))],
          ['Sự cố NCC', d.supplierIssueId ? 'Đã ghi nhận sự cố' : ''], ['Trạng thái', esc(d.statusName || st('GoodsReceipt', gr.status))], ['Ghi chú', esc(gr.note || '')]])),
        D.block('Dòng nhận hàng', lines.length ? grLines(lines.slice(0, 8)) : D.none)
        + (lots.length ? D.block('Lô đã tạo', lots.map(l => `${link('MaterialLot', l.materialLotId, l.lotNumber)} · ${esc(l.ingredientName || '')} · ${q3(l.acceptedQuantity)} ${esc(l.unitName || '')}${l.lotStatus ? ' ' + badge('MaterialLot', l.lotStatus) : ''}`).join('<br>')) : ''));
    },
    tabs: [
      { label: 'Dòng & QC', render: (box, o) => { box.innerHTML = grLines((o.detail || {}).listLine || []); } },
      { label: 'Kiểm tra cảm quan', render: (box, o) => {
        box.innerHTML = D.table((o.detail || {}).listLine || [], [
          { label: 'Nguyên liệu', html: l => esc(l.ingredientName || '') },
          { label: 'Bảo quản', html: l => esc(l.storageType || '') },
          { label: 'Khối lượng', num: true, html: l => (has(l.weight) ? U.num(l.weight, 3) : '–') },
          { label: 'Cảm quan', html: l => esc(l.appearance || '') },
          { label: 'Bao bì', html: l => esc(l.packaging || '') },
          { label: 'Gợi ý', html: l => esc(l.suggestedResult || '') },
          { label: 'Ghi chú', html: l => esc(l.remark || '') },
        ]);
      } },
    ],
    onAct: (act, o) => { const A = APP.scmGr; if (A && A[act]) A[act](grRef(o.detail || {})); },
  });

  // ================= Đề nghị mua =================
  const PR_FLOW = ['Draft', 'PendingApproval', 'Approved', 'Converted'];
  const prRef = d => ({ Id: (d.purchaseRequest || {}).id, RequestNumber: (d.purchaseRequest || {}).requestNumber, Status: (d.purchaseRequest || {}).status });
  const prLines = lines => D.table(lines, [
    { label: 'Nguyên liệu', html: l => `<b>${link('Ingredient', l.ingredientId, l.ingredientName)}</b><span class="sub">${esc(l.ingredientCode || '')}</span>` },
    { label: 'Số lượng', num: true, html: l => q3(l.quantity) + ' ' + esc(l.unitName || '') },
    { label: 'Ngày cần', html: l => D.dmy(l.requiredDate) },
    { label: 'NCC gợi ý', html: l => (l.supplierName ? esc(l.supplierName) : '<span class="warn">Chưa có NCC</span>') },
    { label: 'Đơn giá', num: true, html: l => (has(l.unitPrice) ? U.money(l.unitPrice) + (l.purchaseUnitName ? `<span class="sub">/${esc(l.purchaseUnitName)}</span>` : '') : '–') },
    { label: 'Ước tính', num: true, html: l => (has(l.estimatedAmount) ? U.money(l.estimatedAmount) : '–') },
    { label: 'Ghi chú', html: l => esc(l.note || '') },
  ]);
  const poBrief = rows => D.table(rows, [
    { label: 'Số PO', html: p => `<b>${link('PurchaseOrder', p.id, p.purchaseOrderNumber)}</b>` },
    { label: 'Nhà cung cấp', html: p => esc(p.supplierName || '') },
    { label: 'Số dòng', num: true, html: p => U.num(p.lineCount || 0) },
    { label: 'Giá trị', num: true, html: p => U.money(p.totalAmount || 0) },
    { label: 'Trạng thái', html: p => badge('PurchaseOrder', p.status) },
  ]);
  D.page({
    id: 'scm-pr-detail', screen: 'scm-pr', table: 'PurchaseRequest', kind: 'doc', api: 'PurchaseRequest/GetOverview', resource: 'PurchaseRequest',
    head: o => {
      const d = o.detail || {}, pr = d.purchaseRequest || {}, acts = d.allowedActions || [], edit = D.can('scm-pr', 'edit');
      return {
        title: 'Đề nghị mua ' + (pr.requestNumber || pr.requestNo || ''), no: pr.requestNumber || pr.requestNo, avatar: 'DN', avatarCls: 'biz',
        tags: [badge('PurchaseRequest', pr.status)],
        meta: [pr.requestDate && 'Ngày ' + D.dmy(pr.requestDate), pr.requiredDate && 'Cần hàng ' + D.dmy(pr.requiredDate), o.requesterName && esc('Người đề nghị: ' + o.requesterName)],
        steps: D.stepsOf(PR_FLOW, pr.status, ['Rejected', 'Cancelled'], s => st('PurchaseRequest', s)),
        buttons: [
          { act: 'convert', icon: 'plus', label: 'Lập PO', ok: edit && acts.includes('convertToPurchaseOrder'), primary: true },
          { act: 'submit', icon: 'approve', label: 'Gửi duyệt', ok: edit && acts.includes('submit'), primary: true },
          { act: 'editLines', icon: 'edit', label: 'Sửa dòng', ok: edit && pr.status === 'Draft' },
        ],
        more: [inbox(d), edit && acts.includes('cancel') ? `<button data-h="cancel">Hủy đề nghị mua</button>` : ''],
      };
    },
    kpis: o => {
      const d = o.detail || {};
      return UI.kpi('Số dòng', U.num(o.lineCount || 0), '', 'clipboard', 'blue')
        + UI.kpi('Giá trị ước tính', U.moneyShort(d.estimatedAmount || 0), 'theo bảng giá NCC', 'calc', 'blue')
        + UI.kpi('NCC gợi ý', U.num(o.supplierCount || 0), o.noSupplierLineCount ? `${U.num(o.noSupplierLineCount)} dòng chưa có giá` : 'đủ giá', 'building', o.noSupplierLineCount ? 'orange' : 'green')
        + UI.kpi('PO đã lập', U.num(o.purchaseOrderCount || 0), '', 'package', o.purchaseOrderCount ? 'green' : 'blue');
    },
    overview: (box, o) => {
      const d = o.detail || {}, pr = d.purchaseRequest || {};
      box.innerHTML = D.split(
        D.block('Thông tin đề nghị', D.kv([['Số đề nghị', esc(pr.requestNumber || pr.requestNo || '')], ['Ngày lập', D.dmy(pr.requestDate)], ['Ngày cần hàng', D.dmy(pr.requiredDate)],
          ['Người đề nghị', esc(o.requesterName || '')], ['Trạng thái', esc(d.statusName || st('PurchaseRequest', pr.status))], ['Giá trị ước tính', U.money(d.estimatedAmount || 0)], ['Ghi chú', esc(pr.note || '')]])),
        D.block('Dòng đề nghị', (d.listLine || []).length ? prLines(d.listLine.slice(0, 8)) : D.none)
        + ((d.listPurchaseOrder || []).length ? D.block('Đơn mua đã lập', poBrief(d.listPurchaseOrder)) : '')
        + pendingBlock(d.pendingApproval));
    },
    tabs: [
      { label: 'Dòng đề nghị', render: (box, o) => { box.innerHTML = prLines((o.detail || {}).listLine || []); } },
      { label: 'Đơn mua đã lập', render: (box, o) => { const r = (o.detail || {}).listPurchaseOrder || []; box.innerHTML = r.length ? poBrief(r) : '<div class="muted small">Chưa lập đơn mua.</div>'; } },
    ],
    onAct: (act, o, id, reload) => {
      if (act === 'editLines' && APP.prForm) return APP.prForm({ Id: (o.detail || {}).purchaseRequest.id }, { onSave: () => reload && reload() }); // UI-6.18 A: sửa dòng đề nghị Nháp
      if (act === 'inbox') return APP.go('wf-inbox');
      const A = APP.scmPr; if (A && A[act]) A[act](prRef(o.detail || {}));
    },
  });

  // ================= Lô hàng =================
  const TRACE = [['listSupplier', 'Nhà cung cấp'], ['listPurchaseOrder', 'Đơn mua'], ['listGoodsReceipt', 'Phiếu nhập'], ['listLot', 'Lô nguyên liệu'], ['listIssue', 'Phiếu xuất kho'], ['listProductionOrder', 'Lệnh sản xuất'], ['listBatch', 'Mẻ sản xuất'], ['listTemperature', 'Nhiệt độ đã đo'], ['listPackingOrder', 'Lệnh đóng gói'], ['listOrder', 'Đơn hàng'], ['listDeliveryOrder', 'Đơn giao hàng'], ['listCustomer', 'Khách hàng nhận suất ăn']];
  const expiryTag = (days, expired) => (expired ? UI.tag('Đã hết hạn', 'red') : has(days) && days <= 3 ? UI.tag(`Còn ${days} ngày`, 'orange') : '');
  D.page({
    id: 'scm-lot-detail', screen: 'scm-lot', table: 'MaterialLot', kind: 'profile', api: 'MaterialLot/GetOverview', resource: 'MaterialLot',
    head: o => {
      const l = o.lot || {}, canQa = Store.canApprove('scm-lot');
      return {
        title: 'Lô ' + (l.lotNumber || l.lotNo || ''), no: l.lotNumber || l.lotNo, avatar: 'LÔ', avatarCls: 'biz',
        tags: [badge('MaterialLot', l.status), l.qcStatus ? UI.badge(l.qcStatus, 'MaterialLot', 'QcStatus') : '', expiryTag(o.daysToExpiry, o.isExpired)],
        meta: [link('Ingredient', l.ingredientId, o.ingredientName), esc(o.supplierName || ''), l.receivedDate && 'Nhận ' + D.dmy(l.receivedDate), l.expiryDate && 'HSD ' + D.dmy(l.expiryDate)],
        buttons: [
          { act: 'trace', icon: 'link', label: 'Truy xuất', ok: true, primary: true },
          { act: 'hold', icon: 'alert', label: 'Tạm giữ', ok: canQa && l.status === 'Available' },
          { act: 'release', icon: 'check', label: 'Mở tạm giữ', ok: canQa && l.status === 'OnHold' },
        ],
      };
    },
    kpis: o => UI.kpi('Tồn thực', q3(o.onHandQuantity), esc(o.unitName || ''), 'package', 'blue')
      + UI.kpi('Khả dụng', q3(o.availableQuantity), o.reservedQuantity ? 'đã giữ ' + q3(o.reservedQuantity) : '', 'check', o.blockReason ? 'red' : 'green')
      + UI.kpi('Đã xuất', q3(o.issuedQuantity), has(o.acceptedQuantity) ? 'nhập ' + q3(o.acceptedQuantity) : '', 'down', 'blue')
      + UI.kpi('Hạn dùng', has(o.daysToExpiry) ? (o.isExpired ? 'Hết hạn' : U.num(o.daysToExpiry) + ' ngày') : '–', (o.lot || {}).expiryDate ? D.dmy(o.lot.expiryDate) : '', 'calendar', o.isExpired ? 'red' : has(o.daysToExpiry) && o.daysToExpiry <= 3 ? 'orange' : 'green'),
    overview: (box, o) => {
      const l = o.lot || {};
      box.innerHTML = (o.blockReason ? `<div class="notice warn">${I('alert')}<div><b>Không xuất được</b>${esc(o.blockReason)}</div></div>` : '') + D.split(
        D.block('Thông tin lô', D.kv([['Số lô', esc(l.lotNumber || l.lotNo || '')], ['Nguyên liệu', link('Ingredient', l.ingredientId, [o.ingredientCode, o.ingredientName].filter(Boolean).join(' – '))],
          ['Nhà cung cấp', esc(o.supplierName || '')], ['Phiếu nhập', o.goodsReceiptId ? link('GoodsReceipt', o.goodsReceiptId, o.goodsReceiptNumber) : ''],
          ['Đơn mua', o.purchaseOrderId ? link('PurchaseOrder', o.purchaseOrderId, o.purchaseOrderNumber) : ''], ['Ngày sản xuất', D.dmy(l.manufactureDate)], ['Ngày nhận', D.dmy(l.receivedDate)],
          ['Hạn dùng', D.dmy(l.expiryDate)], ['Đơn giá nhập', has(l.unitCost) ? U.money(l.unitCost) : ''], ['Số nhận / đạt', has(o.receivedQuantity) ? `${q3(o.receivedQuantity)} / ${q3(o.acceptedQuantity)} ${esc(o.unitName || '')}` : ''],
          ['Trạng thái', esc(o.statusName || st('MaterialLot', l.status))], ['Ghi chú', esc(l.note || '')]])),
        D.block('Tồn theo kho', balTable(o.listBalance || []))
        + D.block('Giao dịch gần nhất', txTable((o.listTransaction || []).slice(0, 6))));
    },
    tabs: [
      { label: 'Giao dịch kho', render: (box, o) => { const t = o.listTransaction || []; box.innerHTML = t.length ? txTable(t) : '<div class="muted small">Lô chưa có giao dịch kho.</div>'; } },
      { label: 'Truy xuất', render: (box, o, id) => D.run(box, Store.query('MaterialLot/Trace', { id }), x => {
        const d = x.data || {};
        const node = n => `${esc(n.number || n.name || '')}${n.number && n.name ? ' – ' + esc(n.name) : ''}${has(n.quantity) ? ' · ' + U.num(n.quantity, 2) : ''}${n.date ? ' · ' + U.date(n.date) : ''}${n.inferred ? ' <span class="badge orange" title="Suy ra theo ngày và ca sản xuất">suy ra</span>' : ''}`;
        box.innerHTML = warns(d.listWarning) + `<div class="timeline">${TRACE.map(([k, lb]) => { const items = d[k] || []; return `<div class="tl ${items.length ? '' : 'orange'}"><b>${lb} <span class="muted small">${items.length || ''}</span></b><small>${items.length ? items.slice(0, 12).map(node).join('<br>') + (items.length > 12 ? `<br>… và ${items.length - 12} dòng khác` : '') : 'Không có dữ liệu'}</small></div>`; }).join('')}</div>`;
      }) },
    ],
    onAct: (act, o, id) => {
      const l = o.lot || {}, A = APP.scmLot; if (!A) return;
      if (act === 'trace') A.trace(id);
      else if (A[act]) A[act]({ Id: id, LotNumber: l.lotNumber || l.lotNo });
    },
  });

  // ================= Nguyên liệu (khuôn hồ sơ) =================
  D.page({
    id: 'scm-ingredient-detail', screen: 'scm-ingredient', table: 'Ingredient', kind: 'profile', api: 'Ingredient/GetOverview', resource: 'Ingredient',
    head: o => {
      const g = o.ingredient || {};
      return {
        title: g.name || '', no: g.code, avatarCls: 'ind',
        tags: [g.status ? UI.badge(g.status, 'Ingredient', 'Status') : '', o.belowMinimum ? UI.tag('Dưới tồn tối thiểu', 'red') : '', o.expiringLotCount ? UI.tag(`${o.expiringLotCount} lô sắp hết hạn`, 'orange') : ''],
        meta: [esc(g.code || ''), esc(o.categoryName || ''), o.baseUnitName && esc('ĐVT ' + o.baseUnitName), g.storageType && esc(UI.statusVi('Ingredient', 'StorageType', g.storageType) || g.storageType), o.preferredSupplierName && esc('NCC ưu tiên: ' + o.preferredSupplierName)],
        buttons: [
          { act: 'edit', icon: 'edit', label: 'Sửa', ok: D.can('scm-ingredient', 'edit'), primary: true },
          { act: 'lots', icon: 'lot', label: 'Danh sách lô', ok: D.can('scm-lot') },
        ],
      };
    },
    kpis: o => UI.kpi('Tồn khả dụng', q3(o.availableQuantity), (has(o.minimumStock) ? 'tối thiểu ' + q3(o.minimumStock) : 'tồn thực ' + q3(o.onHandQuantity)) + ' ' + esc(o.baseUnitName || ''), 'package', o.belowMinimum ? 'red' : 'green')
      + UI.kpi('Lô còn tồn', U.num(o.activeLotCount || 0), o.expiringLotCount ? `${U.num(o.expiringLotCount)} lô hết hạn ≤ ${o.expiryWarningDays} ngày` : '', 'lot', o.expiringLotCount ? 'orange' : 'blue')
      + UI.kpi('Giá mua gần nhất', has(o.lastPurchasePrice) ? U.moneyShort(o.lastPurchasePrice) : '–', o.lastPurchaseDate ? D.dmy(o.lastPurchaseDate) + (o.lastPurchaseUnitName ? ' /' + esc(o.lastPurchaseUnitName) : '') : 'chưa mua', 'calc', 'blue')
      + UI.kpi('Nhà cung cấp', U.num(o.supplierCount || 0), 'bảng giá đang hiệu lực', 'building', o.supplierCount ? 'green' : 'orange'),
    overview: (box, o) => {
      const g = o.ingredient || {};
      const lots = (o.listLot || []).filter(l => l.onHandQuantity > 0).slice(0, 8);
      box.innerHTML = D.split(
        D.block('Thông tin nguyên liệu', D.kv([['Mã', esc(g.code || '')], ['Tên', esc(g.name || '')], ['Nhóm', esc(o.categoryName || '')], ['Đơn vị tính', esc(o.baseUnitName || '')],
          ['Quy cách', esc(g.specification || '')], ['Bảo quản', esc([g.storageType && (UI.statusVi('Ingredient', 'StorageType', g.storageType) || g.storageType), g.storageCondition].filter(Boolean).join(' – '))],
          ['Hạn dùng', has(g.shelfLifeDays) ? U.num(g.shelfLifeDays) + ' ngày' : ''], ['Tồn tối thiểu', has(o.minimumStock) ? q3(o.minimumStock) : ''], ['Tồn an toàn', has(o.safetyStock) ? q3(o.safetyStock) : ''],
          ['Giá mua gần nhất', has(o.lastPurchasePrice) ? `${U.money(o.lastPurchasePrice)} · ${esc(o.lastPurchaseSupplierName || '')} · ${esc(o.lastPurchaseOrderNumber || '')}` : ''], ['Ghi chú', esc(g.note || '')]])),
        D.block('Tồn theo kho', balTable(o.listWarehouse || []))
        + D.block('Lô còn tồn (FEFO)', lots.length ? lotTable(lots) : '<div class="muted small">Không còn lô tồn kho.</div>'));
    },
    tabs: [
      { label: 'Lô', screen: 'scm-lot', render: (box, o) => { box.innerHTML = lotTable(o.listLot || []); } },
      { label: 'Giao dịch kho', screen: 'scm-inv-txn', render: (box, o) => { const t = o.listTransaction || []; box.innerHTML = t.length ? txTable(t) : '<div class="muted small">Chưa có giao dịch kho.</div>'; } },
      { label: 'Giá & nhà cung cấp', render: (box, o) => {
        box.innerHTML = D.block('Bảng giá NCC', D.table(o.listSupplierPrice || [], [
          { label: 'Nhà cung cấp', html: p => esc(p.supplierName || '') + (p.isPreferred ? ' <span class="tag green">Ưu tiên</span>' : '') },
          { label: 'Đơn giá', num: true, html: p => (has(p.unitPrice) ? U.money(p.unitPrice) : '–') + (p.purchaseUnitName ? `<span class="sub">/${esc(p.purchaseUnitName)}</span>` : '') },
          { label: 'Đặt tối thiểu', num: true, html: p => (has(p.minimumOrderQuantity) ? q3(p.minimumOrderQuantity) : '') },
          { label: 'Giao (ngày)', num: true, html: p => (has(p.leadTimeDays) ? U.num(p.leadTimeDays) : '') },
          { label: 'Hiệu lực', html: p => `${D.dmy(p.effectiveFrom) || '…'} – ${D.dmy(p.effectiveTo) || '…'}` },
          { label: '', html: p => (p.isEffective ? '<span class="tag green">Đang áp dụng</span>' : '<span class="tag">Hết hiệu lực</span>') },
        ])) + D.block('Lịch sử giá mua (dòng đơn mua)', D.table(o.listPriceHistory || [], [
          { label: 'Ngày đặt', html: h => D.dmy(h.orderDate) },
          { label: 'Số PO', html: h => `<b>${link('PurchaseOrder', h.purchaseOrderId, h.purchaseOrderNumber)}</b>` },
          { label: 'Nhà cung cấp', html: h => esc(h.supplierName || '') },
          { label: 'Số lượng', num: true, html: h => q3(h.quantity) + ' ' + esc(h.unitName || '') },
          { label: 'Đơn giá', num: true, html: h => U.money(h.unitPrice) },
          { label: 'Trạng thái', html: h => badge('PurchaseOrder', h.status) },
        ]));
      } },
    ],
    onAct: (act, o, id, reload) => {
      // UI-6.20 (D-101): Sửa = form Nguyên liệu mới (đọc bản ghi đầy đủ qua Ingredient/GetById).
      if (act === 'edit') return Store.query('Ingredient/GetById', { id }).then(x => { if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không tải được nguyên liệu'), 'err'); const row = {}; Object.keys(x.data).forEach(k => { row[k.charAt(0).toUpperCase() + k.slice(1)] = x.data[k]; }); APP.ingredientForm(row, { onSave: reload }); });
      else if (act === 'lots') APP.go('scm-lot');
    },
  });
  function lotTable(rows) {
    return D.table(rows, [
      { label: 'Số lô', html: l => `<b>${link('MaterialLot', l.id, l.lotNumber)}</b>` },
      { label: 'NCC', html: l => esc(l.supplierName || '') },
      { label: 'Ngày nhận', html: l => D.dmy(l.receivedDate) },
      { label: 'HSD', html: l => `<span class="${has(l.daysToExpiry) && l.daysToExpiry <= 3 && l.onHandQuantity > 0 ? 'bad' : ''}">${D.dmy(l.expiryDate) || '–'}</span>` },
      { label: 'Tồn', num: true, html: l => q3(l.onHandQuantity) },
      { label: 'Khả dụng', num: true, html: l => q3(l.availableQuantity) },
      { label: 'QC', html: l => (l.qcStatus ? UI.badge(l.qcStatus, 'MaterialLot', 'QcStatus') : '') },
      { label: 'Trạng thái', html: l => badge('MaterialLot', l.status) + (l.isBlocked && l.onHandQuantity > 0 ? ' <span class="tag red">Chặn xuất</span>' : '') },
    ]);
  }
})();
