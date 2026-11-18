/* P1/P4 – MRP, Đề nghị mua, Đơn mua, Nhập kho (cổng QC), Lô & truy xuất, Tồn kho, Kiểm kê */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const avail = iid => U.sum(Store.by('InventoryBalance', 'IngredientId', iid), 'AvailableQuantity');
  const ing = id => Store.get('Ingredient', id) || {};
  const prefPrice = id => { const s = Store.rows('SupplierMaterial').find(x => x.IngredientId === id && x.IsPreferred); return s ? s.UnitPrice : 0; };
  // ================= CHẾ ĐỘ DỮ LIỆU THẬT – Mua hàng theo docs/frontend-handoff/P5.md + BE2.3.md =================
  // Mọi thao tác đi qua action nghiệp vụ của backend (CRUD thô bị chặn): MaterialRequirement/Calculate · CalculateTopUp,
  // PurchaseRequest/Submit · ConvertToPurchaseOrder · Cancel, PurchaseOrder/Submit · Send · Cancel · Close · ExportOrder,
  // GoodsReceipt/Create · Post · Cancel. Duyệt đề nghị mua / PO thực hiện ở Hộp phê duyệt.
  const PUR_TABLES = ['MaterialRequirement', 'ProductionPlan', 'ProductionPlanLine', 'PurchaseRequest', 'PurchaseRequestLine', 'PurchaseOrder', 'PurchaseOrderLine', 'GoodsReceipt', 'GoodsReceiptLine', 'QCInspection', 'QCInspectionLine', 'MaterialLot', 'InventoryBalance', 'InventoryTransaction', 'ApprovalRequest', 'SupplierIssue'];
  const run = (path, form, okMsg) => Store.act(path, { form, tables: PUR_TABLES }).then(r => { if (r.ok) { const m = r.res && r.res.responseMessage; UI.toast(esc(m && m !== 'Lưu thông tin thành công' ? m : okMsg), 'ok'); APP.refresh(); } return r; });
  const askReason = (title, fn) => UI.confirm(title, note => fn(note), { input: 'Lý do', required: true, ok: 'Xác nhận', danger: true });
  const rowBtn = (td, cls, text, fn) => { const b = document.createElement('button'); b.className = 'btn xs ' + cls; b.textContent = text; b.onclick = e => { e.stopPropagation(); fn(); }; td.insertBefore(document.createTextNode(' '), td.firstChild); td.insertBefore(b, td.firstChild); };

  // ---------- Nhu cầu nguyên liệu: kết quả do máy chủ tính theo ngày sản xuất ----------
  const rq = { date: null, data: {}, busy: {} };
  function reqLive(el, f) {
    if (!rq.date) rq.date = U.addDays(Store.ctx.date, 1);
    const date = rq.date;
    el.innerHTML = APP.header(f, `<input type="date" class="input" id="rqDate" value="${date}" style="width:auto"><button class="btn primary" id="rqCalc">${I('calc')} Tính nhu cầu</button><button class="btn" id="rqTop" style="display:none">${I('plus')} Tính bổ sung</button><button class="btn" id="rqPrint" style="display:none">${I('export')} In phiếu trước sản xuất</button>`,
      { noNote: true, sub: 'Chọn ngày sản xuất rồi bấm Tính nhu cầu. Hệ thống lấy các đơn đã chốt của ngày, tính nguyên liệu theo công thức, so với tồn kho và tự lập đề nghị mua nháp cho phần thiếu.' }) + `<div id="rqBody"><div class="card panel empty">${I('calc')}<h2 class="mt">Đang tải…</h2></div></div>`;
    const body = el.querySelector('#rqBody');
    const draw = x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="notice gap">${I('alert')}<div><b>Không tải được nhu cầu nguyên liệu</b>${esc(x.message || '')}</div></div>`; return; }
      const d = x.data;
      if (!d) { body.innerHTML = `<div class="card panel empty">${I('calc')}<h2 class="mt">Chưa tính nhu cầu cho ngày ${U.date(date)}</h2><p class="muted">${esc((x.res && x.res.responseMessage) || 'Bấm "Tính nhu cầu" để tính từ các đơn đã chốt của ngày này.')}</p></div>`; return; }
      const pr = d.purchaseRequest, tops = d.listTopUpPurchaseRequest || [];
      const locked = [pr].concat(tops).filter(Boolean).some(p => ['PendingApproval', 'InProgress', 'Approved', 'Converted'].includes(p.status));
      const bt = el.querySelector('#rqTop'), bp = el.querySelector('#rqPrint');
      if (bt) bt.style.display = locked ? '' : 'none';
      if (bp) { bp.style.display = d.productionPlanId ? '' : 'none'; bp.onclick = () => Store.download('MaterialRequirement/ExportPreProduction', { id: d.productionPlanId }).then(r => { if (r.ok) UI.toast('Đã tải ' + esc(r.fileName || 'phiếu trước sản xuất'), 'ok'); }); }
      const prRow = p => `<div class="spread" style="padding:6px 0;border-bottom:1px solid #F0F2ED"><span><b>${esc(p.requestNumber || '')}</b> · ${p.lineCount || 0} dòng · ước tính ${U.money(p.estimatedAmount || 0)}</span><span>${UI.badge(p.status, 'PurchaseRequest', 'Status')}</span></div>`;
      const itemRow = r => {
        const p = Math.min(100, r.coveragePercent === null || r.coveragePercent === undefined ? 100 : r.coveragePercent);
        return `<tr${r.incomplete ? ' style="background:#FFF8E6"' : ''}><td><b>${esc(r.ingredientName || '')}</b><span class="sub">${esc(r.ingredientCode || '')}${r.warning ? ' · ' + esc(r.warning) : ''}</span></td><td class="num">${U.num(r.requiredQuantity, 2)}</td><td class="num">${U.num(r.availableQuantity, 2)}${r.blockedQuantity ? `<span class="sub">bị chặn ${U.num(r.blockedQuantity, 2)}</span>` : ''}</td><td class="num">${U.num(r.expectedIncomingQuantity || 0, 2)}</td><td class="num">${r.purchaseQuantity > 0 ? `<b class="bad">${U.num(r.purchaseQuantity, 2)}</b>` : '<span class="up">0</span>'}</td><td>${esc(r.unitName || '')}</td><td><div class="track" style="width:110px"><div class="fill ${p >= 100 ? '' : p > 50 ? 'orange' : 'red'}" style="width:${p}%"></div></div></td><td>${r.supplier ? esc(r.supplier.supplierName || '') : '<span class="muted">Chưa có NCC</span>'}</td><td><span class="badge ${r.status === 'Sufficient' ? 'green' : r.status === 'Shortage' ? 'red' : 'orange'}">${esc(r.statusName || r.status || '')}</span></td></tr>`;
      };
      const dishRow = m => `<tr><td><b>${esc(m.dishName || '')}</b><span class="sub">${esc(m.dishCode || '')}</span></td><td class="num">${U.num(m.portions, 1)}${m.bufferPortions ? `<span class="sub">+${U.num(m.bufferPortions, 1)} dự phòng</span>` : ''}</td><td>${m.recipeVersion ? 'Phiên bản ' + esc(m.recipeVersion) : '<span class="muted">–</span>'}</td><td>${m.warning ? `<span class="warn">${esc(m.warning)}</span>` : ''}</td></tr>`;
      body.innerHTML = `
        <div class="kpis">${UI.kpi('Đơn đã chốt', U.num(d.orderCount || 0), `Kế hoạch ${esc(d.planNumber || '–')} · ${esc(d.planStatusName || '')}`, 'clipboard', 'green')}${UI.kpi('Tổng suất', U.num(d.totalMeals || 0), `${d.dishCount || 0} món`, 'utensils', 'blue')}${UI.kpi('Nguyên liệu cần mua', U.num(d.shortageCount || 0), d.shortageCount ? '<span class="bad">Thiếu hàng</span>' : 'Đủ hàng', 'alert', d.shortageCount ? 'red' : 'green')}${UI.kpi('Giá trị mua ước tính', U.money(d.estimatedAmount || 0), d.calculatedAt ? 'Tính lúc ' + U.dt(String(d.calculatedAt).replace(/\.\d+$/, '')) : '', 'calc', 'orange')}</div>
        ${(d.listWarning || []).length ? `<div class="notice warn">${I('alert')}<div><b>Lưu ý</b>${d.listWarning.map(w => esc(w)).join('<br>')}</div></div>` : ''}
        <div class="card panel mb"><div class="panel-head"><div><div class="panel-title">Đề nghị mua của kế hoạch</div><div class="panel-desc">Đề nghị nháp được lập tự động cho phần thiếu. Gửi duyệt và lập PO ở màn Đề nghị mua.</div></div><a class="btn" href="#/scm-pr">Mở Đề nghị mua →</a></div>${pr ? prRow(pr) : '<div class="muted small">Không thiếu nguyên liệu – không có đề nghị mua.</div>'}${tops.map(prRow).join('')}</div>
        <div class="card panel mb"><div class="panel-title mb">Nguyên liệu</div><div class="table-wrap"><table class="dt"><thead><tr><th>Nguyên liệu</th><th class="num">Nhu cầu</th><th class="num">Tồn xuất được</th><th class="num">Hàng sắp về</th><th class="num">Cần mua</th><th>ĐVT</th><th>Độ phủ</th><th>NCC gợi ý</th><th>Trạng thái</th></tr></thead><tbody>${(d.listItem || []).map(itemRow).join('') || '<tr><td colspan="9" class="empty">Không có nguyên liệu</td></tr>'}</tbody></table></div></div>
        <div class="card panel"><div class="panel-title mb">Món và số suất</div><div class="table-wrap"><table class="dt"><thead><tr><th>Món</th><th class="num">Số suất</th><th>Công thức</th><th>Ghi chú</th></tr></thead><tbody>${(d.listDish || []).map(dishRow).join('') || '<tr><td colspan="4" class="empty">Không có món</td></tr>'}</tbody></table></div></div>`;
    };
    el.querySelector('#rqDate').onchange = e => { if (e.target.value) { rq.date = e.target.value; APP.refresh(); } };
    const calc = path => Store.act(path, { form: { ProductionDate: date, OrganizationUnitId: Store.ctx.branch }, tables: PUR_TABLES }).then(r => { if (!r.ok) return; delete rq.data[date]; UI.toast(esc((r.res && r.res.responseMessage) || 'Đã tính nhu cầu nguyên liệu'), 'ok'); APP.refresh(); });
    el.querySelector('#rqCalc').onclick = () => calc('MaterialRequirement/Calculate');
    el.querySelector('#rqTop').onclick = () => calc('MaterialRequirement/CalculateTopUp');
    if (rq.data[date]) return draw(rq.data[date]);
    if (rq.busy[date]) return;
    rq.busy[date] = true;
    Store.query('MaterialRequirement/GetByDate', { productionDate: date, organizationUnitId: Store.ctx.branch }).then(x => { rq.busy[date] = false; rq.data[date] = x; if (rq.date === date && APP.current === 'scm-requirement') { const b = document.getElementById('rqBody'); if (b) APP.refresh(); } });
  }

  // ---------- Đề nghị mua ----------
  function prLive(el) {
    APP.rowExtra(el, tr => {
      const r = Store.get('PurchaseRequest', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || td.dataset.live) return; td.dataset.live = '1';
      const no = esc(r.RequestNumber || '');
      if (['Draft', 'Approved'].includes(r.Status)) rowBtn(td, 'danger', 'Hủy', () => askReason(`Hủy đề nghị mua <b>${no}</b>?`, note => run('PurchaseRequest/Cancel', { Id: r.Id, Reason: note }, 'Đã hủy đề nghị mua')));
      if (r.Status === 'Approved') rowBtn(td, 'primary', 'Lập PO', () => UI.confirm(`Lập đơn mua (PO) từ đề nghị <b>${no}</b>? Mỗi nhà cung cấp gợi ý sẽ có một PO nháp.`, () => Store.act('PurchaseRequest/ConvertToPurchaseOrder', { form: { Id: r.Id }, tables: PUR_TABLES }).then(x => {
        if (!x.ok) return; const d = x.data || {}, pos = d.listPurchaseOrder || [], w = d.listWarning || [];
        UI.toast('Đã lập ' + pos.length + ' PO: ' + pos.map(p => esc(p.purchaseOrderNumber)).join(', ') + (w.length ? '<br><span class="small">' + w.map(s => esc(s)).join('<br>') + '</span>' : ''), w.length ? 'warn' : 'ok'); APP.go('scm-po');
      }), { ok: 'Lập PO' }));
      if (r.Status === 'Draft') rowBtn(td, 'orange', 'Gửi duyệt', () => UI.confirm(`Gửi duyệt đề nghị mua <b>${no}</b>?`, () => run('PurchaseRequest/Submit', { Id: r.Id }, 'Đã gửi duyệt đề nghị mua'), { ok: 'Gửi duyệt' }));
    });
  }

  // ---------- Đơn mua (PO) ----------
  function poLive(el) {
    APP.rowExtra(el, tr => {
      const r = Store.get('PurchaseOrder', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || td.dataset.live) return; td.dataset.live = '1';
      const no = esc(r.PurchaseOrderNumber || '');
      rowBtn(td, '', 'In', () => Store.download('PurchaseOrder/ExportOrder', { id: r.Id }).then(x => { if (x.ok) UI.toast('Đã tải ' + esc(x.fileName || 'đơn đặt hàng'), 'ok'); }));
      if (['Draft', 'Approved', 'Sent'].includes(r.Status)) rowBtn(td, 'danger', 'Hủy', () => askReason(`Hủy đơn mua <b>${no}</b>?`, note => run('PurchaseOrder/Cancel', { Id: r.Id, Reason: note }, 'Đã hủy đơn mua')));
      if (['PartiallyReceived', 'Received'].includes(r.Status)) rowBtn(td, '', 'Đóng', () => UI.confirm(`Đóng đơn mua <b>${no}</b>?`, () => run('PurchaseOrder/Close', { Id: r.Id }, 'Đã đóng đơn mua'), { ok: 'Đóng PO' }));
      if (['Approved', 'Sent', 'PartiallyReceived'].includes(r.Status)) rowBtn(td, 'primary', 'Nhận hàng', () => receiveLive(r));
      if (r.Status === 'Approved') rowBtn(td, 'orange', 'Gửi NCC', () => UI.confirm(`Đánh dấu đã gửi đơn <b>${no}</b> cho nhà cung cấp?`, () => run('PurchaseOrder/Send', { Id: r.Id }, 'Đã gửi nhà cung cấp'), { ok: 'Gửi NCC' }));
      if (r.Status === 'Draft') rowBtn(td, 'orange', 'Gửi duyệt', () => UI.confirm(`Gửi duyệt đơn mua <b>${no}</b>?`, () => run('PurchaseOrder/Submit', { Id: r.Id }, 'Đã gửi duyệt đơn mua'), { ok: 'Gửi duyệt' }));
    });
  }
  // Nhận hàng theo PO: số nhận, số lô NCC, hạn dùng, nhiệt độ từng dòng → GoodsReceipt/Create (JSON) → sinh phiếu QC.
  function receiveLive(po) {
    Promise.all([Store.query('PurchaseOrder/GetDetail', { id: po.Id }), Store.ensure('Warehouse')]).then(([x]) => {
      if (!x.ok) return UI.toast(esc(x.message || 'Không tải được đơn mua'), 'err');
      const d = x.data || {};
      const remain = l => (l.remainingQuantity === undefined || l.remainingQuantity === null ? l.quantity : l.remainingQuantity);
      const lines = (d.listLine || []).filter(l => remain(l) > 0);
      if (!lines.length) return UI.toast('Đơn mua này không còn dòng nào chờ nhận', 'warn');
      const whs = Store.rows('Warehouse'), defWh = APP.rawWarehouseId();
      UI.modal({
        title: 'Nhận hàng – ' + esc(po.PurchaseOrderNumber || ''), sub: esc(d.supplierName || ''), size: 'lg',
        body: `<div class="field mb"><label>Kho nhận</label><select class="select" id="grWh">${whs.map(w => `<option value="${w.Id}" ${w.Id === defWh ? 'selected' : ''}>${esc(Store.label('Warehouse', w))}</option>`).join('')}</select></div>
          <div class="table-wrap"><table class="dt"><thead><tr><th>Nguyên liệu</th><th class="num">Còn lại</th><th>Số nhận</th><th>Số lô NCC</th><th>Hạn dùng</th><th>Nhiệt độ °C</th></tr></thead><tbody>${lines.map(l => `<tr data-l="${l.id}"><td><b>${esc(l.ingredientName || '')}</b><span class="sub">${esc(l.unitName || '')}</span></td><td class="num">${U.num(remain(l), 2)}</td><td><input class="input" type="number" step="0.01" min="0" data-f="q" value="${remain(l)}" style="width:100px"></td><td><input class="input" data-f="lot" placeholder="Số lô" style="width:130px"></td><td><input class="input" type="date" data-f="exp" style="width:150px"></td><td><input class="input" type="number" step="0.1" data-f="t" style="width:90px"></td></tr>`).join('')}</tbody></table></div>
          <div class="small muted mt">Dòng không nhận lần này: nhập số nhận = 0. Hạn dùng để trống = ngày nhận + hạn bảo quản của nguyên liệu.</div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="grSave">Tạo phiếu nhập</button>',
        onMount: (m, close) => m.querySelector('#grSave').onclick = () => {
          const listLine = [...m.querySelectorAll('tr[data-l]')].map(tr => {
            const v = k => tr.querySelector('[data-f="' + k + '"]').value; const q = Number(v('q')) || 0;
            return q > 0 ? { purchaseOrderLineId: tr.dataset.l, receivedQuantity: q, supplierLotNumber: v('lot') || null, expiryDate: v('exp') || null, temperatureC: v('t') === '' ? null : Number(v('t')) } : null;
          }).filter(Boolean);
          if (!listLine.length) return UI.toast('Nhập số nhận cho ít nhất một dòng', 'err');
          Store.act('GoodsReceipt/Create', { json: { purchaseOrderId: po.Id, warehouseId: m.querySelector('#grWh').value, receiptDate: U.iso(new Date()), listLine }, tables: PUR_TABLES }).then(r => {
            if (!r.ok) return; close(); const w = (r.data && r.data.listWarning) || [];
            UI.toast('Đã tạo phiếu nhập – chờ QC kết luận' + (w.length ? '<br><span class="small">' + w.map(s => esc(s)).join('<br>') + '</span>' : ''), w.length ? 'warn' : 'ok'); APP.go('qc-inspection');
          });
        }
      });
    });
  }

  // ---------- Phiếu nhập kho: ghi sổ sau khi QC kết luận ----------
  function grLive(el) {
    APP.rowExtra(el, tr => {
      const r = Store.get('GoodsReceipt', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || td.dataset.live) return; td.dataset.live = '1';
      const no = esc(r.ReceiptNumber || '');
      if (!['Posted', 'Cancelled'].includes(r.Status)) rowBtn(td, 'danger', 'Hủy', () => askReason(`Hủy phiếu nhập <b>${no}</b>?`, note => run('GoodsReceipt/Cancel', { Id: r.Id, Reason: note }, 'Đã hủy phiếu nhập')));
      if (['Accepted', 'PartiallyAccepted', 'Rejected'].includes(r.Status)) rowBtn(td, 'primary', 'Ghi sổ', () => UI.confirm(`Ghi sổ phiếu nhập <b>${no}</b>? Hệ thống tạo lô, nhập kho phần đạt QC và cập nhật đơn mua.`, () => run('GoodsReceipt/Post', { Id: r.Id }, 'Đã ghi sổ nhập kho'), { ok: 'Ghi sổ nhập kho' }));
      if (r.Status === 'QCPending') rowBtn(td, 'orange', 'Mở QC', () => APP.go('qc-inspection'));
    });
  }

  // ---------- Nhu cầu vật tư (MRP) ----------
  const ms = { plan: null };
  APP.register('scm-requirement', (el, f) => {
    if (Store.isLive) return reqLive(el, f);
    const plans = Store.rows('ProductionPlan').filter(p => U.day(p.ProductionDate) >= Store.ctx.date).sort((a, b) => (a.ProductionDate + a.PlanNumber).localeCompare(b.ProductionDate + b.PlanNumber));
    if (!ms.plan) ms.plan = (plans.find(p => Store.by('MaterialRequirement', 'ProductionPlanId', p.Id).some(r => r.Status === 'Shortage')) || plans.find(p => Store.by('MaterialRequirement', 'ProductionPlanId', p.Id).length) || plans[0]).Id;
    const pl = Store.get('ProductionPlan', ms.plan);
    const reqs = Store.by('MaterialRequirement', 'ProductionPlanId', pl.Id);
    const short = reqs.filter(r => r.PurchaseQuantity > 0);
    el.innerHTML = APP.header(f, `<button class="btn" id="mCalc">${I('calc')} Tính lại nhu cầu</button><button class="btn primary" id="mPR" ${short.filter(r => r.Status === 'Shortage').length ? '' : 'disabled'}>${I('plus')} Tạo đề nghị mua từ thiếu hụt</button>`, { sub: 'Luồng 2: Thực đơn → Món → Công thức → Định lượng → Số suất → Nhu cầu → So sánh tồn → Thiếu hụt. Công thức tính PROPOSED – BA/PO CONFIRM.' }) + `
    <div class="card panel mb"><div class="row-flex"><span class="small muted">Kế hoạch sản xuất:</span>${plans.map(p => `<button class="chip ${p.Id === pl.Id ? 'active' : ''}" data-p="${p.Id}">${esc(p.PlanNumber)} ${UI.badge(p.Status, 'ProductionPlan', 'Status')}</button>`).join('')}</div>
      <div class="notice info mt small">${I('calc')}<div><b>Nhu cầu gộp</b> = Σ (PlannedQuantity × RecipeLine.Quantity / 100) ÷ (Ingredient.YieldPercent / 100) · <b>Thiếu hụt</b> = Nhu cầu + SafetyStock(*) − AvailableQuantity − ExpectedIncomingQuantity. <span class="muted">(*) Mockup chưa cộng SafetyStock – chờ BA chốt.</span></div></div></div>
    <div class="kpis">${UI.kpi('Số món trong kế hoạch', Store.by('ProductionPlanLine', 'ProductionPlanId', pl.Id).length, 'ProductionPlanLine', 'utensils', 'green')}${UI.kpi('Tổng suất', U.num(U.sum(Store.by('ProductionPlanLine', 'ProductionPlanId', pl.Id), 'PlannedQuantity')), 'Σ PlannedQuantity', 'clipboard', 'blue')}${UI.kpi('Nguyên liệu cần', reqs.length, 'MaterialRequirement', 'package', 'purple')}${UI.kpi('Thiếu hụt', short.length, short.length ? '<span class="bad">Cần mua</span>' : 'Đủ hàng', 'alert', short.length ? 'red' : 'green')}</div>
    <div class="card panel" id="mT"></div>`;
    el.querySelectorAll('[data-p]').forEach(b => b.onclick = () => { ms.plan = b.dataset.p; APP.refresh(); });
    UI.table(el.querySelector('#mT'), { table: 'MaterialRequirement', rows: reqs, pageSize: 20, sort: 'PurchaseQuantity', dir: -1, onRow: id => APP.openDetail('MaterialRequirement', id, f),
      cols: [{ k: 'IngredientId', label: 'Nguyên liệu' }, { k: 'RequiredQuantity', label: 'Nhu cầu', num: true }, { k: 'AvailableQuantity', label: 'Tồn khả dụng', num: true }, { k: 'ExpectedIncomingQuantity', label: 'Hàng đang về', num: true }, { k: 'PurchaseQuantity', label: 'Cần mua', num: true, render: r => r.PurchaseQuantity > 0 ? `<b class="bad">${U.num(r.PurchaseQuantity, 2)}</b>` : '<span class="up">0</span>' }, { k: 'UnitId', label: 'ĐVT' },
        { k: 'cov', label: 'Độ phủ', col: false, nosort: true, render: r => { const p = r.RequiredQuantity ? Math.min(100, r.AvailableQuantity / r.RequiredQuantity * 100) : 100; return `<div class="track" style="width:110px"><div class="fill ${p >= 100 ? '' : p > 50 ? 'orange' : 'red'}" style="width:${p}%"></div></div>`; } }, { k: 'Status', label: 'Trạng thái' }] });
    el.querySelector('#mCalc').onclick = () => {
      const need = {};
      Store.by('ProductionPlanLine', 'ProductionPlanId', pl.Id).forEach(pll => { const rc = Store.by('Recipe', 'DishId', pll.DishId).find(r => r.IsActive) || Store.by('Recipe', 'DishId', pll.DishId)[0]; if (!rc) return; Store.by('RecipeLine', 'RecipeId', rc.Id).forEach(l => { const g = ing(l.IngredientId); need[l.IngredientId] = (need[l.IngredientId] || 0) + l.Quantity * pll.PlannedQuantity / 100 / ((g.YieldPercent || 100) / 100); }); });
      let n = 0;
      Object.entries(need).forEach(([iid, q]) => { const av = Math.max(0, avail(iid)); const ex = reqs.find(r => r.IngredientId === iid); const pq = Math.max(0, +(q - av).toFixed(3)); const st = ex && ex.Status === 'Requested' ? 'Requested' : pq > 0 ? 'Shortage' : 'Sufficient'; if (ex) Store.update('MaterialRequirement', ex.Id, { RequiredQuantity: +q.toFixed(3), AvailableQuantity: +av.toFixed(3), PurchaseQuantity: pq, Status: st }); else { Store.add('MaterialRequirement', { RequirementNo: 'NCVT-' + pl.PlanNumber.slice(5, 11) + '-' + (++n), ProductionPlanId: pl.Id, IngredientId: iid, RequiredQuantity: +q.toFixed(3), AvailableQuantity: +av.toFixed(3), ExpectedIncomingQuantity: 0, PurchaseQuantity: pq, UnitId: ing(iid).BaseUnitId, Status: st }); } });
      UI.toast(`Đã tính ${Object.keys(need).length} nguyên liệu từ ${Store.by('ProductionPlanLine', 'ProductionPlanId', pl.Id).length} món (MaterialRequirement · DEMO)`, 'ok'); APP.refresh();
    };
    el.querySelector('#mPR').onclick = () => {
      const s = short.filter(r => r.Status === 'Shortage');
      UI.confirm(`Tạo <b>Đề nghị mua</b> cho ${s.length} nguyên liệu thiếu và gửi phê duyệt 2 cấp (Quản lý kho → Ban giám đốc)?`, () => {
        const n = Store.rows('PurchaseRequest').length + 1;
        const emp = Store.rows('Employee').find(e => Store.label('Position', e.PositionId) === 'Nhân viên kho');
        const pr = Store.add('PurchaseRequest', { RequestNumber: 'DNM-2611-' + String(n).padStart(3, '0'), RequestDate: U.iso(new Date()), RequiredDate: U.day(pl.ProductionDate), RequestedByEmployeeId: emp && emp.Id, Status: 'PendingApproval', Note: 'Sinh từ ' + pl.PlanNumber });
        s.forEach(r => { Store.add('PurchaseRequestLine', { PurchaseRequestId: pr.Id, IngredientId: r.IngredientId, Quantity: Math.ceil(r.PurchaseQuantity * 1.05), UnitId: r.UnitId, RequiredDate: pr.RequiredDate }); Store.update('MaterialRequirement', r.Id, { Status: 'Requested' }); });
        APP.submitApproval('PURCHASE_REQUEST', 'PurchaseRequest', pr.Id, [['Quản lý kho'], ['Ban giám đốc']], 'Duyệt đề nghị mua ' + pr.RequestNumber);
        APP.refresh();
      }, { ok: 'Tạo & gửi duyệt' });
    };
  });
  // ---------- Đề nghị mua ----------
  APP.register('scm-pr', (el, f) => {
    APP.genericList(el, f, { noTransit: true, cols: [{ k: 'RequestNumber', label: 'Số đề nghị' }, { k: 'RequestDate', label: 'Ngày' }, { k: 'RequiredDate', label: 'Ngày cần' }, { k: 'RequestedByEmployeeId', label: 'Người đề nghị', render: r => esc(Store.label('Employee', r.RequestedByEmployeeId)) }, { k: 'n', label: 'Số dòng', col: false, num: true, render: r => Store.by('PurchaseRequestLine', 'PurchaseRequestId', r.Id).length }, { k: 'v', label: 'Giá trị ước tính', col: false, num: true, render: r => U.money(U.sum(Store.by('PurchaseRequestLine', 'PurchaseRequestId', r.Id), l => l.Quantity * prefPrice(l.IngredientId))) }, { k: 'Status', label: 'Trạng thái' }] });
    if (Store.isLive) return prLive(el);
    APP.rowExtra(el, tr => { const r = Store.get('PurchaseRequest', tr.dataset.row); const td = tr.querySelector('.actions');
      if (r.Status === 'Draft') { td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-s>Gửi duyệt</button> `); td.querySelector('[data-s]').onclick = () => { Store.update('PurchaseRequest', r.Id, { Status: 'PendingApproval' }); APP.submitApproval('PURCHASE_REQUEST', 'PurchaseRequest', r.Id, [['Quản lý kho'], ['Ban giám đốc']], 'Duyệt ' + r.RequestNumber); APP.refresh(); }; }
      if (r.Status === 'Approved') { td.insertAdjacentHTML('afterbegin', `<button class="btn xs primary" data-po>Lập PO</button> `); td.querySelector('[data-po]').onclick = () => makePO(r); }
    });
  });
  const baseOpen = APP.openDetail;
  APP.openDetail = (t, id, f, opt) => {
    if (t === 'PurchaseRequest' || t === 'PurchaseOrder' || t === 'GoodsReceipt' || t === 'StockCount' || t === 'MaterialIssue') {
      const lt = { PurchaseRequest: ['PurchaseRequestLine', 'PurchaseRequestId'], PurchaseOrder: ['PurchaseOrderLine', 'PurchaseOrderId'], GoodsReceipt: ['GoodsReceiptLine', 'GoodsReceiptId'], StockCount: ['StockCountLine', 'StockCountId'], MaterialIssue: ['MaterialIssueLine', 'MaterialIssueId'] }[t];
      const lines = Store.by(lt[0], lt[1], id); const cols = APP.pickCols(lt[0], 9).filter(c => c.k !== lt[1]);
      return UI.detail(t, id, { screen: f && f.id, onChange: () => APP.refresh(), top: '', tabs: [{ label: `Chi tiết dòng (${lines.length})`, html: `<div class="table-wrap"><table class="dt"><thead><tr>${cols.map(c => `<th class="${c.num ? 'num' : ''}">${c.label}<small>${c.k}</small></th>`).join('')}</tr></thead><tbody>${lines.map(l => `<tr>${cols.map(c => `<td class="${c.num ? 'num' : ''}">${UI.val(lt[0], c.k, l[c.k], l)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>${Store.isLive ? '' : `<div class="small muted mt">dbo.${lt[0]} · ${lt[1]} = ${id.slice(0, 8)}…</div>`}` }] });
    }
    return baseOpen(t, id, f, opt);
  };
  function makePO(pr) {
    const lines = Store.by('PurchaseRequestLine', 'PurchaseRequestId', pr.Id);
    const bySup = U.group(lines, l => { const s = Store.rows('SupplierMaterial').find(x => x.IngredientId === l.IngredientId && x.IsPreferred); return s ? s.SupplierId : 'none'; });
    UI.confirm(`Lập ${Object.keys(bySup).length} đơn mua theo nhà cung cấp ưu tiên (SupplierMaterial.IsPreferred = 1)?<ul class="small">${Object.entries(bySup).map(([s, ls]) => `<li>${esc(Store.label('Supplier', s))}: ${ls.length} dòng</li>`).join('')}</ul>`, () => {
      Object.entries(bySup).forEach(([s, ls]) => {
        const n = Store.rows('PurchaseOrder').length + 1;
        const po = Store.add('PurchaseOrder', { PurchaseOrderNumber: 'PO-2611-' + String(n).padStart(3, '0'), SupplierId: s === 'none' ? null : s, PurchaseRequestId: pr.Id, OrderDate: U.iso(new Date()), ExpectedDeliveryDate: pr.RequiredDate + 'T05:00', Status: 'Draft' });
        ls.forEach(l => { const p = prefPrice(l.IngredientId); Store.add('PurchaseOrderLine', { PurchaseOrderId: po.Id, IngredientId: l.IngredientId, Quantity: l.Quantity, UnitPrice: p, LineAmount: l.Quantity * p, UnitId: l.UnitId }); });
        po.TotalAmount = U.sum(Store.by('PurchaseOrderLine', 'PurchaseOrderId', po.Id), 'LineAmount');
      });
      Store.update('PurchaseRequest', pr.Id, { Status: 'Converted' }); UI.toast('Đã lập đơn mua (PurchaseOrder · DEMO)', 'ok'); APP.go('scm-po');
    }, { ok: 'Lập PO' });
  }
  // ---------- Đơn mua: nhận hàng ----------
  APP.register('scm-po', (el, f) => {
    APP.genericList(el, f, { cols: [{ k: 'PurchaseOrderNumber', label: 'Số PO' }, { k: 'SupplierId', label: 'Nhà cung cấp' }, { k: 'PurchaseRequestId', label: 'Đề nghị mua' }, { k: 'OrderDate', label: 'Ngày đặt' }, { k: 'ExpectedDeliveryDate', label: 'Giao dự kiến' }, { k: 'TotalAmount', label: 'Giá trị', num: true }, { k: 'Status', label: 'Trạng thái' }] });
    if (Store.isLive) return poLive(el);
    APP.rowExtra(el, tr => { const r = Store.get('PurchaseOrder', tr.dataset.row); const td = tr.querySelector('.actions');
      if (['Sent', 'Approved'].includes(r.Status)) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs primary" data-gr>Nhận hàng</button> `); td.querySelector('[data-gr]').onclick = () => receive(r); } });
  });
  function receive(po) {
    const n = Store.rows('GoodsReceipt').length + 1;
    const gr = Store.add('GoodsReceipt', { ReceiptNumber: 'PN-2611-' + String(n).padStart(3, '0'), PurchaseOrderId: po.Id, SupplierId: po.SupplierId, WarehouseId: APP.rawWarehouseId(), ReceiptDate: U.iso(new Date()), Status: 'QCPending' });
    Store.by('PurchaseOrderLine', 'PurchaseOrderId', po.Id).forEach(l => { const g = ing(l.IngredientId); Store.add('GoodsReceiptLine', { GoodsReceiptId: gr.Id, IngredientId: l.IngredientId, Quantity: l.Quantity, ReceivedQuantity: l.Quantity, AcceptedQuantity: 0, RejectedQuantity: 0, UnitId: l.UnitId, UnitCost: l.UnitPrice, LotNumber: 'LOT-' + Store.ctx.date.slice(2).replace(/-/g, '') + '-' + g.Code, ManufactureDate: U.addDays(Store.ctx.date, -1) + 'T00:00', ExpiryDate: U.addDays(Store.ctx.date, g.ShelfLifeDays || 3) + 'T00:00' }); });
    const qi = Store.add('QCInspection', { GoodsReceiptId: gr.Id, InspectionNumber: 'QC-IN-' + String(n).padStart(3, '0'), InspectionDate: U.iso(new Date()), Note: 'Chờ kiểm tra' });
    Store.by('GoodsReceiptLine', 'GoodsReceiptId', gr.Id).forEach(l => Store.add('QCInspectionLine', { QCInspectionId: qi.Id, IngredientId: l.IngredientId, GoodsReceiptLineId: l.Id, Weight: l.ReceivedQuantity, ExpiryDate: l.ExpiryDate, Criterion: 'Cảm quan / nhiệt độ / HSD' }));
    Store.update('PurchaseOrder', po.Id, { Status: 'PartiallyReceived' });
    UI.toast(`Đã tạo phiếu nhập ${esc(gr.ReceiptNumber)} + phiếu QC – chờ QC kết luận`, 'ok'); APP.go('qc-inspection?id=' + qi.Id);
  }
  // ---------- Nhập kho: cổng QC ----------
  APP.register('scm-gr', (el, f) => {
    APP.genericList(el, f, { noTransit: true, top: Store.isLive ? `<div class="notice info">${I('shield')}<div><b>Cổng kiểm soát QC</b>Chỉ ghi sổ được sau khi phiếu QC đầu vào đã kết luận. Ghi sổ tạo lô và nhập kho phần đạt.</div></div>` : `<div class="notice info">${I('shield')}<div><b>Cổng kiểm soát QC (Luồng 3)</b>Chỉ được <b>Ghi sổ</b> khi phiếu QC đầu vào có kết quả PASS cho các dòng. Ghi sổ sinh <code>MaterialLot</code> + <code>InventoryTransaction</code> (RECEIPT) và cập nhật <code>InventoryBalance</code>.</div></div>`,
      cols: [{ k: 'ReceiptNumber', label: 'Số phiếu' }, { k: 'SupplierId', label: 'Nhà cung cấp' }, { k: 'PurchaseOrderId', label: 'PO' }, { k: 'WarehouseId', label: 'Kho' }, { k: 'ReceiptDate', label: 'Ngày nhập' }, { k: 'qc', label: 'Kết quả QC', col: false, nosort: true, render: r => { const q = Store.one('QCInspection', 'GoodsReceiptId', r.Id); return q ? (q.Result ? UI.badge(q.Result, 'QCInspection', 'Result') : '<span class="badge orange">Đang kiểm</span>') : '<span class="muted">–</span>'; } }, { k: 'Status', label: 'Trạng thái' }] });
    if (Store.isLive) return grLive(el);
    APP.rowExtra(el, tr => { const r = Store.get('GoodsReceipt', tr.dataset.row); const td = tr.querySelector('.actions'); if (['QCPending', 'Accepted', 'PartiallyAccepted'].includes(r.Status)) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs primary" data-post>Ghi sổ</button> `); td.querySelector('[data-post]').onclick = () => post(r); } });
  });
  function post(gr) {
    const qi = Store.one('QCInspection', 'GoodsReceiptId', gr.Id);
    if (!qi || !qi.Result) return UI.confirm('Phiếu nhập chưa có kết luận QC (QCInspection.Result). Mở phiếu QC để kết luận?', () => APP.go('qc-inspection?id=' + (qi && qi.Id)), { title: 'Bị chặn bởi cổng QC', ok: 'Mở phiếu QC' });
    if (qi.Result === 'REJECT') return UI.toast('QC kết luận REJECT – không thể ghi sổ. Lập sự cố NCC.', 'err');
    const lines = Store.by('GoodsReceiptLine', 'GoodsReceiptId', gr.Id);
    const ok = lines.filter(l => { const ql = Store.one('QCInspectionLine', 'GoodsReceiptLineId', l.Id); return ql && ql.Result === 'PASS'; });
    UI.confirm(`Ghi sổ ${ok.length}/${lines.length} dòng đạt QC (PASS). Dòng HOLD/REJECT không nhập kho.`, () => {
      const loc = Store.rows('StorageLocation').find(x => x.WarehouseId === gr.WarehouseId);
      ok.forEach(l => {
        Store.update('GoodsReceiptLine', l.Id, { AcceptedQuantity: l.ReceivedQuantity, QualityStatus: 'PASS' });
        const lot = Store.add('MaterialLot', { LotNumber: l.LotNumber, IngredientId: l.IngredientId, SupplierId: gr.SupplierId, GoodsReceiptLineId: l.Id, ManufactureDate: l.ManufactureDate, ExpiryDate: l.ExpiryDate, ReceivedDate: gr.ReceiptDate, UnitCost: l.UnitCost, QcStatus: 'PASS', Status: 'Available' });
        Store.add('InventoryTransaction', { TransactionNumber: 'GD-' + gr.ReceiptNumber + '-' + ing(l.IngredientId).Code, TransactionType: 'RECEIPT', WarehouseId: gr.WarehouseId, StorageLocationId: loc && loc.Id, IngredientId: l.IngredientId, MaterialLotId: lot.Id, ReferenceType: 'GoodsReceipt', ReferenceId: gr.Id, Quantity: l.ReceivedQuantity, QuantityIn: l.ReceivedQuantity, QuantityOut: 0, UnitId: l.UnitId, UnitCost: l.UnitCost, TransactionDate: U.iso(new Date()) });
        Store.add('InventoryBalance', { WarehouseId: gr.WarehouseId, StorageLocationId: loc && loc.Id, IngredientId: l.IngredientId, MaterialLotId: lot.Id, OnHandQuantity: l.ReceivedQuantity, ReservedQuantity: 0, AvailableQuantity: l.ReceivedQuantity, UnitId: l.UnitId, UpdatedDate: U.iso(new Date()) });
      });
      Store.update('GoodsReceipt', gr.Id, { Status: 'Posted' });
      UI.toast(`Đã ghi sổ ${ok.length} lô – sinh MaterialLot, InventoryTransaction (RECEIPT), InventoryBalance`, 'ok'); APP.refresh();
    }, { ok: 'Ghi sổ nhập kho' });
  }
  // ---------- Tồn kho (không sửa trực tiếp) ----------
  APP.register('scm-inv-balance', (el, f) => {
    const pol = Store.rows('InventoryPolicie');
    const agg = Store.rows('Ingredient').map(g => { const bs = Store.by('InventoryBalance', 'IngredientId', g.Id); const p = pol.find(x => x.IngredientId === g.Id) || {}; return { Id: g.Id, g, onh: U.sum(bs, 'OnHandQuantity'), res: U.sum(bs, 'ReservedQuantity'), av: U.sum(bs, 'AvailableQuantity'), lots: bs.length, min: p.MinimumStock, safety: p.SafetyStock, rop: p.ReorderPoint, near: Store.by('MaterialLot', 'IngredientId', g.Id).filter(l => l.Status === 'Available' && U.day(l.ExpiryDate) <= U.addDays(Store.ctx.date, p.ExpiryWarningDays || 3)).length }; });
    el.innerHTML = APP.header(f, `<a class="btn" href="#/scm-inv-txn">${I('history')} Sổ cái giao dịch</a><a class="btn" href="#/scm-stockcount">${I('check')} Kiểm kê</a>`) + `
    <div class="notice info">${I('database')}<div><b>Quy tắc BRD:</b> Inventory Balance được tính từ ledger <code>InventoryTransaction</code>, không phải nguồn ghi biến động gốc → màn hình <b>không có nút Sửa</b>. Điều chỉnh tồn qua Kiểm kê / phiếu điều chỉnh (GAP-F-04).</div></div>
    <div class="kpis">${UI.kpi('Mã nguyên liệu có tồn', agg.filter(a => a.onh > 0).length, 'InventoryBalance', 'package', 'green')}${UI.kpi('Dưới tồn tối thiểu', agg.filter(a => a.min && a.av < a.min).length, 'AvailableQuantity < InventoryPolicie.MinimumStock', 'alert', 'red')}${UI.kpi('Dưới điểm đặt hàng', agg.filter(a => a.rop && a.av < a.rop).length, 'ReorderPoint', 'down', 'orange')}${UI.kpi('Lô sắp hết hạn', U.sum(agg, 'near'), 'ExpiryWarningDays (FEFO)', 'lot', 'purple', 'scm-lot')}</div>
    <div class="card panel" id="bT"></div>`;
    UI.table(el.querySelector('#bT'), { rows: agg, pageSize: 15, sort: 'ratio', onRow: id => lotsOf(id),
      cols: [{ k: 'code', label: 'Mã', render: r => esc(r.g.Code), sortv: r => r.g.Code }, { k: 'name', label: 'Nguyên liệu', render: r => esc(r.g.Name), sortv: r => r.g.Name }, { k: 'onh', label: 'Tồn thực (OnHand)', num: true, render: r => U.num(r.onh, 2) }, { k: 'res', label: 'Đã giữ (Reserved)', num: true, render: r => U.num(r.res, 2) }, { k: 'av', label: 'Khả dụng', num: true, render: r => `<b>${U.num(r.av, 2)}</b>` }, { k: 'u', label: 'ĐVT', render: r => esc(Store.label('UnitMeasure', r.g.BaseUnitId)) }, { k: 'min', label: 'Min / Safety / ROP', render: r => `${U.num(r.min)} / ${U.num(r.safety)} / ${U.num(r.rop)}` },
        { k: 'ratio', label: 'Mức tồn', sortv: r => r.min ? r.av / r.min : 99, render: r => { const p = r.min ? Math.min(100, r.av / (r.min * 3) * 100) : 100; return `<div class="track" style="width:120px"><div class="fill ${r.av < r.min ? 'red' : r.av < r.rop ? 'orange' : ''}" style="width:${p}%"></div></div>`; } }, { k: 'lots', label: 'Số lô', num: true }, { k: 'near', label: 'Sắp hết hạn', num: true, render: r => r.near ? `<span class="bad">${r.near}</span>` : '0' }] });
  });
  function lotsOf(iid) {
    const g = ing(iid);
    UI.modal({ title: 'Tồn theo lô – ' + esc(g.Name), sub: 'InventoryBalance × MaterialLot · sắp xếp FEFO (hạn dùng sớm xuất trước)', size: 'lg', body: `<table class="dt"><thead><tr><th>Lô</th><th>Kho / vị trí</th><th>HSD</th><th class="num">Tồn thực</th><th class="num">Đã giữ</th><th class="num">Khả dụng</th><th>QC</th></tr></thead><tbody>${Store.by('InventoryBalance', 'IngredientId', iid).map(b => ({ b, l: Store.get('MaterialLot', b.MaterialLotId) || {} })).sort((x, y) => (x.l.ExpiryDate || '').localeCompare(y.l.ExpiryDate || '')).map(({ b, l }, i) => `<tr><td><a href="#/scm-lot?id=${l.Id}">${esc(l.LotNumber)}</a>${i === 0 ? ' <span class="tag green">FEFO</span>' : ''}</td><td class="small">${esc(Store.label('Warehouse', b.WarehouseId))} · ${esc(Store.label('StorageLocation', b.StorageLocationId))}</td><td>${U.date(l.ExpiryDate)}</td><td class="num">${U.num(b.OnHandQuantity, 2)}</td><td class="num">${U.num(b.ReservedQuantity, 2)}</td><td class="num"><b>${U.num(b.AvailableQuantity, 2)}</b></td><td>${UI.badge(l.QcStatus, 'MaterialLot', 'QcStatus')}</td></tr>`).join('') || '<tr><td colspan="7" class="empty">Không có tồn</td></tr>'}</tbody></table>` });
  }
  // ---------- Lô & truy xuất nguồn gốc ----------
  APP.register('scm-lot', (el, f, params) => {
    APP.genericList(el, f, { noAdd: true, top: `<div class="notice info">${I('link')}<div><b>Truy xuất nguồn gốc theo lô (GAP-F-10: chưa có mã QR)</b>Bấm "Truy xuất" để xem chuỗi NCC → Phiếu nhập → QC → Lô → Xuất kho → Lệnh SX → Mẻ → Đóng gói → Giao hàng → Khách hàng, dựng từ các quan hệ logic hiện có.</div></div>`,
      cols: [{ k: 'LotNumber', label: 'Số lô' }, { k: 'IngredientId', label: 'Nguyên liệu' }, { k: 'SupplierId', label: 'NCC' }, { k: 'ReceivedDate', label: 'Ngày nhận' }, { k: 'ExpiryDate', label: 'HSD', render: r => `<span class="${U.day(r.ExpiryDate) <= U.addDays(Store.ctx.date, 2) && r.Status === 'Available' ? 'bad' : ''}">${U.date(r.ExpiryDate)}</span>` }, { k: 'QcStatus', label: 'QC' }, { k: 'Status', label: 'Trạng thái' }] });
    APP.rowExtra(el, tr => { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs primary" data-tr="${tr.dataset.row}">Truy xuất</button> `); td.querySelector('[data-tr]').onclick = () => trace(tr.dataset.row); });
    if (params.id) setTimeout(() => trace(params.id), 50);
  });
  function trace(lid) {
    const l = Store.get('MaterialLot', lid); if (!l) return;
    const gl = Store.get('GoodsReceiptLine', l.GoodsReceiptLineId), gr = gl && Store.get('GoodsReceipt', gl.GoodsReceiptId), po = gr && Store.get('PurchaseOrder', gr.PurchaseOrderId);
    const ql = gl && Store.one('QCInspectionLine', 'GoodsReceiptLineId', gl.Id);
    const iss = Store.by('MaterialIssueLine', 'MaterialLotId', lid).map(x => ({ x, mi: Store.get('MaterialIssue', x.MaterialIssueId) }));
    const pos = [...new Set(iss.map(i => i.mi && i.mi.ProductionOrderId))].map(id => Store.get('ProductionOrder', id)).filter(Boolean);
    const bts = pos.flatMap(p => Store.by('ProductionBatche', 'ProductionOrderId', p.Id));
    const pks = bts.flatMap(b => Store.by('PackingOrder', 'ProductionBatchId', b.Id));
    const dvs = pks.flatMap(p => Store.by('DeliveryOrder', 'OrderId', p.OrderId));
    const cus = [...new Set(dvs.map(d => d.CustomerId))];
    const node = (t, title, items) => `<div class="tl ${items.length ? '' : 'orange'}"><b>${title} <span class="muted small">dbo.${t}</span></b><small>${items.length ? items.slice(0, 6).map(r => `<a href="javascript:void 0" data-open="${t}" data-id="${r.Id}">${esc(Store.label(t, r))}</a>`).join(' · ') + (items.length > 6 ? ` … +${items.length - 6}` : '') : 'Không có dữ liệu'}</small></div>`;
    UI.modal({ title: 'Truy xuất lô ' + esc(l.LotNumber), sub: esc(Store.label('Ingredient', l.IngredientId)) + ' · HSD ' + U.date(l.ExpiryDate), size: 'lg', body: `<div class="timeline">${node('Supplier', 'Nhà cung cấp', [Store.get('Supplier', l.SupplierId)].filter(Boolean))}${node('PurchaseOrder', 'Đơn mua', [po].filter(Boolean))}${node('GoodsReceipt', 'Phiếu nhập', [gr].filter(Boolean))}${node('QCInspectionLine', 'Kiểm tra QC đầu vào: ' + (ql ? ql.Result + ' · ' + U.num(ql.Temperature) + '°C' : '–'), [ql].filter(Boolean))}${node('MaterialLot', 'Lô nguyên liệu', [l])}${node('MaterialIssue', 'Phiếu xuất cho sản xuất', iss.map(i => i.mi).filter(Boolean))}${node('ProductionOrder', 'Lệnh sản xuất', pos)}${node('ProductionBatche', 'Mẻ sản xuất', bts)}${node('PackingOrder', 'Lệnh đóng gói', pks)}${node('DeliveryOrder', 'Đơn giao hàng', dvs)}${node('Customer', 'Khách hàng nhận suất ăn', cus.map(c => Store.get('Customer', c)))}</div>` });
  }
  // ---------- Kiểm kê: duyệt & ghi sổ chênh lệch ----------
  APP.register('scm-stockcount', (el, f) => {
    APP.genericList(el, f, { noTransit: true, cols: [{ k: 'CountNumber', label: 'Số phiếu' }, { k: 'WarehouseId', label: 'Kho' }, { k: 'CountType', label: 'Loại' }, { k: 'CountDate', label: 'Ngày KK' }, { k: 'ResponsibleEmployeeId', label: 'Phụ trách', render: r => esc(Store.label('Employee', r.ResponsibleEmployeeId)) }, { k: 'v', label: 'Giá trị chênh lệch', col: false, num: true, render: r => { const v = U.sum(Store.by('StockCountLine', 'StockCountId', r.Id), 'VarianceValue'); return `<span class="${v < 0 ? 'bad' : ''}">${U.money(v)}</span>`; } }, { k: 'Status', label: 'Trạng thái' }] });
    APP.rowExtra(el, tr => { const r = Store.get('StockCount', tr.dataset.row); const td = tr.querySelector('.actions'); if (r.Status === 'PendingApproval' && r.ApprovedBy) { td.insertAdjacentHTML('afterbegin', `<button class="btn xs primary" data-p>Ghi sổ</button> `); td.querySelector('[data-p]').onclick = () => postCount(r); } if (r.Status === 'PendingApproval' && !r.ApprovedBy) td.insertAdjacentHTML('afterbegin', `<a class="btn xs" href="#/wf-inbox">Chờ duyệt →</a> `); });
  });
  function postCount(sc) {
    const ls = Store.by('StockCountLine', 'StockCountId', sc.Id).filter(l => l.VarianceQuantity);
    if (ls.some(l => !l.VarianceReason)) return UI.toast('Dòng có chênh lệch bắt buộc có VarianceReason', 'err');
    ls.forEach(l => { Store.add('InventoryTransaction', { TransactionNumber: 'GD-' + sc.CountNumber + '-' + ing(l.IngredientId).Code, TransactionType: 'ADJUSTMENT', WarehouseId: sc.WarehouseId, IngredientId: l.IngredientId, MaterialLotId: l.MaterialLotId, ReferenceType: 'StockCount', ReferenceId: sc.Id, Quantity: l.VarianceQuantity, QuantityIn: Math.max(0, l.VarianceQuantity), QuantityOut: Math.max(0, -l.VarianceQuantity), UnitId: l.UnitId, TransactionDate: U.iso(new Date()), Reason: l.VarianceReason }); const b = Store.rows('InventoryBalance').find(x => x.MaterialLotId === l.MaterialLotId); if (b) Store.update('InventoryBalance', b.Id, { OnHandQuantity: +(b.OnHandQuantity + l.VarianceQuantity).toFixed(3), AvailableQuantity: +(b.AvailableQuantity + l.VarianceQuantity).toFixed(3) }); });
    Store.update('StockCount', sc.Id, { Status: 'Posted' }); UI.toast(`Đã ghi sổ ${ls.length} dòng ADJUSTMENT`, 'ok'); APP.refresh();
  }
  // ---------- Xuất kho cho sản xuất: theo FEFO ----------
  APP.register('scm-issue', (el, f) => {
    APP.genericList(el, f, { cols: [{ k: 'IssueNumber', label: 'Số phiếu' }, { k: 'ProductionOrderId', label: 'Lệnh SX' }, { k: 'WarehouseId', label: 'Kho' }, { k: 'IssueDate', label: 'Ngày xuất' }, { k: 'n', label: 'Số dòng', col: false, num: true, render: r => Store.by('MaterialIssueLine', 'MaterialIssueId', r.Id).length }, { k: 'Status', label: 'Trạng thái' }], noAdd: true,
      top: `<div class="card panel mb"><div class="spread"><div><div class="panel-title">Lệnh sản xuất đã giữ chỗ – chờ xuất kho</div><div class="panel-desc">MaterialReservation (Reserved) → MaterialIssue + MaterialIssueLine theo FEFO → InventoryTransaction (ISSUE)</div></div></div><div class="row-flex mt">${Store.rows('ProductionOrder').filter(p => Store.by('MaterialReservation', 'ProductionOrderId', p.Id).some(r => r.Status === 'Reserved')).slice(0, 12).map(p => `<button class="btn sm" data-iss="${p.Id}">${esc(p.ProductionOrderNumber)} · ${esc(Store.label('Dishe', p.DishId))}</button>`).join('') || '<span class="muted small">Không có lệnh chờ xuất</span>'}</div></div>` });
    el.querySelectorAll('[data-iss]').forEach(b => b.onclick = () => issue(b.dataset.iss));
  });
  function issue(poid) {
    const po = Store.get('ProductionOrder', poid), res = Store.by('MaterialReservation', 'ProductionOrderId', poid).filter(r => r.Status === 'Reserved');
    const plan = res.map(r => { let need = r.ReservedQuantity; const picks = []; Store.by('InventoryBalance', 'IngredientId', r.IngredientId).map(b => ({ b, l: Store.get('MaterialLot', b.MaterialLotId) })).filter(x => x.l && x.l.QcStatus === 'PASS' && x.l.Status === 'Available').sort((a, b) => (a.l.ExpiryDate || '').localeCompare(b.l.ExpiryDate || '')).forEach(x => { if (need <= 0) return; const q = Math.min(need, x.b.OnHandQuantity); if (q > 0) { picks.push({ x, q }); need -= q; } }); return { r, picks, short: Math.max(0, need) }; });
    UI.modal({ title: 'Xuất kho cho ' + esc(po.ProductionOrderNumber), sub: 'Đề xuất lô theo FEFO (InventoryPolicie.UseFefo = 1)', size: 'lg', body: `<table class="dt"><thead><tr><th>Nguyên liệu</th><th class="num">Đã giữ</th><th>Lô đề xuất (FEFO)</th><th class="num">Thiếu</th></tr></thead><tbody>${plan.map(p => `<tr><td>${esc(Store.label('Ingredient', p.r.IngredientId))}</td><td class="num">${U.num(p.r.ReservedQuantity, 3)}</td><td class="small">${p.picks.map(k => `${esc(k.x.l.LotNumber)} (HSD ${U.date(k.x.l.ExpiryDate)}): <b>${U.num(k.q, 3)}</b>`).join('<br>') || '<span class="bad">Không có lô đạt QC</span>'}</td><td class="num ${p.short ? 'bad' : ''}">${U.num(p.short, 3)}</td></tr>`).join('')}</tbody></table>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="doIss">Xác nhận xuất kho</button>',
      onMount: (m, close) => m.querySelector('#doIss').onclick = () => {
        const n = Store.rows('MaterialIssue').length + 1; const mi = Store.add('MaterialIssue', { IssueNumber: 'PX-' + String(n).padStart(4, '0'), ProductionOrderId: poid, WarehouseId: APP.rawWarehouseId(), IssueDate: U.iso(new Date()), Status: 'Issued' });
        plan.forEach(p => { p.picks.forEach(k => { Store.add('MaterialIssueLine', { MaterialIssueId: mi.Id, IngredientId: p.r.IngredientId, Quantity: +k.q.toFixed(3), MaterialLotId: k.x.l.Id, UnitId: p.r.UnitId }); Store.add('InventoryTransaction', { TransactionNumber: 'GD-' + mi.IssueNumber + '-' + ing(p.r.IngredientId).Code, TransactionType: 'ISSUE', WarehouseId: k.x.b.WarehouseId, StorageLocationId: k.x.b.StorageLocationId, IngredientId: p.r.IngredientId, MaterialLotId: k.x.l.Id, ReferenceType: 'MaterialIssue', ReferenceId: mi.Id, Quantity: -k.q, QuantityIn: 0, QuantityOut: +k.q.toFixed(3), UnitId: p.r.UnitId, UnitCost: k.x.l.UnitCost, TransactionDate: U.iso(new Date()) }); Store.update('InventoryBalance', k.x.b.Id, { OnHandQuantity: +(k.x.b.OnHandQuantity - k.q).toFixed(3), ReservedQuantity: Math.max(0, +(k.x.b.ReservedQuantity - k.q).toFixed(3)) }); }); Store.update('MaterialReservation', p.r.Id, { Status: p.short ? 'PartiallyIssued' : 'Issued' }); });
        if (po.Status === 'Released') Store.update('ProductionOrder', poid, { Status: 'InProgress', ActualStartDate: U.iso(new Date()) });
        close(); UI.toast(`Đã xuất kho ${esc(mi.IssueNumber)} – InventoryTransaction ISSUE theo FEFO`, 'ok'); APP.refresh();
      } });
  }
})();
