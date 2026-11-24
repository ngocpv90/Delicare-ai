/* UI-6 (6.2 nhóm A – D-80) – trang chi tiết toàn màn nhóm KINH DOANH (khung APP.D360.page, modules/detail-page.js; mẫu: detail-stockcount.js):
 *   #/crm-order-detail?id=     Đơn đặt suất   – Order/GetOverview
 *   #/crm-contract-detail?id=  Hợp đồng       – Contract/GetOverview
 *   #/log-complaint-detail?id= Phản ánh       – CustomerComplaint/GetOverview
 *   #/crm-location-detail?id=  Điểm giao hàng – CustomerDeliveryLocation/GetOverview
 * Nút phần đầu gọi lại đúng thao tác đang có ở danh sách: APP.crmOrder / APP.crmContract (modules/sales.js), APP.logComplaint (modules/logistics.js),
 * UI.liveAct (modules/ghep.js: duyệt / từ chối, yêu cầu sửa sau chốt, thêm ghi chú) và UI.form (Sửa) – không viết lại luật. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, D = APP.D360;
  const noop = () => {};
  const refName = (list, code) => (code ? ((Store.refList(list) || []).find(x => x.code === code) || {}).name || code : '');
  // Dòng PascalCase cho các thao tác dùng chung (đọc từ Store nếu đã tải, không thì dựng từ dữ liệu trang).
  const rowOf = (t, id, api) => Object.assign(D.pas(api || {}), Store.get(t, id) || {});
  // Sửa = đúng form chung của ngăn chi tiết (UI.form) – bảng chưa tải thì tải rồi mở.
  const editRow = (t, id) => {
    const go = () => { const row = Store.get(t, id); if (!row) return UI.toast('Không tải được bản ghi – tải lại trang rồi thử lại', 'err'); UI.form(t, row, { onSave: () => APP.refresh() }); };
    if (Store.get(t, id)) go(); else Store.reload([t]).then(go);
  };
  // Liên kết sang bản ghi khác: data-od="Bảng|id" (trang chi tiết nếu có, không thì ngăn trượt); khách hàng → hồ sơ 360.
  const link = (t, id, text) => (id && text ? `<a href="javascript:void 0" data-od="${esc(t)}|${esc(id)}">${esc(text)}</a>` : esc(text || ''));
  const bindLinks = box => box.querySelectorAll('[data-od]').forEach(a => a.onclick = () => {
    const [t, id] = a.dataset.od.split('|');
    if (t === 'Customer') return D.can('crm-customer') ? APP.go('crm-customer-360?id=' + encodeURIComponent(id)) : null;
    APP.openDetail(t, id, APP.FN[{ Order: 'crm-order', Contract: 'crm-contract', CustomerComplaint: 'log-complaint', CustomerDeliveryLocation: 'crm-location', DeliveryOrder: 'log-delivery-order' }[t]]);
  });
  // Bảng gọn trong 1 ô riêng (mỗi bảng tự gắn dòng bấm được).
  const fill = (box, sel, rows, cols, onRow) => { const el = box.querySelector(sel); if (!el) return; el.innerHTML = rows.length ? D.table(rows, cols, !!onRow) : D.none; D.bindRows(el, rows, onRow); };
  const orderCols = [
    { label: 'Số đơn', html: o => `<b>${esc(o.orderNumber || '')}</b>` }, { label: 'Ngày ăn', html: o => D.dmy(o.mealDate) },
    { label: 'Suất', num: true, html: o => U.num(o.totalQuantity || 0) }, { label: 'Thành tiền', num: true, html: o => U.money(o.totalAmount || 0) },
    { label: 'Trạng thái', html: o => UI.badge(o.status, 'Order', 'Status') }];
  const deliveryCols = withOrder => [
    { label: 'Số phiếu giao', html: d => `<b>${esc(d.deliveryNumber || '')}</b>${withOrder && d.orderNumber ? `<span class="sub">${esc(d.orderNumber)}</span>` : ''}` },
    { label: 'Ngày giao', html: d => D.dmy(d.deliveryDate) }, { label: 'Điểm giao', html: d => esc(d.locationName || '') }, { label: 'Chuyến', html: d => esc(d.routeNo || '–') },
    { label: 'Giờ dự kiến / thực tế', html: d => `${U.time(d.plannedDeliveryTime)}${d.actualDeliveryTime ? ' / ' + U.time(d.actualDeliveryTime) : ''}` },
    { label: 'Khách nhận', num: true, html: d => (d.acceptedQuantity === null || d.acceptedQuantity === undefined ? '–' : U.num(d.acceptedQuantity) + (d.rejectedQuantity ? ` <span class="bad small">(trả ${U.num(d.rejectedQuantity)})</span>` : '')) },
    { label: 'Trạng thái', html: d => UI.badge(d.status, 'DeliveryOrder', 'Status') }];
  const complaintCols = [
    { label: 'Số phản ánh', html: c => `<b>${esc(c.complaintNo || '')}</b>${c.orderNumber ? `<span class="sub">${esc(c.orderNumber)}</span>` : ''}` },
    { label: 'Phân loại', html: c => esc(refName('ComplaintCategory', c.categoryCode)) }, { label: 'Mức độ', html: c => UI.badge(c.severity) },
    { label: 'Tiếp nhận', html: c => esc(U.dt(c.receivedAt)) }, { label: 'Hạn', html: c => `<span class="${c.isOverdue ? 'bad' : ''}">${esc(U.dt(c.slaDueAt) || '–')}</span>` },
    { label: 'Trạng thái', html: c => UI.badge(c.status, 'CustomerComplaint', 'ComplaintStatus') }];
  const openOrder = o => APP.openDetail('Order', o.id, APP.FN['crm-order']);
  const openComplaint = c => APP.openDetail('CustomerComplaint', c.id, APP.FN['log-complaint']);
  const openDelivery = d => APP.openDetail('DeliveryOrder', d.id, APP.FN['log-delivery-order']);
  const notices = list => list.filter(Boolean).map(([cls, html]) => `<div class="notice ${cls} mb">${I('alert')}<div>${html}</div></div>`).join('');

  // ================================================================ ĐƠN ĐẶT SUẤT
  const ORDER_FLOW = ['Draft', 'Submitted', 'Confirmed', 'Locked', 'InProduction', 'Delivered', 'Reconciled'];
  const lineCols = [
    { label: 'Ca', html: l => esc(l.shiftName || '') }, { label: 'Loại suất', html: l => `${UI.tag(l.mealType || '')} ${esc(l.mealName || '')}` },
    { label: 'Lựa chọn', html: l => (l.menuOptionNo ? `<b>Lựa chọn ${l.menuOptionNo}</b>${l.optionDishText ? `<span class="sub">${esc(l.optionDishText)}</span>` : ''}` : '<span class="muted">–</span>') },
    { label: 'Thực đơn', html: l => esc(l.menuCode || '–') }, { label: 'Số lượng', num: true, html: l => U.num(l.quantity) },
    { label: 'Dự báo', num: true, html: l => (l.forecastQuantity === null || l.forecastQuantity === undefined ? '–' : U.num(l.forecastQuantity)) },
    { label: 'Đơn giá', num: true, html: l => U.money(l.unitPrice || 0) }, { label: 'Thành tiền', num: true, html: l => U.money(l.lineAmount || 0) },
    { label: 'Ghi chú', html: l => esc(l.note || '') }];
  // Sửa số suất (Order/UpdateLines qua APP.crmOrder.saveLines – máy chủ tự gửi duyệt khi lệch dự báo).
  function editLines(d, id) {
    const lines = d.listLine || [];
    UI.modal({ title: 'Sửa số suất – ' + esc((d.order || {}).orderNumber || ''), size: 'lg',
      body: `<div class="small muted mb">Sửa được khi đơn Nháp / Đã gửi / Đã xác nhận và chưa tới giờ chốt (${esc(U.dt(d.cutoffAt))}). Sau giờ chốt dùng "Yêu cầu sửa sau chốt".</div>
        <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Loại suất</th><th>Ca</th><th class="num">Dự báo</th><th class="num">Đang đặt</th><th>Số mới</th></tr></thead><tbody>${lines.map(l => `<tr><td>${UI.tag(l.mealType || '')} ${esc(l.mealName || '')}${l.menuOptionNo ? ` · Lựa chọn ${l.menuOptionNo}` : ''}</td><td>${esc(l.shiftName || '')}</td><td class="num muted">${l.forecastQuantity === null || l.forecastQuantity === undefined ? '–' : U.num(l.forecastQuantity)}</td><td class="num">${U.num(l.quantity)}</td><td><input type="number" min="0" class="input" style="width:110px;text-align:right" data-ql="${esc(l.id)}" value="${l.quantity}"></td></tr>`).join('')}</tbody></table></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="dpSaveQ">Lưu số lượng</button>',
      onMount: (m, close) => m.querySelector('#dpSaveQ').onclick = () => {
        const qty = [...m.querySelectorAll('[data-ql]')].map(i => ({ id: i.dataset.ql, quantity: Math.max(0, Number(i.value) || 0) }));
        APP.crmOrder.saveLines(id, qty, () => { close(); APP.refresh(); });
      } });
  }
  D.page({
    id: 'crm-order-detail', screen: 'crm-order', table: 'Order', kind: 'doc', api: 'Order/GetOverview', resource: 'Order',
    head: x => {
      const d = x.detail || {}, o = d.order || {}, acts = d.allowedActions || [], ed = D.can('crm-order', 'edit');
      return {
        title: 'Đơn ' + (o.orderNumber || o.orderNo || ''), no: o.orderNumber || o.orderNo, avatar: 'ĐH', avatarCls: 'biz',
        tags: [UI.badge(o.status, 'Order', 'Status'), d.pendingApprovalNo ? UI.tag('Chờ duyệt ' + d.pendingApprovalNo, 'orange') : '',
          d.isAfterCutoff && !['Cancelled', 'Delivered', 'Reconciled'].includes(o.status) ? UI.tag('Đã quá giờ chốt', 'red') : ''],
        meta: [esc(d.customerName || ''), d.locationName && esc(d.locationName), o.mealDate && 'Ngày ăn ' + D.dmy(o.mealDate), d.contractNumber && esc('HĐ ' + d.contractNumber), x.responsibleName && esc('Phụ trách: ' + x.responsibleName)],
        steps: D.stepsOf(ORDER_FLOW, o.status, ['Cancelled'], s => UI.statusVi('Order', 'Status', s)),
        buttons: [
          { act: 'confirm', icon: 'approve', label: 'Xác nhận', ok: ed && acts.includes('confirm'), primary: true },
          { act: 'lock', icon: 'key', label: 'Chốt số lượng', ok: ed && acts.includes('lock'), primary: !acts.includes('confirm') },
          { act: 'lines', icon: 'edit', label: 'Sửa số suất', ok: ed && acts.includes('updateLines') },
          { act: 'change', icon: 'edit', label: 'Yêu cầu sửa sau chốt', ok: ed && acts.includes('requestChange') },
        ],
        more: [ed && acts.includes('cancel') ? '<button data-h="cancel">Hủy đơn</button>' : '', d.pendingApprovalId ? '<button data-h="inbox">Mở màn Phê duyệt</button>' : '',
          D.can('crm-customer') ? '<button data-h="customer">Hồ sơ khách hàng</button>' : ''],
      };
    },
    kpis: x => {
      const d = x.detail || {}, o = d.order || {}, fq = d.forecastQuantity, fv = d.forecastVariancePercent;
      return UI.kpi('Tổng suất', U.num(o.totalQuantity || 0), fq === null || fq === undefined ? 'chưa có dự báo' : `dự báo ${U.num(fq)}${fv === null || fv === undefined ? '' : ' · lệch ' + U.pct(fv)}`, 'utensils', d.forecastVarianceExceeded ? 'orange' : 'green')
        + UI.kpi('Thành tiền', U.moneyShort(o.totalAmount || 0), `${U.num(x.lineCount || 0)} dòng suất`, 'calc', 'blue')
        + UI.kpi('Giao hàng', U.num(x.deliveryCount || 0) + ' phiếu', x.acceptedQuantity === null || x.acceptedQuantity === undefined ? 'chưa có xác nhận giao' : `khách nhận ${U.num(x.acceptedQuantity)} suất`, 'route', 'purple')
        + UI.kpi('Phản ánh đang mở', U.num(x.openComplaintCount || 0), `/ ${U.num(x.complaintCount || 0)}`, 'msg', x.openComplaintCount ? 'red' : 'green');
    },
    overview: (box, x) => {
      const d = x.detail || {}, o = d.order || {}, r = x.reconciliation;
      const pt = { ORDER_DEVIATION: ' (lệch suất so với dự báo)', ORDER_CHANGE_AFTER_CUTOFF: ' (thay đổi sau giờ chốt)' }[d.pendingApprovalType] || '';
      box.innerHTML = D.split(
        D.block('Thông tin đơn', D.kv([['Số đơn', esc(o.orderNumber || o.orderNo || '')], ['Khách hàng', link('Customer', o.customerId, d.customerName)], ['Mã khách', esc(x.customerCode || '')],
          ['Điểm giao', link('CustomerDeliveryLocation', o.deliveryLocationId, d.locationName)], ['Hợp đồng', link('Contract', o.contractId, d.contractNumber)],
          ['Ngày đặt', D.dmy(o.orderDate)], ['Ngày ăn', D.dmy(o.mealDate)], ['Ngày giao', esc(U.dt(o.deliveryDate))], ['Hạn chốt đơn', esc(U.dt(d.cutoffAt)) + (d.isAfterCutoff ? ' <span class="tag red">đã qua</span>' : '')],
          ['Trạng thái', esc(d.statusName || '')], ['NV phụ trách khách', esc(x.responsibleName || '')], ['Người tạo', esc(x.createdByName || '')], ['Ghi chú', esc(o.note || '')]])),
        notices([d.pendingApprovalNo && ['warn', `Đang chờ duyệt <b>${esc(d.pendingApprovalNo)}</b>${pt}`], d.forecastVarianceExceeded && ['warn', `Số suất lệch dự báo ${U.pct(d.forecastVariancePercent)} – vượt ngưỡng cảnh báo`]]
          .concat((d.listWarning || []).map(w => ['warn', esc(w)])))
        + D.block('Suất theo ca / loại suất', '<div data-lines></div>')
        + (r ? D.block('Đối soát', D.kv([['Trạng thái', esc(r.statusName || '')], ['Ngày', D.dmy(r.reconciliationDate)], ['Đặt → Sản xuất → Đóng gói', `${U.num(r.orderedQuantity)} → ${r.producedQty === null || r.producedQty === undefined ? '–' : U.num(r.producedQty)} → ${r.packedQty === null || r.packedQty === undefined ? '–' : U.num(r.packedQty)}`],
          ['Giao → Nhận', `${U.num(r.deliveredQuantity)} → ${U.num(r.acceptedQuantity)}${r.variancePercent === null || r.variancePercent === undefined ? '' : ` (${U.pct(r.variancePercent, 2)})`}`], ['Giá trị chốt', r.finalAmount === null || r.finalAmount === undefined ? '' : U.money(r.finalAmount)]])) : ''));
      fill(box, '[data-lines]', d.listLine || [], lineCols.filter(c => !['Thực đơn', 'Ghi chú'].includes(c.label)));
      bindLinks(box);
    },
    tabs: [
      { label: 'Chi tiết dòng', render: (box, x) => { box.innerHTML = '<div data-t></div>'; fill(box, '[data-t]', (x.detail || {}).listLine || [], lineCols); } },
      { label: 'Giao hàng', screen: 'log-delivery-order', render: (box, x) => { box.innerHTML = '<div data-t></div>'; fill(box, '[data-t]', x.listDelivery || [], deliveryCols(false), openDelivery); } },
      { label: 'Phản ánh', screen: 'log-complaint', render: (box, x) => { box.innerHTML = '<div data-t></div>'; fill(box, '[data-t]', x.listComplaint || [], complaintCols, openComplaint); } },
    ],
    onAct: (act, x, id, reload) => {
      const d = x.detail || {}, o = d.order || {}, row = rowOf('Order', id, o);
      const after = msg => { UI.toast(esc(msg), 'ok'); reload(); };
      if (['confirm', 'lock', 'cancel'].includes(act) && APP.crmOrder) APP.crmOrder.act(act, { Id: id, OrderNumber: o.orderNumber || o.orderNo, TotalQuantity: o.totalQuantity || 0 }, d.forecastQuantity || 0, after);
      else if (act === 'lines' && APP.crmOrder) editLines(d, id);
      else if (act === 'change' && UI.liveAct) UI.liveAct('gx-order-change', row, noop, 'Order');
      else if (act === 'inbox') APP.go('wf-inbox');
      else if (act === 'customer') APP.go('crm-customer-360?id=' + encodeURIComponent(o.customerId));
    },
  });

  // ================================================================ HỢP ĐỒNG
  const CONTRACT_FLOW = ['Draft', 'PendingApproval', 'Active', 'Expired'];
  D.page({
    id: 'crm-contract-detail', screen: 'crm-contract', table: 'Contract', kind: 'doc', api: 'Contract/GetOverview', resource: 'Contract',
    head: x => {
      const d = x.detail || {}, c = d.contract || {}, acts = d.allowedActions || [], ed = D.can('crm-contract', 'edit');
      return {
        title: 'Hợp đồng ' + (c.contractNumber || c.contractNo || ''), no: c.contractNumber || c.contractNo, avatar: 'HĐ', avatarCls: 'biz',
        tags: [UI.badge(c.status, 'Contract', 'Status'), d.pendingApprovalNo ? UI.tag('Chờ duyệt ' + d.pendingApprovalNo, 'orange') : '',
          c.status === 'Active' && x.daysToEnd !== null && x.daysToEnd !== undefined && x.daysToEnd <= 30 ? UI.tag(x.daysToEnd < 0 ? 'Đã quá ngày hiệu lực' : `Còn ${x.daysToEnd} ngày`, x.daysToEnd < 0 ? 'red' : 'orange') : ''],
        meta: [esc(d.customerName || ''), c.name && esc(c.name), `Hiệu lực ${D.dmy(c.effectiveFrom || c.startDate)}${c.effectiveTo || c.endDate ? ' – ' + D.dmy(c.effectiveTo || c.endDate) : ' – không thời hạn'}`, c.paymentTerm && esc(c.paymentTerm), x.responsibleName && esc('Phụ trách: ' + x.responsibleName)],
        steps: D.stepsOf(CONTRACT_FLOW, c.status, ['Terminated'], s => UI.statusVi('Contract', 'Status', s)),
        buttons: [
          { act: 'submit', icon: 'approve', label: 'Gửi duyệt', ok: ed && acts.includes('submit'), primary: true },
          { act: 'approve', icon: 'approve', label: 'Duyệt', ok: c.status === 'PendingApproval' && !!UI.liveAct, primary: true },
          { act: 'reject', icon: 'x', label: 'Từ chối', ok: c.status === 'PendingApproval' && !!UI.liveAct },
          { act: 'edit', icon: 'edit', label: 'Sửa', ok: ed && acts.includes('update') },
        ],
        more: [ed && acts.includes('terminate') ? '<button data-h="terminate">Thanh lý hợp đồng</button>' : '', d.pendingApprovalId ? '<button data-h="inbox">Mở màn Phê duyệt</button>' : '',
          D.can('crm-customer') ? '<button data-h="customer">Hồ sơ khách hàng</button>' : ''],
      };
    },
    kpis: x => {
      const d = x.detail || {}, c = d.contract || {}, dte = x.daysToEnd;
      return UI.kpi('Giá trị hợp đồng', c.contractValue === null || c.contractValue === undefined ? '–' : U.moneyShort(c.contractValue), `${U.num(x.mealCount || 0)} loại suất`, 'file', 'blue')
        + UI.kpi('Còn hiệu lực', dte === null || dte === undefined ? 'Không thời hạn' : dte < 0 ? 'Đã quá' : U.num(dte) + ' ngày', c.effectiveTo || c.endDate ? 'đến ' + D.dmy(c.effectiveTo || c.endDate) : '', 'calendar', dte !== null && dte !== undefined && dte <= 30 ? 'orange' : 'green')
        + UI.kpi('Đơn hàng', U.num(x.orderCount || 0), `${U.num(d.openOrderCount || 0)} chưa giao · ${U.num(x.orderedQuantity30d || 0)} suất 30 ngày`, 'clipboard', 'purple')
        + UI.kpi('Đã đối soát', U.moneyShort(x.reconciledAmount || 0), '', 'calc', 'green');
    },
    overview: (box, x, id) => {
      const d = x.detail || {}, c = d.contract || {}, canBill = D.can('crm-contract', 'edit') && c.status !== 'Expired' && c.status !== 'Terminated' && APP.crmContract;
      box.innerHTML = D.split(
        D.block('Thông tin hợp đồng', D.kv([['Số hợp đồng', esc(c.contractNumber || c.contractNo || '')], ['Tên', esc(c.name || '')], ['Khách hàng', link('Customer', c.customerId, d.customerName)], ['Mã khách', esc(x.customerCode || '')],
          ['Ngày ký', D.dmy(c.signedDate)], ['Hiệu lực từ', D.dmy(c.effectiveFrom || c.startDate)], ['Hiệu lực đến', c.effectiveTo || c.endDate ? D.dmy(c.effectiveTo || c.endDate) : 'Không thời hạn'],
          ['Giá trị', c.contractValue === null || c.contractValue === undefined ? '' : U.money(c.contractValue)], ['Điều khoản thanh toán', esc(c.paymentTerm || '')], ['Trạng thái', esc(d.statusName || '')],
          ['Điểm giao của khách', U.num(x.locationCount || 0)], ['NV phụ trách khách', esc(x.responsibleName || '')], ['Ghi chú', esc(c.note || '')]])),
        notices((d.listWarning || []).map(w => ['warn', esc(w)]).concat([d.pendingApprovalNo && ['warn', `Đang chờ duyệt <b>${esc(d.pendingApprovalNo)}</b>`]]))
        + D.block('Loại suất & đơn giá', '<div data-meals></div>')
        + D.block('Cách tính tiền đối soát', `<div class="spread"><b>${esc(d.billingBasisName || 'Theo cấu hình chung')}</b>${canBill ? `<div class="row-flex">${APP.crmContract.billingSelect('dpBill', d.billingBasis)}<button class="btn sm" data-bill>Đổi</button></div>` : ''}</div><div class="small muted mt">Áp cho các lần đối soát sau; trống = theo cấu hình chung của hệ thống.</div>`));
      fill(box, '[data-meals]', d.listMeal || [], [{ label: 'Loại suất', html: m => UI.tag(m.mealType || '') }, { label: 'Tên suất', html: m => esc(m.mealName || '') }, { label: 'Hình thức phục vụ', html: m => esc(m.serviceType || '') },
        { label: 'Đơn giá', num: true, html: m => U.money(m.unitPrice || 0) }, { label: 'Trạng thái', html: m => UI.badge(m.status, 'ContractMeal', 'Status') }]);
      const b = box.querySelector('[data-bill]');
      if (b) b.onclick = () => APP.crmContract.setBilling(id, box.querySelector('#dpBill').value).then(r => { if (r.ok) APP.refresh(); });
      bindLinks(box);
    },
    tabs: [
      { label: 'Đơn hàng', screen: 'crm-order', render: (box, x) => { box.innerHTML = '<div class="small muted mb">20 đơn gần nhất theo ngày ăn.</div><div data-t></div>'; fill(box, '[data-t]', x.listRecentOrder || [], orderCols, openOrder); } },
    ],
    onAct: (act, x, id, reload) => {
      const d = x.detail || {}, c = d.contract || {}, row = rowOf('Contract', id, c);
      if (act === 'submit' && APP.crmContract) APP.crmContract.submit(row, reload);
      else if (act === 'terminate' && APP.crmContract) APP.crmContract.terminate(row, reload);
      else if ((act === 'approve' || act === 'reject') && UI.liveAct) UI.liveAct('gx-' + act, row, noop, 'Contract');
      else if (act === 'edit') editRow('Contract', id);
      else if (act === 'inbox') APP.go('wf-inbox');
      else if (act === 'customer') APP.go('crm-customer-360?id=' + encodeURIComponent(c.customerId));
    },
  });

  // ================================================================ PHẢN ÁNH KHÁCH HÀNG
  const COMPLAINT_FLOW = ['Open', 'InProgress', 'Resolved', 'Closed'];
  const hoursText = h => { const a = Math.abs(Number(h) || 0); return a >= 48 ? U.num(a / 24, 1) + ' ngày' : U.num(a, 1) + ' giờ'; };
  const historyHtml = list => (list.length ? `<div class="timeline">${list.map(h => `<div class="tl ${h.newStatusName === 'Từ chối' ? 'red' : h.newStatusName && h.newStatusName !== h.oldStatusName ? '' : 'orange'}"><b>${esc(U.dt(h.eventAt))} · ${esc(h.newStatusName && h.newStatusName !== h.oldStatusName ? (h.oldStatusName ? h.oldStatusName + ' → ' : '') + h.newStatusName : 'Ghi chú')}</b><small>${[h.actorName && esc(h.actorName), h.comment && esc(h.comment)].filter(Boolean).join(' · ')}</small></div>`).join('')}</div>` : D.none);
  D.page({
    id: 'log-complaint-detail', screen: 'log-complaint', table: 'CustomerComplaint', kind: 'doc', api: 'CustomerComplaint/GetOverview', resource: 'CustomerComplaint', related: false,
    head: x => {
      const d = x.detail || {}, c = d.complaint || {}, acts = d.allowedActions || [], ed = D.can('log-complaint', 'edit');
      const nx = APP.nextStatus('CustomerComplaint', 'ComplaintStatus', c.complaintStatus);
      const canBu = x.canCompensate && Store.perm('log-complaint').add === true && Store.perm('crm-order').add === true && !!APP.logComplaint;
      return {
        title: 'Phản ánh ' + (c.complaintNo || ''), no: c.complaintNo, avatar: 'PA', avatarCls: 'biz',
        tags: [UI.badge(c.complaintStatus, 'CustomerComplaint', 'ComplaintStatus'), c.severity ? UI.badge(c.severity) : '', d.isOverdue ? UI.tag('Quá hạn xử lý', 'red') : ''],
        meta: [esc(d.customerName || ''), c.categoryCode && esc(refName('ComplaintCategory', c.categoryCode)), c.receivedAt && 'Tiếp nhận ' + esc(U.dt(c.receivedAt)), c.sourceChannel && esc(refName('ComplaintChannel', c.sourceChannel)), d.assignedToName && esc('Xử lý: ' + d.assignedToName)],
        steps: D.stepsOf(COMPLAINT_FLOW, c.complaintStatus, ['Rejected'], s => UI.statusVi('CustomerComplaint', 'ComplaintStatus', s)),
        buttons: [
          { act: 'next', icon: 'approve', label: nx ? '→ ' + esc(UI.statusVi('CustomerComplaint', 'ComplaintStatus', nx)) : '', ok: ed && !!nx && !!APP.logComplaint, primary: true },
          { act: 'compensate', icon: 'plus', label: 'Tạo đơn bù', ok: canBu },
          { act: 'comment', icon: 'msg', label: 'Thêm ghi chú', ok: ed && acts.includes('addComment') && !!UI.liveAct },
          { act: 'edit', icon: 'edit', label: 'Sửa', ok: ed && acts.includes('update') },
        ],
        more: [ed && acts.includes('reject') && APP.logComplaint ? '<button data-h="reject">Từ chối phản ánh</button>' : '', D.can('crm-customer') ? '<button data-h="customer">Hồ sơ khách hàng</button>' : ''],
      };
    },
    kpis: x => {
      const d = x.detail || {}, c = d.complaint || {}, h = x.hoursToDue;
      const sla = x.resolveHours !== null && x.resolveHours !== undefined ? ['Đã xử lý trong', hoursText(x.resolveHours), c.slaDueAt ? 'hạn ' + esc(U.dt(c.slaDueAt)) : '', c.resolvedAt && c.slaDueAt && c.resolvedAt > c.slaDueAt ? 'red' : 'green']
        : h === null || h === undefined ? ['Hạn xử lý', c.slaDueAt ? esc(U.dt(c.slaDueAt)) : '–', '', 'blue']
          : [h < 0 ? 'Quá hạn' : 'Còn lại', hoursText(h), 'hạn ' + esc(U.dt(c.slaDueAt)), h < 0 ? 'red' : h < 4 ? 'orange' : 'green'];
      return UI.kpi(sla[0], sla[1], sla[2], 'clock', sla[3])
        + UI.kpi('Diễn biến xử lý', U.num(x.historyCount || 0), 'lần ghi nhận', 'history', 'blue')
        + UI.kpi('Đơn bù', U.num((x.listCompensationOrder || []).length), x.canCompensate ? 'chưa tạo – được tạo đơn bù' : '', 'package', 'purple')
        + UI.kpi('Phản ánh khác đang mở', U.num(x.customerOpenComplaintCount || 0), 'của khách hàng này', 'msg', x.customerOpenComplaintCount ? 'orange' : 'green');
    },
    overview: (box, x) => {
      const d = x.detail || {}, c = d.complaint || {}, last = (x.listHistory || []).slice(-5).reverse();
      box.innerHTML = D.split(
        D.block('Thông tin phản ánh', D.kv([['Số phản ánh', esc(c.complaintNo || '')], ['Khách hàng', link('Customer', c.customerId, d.customerName)], ['Mã khách', esc(x.customerCode || '')],
          ['Đơn hàng', link('Order', c.orderId, d.orderNumber)], ['Đơn giao', link('DeliveryOrder', c.deliveryOrderId, d.deliveryNumber)], ['Chuyến', esc(d.routeNo || '')],
          ['Kênh tiếp nhận', esc(refName('ComplaintChannel', c.sourceChannel))], ['Phân loại', esc(refName('ComplaintCategory', c.categoryCode))], ['Mức độ', esc(d.severityName || '')],
          ['Tiếp nhận', esc(U.dt(c.receivedAt))], ['Hạn xử lý', esc(U.dt(c.slaDueAt)) + (d.isOverdue ? ' <span class="tag red">quá hạn</span>' : '')], ['Người xử lý', esc(d.assignedToName || '')],
          ['Xử lý xong', esc(U.dt(c.resolvedAt))], ['NV phụ trách khách', esc(x.responsibleName || '')]])),
        D.block('Nội dung phản ánh', `<div style="white-space:pre-wrap">${esc(c.description || '') || D.none}</div>`)
        + (c.resolution ? D.block('Kết quả xử lý', `<div style="white-space:pre-wrap">${esc(c.resolution)}</div>`) : '')
        + D.block('Diễn biến gần nhất', historyHtml(last))
        + ((x.listCompensationOrder || []).length ? D.block('Đơn bù', '<div data-bu></div>') : ''));
      fill(box, '[data-bu]', x.listCompensationOrder || [], orderCols, openOrder);
      bindLinks(box);
    },
    tabs: [
      { label: 'Diễn biến xử lý', render: (box, x) => { box.innerHTML = historyHtml(x.listHistory || []); } },
    ],
    onAct: (act, x, id) => {
      const c = (x.detail || {}).complaint || {}, row = rowOf('CustomerComplaint', id, c);
      if (act === 'next' && APP.logComplaint) APP.logComplaint.next(row, APP.nextStatus('CustomerComplaint', 'ComplaintStatus', c.complaintStatus));
      else if (act === 'reject' && APP.logComplaint) APP.logComplaint.next(row, 'Rejected');
      else if (act === 'compensate' && APP.logComplaint) APP.logComplaint.compensate(row);
      else if (act === 'comment' && UI.liveAct) UI.liveAct('gx-comment', row, noop, 'CustomerComplaint');
      else if (act === 'edit') editRow('CustomerComplaint', id);
      else if (act === 'customer') APP.go('crm-customer-360?id=' + encodeURIComponent(c.customerId));
    },
  });

  // ================================================================ ĐIỂM GIAO HÀNG (hồ sơ – không có thanh trạng thái)
  const mapUrl = l => (l.latitude !== null && l.latitude !== undefined && l.longitude !== null && l.longitude !== undefined ? `https://www.google.com/maps?q=${encodeURIComponent(l.latitude + ',' + l.longitude)}` : null);
  const windowText = l => (l.deliveryWindowFrom || l.deliveryWindowTo ? `${U.time(l.deliveryWindowFrom) || '…'}–${U.time(l.deliveryWindowTo) || '…'}` : '');
  // GAP: CustomerDeliveryLocation chưa có trong CommonRegistry (máy chủ) → chưa có tab Lịch sử / Tài liệu / Liên quan chuẩn.
  D.page({
    id: 'crm-location-detail', screen: 'crm-location', table: 'CustomerDeliveryLocation', kind: 'profile', api: 'CustomerDeliveryLocation/GetOverview',
    head: x => {
      const l = x.location || {};
      return {
        title: l.name || l.code || 'Điểm giao', no: l.code || l.name, avatarCls: 'biz',
        tags: [UI.badge(l.status, 'CustomerDeliveryLocation', 'Status')],
        meta: [esc(l.code || ''), esc(x.customerName || ''), l.address && esc(l.address), windowText(l) && 'Nhận ' + windowText(l), (l.contactName || l.contactPhone) && esc([l.contactName, l.contactPhone].filter(Boolean).join(' '))],
        buttons: [
          { act: 'edit', icon: 'edit', label: 'Sửa', ok: D.can('crm-location', 'edit'), primary: true },
          { act: 'map', icon: 'pin', label: 'Bản đồ', ok: !!mapUrl(l) },
        ],
        more: [D.can('crm-customer') ? '<button data-h="customer">Hồ sơ khách hàng</button>' : ''],
      };
    },
    kpis: x => UI.kpi('Suất TB / ngày', U.num(x.avgMealsPerDay30d || 0, 0), `30 ngày · Σ ${U.num(x.mealQuantity30d || 0)} suất`, 'utensils', 'green')
      + UI.kpi('Đơn 30 ngày', U.num(x.orderCount30d || 0), '', 'clipboard', 'blue')
      + UI.kpi('Giao đúng giờ', x.onTimePct30d === null || x.onTimePct30d === undefined ? '–' : U.pct(x.onTimePct30d), `${U.num(x.deliveryCount30d || 0)} lượt giao 30 ngày`, 'route', x.onTimePct30d === null || x.onTimePct30d === undefined ? 'blue' : x.onTimePct30d >= 95 ? 'green' : 'orange')
      + UI.kpi('Phản ánh đang mở', U.num(x.openComplaintCount || 0), `/ ${U.num(x.complaintCount || 0)} của đơn tại điểm`, 'msg', x.openComplaintCount ? 'red' : 'green'),
    overview: (box, x) => {
      const l = x.location || {}, map = mapUrl(l);
      box.innerHTML = D.split(
        D.block('Thông tin điểm giao', D.kv([['Mã', esc(l.code || '')], ['Tên', esc(l.name || '')], ['Khách hàng', link('Customer', l.customerId, x.customerName)], ['Mã khách', esc(x.customerCode || '')],
          ['Địa chỉ', esc(l.address || '')], ['Khung giờ nhận', windowText(l)], ['Người nhận', esc(l.contactName || '')], ['Điện thoại', esc(l.contactPhone || '')],
          ['Toạ độ', map ? `<a href="${esc(map)}" target="_blank" rel="noopener">${esc(l.latitude + ', ' + l.longitude)}</a>` : ''], ['Hướng dẫn giao', esc(l.deliveryInstructions || '')],
          ['Trạng thái', UI.badge(l.status, 'CustomerDeliveryLocation', 'Status')], ['NV phụ trách khách', esc(x.responsibleName || '')]])),
        D.block('Đơn gần đây', '<div data-o></div>') + D.block('Giao hàng gần đây', '<div data-d></div>'));
      fill(box, '[data-o]', (x.listRecentOrder || []).slice(0, 8), orderCols, D.can('crm-order') ? openOrder : null);
      fill(box, '[data-d]', (x.listRecentDelivery || []).slice(0, 8), deliveryCols(true).filter(c => c.label !== 'Điểm giao'), D.can('log-delivery-order') ? openDelivery : null);
      bindLinks(box);
    },
    tabs: [
      { label: 'Đơn hàng', screen: 'crm-order', render: (box, x) => { box.innerHTML = '<div class="small muted mb">20 đơn gần nhất theo ngày ăn.</div><div data-t></div>'; fill(box, '[data-t]', x.listRecentOrder || [], orderCols, openOrder); } },
      { label: 'Giao hàng', screen: 'log-delivery-order', render: (box, x) => { box.innerHTML = '<div class="small muted mb">20 phiếu giao gần nhất.</div><div data-t></div>'; fill(box, '[data-t]', x.listRecentDelivery || [], deliveryCols(true).filter(c => c.label !== 'Điểm giao'), openDelivery); } },
      { label: 'Phản ánh', screen: 'log-complaint', render: (box, x) => { box.innerHTML = '<div data-t></div>'; fill(box, '[data-t]', x.listComplaint || [], complaintCols, openComplaint); } },
    ],
    onAct: (act, x, id) => {
      const l = x.location || {};
      if (act === 'edit') editRow('CustomerDeliveryLocation', id);
      else if (act === 'map' && mapUrl(l)) window.open(mapUrl(l), '_blank', 'noopener');
      else if (act === 'customer') APP.go('crm-customer-360?id=' + encodeURIComponent(l.customerId));
    },
  });
})();
