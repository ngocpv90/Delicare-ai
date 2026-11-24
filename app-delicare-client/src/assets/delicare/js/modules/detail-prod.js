/* UI-6 (D-80) nhóm C – trang chi tiết toàn màn SẢN XUẤT / THỰC ĐƠN / GIAO HÀNG (khung APP.D360.page – modules/detail-page.js, mẫu detail-stockcount.js):
 *   Mẻ nấu      #/prod-batch-detail?id=   ProductionBatch/GetOverview   (thao tác: production.js – APP.prodBatch)
 *   Lệnh SX     #/prod-order-detail?id=   ProductionOrder/GetOverview   (APP.prodOrder)
 *   Kế hoạch SX #/prod-plan-detail?id=    ProductionPlan/GetOverview    (APP.prodPlan; tab Duyệt khi kế hoạch giữ Nháp chờ COO – D-81 g)
 *   Món ăn      #/menu-dish-detail?id=    Dish/GetOverview              (hồ sơ: giá vốn, định mức, menu dùng món, lịch sử nấu)
 *   Chuyến giao #/log-route-detail?id=    DeliveryRoute/GetOverview     (logistics.js – APP.logRoute)
 * Nút phần đầu gọi lại đúng thao tác đang có ở danh sách (máy chủ vẫn kiểm quyền / trạng thái). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, D = APP.D360;
  const nil = v => v === null || v === undefined;
  const qty = v => (nil(v) ? '–' : U.num(v));
  // Liên kết sang trang chi tiết khác (chưa có trang / DEMO → chữ thường).
  const lnk = (t, id, text) => { const h = id && APP.detailHref && APP.detailHref(t, id); return h ? `<a href="${h}">${esc(text || '')}</a>` : esc(text || ''); };
  const notes = (list, kind) => ((list || []).length ? `<div class="notice ${kind || 'warn'} mb">${I('alert')}<div>${list.map(esc).join('<br>')}</div></div>` : '');
  const has = (d, a) => (d.allowedActions || []).includes(a);
  // Bảng có dòng bấm được → mở trang chi tiết của bảng t (Ctrl / chuột giữa: tab mới – APP.openDetail).
  const rowsTo = (box, rows, t) => D.bindRows(box, rows, r => APP.openDetail(t, r.id));
  const st = (t, col) => s => UI.statusVi(t, col, s) || s;

  // ================= MẺ NẤU =================
  const BFLOW = ['Planned', 'Cooking', 'QCPending', 'Passed', 'Packed'];
  const tempTable = list => D.table(list || [], [
    { label: 'Thời điểm', html: t => esc(U.dt(t.measuredAt)) },
    { label: 'Nhiệt độ lõi', num: true, html: t => U.num(t.temperatureC, 1) + ' °C' },
    { label: 'Kết quả', html: t => (t.isWithinRange ? UI.tag('Đạt', 'green') : UI.tag('Không đạt', 'red')) },
    { label: 'Khắc phục', html: t => esc(t.correctiveAction || '') },
  ]);
  D.page({
    id: 'prod-batch-detail', screen: 'prod-batch', table: 'ProductionBatche', kind: 'doc', api: 'ProductionBatch/GetOverview', resource: 'ProductionBatch',
    head: o => {
      const d = o.detail || {}, b = d.batch || {}, s = b.status, edit = D.can('prod-batch', 'edit'), qc = Store.canApprove('prod-batch');
      return {
        title: 'Mẻ ' + (b.batchNumber || ''), no: b.batchNumber, avatar: 'ME', avatarCls: 'biz',
        tags: [s ? UI.badge(s, 'ProductionBatche', 'Status') : '', b.qcStatus ? UI.badge(b.qcStatus, 'ProductionBatche', 'QcStatus') : '', b.priorityLabel ? UI.tag(b.priorityLabel + ' – ưu tiên', 'red') : ''],
        meta: [esc(b.dishName || ''), o.productionDate && 'Ngày SX ' + D.dmy(o.productionDate), esc(o.shiftName || b.shiftCode || ''), b.productionLineName && esc(b.productionLineName), U.num(b.actualQuantity || b.plannedQuantity || 0) + ' suất'],
        steps: D.stepsOf(BFLOW, s, ['Failed', 'Cancelled'], st('ProductionBatche', 'Status')),
        buttons: [
          { act: 'cook', icon: 'play', label: 'Bắt đầu nấu', ok: edit && s === 'Planned' && has(d, 'start'), primary: true },
          { act: 'finish', icon: 'check', label: 'Kết thúc nấu → Chờ QC', ok: edit && s === 'Cooking' && has(d, 'finish'), primary: true },
          { act: 'pass', icon: 'approve', label: 'QC: Đạt', ok: qc && s === 'QCPending', primary: true },
          { act: 'pack', icon: 'package', label: 'Tạo lệnh đóng gói', ok: edit && s === 'Passed' && has(d, 'pack'), primary: true },
          { act: 'output', icon: 'edit', label: 'Ghi sản lượng', ok: edit && s === 'QCPending' && !d.output },
          { act: 'sample', icon: 'check', label: 'Ghi lưu mẫu', ok: edit && has(d, 'recordSampleRetained') && !b.sampleRetained },
          { act: 'hold', icon: 'alert', label: 'Tạm giữ', ok: qc && s === 'QCPending' },
          { act: 'reject', icon: 'trash', label: 'Loại mẻ', ok: qc && ['QCPending', 'Passed'].includes(s) },
        ],
        // Thao tác phụ trong menu ⋯ (phần đầu gọn – D-80 d).
        more: [edit && ['Cooking', 'QCPending', 'Passed', 'Packed', 'Failed'].includes(s) ? `<button data-h="waste">Ghi hao hụt</button>` : '', `<button data-h="sheet">In phiếu sản xuất</button>`,
          b.productionOrderId ? `<button data-h="order">Mở lệnh sản xuất</button>` : ''],
      };
    },
    kpis: o => {
      const d = o.detail || {}, b = d.batch || {}, out = d.output;
      return UI.kpi('Sản lượng', `${qty(b.actualQuantity)} / ${U.num(b.plannedQuantity || 0)}`, out ? `đạt ${U.num(out.acceptedQuantity || 0)}` : 'suất', 'package', 'blue')
        + UI.kpi('Nhiệt độ lõi', nil(b.lastCoreTemperatureC) ? '–' : U.num(b.lastCoreTemperatureC, 1) + ' °C', `ngưỡng ≥ ${U.num(d.coreTempMinC || 0, 1)}`, 'alert', nil(b.lastCoreTemperatureC) ? 'orange' : b.coreTemperatureOk ? 'green' : 'red')
        + UI.kpi('Công đoạn', o.stageCount ? `${U.num(o.stageDoneCount || 0)} / ${U.num(o.stageCount)}` : '–', o.stageCount ? 'đã xong' : 'không theo dõi', 'flow', o.stageCount && o.stageDoneCount === o.stageCount ? 'green' : 'blue')
        + UI.kpi('Hao hụt', U.num(o.wasteMeals || 0) + ' suất', '', 'trash', o.wasteMeals ? 'orange' : 'green');
    },
    overview: (box, o) => {
      const d = o.detail || {}, b = d.batch || {}, ord = d.order || {}, out = d.output;
      box.innerHTML = D.split(
        D.block('Thông tin mẻ', D.kv([['Số mẻ', esc(b.batchNumber || '')], ['Món', esc(b.dishName || '')], ['Lệnh sản xuất', lnk('ProductionOrder', b.productionOrderId, b.productionOrderNumber)],
          ['Kế hoạch', lnk('ProductionPlan', o.productionPlanId, o.planNumber)], ['Ngày sản xuất', D.dmy(o.productionDate)], ['Ca', esc(o.shiftName || b.shiftCode || '')],
          ['Dây chuyền', esc(b.productionLineName || '')], ['Công thức (khóa)', esc(o.recipeVersion || ord.recipeVersion || '')], ['Phụ trách lệnh', esc(o.responsibleName || '')],
          ['Giờ kế hoạch', esc([U.dt(b.plannedStart), b.plannedEnd && U.time(b.plannedEnd)].filter(Boolean).join(' – '))],
          ['Giờ thực tế', esc([U.dt(b.actualStart), b.actualEnd && U.time(b.actualEnd)].filter(Boolean).join(' – '))],
          ['Trạng thái', esc(b.statusName || st('ProductionBatche', 'Status')(b.status))]])),
        notes(d.listBlocker, 'gap') + notes(d.listWarning)
        + D.block('An toàn thực phẩm', D.kv([['Nhiệt độ lõi gần nhất', nil(b.lastCoreTemperatureC) ? '<span class="muted">Chưa đo</span>' : `${U.num(b.lastCoreTemperatureC, 1)} °C ${b.coreTemperatureOk ? UI.tag('Đạt', 'green') : UI.tag('Không đạt', 'red')}`],
          ['Số lần đo', `${U.num(o.temperatureCount || 0)}${o.temperatureFailCount ? ` · <span class="bad">${U.num(o.temperatureFailCount)} lần không đạt</span>` : ''}`],
          ['Lưu mẫu thức ăn', b.sampleRetained ? 'Đã lưu mẫu' : '<span class="muted">Chưa lưu mẫu</span>'], ['Xuất nguyên liệu', b.materialIssued ? 'Đã xuất kho' : '<span class="muted">Chưa xuất kho</span>'],
          ['Sản lượng', out ? `${U.num(out.producedQuantity)} phần · đạt ${U.num(out.acceptedQuantity || 0)} · loại ${U.num(out.rejectedQuantity || 0)}` : '<span class="muted">Chưa ghi</span>']]))
        + D.block('Nguyên liệu theo định lượng', D.table(d.listMaterial || [], [{ label: 'Nguyên liệu', html: m => esc(m.ingredientName || m.ingredientCode || '') },
          { label: 'Cần', num: true, html: m => U.num(m.requiredQuantity, 3) + ' ' + esc(m.recipeUnitName || '') }, { label: 'Đã xuất', num: true, html: m => U.num(m.issuedQuantity, 3) + ' ' + esc(m.issuedUnitName || '') }])));
    },
    tabs: [
      { label: 'QC & an toàn', render: (box, o) => {
        const d = o.detail || {};
        box.innerHTML = `${notes(d.listBlocker, 'gap')}<h3>Nhiệt độ lõi</h3>${tempTable(d.listTemperature)}
          <h3 class="mt">Lưu mẫu</h3>${D.table(d.listSample || [], [{ label: 'Thời điểm', html: x => esc(U.dt(x.checkedAt)) }, { label: 'Mô tả mẫu', html: x => esc(x.actualValue || '') }])}`;
      } },
      { label: 'Công đoạn', render: (box, o, id) => { if (APP.prodBatch) APP.prodBatch.stagePane(box, id); else box.innerHTML = D.none; } },
      { label: 'Nguyên liệu & xuất kho', render: (box, o) => { box.innerHTML = APP.prodBatch ? APP.prodBatch.detailHtml(o.detail || {}) : D.none; } },
    ],
    onAct: (act, o, id) => {
      const b = (o.detail || {}).batch || {};
      if (act === 'sheet') return Store.download('ProductionBatch/ExportProductionSheet', { id });
      if (act === 'order') return APP.openDetail('ProductionOrder', b.productionOrderId);
      // Dòng tối thiểu cho hộp nhập khi bảng mẻ chưa nạp (sản lượng mặc định).
      if (APP.prodBatch) APP.prodBatch.act(act, id, { Id: id, Status: b.status, PlannedQuantity: b.plannedQuantity, ActualQuantity: b.actualQuantity, ProductionOrderId: b.productionOrderId, DishId: b.dishId });
    },
  });

  // ================= LỆNH SẢN XUẤT =================
  const OFLOW = ['Planned', 'Released', 'InProgress', 'Completed', 'Closed'];
  const batchCols = [
    { label: 'Số mẻ', html: b => `<b>${esc(b.batchNumber || '')}</b>${b.priorityLabel ? ` <span class="small bad">${esc(b.priorityLabel)}</span>` : ''}` },
    { label: 'Món', html: b => esc(b.dishName || '') },
    { label: 'Kế hoạch', num: true, html: b => U.num(b.plannedQuantity) },
    { label: 'Thực tế', num: true, html: b => qty(b.actualQuantity) },
    { label: 'Bắt đầu', html: b => esc(U.dt(b.actualStart || b.plannedStart)) },
    { label: 'Trạng thái', html: b => UI.badge(b.status, 'ProductionBatche', 'Status') + (b.qcStatus ? ' ' + UI.badge(b.qcStatus, 'ProductionBatche', 'QcStatus') : '') },
  ];
  D.page({
    id: 'prod-order-detail', screen: 'prod-order', table: 'ProductionOrder', kind: 'doc', api: 'ProductionOrder/GetOverview', resource: 'ProductionOrder',
    head: o => {
      const d = o.detail || {}, po = d.productionOrder || {}, br = d.order || {}, edit = D.can('prod-order', 'edit');
      return {
        title: 'Lệnh SX ' + (po.productionOrderNumber || ''), no: po.productionOrderNumber, avatar: 'LSX', avatarCls: 'biz',
        tags: [po.status ? UI.badge(po.status, 'ProductionOrder', 'Status') : ''],
        meta: [esc(br.dishName || ''), o.productionDate && 'Ngày SX ' + D.dmy(o.productionDate), esc(o.shiftName || br.shiftCode || ''), o.productionLineName && esc(o.productionLineName), U.num(po.plannedQty || 0) + ' suất'],
        steps: D.stepsOf(OFLOW, po.status, ['Cancelled'], st('ProductionOrder', 'Status')),
        buttons: [
          { act: 'close', icon: 'check', label: 'Đóng lệnh', ok: edit && has(d, 'close'), primary: true },
          { act: 'cost', icon: 'calc', label: 'Tính giá thành', ok: edit && has(d, 'calculateCost') },
          { act: 'cancel', icon: 'trash', label: 'Hủy lệnh', ok: edit && has(d, 'cancel') },
        ],
        more: [po.productionPlanId ? `<button data-h="plan">Mở kế hoạch sản xuất</button>` : ''],
      };
    },
    kpis: o => {
      const d = o.detail || {}, po = d.productionOrder || {}, c = d.actualCost;
      return UI.kpi('Mẻ đạt', `${U.num(o.batchDoneCount || 0)} / ${U.num(o.batchCount || 0)}`, o.batchFailedCount ? `· ${U.num(o.batchFailedCount)} loại` : '', 'chef', o.batchFailedCount ? 'orange' : 'green')
        + UI.kpi('Suất đạt', `${U.num(o.acceptedPortions || 0)} / ${U.num(po.plannedQty || 0)}`, '', 'package', 'blue')
        + UI.kpi('Giữ chỗ thiếu', U.num(o.shortReservationCount || 0), 'dòng nguyên liệu', 'alert', o.shortReservationCount ? 'red' : 'green')
        + UI.kpi('Giá thành / phần', c && !nil(c.costPerPortion) ? U.moneyShort(c.costPerPortion) : '–', c && !nil(c.standardCostPerPortion) ? 'chuẩn ' + U.money(c.standardCostPerPortion) : 'chưa tính', 'calc', c && !nil(c.costPerPortion) && !nil(c.standardCostPerPortion) && c.costPerPortion > c.standardCostPerPortion ? 'red' : 'blue');
    },
    overview: (box, o) => {
      const d = o.detail || {}, po = d.productionOrder || {}, br = d.order || {}, rows = d.listBatch || [];
      box.innerHTML = D.split(
        D.block('Thông tin lệnh', D.kv([['Số lệnh', esc(po.productionOrderNumber || '')], ['Món', esc(br.dishName || '')], ['Công thức (khóa lúc phát hành)', esc(br.recipeVersion || '')],
          ['Kế hoạch', lnk('ProductionPlan', po.productionPlanId, o.planNumber)], ['Ngày sản xuất', D.dmy(o.productionDate)], ['Ca', esc(o.shiftName || br.shiftCode || '')],
          ['Dây chuyền', esc(o.productionLineName || '')], ['Phụ trách', esc(o.responsibleName || '')], ['Số suất kế hoạch', U.num(po.plannedQty || 0)],
          ['Bắt đầu kế hoạch', esc(U.dt(po.plannedStartDate))], ['Thực tế', esc([U.dt(po.actualStartDate), po.actualEndDate && U.dt(po.actualEndDate)].filter(Boolean).join(' – '))],
          ['Trạng thái', esc(br.statusName || st('ProductionOrder', 'Status')(po.status))], ['Ghi chú', esc(po.note || '')]])),
        notes(d.listWarning) + D.block('Mẻ sản xuất', D.table(rows, batchCols, true))
        + (o.varianceOverCount ? D.block('Chênh lệch tiêu hao vượt ngưỡng', `<div class="small bad">${U.num(o.varianceOverCount)} nguyên liệu vượt ngưỡng – xem tab Giá thành & chênh lệch.</div>`) : ''));
      rowsTo(box, rows, 'ProductionBatche');
    },
    tabs: [
      { label: 'Mẻ & nguyên liệu', render: (box, o) => { box.innerHTML = APP.prodOrder ? APP.prodOrder.workHtml(o.detail || {}) : D.none; } },
      { label: 'Giá thành & chênh lệch', render: (box, o) => { box.innerHTML = APP.prodOrder ? APP.prodOrder.costHtml(o.detail || {}) : D.none; } },
    ],
    onAct: (act, o, id) => {
      const po = (o.detail || {}).productionOrder || {};
      if (act === 'plan') return APP.openDetail('ProductionPlan', po.productionPlanId);
      if (APP.prodOrder) APP.prodOrder.act(act, { Id: id, ProductionOrderNumber: po.productionOrderNumber, Status: po.status }, () => APP.refresh());
    },
  });

  // ================= KẾ HOẠCH SẢN XUẤT =================
  const PFLOW = ['Draft', 'Approved', 'Released', 'Completed'];
  D.page({
    id: 'prod-plan-detail', screen: 'prod-plan', table: 'ProductionPlan', kind: 'doc', api: 'ProductionPlan/GetOverview', resource: 'ProductionPlan',
    head: o => {
      const d = o.detail || {}, p = d.productionPlan || {}, no = p.planNumber || p.planNo || '';
      return {
        title: 'Kế hoạch ' + no, no, avatar: 'KH', avatarCls: 'biz',
        tags: [p.status ? UI.badge(p.status, 'ProductionPlan', 'Status') : '', o.heldReason ? UI.tag('Chờ COO duyệt', 'orange') : ''],
        meta: ['Ngày SX ' + D.dmy(p.productionDate), o.organizationUnitName && esc(o.organizationUnitName), o.shiftName && esc(o.shiftName), U.num(d.totalPortions || 0) + ' suất · ' + U.num(o.dishCount || 0) + ' món'],
        steps: D.stepsOf(PFLOW, p.status, ['Cancelled'], st('ProductionPlan', 'Status')),
        buttons: [
          { act: 'approve', icon: 'approve', label: 'Duyệt kế hoạch', ok: has(d, 'approve') && Store.canApprove('prod-plan'), primary: true },
          { act: 'release', icon: 'flow', label: 'Ra lệnh SX', ok: has(d, 'release') && D.can('prod-plan', 'edit'), primary: true },
          { act: 'sheet', icon: 'export', label: 'Phiếu sản xuất', ok: has(d, 'exportProductionSheet') },
        ],
        more: [o.approvalRequestId ? `<button data-h="inbox">Mở màn Phê duyệt</button>` : ''],
      };
    },
    kpis: o => {
      const d = o.detail || {}, sh = d.listShift || [], rel = sh.filter(s => s.released).length;
      return UI.kpi('Tổng suất', U.num(d.totalPortions || 0), U.num(o.dishCount || 0) + ' món', 'utensils', 'blue')
        + UI.kpi('Ca đã phát hành', `${U.num(rel)} / ${U.num(sh.length)}`, '', 'calendar', sh.length && rel === sh.length ? 'green' : 'orange')
        + UI.kpi('Lệnh SX', U.num(o.orderCount || 0), `${U.num(o.batchDoneCount || 0)}/${U.num(o.batchCount || 0)} mẻ đạt`, 'clipboard', 'blue')
        + UI.kpi('Giữ chỗ thiếu', U.num(o.shortReservationCount || 0), 'dòng nguyên liệu', 'alert', o.shortReservationCount ? 'red' : 'green');
    },
    overview: (box, o) => {
      const d = o.detail || {}, p = d.productionPlan || {};
      box.innerHTML = D.split(
        D.block('Thông tin kế hoạch', D.kv([['Số kế hoạch', esc(p.planNumber || p.planNo || '')], ['Ngày sản xuất', D.dmy(p.productionDate)], ['Ngày lập', D.dmy(p.planDate)],
          ['Đơn vị', esc(o.organizationUnitName || '')], ['Ca', esc(o.shiftName || '')], ['Trạng thái', esc(d.statusName || st('ProductionPlan', 'Status')(p.status))],
          ['Giờ xuất bếp muộn nhất', d.dispatchAt ? esc(U.dt(d.dispatchAt)) : ''], ['Chờ duyệt', o.heldReason ? `<span class="bad">${esc(o.heldReason)}</span>` : '']])),
        notes(d.listWarning)
        + D.block('Theo ca', D.table(d.listShift || [], [{ label: 'Ca', html: s => `<b>${esc(s.shiftName || s.shiftCode || '')}</b>` }, { label: 'Suất', num: true, html: s => U.num(s.portions) },
          { label: 'Phát hành', html: s => (s.released ? UI.tag('Đã phát hành', 'green') : UI.tag('Chưa', 'gray')) }, { label: 'Lệnh / mẻ', num: true, html: s => `${U.num(s.orderCount)} / ${U.num(s.batchCount)}` },
          { label: 'Xuất bếp', html: s => esc(U.dt(s.dispatchAt)) }]))
        + D.block('Món & suất', D.table(d.listLine || [], [{ label: 'Món', html: l => `<b>${esc(l.dishName || '')}</b><span class="sub">${esc(l.dishCode || '')}</span>` },
          { label: 'Suất', num: true, html: l => U.num(l.plannedQuantity) }, { label: 'Theo ca', html: l => esc((l.listShift || []).map(x => `${x.shiftCode}: ${U.num(x.quantity)}`).join(' · ')) }])));
    },
    tabs: [
      { label: 'Lệnh sản xuất', render: (box, o) => {
        const rows = (o.detail || {}).listOrder || [];
        box.innerHTML = D.table(rows, [{ label: 'Số lệnh', html: r => `<b>${esc(r.productionOrderNumber || '')}</b>` }, { label: 'Món', html: r => esc(r.dishName || '') }, { label: 'Ca', html: r => esc(r.shiftCode || '') },
          { label: 'Suất', num: true, html: r => U.num(r.plannedQuantity) }, { label: 'Mẻ', num: true, html: r => U.num(r.batchCount) }, { label: 'Trạng thái', html: r => UI.badge(r.status, 'ProductionOrder', 'Status') }], true);
        rowsTo(box, rows, 'ProductionOrder');
      } },
      { label: 'Mẻ', render: (box, o) => { const rows = (o.detail || {}).listBatch || []; box.innerHTML = D.table(rows, batchCols, true); rowsTo(box, rows, 'ProductionBatche'); } },
      { label: 'Giữ chỗ nguyên liệu', render: (box, o) => {
        box.innerHTML = D.table((o.detail || {}).listReservation || [], [{ label: 'Lệnh', html: x => esc(x.productionOrderNumber || '') }, { label: 'Nguyên liệu', html: x => esc(x.ingredientName || x.ingredientCode || '') },
          { label: 'Cần', num: true, html: x => U.num(x.requiredQuantity, 3) + ' ' + esc(x.unitName || '') }, { label: 'Đã giữ', num: true, html: x => U.num(x.reservedQuantity, 3) },
          { label: 'Thiếu', num: true, html: x => (x.shortQuantity > 0 ? `<b class="bad">${U.num(x.shortQuantity, 3)}</b>` : '0') }, { label: 'Trạng thái', html: x => esc(x.statusName || x.status || '') }]);
      } },
    ],
    onAct: (act, o, id) => {
      const p = (o.detail || {}).productionPlan || {}, r = { Id: id, PlanNumber: p.planNumber || p.planNo };
      if (act === 'inbox') return APP.go('wf-inbox');
      if (act === 'sheet') return Store.download('ProductionPlan/ExportProductionSheet', { id });
      if (!APP.prodPlan) return;
      if (act === 'approve') APP.prodPlan.approve(r);
      else if (act === 'release') Store.ensure('ProductionPlanLine').then(() => APP.prodPlan.release(r));
    },
  });

  // ================= MÓN ĂN (hồ sơ) =================
  const costCols = [
    { label: 'Nguyên liệu', html: l => `<b>${esc(l.ingredientName || '')}</b>${l.warning ? `<span class="sub bad">${esc(l.warning)}</span>` : ''}` },
    { label: 'Định lượng tinh', num: true, html: l => U.num(l.quantity, 3) + ' ' + esc(Store.label('UnitMeasure', l.unitId) || '') },
    { label: 'Lượng mua', num: true, html: l => U.num(l.grossQuantity, 3) },
    { label: 'Đơn giá', num: true, html: l => (nil(l.unitPrice) ? '–' : U.money(l.unitPrice)) },
    { label: 'Thành tiền', num: true, html: l => (nil(l.amount) ? '–' : U.money(l.amount)) },
  ];
  D.page({
    id: 'menu-dish-detail', screen: 'menu-dish', table: 'Dishe', kind: 'profile', api: 'Dish/GetOverview', resource: 'Dish',
    head: o => {
      const m = o.dish || {}, r = o.activeRecipe;
      return {
        title: m.name || '', no: m.code, avatarCls: 'biz',
        tags: [m.status ? UI.badge(m.status, 'Dishe', 'Status') : '', o.categoryName ? UI.tag(o.categoryName) : '', r ? '' : UI.tag('Chưa có công thức hiệu lực', 'orange')],
        meta: [esc(m.code || ''), o.unitName && esc(o.unitName), r && esc('Công thức ' + (r.versionCode || 'V' + r.versionNo)), m.mealCategory && esc(m.mealCategory)],
        buttons: [
          { act: 'edit', icon: 'edit', label: 'Sửa', ok: D.can('menu-dish', 'edit'), primary: true },
          { act: 'recipe', icon: 'clipboard', label: 'Công thức', ok: D.can('menu-recipe') },
        ],
      };
    },
    kpis: o => {
      const c = o.cost, std = (o.dish || {}).standardCost;
      return UI.kpi('Giá vốn NVL / suất', c ? U.moneyShort(c.costPerMeal || 0) : '–', !nil(std) ? 'chuẩn ' + U.money(std) : c && !c.complete ? 'thiếu giá' : '', 'calc', !c ? 'orange' : !c.complete ? 'orange' : !nil(std) && c.costPerMeal > std ? 'red' : 'green')
        + UI.kpi('Định mức', U.num(o.ingredientCount || 0), 'nguyên liệu', 'clipboard', o.ingredientCount ? 'blue' : 'orange')
        + UI.kpi('Menu dùng món', U.num(o.menuCount || 0), '', 'calendar', 'blue')
        + UI.kpi('Mẻ đạt QC', nil(o.qcPassPercent) ? '–' : U.num(o.qcPassPercent, 1) + '%', `${U.num(o.batchCount || 0)} mẻ đã nấu`, 'chef', nil(o.qcPassPercent) ? 'blue' : o.qcPassPercent >= 95 ? 'green' : 'orange');
    },
    overview: (box, o) => {
      const m = o.dish || {}, c = o.cost, r = o.activeRecipe;
      box.innerHTML = D.split(
        D.block('Thông tin món', D.kv([['Mã', esc(m.code || '')], ['Tên món', esc(m.name || '')], ['Nhóm món', esc(o.categoryName || '')], ['Đơn vị', esc(o.unitName || '')],
          ['Định lượng chuẩn', nil(m.standardPortion) ? '' : U.num(m.standardPortion, 3)], ['Giá thành chuẩn', nil(m.standardCost) ? '' : U.money(m.standardCost)],
          ['Công thức hiệu lực', r ? `${esc(r.versionCode || 'V' + r.versionNo)} ${UI.badge(r.status, 'Recipe', 'Status')}` : '<span class="muted">Chưa có</span>'],
          ['Hiệu lực từ', r ? D.dmy(r.effectiveFrom) : ''], ['Mô tả', esc(m.description || '')], ['Ghi chú', esc(m.note || '')]])),
        D.block('Định mức nguyên liệu / suất', c ? `${c.complete ? '' : '<div class="notice warn mb">' + I('alert') + '<div>Có dòng thiếu giá nhà cung cấp / thiếu quy đổi đơn vị – giá vốn chưa đủ.</div></div>'}${D.table(c.listLine || [], costCols)}
          <div class="spread mt"><span class="muted small">Tính cho 1 suất (định lượng chuẩn ${U.num(c.standardServings || 0)} suất)</span><b>${U.money(c.costPerMeal || 0)} / suất</b></div>` : '<div class="muted small">Món chưa có công thức hiệu lực.</div>'));
    },
    tabs: [
      { label: 'Định mức', render: (box, o) => {
        box.innerHTML = `<h3>Các phiên bản công thức</h3>${D.table(o.listRecipe || [], [{ label: 'Phiên bản', html: r => `<b>${esc(r.versionCode || 'V' + r.versionNo)}</b>${r.isActive ? ' ' + UI.tag('Đang dùng', 'green') : ''}` },
          { label: 'Trạng thái', html: r => UI.badge(r.status, 'Recipe', 'Status') }, { label: 'Hiệu lực', html: r => esc([D.dmy(r.effectiveFrom), r.effectiveTo && D.dmy(r.effectiveTo)].filter(Boolean).join(' – ')) },
          { label: 'Số nguyên liệu', num: true, html: r => U.num(r.lineCount) }])}
          <h3 class="mt">Định mức của công thức đang dùng</h3>${o.cost ? D.table(o.cost.listLine || [], costCols) : D.none}`;
      } },
      { label: 'Menu', render: (box, o) => {
        box.innerHTML = D.table(o.listMenu || [], [{ label: 'Thực đơn', html: m => `<b>${esc(m.name || '')}</b><span class="sub">${esc(m.code || '')}</span>` }, { label: 'Trạng thái', html: m => UI.badge(m.status, 'Menu', 'Status') },
          { label: 'Hiệu lực', html: m => esc([D.dmy(m.effectiveFrom), m.effectiveTo && D.dmy(m.effectiveTo)].filter(Boolean).join(' – ')) }, { label: 'Bữa', html: m => esc((m.listMealType || []).join(', ')) },
          { label: 'Số lần có món', num: true, html: m => U.num(m.lineCount) }]);
      } },
      { label: 'Lịch sử nấu', render: (box, o) => {
        const rows = o.listBatch || [];
        box.innerHTML = `${o.batchCount > rows.length ? `<div class="muted small mb">${U.num(rows.length)} mẻ gần nhất / ${U.num(o.batchCount)} mẻ</div>` : ''}${D.table(rows, [{ label: 'Số mẻ', html: b => `<b>${esc(b.batchNumber || '')}</b>` },
          { label: 'Lệnh SX', html: b => esc(b.productionOrderNumber || '') }, { label: 'Ngày', html: b => esc(U.dt(b.startDate)) }, { label: 'Kế hoạch', num: true, html: b => qty(b.plannedQuantity) },
          { label: 'Đạt', num: true, html: b => qty(b.acceptedQuantity) }, { label: 'Trạng thái', html: b => UI.badge(b.status, 'ProductionBatche', 'Status') + (b.qcStatus ? ' ' + UI.badge(b.qcStatus, 'ProductionBatche', 'QcStatus') : '') }], true)}`;
        rowsTo(box, rows, 'ProductionBatche');
      } },
    ],
    onAct: (act, o, id, reload) => {
      if (act === 'recipe') return APP.go('menu-recipe');
      if (act === 'edit') Store.ensure('Dishe').then(() => { const r = Store.get('Dishe', id); if (!r) return UI.toast('Không tải được món ăn', 'err'); UI.form('Dishe', r, { title: 'Món ăn', onSave: reload }); });
    },
  });

  // ================= CHUYẾN GIAO =================
  const RFLOW = ['Planned', 'Loading', 'InTransit', 'Completed'];
  D.page({
    id: 'log-route-detail', screen: 'log-route', table: 'DeliveryRoute', kind: 'doc', api: 'DeliveryRoute/GetOverview', resource: 'DeliveryRoute',
    head: o => {
      const d = o.detail || {}, r = d.route || {}, s = r.status, edit = D.can('log-route', 'edit');
      return {
        title: 'Chuyến ' + (r.routeNo || ''), no: r.routeNo, avatar: 'CX', avatarCls: 'biz',
        tags: [s ? UI.badge(s, 'DeliveryRoute', 'Status') : '', (d.listBlocker || []).length && ['Planned', 'Loading'].includes(s) ? UI.tag('Chưa đủ điều kiện xuất bến', 'orange') : ''],
        meta: [r.name && esc(r.name), 'Ngày giao ' + D.dmy(r.deliveryDate), (o.shiftNames || []).length && esc('Ca ' + o.shiftNames.join(', ')), esc([d.licensePlate || d.vehicleCode, d.driverName && 'Tài xế ' + d.driverName].filter(Boolean).join(' · '))],
        steps: D.stepsOf(RFLOW, s, [], st('DeliveryRoute', 'Status')),
        buttons: [
          { act: 'load', icon: 'package', label: 'Xếp hàng lên xe', ok: edit && s === 'Planned', primary: true },
          { act: 'dispatch', icon: 'play', label: 'Xuất bến', ok: edit && s === 'Loading', primary: true },
          { act: 'stops', icon: 'edit', label: 'Sắp điểm dừng', ok: edit && s === 'Planned' && (d.listStop || []).length > 0 },
          { act: 'position', icon: 'pin', label: 'Ghi vị trí hiện tại', ok: ['Loading', 'InTransit'].includes(s) },
          { act: 'print', icon: 'export', label: 'In phiếu giao (PDF)', ok: true },
          { act: 'note', icon: 'export', label: 'Biên bản giao hàng', ok: true },
        ],
        more: [D.can('log-execution') ? `<button data-h="exec">Theo dõi giao hàng</button>` : ''],
      };
    },
    kpis: o => {
      const d = o.detail || {};
      return UI.kpi('Điểm đã giao', `${U.num(o.deliveredStopCount || 0)} / ${U.num(o.stopCount || 0)}`, o.failedStopCount ? `· ${U.num(o.failedStopCount)} thất bại` : '', 'pin', o.failedStopCount ? 'red' : 'green')
        + UI.kpi('Suất đã giao', `${U.num(o.deliveredMeals || 0)} / ${U.num(o.plannedMeals || 0)}`, '', 'package', 'blue')
        + UI.kpi('Tải xe', nil(d.loadPercent) ? U.num(d.loadMeals || 0) : U.num(d.loadPercent, 0) + '%', nil(d.capacity) ? 'suất' : `${U.num(d.loadMeals || 0)} / ${U.num(d.capacity)} suất`, 'truck', (d.loadPercent || 0) > 100 ? 'red' : (d.loadPercent || 0) > 85 ? 'orange' : 'blue')
        + UI.kpi('Phản ánh', U.num(o.complaintCount || 0), o.openComplaintCount ? `· ${U.num(o.openComplaintCount)} đang mở` : '', 'msg', o.openComplaintCount ? 'red' : 'green');
    },
    overview: (box, o) => {
      const d = o.detail || {}, r = d.route || {}, stops = d.listStop || [];
      const tempOk = !nil(d.departureTemperatureC) && d.departureTemperatureC >= (d.deliveryTempMinC || 0);
      box.innerHTML = D.split(
        D.block('Thông tin chuyến', D.kv([['Số chuyến', esc(r.routeNo || '')], ['Tên chuyến', esc(r.name || '')], ['Ngày giao', D.dmy(r.deliveryDate)], ['Ca', esc((o.shiftNames || []).join(', ') || '–')],
          ['Xe', esc([d.licensePlate, d.vehicleCode].filter(Boolean).join(' · '))], ['Tài xế', esc(d.driverName || '')],
          ['Giờ kế hoạch', esc([U.time(r.plannedStart), r.plannedEnd && U.time(r.plannedEnd)].filter(Boolean).join(' – '))],
          ['Giờ thực tế', esc([r.actualStart && U.dt(r.actualStart), r.actualEnd && U.time(r.actualEnd)].filter(Boolean).join(' – '))],
          ['Nhiệt độ xuất bến', nil(d.departureTemperatureC) ? '<span class="muted">Chưa đo</span>' : `${U.num(d.departureTemperatureC, 1)} °C ${tempOk ? UI.tag('Đạt', 'green') : UI.tag('Dưới ngưỡng', 'red')}`],
          ['Ngưỡng giữ nóng', '≥ ' + U.num(d.deliveryTempMinC || 0, 1) + ' °C'], ['Trạng thái', esc(d.statusName || st('DeliveryRoute', 'Status')(r.status))], ['Mô tả', esc(r.description || '')]])),
        (['Planned', 'Loading'].includes(r.status) ? notes(d.listBlocker, 'gap') : '') + notes(d.listWarning)
        + D.block('Điểm giao', `<div class="timeline"><div class="tl blue"><b>Bếp trung tâm (điểm đầu)</b><small>KH xuất phát ${esc(U.time(r.plannedStart))}${r.actualStart ? ' · TT ' + esc(U.time(r.actualStart)) : ''}</small></div>${stops.map(s => `<div class="tl ${s.stopStatus === 'Delivered' ? '' : s.stopStatus === 'Failed' ? 'red' : 'orange'}"><b>${s.sequenceNo}. ${esc(s.locationName || s.customerName || '')}</b><small>${U.num(s.plannedMealQty || 0)} suất${nil(s.deliveredMealQty) ? '' : ' · giao ' + U.num(s.deliveredMealQty)} · đến KH ${esc(U.time(s.plannedArrival))}${s.actualArrival ? ' · TT ' + esc(U.time(s.actualArrival)) : ''} · ${esc(s.statusName || s.stopStatus || '')}</small></div>`).join('') || D.none}</div>`));
    },
    tabs: [
      { label: 'Điểm giao & POD', render: (box, o) => {
        box.innerHTML = D.table((o.detail || {}).listStop || [], [{ label: '#', num: true, html: s => esc(s.sequenceNo) }, { label: 'Khách hàng / điểm giao', html: s => `<b>${esc(s.customerName || '')}</b><span class="sub">${esc([s.locationName, s.address].filter(Boolean).join(' – '))}</span>` },
          { label: 'Đơn / phiếu giao', html: s => esc([s.orderNumber, s.deliveryNumber].filter(Boolean).join(' · ')) }, { label: 'Suất KH', num: true, html: s => qty(s.plannedMealQty) }, { label: 'Đã xếp', num: true, html: s => qty(s.loadedQty) },
          { label: 'Đã giao', num: true, html: s => qty(s.deliveredMealQty) + (s.shortageQty ? ` <span class="bad small">thiếu ${U.num(s.shortageQty)}</span>` : '') },
          { label: 'Nhiệt độ bàn giao', num: true, html: s => (nil(s.handoverTemperatureC) ? '–' : U.num(s.handoverTemperatureC, 1) + ' °C') },
          { label: 'POD', html: s => (s.pod ? `${esc(s.pod.receiverName || '')}<span class="sub">nhận ${U.num(s.pod.acceptedQuantity ?? s.pod.acceptedQty ?? 0)} · ${esc(U.dt(s.pod.receivedAt || s.pod.confirmedAt))}</span>` : '<span class="muted">–</span>') },
          { label: 'Trạng thái', html: s => UI.badge(s.stopStatus, 'DeliveryStop', 'StopStatus') }]);
      } },
      { label: 'Sự kiện & nhiệt độ', render: (box, o) => {
        const ev = ((o.detail || {}).listEvent || []).slice().sort((a, b) => String(b.eventAt).localeCompare(String(a.eventAt)));
        box.innerHTML = ev.length ? `<div class="timeline">${ev.map(e => `<div class="tl ${e.eventType === 'FAILED' ? 'red' : ''}"><b>${esc(UI.statusVi('DeliveryExecution', 'Status', e.eventType) || e.eventType || '')} ${esc(e.description || '')}</b><small>${esc(U.dt(e.eventAt))}${nil(e.temperatureC) ? '' : ' · ' + U.num(e.temperatureC, 1) + ' °C'}${e.latitude ? ' · ' + U.num(e.latitude, 4) + ', ' + U.num(e.longitude, 4) : ''}${e.sourceChannel ? ' · ' + esc(e.sourceChannel) : ''}</small></div>`).join('')}</div>` : D.none;
      } },
      { label: 'Mẻ phục vụ', render: (box, o) => {
        const rows = (o.detail || {}).listBatch || [];
        box.innerHTML = D.table(rows, [{ label: 'Số mẻ', html: b => `<b>${esc(b.batchNumber || '')}</b>` }, { label: 'Món', html: b => esc(b.dishName || '') }, { label: 'Trạng thái', html: b => esc(b.statusName || b.status || '') },
          { label: 'Lưu mẫu', html: b => (b.sampleRetained ? UI.tag('Đã lưu', 'green') : UI.tag('Chưa', 'orange')) }, { label: 'Lỗi ATTP mở', html: b => esc((b.openCriticalFailures || []).join(', ')) }], true);
        rowsTo(box, rows, 'ProductionBatche');
      } },
      { label: 'Phản ánh', render: (box, o) => {
        const rows = o.listComplaint || [];
        box.innerHTML = D.table(rows, [{ label: 'Số phản ánh', html: c => `<b>${esc(c.complaintNo || '')}</b>` }, { label: 'Khách hàng', html: c => esc(c.customerName || '') },
          { label: 'Loại', html: c => esc(c.categoryCode || '') }, { label: 'Mức độ', html: c => esc(c.severity || '') }, { label: 'Nhận lúc', html: c => esc(U.dt(c.receivedAt)) },
          { label: 'Trạng thái', html: c => UI.badge(c.complaintStatus, 'CustomerComplaint', 'ComplaintStatus') }, { label: 'Nội dung', html: c => esc(c.description || '') }], true);
        rowsTo(box, rows, 'CustomerComplaint');
      } },
    ],
    onAct: (act, o, id) => {
      if (act === 'exec') return APP.go('log-execution');
      const L = APP.logRoute; if (!L || !L[act]) return;
      L[act](id);
    },
  });
})();
