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
  const run = (path, form, okMsg) => Store.act(path, { form, tables: PUR_TABLES }).then(r => { if (r.ok) { invDirty(); const m = r.res && r.res.responseMessage; UI.toast(esc(m && m !== 'Lưu thông tin thành công' ? m : okMsg), 'ok'); APP.refresh(); } return r; });
  const askReason = (title, fn) => UI.confirm(title, note => fn(note), { input: 'Lý do', required: true, ok: 'Xác nhận', danger: true });
  const rowBtn = (td, cls, text, fn) => { const b = document.createElement('button'); b.className = 'btn xs ' + cls; b.textContent = text; b.onclick = e => { e.stopPropagation(); fn(); }; td.insertBefore(document.createTextNode(' '), td.firstChild); td.insertBefore(b, td.firstChild); };

  // ---------- MENU-4.7 (D-75 f3): lọc Đề nghị mua / Đơn mua / Phiếu nhập / Phiếu xuất theo nguyên liệu (thay tra cứu ở bảng con đã ẩn) ----------
  // Lọc ở máy chủ: <Ctrl>/GetAll?IngredientId= trả chứng từ có dòng chứa nguyên liệu + tổng số lượng; danh sách hiện thêm cột số lượng.
  const ingF = {};
  function ingList(el, f, opt) {
    if (!Store.isLive) return APP.genericList(el, f, opt);
    const t = f.table, s = ingF[f.id], out = Object.assign({}, opt);
    if (s && s.map) {
      out.rows = () => Store.rows(t).filter(r => s.map.has(r.Id));
      out.cols = (opt.cols || APP.pickCols(t)).concat([{ k: 'ingQty', label: 'SL nguyên liệu', num: true, col: false, sortv: r => (s.map.get(r.Id) || {}).q || 0, text: r => String((s.map.get(r.Id) || {}).q || ''), render: r => { const v = s.map.get(r.Id); return v ? '<b>' + U.num(v.q, 2) + '</b> ' + esc(v.u || '') : ''; } }]);
    }
    out.top = (opt.top || '') + `<div class="card panel mb"><div class="row-flex"><span class="small muted">Lọc theo nguyên liệu:</span><select class="select" data-ingf style="max-width:340px"><option value="">Tất cả nguyên liệu</option>${Store.rows('Ingredient').slice().sort((a, b) => String(a.Name).localeCompare(String(b.Name))).map(g => `<option value="${g.Id}" ${s && s.id === g.Id ? 'selected' : ''}>${esc(Store.label('Ingredient', g))}</option>`).join('')}</select>${s ? (s.map ? `<span class="small muted">${s.map.size} chứng từ có nguyên liệu này</span>` : '<span class="small muted">Đang lọc…</span>') : ''}</div></div>`;
    const tbl = APP.genericList(el, f, out);
    const sel = el.querySelector('[data-ingf]');
    if (sel) sel.onchange = () => {
      const id = sel.value; if (!id) { delete ingF[f.id]; return APP.refresh(); }
      ingF[f.id] = { id, map: null }; APP.refresh();
      Store.query(Store.controllerOf(t) + '/GetAll', { IngredientId: id, Page: 1, PageSize: 1000 }).then(x => {
        const cur = ingF[f.id]; if (!cur || cur.id !== id) return;
        if (!x.ok) { delete ingF[f.id]; UI.toast(esc(x.message || 'Không lọc được theo nguyên liệu'), 'err'); return APP.refresh(); }
        cur.map = new Map((x.list || []).map(r => [r.id, { q: r.ingredientQuantity, u: r.ingredientUnitName }])); if (APP.current === f.id) APP.refresh();
      });
    };
    if (!Store.rows('Ingredient').length) Store.ensure('Ingredient');
    return tbl;
  }

  // ---------- BIZ-1.3 (D-77 c): kế hoạch mua theo ngày cần hàng – D+1 → D+7, nguồn đơn chốt → đăng ký → dự báo, trừ tồn lũy kế ----------
  // MaterialRequirement/GetHorizon chỉ tính khi xem (không lưu); "Lập đề nghị mua cho phần phải đặt" → CreateHorizonPurchaseRequest (dòng theo ngày cần hàng).
  const hz = { data: null, busy: false };
  const HZ_TAG = { LATE: 'red', TODAY: 'orange', UPCOMING: 'blue', NONE: 'green' };
  const HZ_SRC = { LOCKED: 'green', CONFIRMED: 'blue', FORECAST: 'purple', MIXED: 'orange', NONE: '' };
  function horizonPanel(el) {
    if (!el) return;
    hz.host = el;   // màn vẽ lại nhiều lần khi đang tải – luôn vẽ vào khung mới nhất
    const draw = () => {
      const host = hz.host;
      if (!host || !host.isConnected) return;
      const x = hz.data;
      if (!x) { host.innerHTML = `<div class="card panel mb"><div class="panel-title">Kế hoạch mua 7 ngày tới</div><div class="muted small mt">Đang tính…</div></div>`; return; }
      if (!x.ok) { host.innerHTML = `<div class="notice gap">${I('alert')}<div><b>Không tính được kế hoạch mua</b>${esc(x.message || '')}</div></div>`; return; }
      const d = x.data || {}, days = d.listDay || [], items = d.listItem || [];
      const due = items.filter(i => ['LATE', 'TODAY'].includes(i.orderStatus) && i.totalShortage > 0);
      const dd = v => U.date(v).slice(0, 5);
      host.innerHTML = `<div class="card panel mb"><div class="panel-head"><div><div class="panel-title">Kế hoạch mua theo ngày cần hàng · ${dd(d.fromDate)} – ${dd(d.toDate)}</div>
          <div class="panel-desc">Mỗi ngày lấy đơn đã chốt; chưa chốt thì đăng ký / đơn đã xác nhận; chưa có thì dự báo đã công bố. Tồn kho, hàng sắp về và đề nghị mua đang mở được trừ dồn qua các ngày. Ngày phải đặt = ngày cần − thời gian giao của NCC − 1 ngày.</div></div>
          <div class="row-flex">${due.length ? `<button class="btn sm primary" id="hzPr">${I('plus')} Lập đề nghị mua cho phần phải đặt (${due.length})</button>` : ''}<button class="btn sm" id="hzReload">${I('reset')} Tính lại</button></div></div>
        <div class="kpis">${UI.kpi('Phải đặt hôm nay', U.num(d.dueTodayCount || 0), 'nguyên liệu', 'clipboard', d.dueTodayCount ? 'orange' : 'green')}${UI.kpi('Đã trễ ngày đặt', U.num(d.lateCount || 0), 'nguyên liệu', 'alert', d.lateCount ? 'red' : 'green')}${UI.kpi('Nguyên liệu trong kỳ', U.num(items.length), '', 'layers', 'blue')}</div>
        ${(d.listWarning || []).map(w => `<div class="notice warn small">${I('alert')}<div>${esc(w)}</div></div>`).join('')}
        <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Nguyên liệu</th>${days.map(x => `<th class="num" title="${esc((x.listWarning || []).join('\n'))}">${dd(x.date)}<span class="sub"><span class="tag ${HZ_SRC[x.source] || ''}">${esc(x.sourceName || '')}</span></span></th>`).join('')}<th class="num">Phải mua</th><th>ĐVT</th><th>Ngày phải đặt</th><th>NCC gợi ý</th></tr></thead>
          <tbody>${items.map(i => `<tr><td><b>${esc(i.ingredientName || '')}</b><span class="sub">${esc(i.ingredientCode || '')}${i.safetyStock ? ' · tồn an toàn ' + U.num(i.safetyStock, 2) : ''}</span></td>${days.map(x => { const c = (i.listDay || []).find(y => U.day(y.date) === U.day(x.date)); return `<td class="num">${c ? U.num(c.required, 2) : '<span class="muted">–</span>'}${c && c.shortage > 0 ? `<span class="sub bad">mua ${U.num(c.shortage, 2)}</span>` : ''}</td>`; }).join('')}<td class="num">${i.totalShortage > 0 ? `<b class="bad">${U.num(i.totalShortage, 2)}</b>` : '<span class="up">0</span>'}</td><td>${esc(i.unitName || '')}</td><td><span class="tag ${HZ_TAG[i.orderStatus] || ''}">${esc(i.orderStatusName || '')}</span></td><td>${i.supplierName ? esc(i.supplierName) + (i.leadTimeDays ? `<span class="sub">giao ${i.leadTimeDays} ngày</span>` : '') : '<span class="muted">Chưa có NCC</span>'}</td></tr>`).join('') || `<tr><td colspan="${days.length + 5}" class="empty">Không có nhu cầu nguyên liệu trong kỳ</td></tr>`}</tbody></table></div></div>`;
      host.querySelector('#hzReload').onclick = () => load(true);
      const b = host.querySelector('#hzPr');
      if (b) b.onclick = () => UI.confirm(`Lập đề nghị mua <b>Nháp</b> cho ${due.length} nguyên liệu phải đặt tới hôm nay? Mỗi dòng ghi ngày cần hàng; gửi duyệt ở màn Đề nghị mua.`, () =>
        Store.act('MaterialRequirement/CreateHorizonPurchaseRequest', { json: { onlyDue: true, organizationUnitId: Store.ctx.branch || null }, tables: PUR_TABLES }).then(r => {
          if (!r.ok) return; UI.toast(okText(r, 'Đã lập đề nghị mua nháp'), 'ok'); hz.data = { ok: true, data: r.data }; draw();
        }), { ok: 'Lập đề nghị mua' });
    };
    const load = force => {
      if (hz.busy) return; if (hz.data && !force) return draw();
      hz.busy = true; if (force) { hz.data = null; draw(); }
      Store.query('MaterialRequirement/GetHorizon', { organizationUnitId: Store.ctx.branch }).then(x => { hz.busy = false; hz.data = x; draw(); });
    };
    draw(); load(false);
  }

  // ---------- Nhu cầu nguyên liệu: kết quả do máy chủ tính theo ngày sản xuất ----------
  const rq = { date: null, data: {}, busy: {} };
  function reqLive(el, f) {
    if (!rq.date) rq.date = U.addDays(Store.ctx.date, 1);
    const date = rq.date;
    el.innerHTML = APP.header(f, `<input type="date" class="input" id="rqDate" value="${date}" style="width:auto"><button class="btn primary" id="rqCalc">${I('calc')} Tính nhu cầu</button><button class="btn" id="rqTop" style="display:none">${I('plus')} Tính bổ sung</button><button class="btn" id="rqPrint" style="display:none">${I('export')} In phiếu trước sản xuất</button>`,
      { noNote: true, sub: 'Chọn ngày sản xuất rồi bấm Tính nhu cầu. Hệ thống lấy các đơn đã chốt của ngày, tính nguyên liệu theo công thức, so với tồn kho và tự lập đề nghị mua nháp cho phần thiếu.' }) + `<div id="hzBody"></div><div id="rqBody"><div class="card panel empty">${I('calc')}<h2 class="mt">Đang tải…</h2></div></div>`;
    horizonPanel(el.querySelector('#hzBody'));
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
        return `<tr${r.incomplete ? ' style="background:#FFF8E6"' : ''}><td><b>${esc(r.ingredientName || '')}</b><span class="sub">${esc(r.ingredientCode || '')}${r.warning ? ' · ' + esc(r.warning) : ''}</span></td><td class="num">${U.num(r.requiredQuantity, 2)}</td><td class="num">${r.safetyStock ? U.num(r.safetyStock, 2) : '<span class="muted">0</span>'}</td><td class="num">${U.num(r.availableQuantity, 2)}${r.blockedQuantity ? `<span class="sub">bị chặn ${U.num(r.blockedQuantity, 2)}</span>` : ''}</td><td class="num">${U.num(r.expectedIncomingQuantity || 0, 2)}</td><td class="num">${r.purchaseQuantity > 0 ? `<b class="bad">${U.num(r.purchaseQuantity, 2)}</b>` : '<span class="up">0</span>'}</td><td>${esc(r.unitName || '')}</td><td><div class="track" style="width:110px"><div class="fill ${p >= 100 ? '' : p > 50 ? 'orange' : 'red'}" style="width:${p}%"></div></div></td><td>${r.supplier ? esc(r.supplier.supplierName || '') : '<span class="muted">Chưa có NCC</span>'}</td><td><span class="badge ${r.status === 'Sufficient' ? 'green' : r.status === 'Shortage' ? 'red' : 'orange'}">${esc(r.statusName || r.status || '')}</span></td></tr>`;
      };
      const dishRow = m => `<tr><td><b>${esc(m.dishName || '')}</b><span class="sub">${esc(m.dishCode || '')}</span></td><td class="num">${U.num(m.portions, 1)}${m.bufferPortions ? `<span class="sub">+${U.num(m.bufferPortions, 1)} dự phòng</span>` : ''}</td><td>${m.recipeVersion ? 'Phiên bản ' + esc(m.recipeVersion) : '<span class="muted">–</span>'}</td><td>${m.warning ? `<span class="warn">${esc(m.warning)}</span>` : ''}</td></tr>`;
      body.innerHTML = `
        <div class="kpis">${UI.kpi('Đơn đã chốt', U.num(d.orderCount || 0), `Kế hoạch ${esc(d.planNumber || '–')} · ${esc(d.planStatusName || '')}`, 'clipboard', 'green')}${UI.kpi('Tổng suất', U.num(d.totalMeals || 0), `${d.dishCount || 0} món`, 'utensils', 'blue')}${UI.kpi('Nguyên liệu cần mua', U.num(d.shortageCount || 0), d.shortageCount ? '<span class="bad">Thiếu hàng</span>' : 'Đủ hàng', 'alert', d.shortageCount ? 'red' : 'green')}${UI.kpi('Giá trị mua ước tính', U.money(d.estimatedAmount || 0), d.calculatedAt ? 'Tính lúc ' + U.dt(String(d.calculatedAt).replace(/\.\d+$/, '')) : '', 'calc', 'orange')}</div>
        ${(d.listWarning || []).length ? `<div class="notice warn">${I('alert')}<div><b>Lưu ý</b>${d.listWarning.map(w => esc(w)).join('<br>')}</div></div>` : ''}
        <div class="card panel mb"><div class="panel-head"><div><div class="panel-title">Đề nghị mua của kế hoạch</div><div class="panel-desc">Đề nghị nháp được lập tự động cho phần thiếu. Gửi duyệt và lập PO ở màn Đề nghị mua.</div></div><div class="row-flex">${d.productionPlanId && !locked && (d.listItem || []).some(r => r.purchaseQuantity > 0) ? `<button class="btn" id="rqPR">${I('plus')} Lập đề nghị mua theo dòng chọn</button>` : ''}<a class="btn" href="#/scm-pr">Mở Đề nghị mua →</a></div></div>${pr ? prRow(pr) : '<div class="muted small">Không thiếu nguyên liệu – không có đề nghị mua.</div>'}${tops.map(prRow).join('')}</div>
        <div class="card panel mb"><div class="panel-title mb">Nguyên liệu</div><div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Nguyên liệu</th><th class="num">Nhu cầu thuần</th><th class="num">Tồn an toàn</th><th class="num">Tồn xuất được</th><th class="num">Hàng sắp về</th><th class="num">Cần mua</th><th>ĐVT</th><th>Độ phủ</th><th>NCC gợi ý</th><th>Trạng thái</th></tr></thead><tbody>${(d.listItem || []).map(itemRow).join('') || '<tr><td colspan="10" class="empty">Không có nguyên liệu</td></tr>'}</tbody></table></div></div>
        <div class="card panel"><div class="panel-title mb">Món và số suất</div><div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Món</th><th class="num">Số suất</th><th>Công thức</th><th>Ghi chú</th></tr></thead><tbody>${(d.listDish || []).map(dishRow).join('') || '<tr><td colspan="4" class="empty">Không có món</td></tr>'}</tbody></table></div></div>`;
      const bq = body.querySelector('#rqPR'); if (bq) bq.onclick = () => prFromShortage(d, () => { delete rq.data[date]; APP.refresh(); });
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
          <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Nguyên liệu</th><th class="num">Còn lại</th><th>Số nhận</th><th>Số lô NCC</th><th>Hạn dùng</th><th>Nhiệt độ °C</th></tr></thead><tbody>${lines.map(l => `<tr data-l="${l.id}"><td><b>${esc(l.ingredientName || '')}</b><span class="sub">${esc(l.unitName || '')}</span></td><td class="num">${U.num(remain(l), 2)}</td><td><input class="input" type="number" step="0.01" min="0" data-f="q" value="${remain(l)}" style="width:100px"></td><td><input class="input" data-f="lot" placeholder="Số lô" style="width:130px"></td><td><input class="input" type="date" data-f="exp" style="width:150px"></td><td><input class="input" type="number" step="0.1" data-f="t" style="width:90px"></td></tr>`).join('')}</tbody></table></div>
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
      if (['Accepted', 'PartiallyAccepted', 'Rejected', 'Posted'].includes(r.Status)) rowBtn(td, '', 'In', () => printDoc('GoodsReceipt/Print', r.Id, 'phiếu nhập kho'));
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
    <div class="kpis">${UI.kpi('Số món trong kế hoạch', Store.by('ProductionPlanLine', 'ProductionPlanId', pl.Id).length, '', 'utensils', 'green')}${UI.kpi('Tổng suất', U.num(U.sum(Store.by('ProductionPlanLine', 'ProductionPlanId', pl.Id), 'PlannedQuantity')), '', 'clipboard', 'blue')}${UI.kpi('Nguyên liệu cần', reqs.length, '', 'package', 'purple')}${UI.kpi('Thiếu hụt', short.length, short.length ? '<span class="bad">Cần mua</span>' : 'Đủ hàng', 'alert', short.length ? 'red' : 'green')}</div>
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
    ingList(el, f, { noTransit: true, cols: [{ k: 'RequestNumber', label: 'Số đề nghị' }, { k: 'RequestDate', label: 'Ngày' }, { k: 'RequiredDate', label: 'Ngày cần' }, { k: 'RequestedByEmployeeId', label: 'Người đề nghị', render: r => esc(Store.label('Employee', r.RequestedByEmployeeId)) }, { k: 'n', label: 'Số dòng', col: false, num: true, render: r => Store.by('PurchaseRequestLine', 'PurchaseRequestId', r.Id).length }, { k: 'v', label: 'Giá trị ước tính', col: false, num: true, render: r => U.money(U.sum(Store.by('PurchaseRequestLine', 'PurchaseRequestId', r.Id), l => l.Quantity * prefPrice(l.IngredientId))) }, { k: 'Status', label: 'Trạng thái' }] });
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
      const pd = Store.isLive && PRINTS[t];
      return UI.detail(t, id, { screen: f && f.id, onChange: () => APP.refresh(), top: '', foot: pd ? row => (pd[2](row) ? `<button class="btn" data-act="print">${I('export')} In phiếu</button>` : '') : null, onAct: (act, row) => { if (act === 'print' && pd) printDoc(pd[0], row.Id, pd[1]); }, tabs: [{ label: `Chi tiết dòng (${lines.length})`, html: `<div class="table-wrap"><table class="table dc-table dt"><thead><tr>${cols.map(c => `<th class="${c.num ? 'num' : ''}">${c.label}<small>${c.k}</small></th>`).join('')}</tr></thead><tbody>${lines.map(l => `<tr>${cols.map(c => `<td class="${c.num ? 'num' : ''}">${UI.val(lt[0], c.k, l[c.k], l)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>${Store.isLive ? '' : `<div class="small muted mt">dbo.${lt[0]} · ${lt[1]} = ${id.slice(0, 8)}…</div>`}` }] });
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
    ingList(el, f, { cols: [{ k: 'PurchaseOrderNumber', label: 'Số PO' }, { k: 'SupplierId', label: 'Nhà cung cấp' }, { k: 'PurchaseRequestId', label: 'Đề nghị mua' }, { k: 'OrderDate', label: 'Ngày đặt' }, { k: 'ExpectedDeliveryDate', label: 'Giao dự kiến' }, { k: 'TotalAmount', label: 'Giá trị', num: true }, { k: 'Status', label: 'Trạng thái' }] });
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
    ingList(el, f, { noTransit: true, top: Store.isLive ? `<div class="notice info">${I('shield')}<div><b>Cổng kiểm soát QC</b>Chỉ ghi sổ được sau khi phiếu QC đầu vào đã kết luận. Ghi sổ tạo lô và nhập kho phần đạt.</div></div>` : `<div class="notice info">${I('shield')}<div><b>Cổng kiểm soát QC (Luồng 3)</b>Chỉ được <b>Ghi sổ</b> khi phiếu QC đầu vào có kết quả PASS cho các dòng. Ghi sổ sinh <code>MaterialLot</code> + <code>InventoryTransaction</code> (RECEIPT) và cập nhật <code>InventoryBalance</code>.</div></div>`,
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
    if (Store.isLive) return invLive(el, f);
    const pol = Store.rows('InventoryPolicie');
    const agg = Store.rows('Ingredient').map(g => { const bs = Store.by('InventoryBalance', 'IngredientId', g.Id); const p = pol.find(x => x.IngredientId === g.Id) || {}; return { Id: g.Id, g, onh: U.sum(bs, 'OnHandQuantity'), res: U.sum(bs, 'ReservedQuantity'), av: U.sum(bs, 'AvailableQuantity'), lots: bs.length, min: p.MinimumStock, safety: p.SafetyStock, rop: p.ReorderPoint, near: Store.by('MaterialLot', 'IngredientId', g.Id).filter(l => l.Status === 'Available' && U.day(l.ExpiryDate) <= U.addDays(Store.ctx.date, p.ExpiryWarningDays || 3)).length }; });
    el.innerHTML = APP.header(f, `<a class="btn" href="#/scm-inv-txn">${I('history')} Sổ cái giao dịch</a><a class="btn" href="#/scm-stockcount">${I('check')} Kiểm kê</a>`) + `
    <div class="notice info">${I('database')}<div><b>Quy tắc BRD:</b> Inventory Balance được tính từ ledger <code>InventoryTransaction</code>, không phải nguồn ghi biến động gốc → màn hình <b>không có nút Sửa</b>. Điều chỉnh tồn qua Kiểm kê / phiếu điều chỉnh (GAP-F-04).</div></div>
    <div class="kpis">${UI.kpi('Mã nguyên liệu có tồn', agg.filter(a => a.onh > 0).length, '', 'package', 'green')}${UI.kpi('Dưới tồn tối thiểu', agg.filter(a => a.min && a.av < a.min).length, '', 'alert', 'red')}${UI.kpi('Dưới điểm đặt hàng', agg.filter(a => a.rop && a.av < a.rop).length, '', 'down', 'orange')}${UI.kpi('Lô sắp hết hạn', U.sum(agg, 'near'), '', 'lot', 'purple', 'scm-lot')}</div>
    <div class="card panel" id="bT"></div>`;
    UI.table(el.querySelector('#bT'), { rows: agg, pageSize: 15, sort: 'ratio', onRow: id => lotsOf(id),
      cols: [{ k: 'code', label: 'Mã', render: r => esc(r.g.Code), sortv: r => r.g.Code }, { k: 'name', label: 'Nguyên liệu', render: r => esc(r.g.Name), sortv: r => r.g.Name }, { k: 'onh', label: 'Tồn thực (OnHand)', num: true, render: r => U.num(r.onh, 2) }, { k: 'res', label: 'Đã giữ (Reserved)', num: true, render: r => U.num(r.res, 2) }, { k: 'av', label: 'Khả dụng', num: true, render: r => `<b>${U.num(r.av, 2)}</b>` }, { k: 'u', label: 'ĐVT', render: r => esc(Store.label('UnitMeasure', r.g.BaseUnitId)) }, { k: 'min', label: 'Min / Safety / ROP', render: r => `${U.num(r.min)} / ${U.num(r.safety)} / ${U.num(r.rop)}` },
        { k: 'ratio', label: 'Mức tồn', sortv: r => r.min ? r.av / r.min : 99, render: r => { const p = r.min ? Math.min(100, r.av / (r.min * 3) * 100) : 100; return `<div class="track" style="width:120px"><div class="fill ${r.av < r.min ? 'red' : r.av < r.rop ? 'orange' : ''}" style="width:${p}%"></div></div>`; } }, { k: 'lots', label: 'Số lô', num: true }, { k: 'near', label: 'Sắp hết hạn', num: true, render: r => r.near ? `<span class="bad">${r.near}</span>` : '0' }] });
  });
  function lotsOf(iid) {
    const g = ing(iid);
    UI.modal({ title: 'Tồn theo lô – ' + esc(g.Name), sub: 'InventoryBalance × MaterialLot · sắp xếp FEFO (hạn dùng sớm xuất trước)', size: 'lg', body: `<table class="table dc-table dt"><thead><tr><th>Lô</th><th>Kho / vị trí</th><th>HSD</th><th class="num">Tồn thực</th><th class="num">Đã giữ</th><th class="num">Khả dụng</th><th>QC</th></tr></thead><tbody>${Store.by('InventoryBalance', 'IngredientId', iid).map(b => ({ b, l: Store.get('MaterialLot', b.MaterialLotId) || {} })).sort((x, y) => (x.l.ExpiryDate || '').localeCompare(y.l.ExpiryDate || '')).map(({ b, l }, i) => `<tr><td><a href="#/scm-lot?id=${l.Id}">${esc(l.LotNumber)}</a>${i === 0 ? ' <span class="tag green">FEFO</span>' : ''}</td><td class="small">${esc(Store.label('Warehouse', b.WarehouseId))} · ${esc(Store.label('StorageLocation', b.StorageLocationId))}</td><td>${U.date(l.ExpiryDate)}</td><td class="num">${U.num(b.OnHandQuantity, 2)}</td><td class="num">${U.num(b.ReservedQuantity, 2)}</td><td class="num"><b>${U.num(b.AvailableQuantity, 2)}</b></td><td>${UI.badge(l.QcStatus, 'MaterialLot', 'QcStatus')}</td></tr>`).join('') || '<tr><td colspan="7" class="empty">Không có tồn</td></tr>'}</tbody></table>` });
  }
  // ---------- Lô & truy xuất nguồn gốc ----------
  APP.register('scm-lot', (el, f, params) => {
    const live = Store.isLive, m = lotS.map;
    if (live) lotLoad();
    const useF = live && lotS.f && lotS.key === lotS.f && lotS.ok;
    const qty = k => ({ k, label: k === 'onHandQuantity' ? 'Tồn' : 'Khả dụng', col: false, num: true, sortv: r => (m[r.Id] ? m[r.Id][k] : -1), render: r => (m[r.Id] ? U.num(m[r.Id][k], 2) : '<span class="muted">–</span>') });
    APP.genericList(el, f, { noAdd: true, rows: useF ? () => Store.rows('MaterialLot').filter(r => m[r.Id]) : undefined,
      top: live ? `<div class="card panel mb"><div class="row-flex"><span class="small muted">Lọc theo tồn và hạn dùng:</span>${LOT_FILTERS.map(([k, lb]) => `<button class="chip ${lotS.f === k ? 'active' : ''}" data-lf="${k}">${lb}</button>`).join('')}</div><div class="small muted mt">Bấm "Truy xuất" để xem chuỗi nhà cung cấp → phiếu nhập → lô → xuất kho → lệnh sản xuất → mẻ → giao hàng → khách hàng.</div></div>`
        : `<div class="notice info">${I('link')}<div><b>Truy xuất nguồn gốc theo lô (GAP-F-10: chưa có mã QR)</b>Bấm "Truy xuất" để xem chuỗi NCC → Phiếu nhập → QC → Lô → Xuất kho → Lệnh SX → Mẻ → Đóng gói → Giao hàng → Khách hàng, dựng từ các quan hệ logic hiện có.</div></div>`,
      cols: [{ k: 'LotNumber', label: 'Số lô' }, { k: 'IngredientId', label: 'Nguyên liệu' }, { k: 'SupplierId', label: 'NCC' }, { k: 'ReceivedDate', label: 'Ngày nhận' }, { k: 'ExpiryDate', label: 'HSD', render: r => `<span class="${U.day(r.ExpiryDate) <= U.addDays(Store.ctx.date, 2) && r.Status === 'Available' ? 'bad' : ''}">${U.date(r.ExpiryDate)}</span>` }].concat(live ? [qty('onHandQuantity'), qty('availableQuantity')] : [], [{ k: 'QcStatus', label: 'QC' }, { k: 'Status', label: 'Trạng thái' }]) });
    APP.rowExtra(el, tr => {
      const td = tr.querySelector('.actions'); if (!td || td.dataset.live) return; td.dataset.live = '1';
      td.insertAdjacentHTML('afterbegin', `<button class="btn xs primary" data-tr="${tr.dataset.row}">Truy xuất</button> `); td.querySelector('[data-tr]').onclick = () => trace(tr.dataset.row);
      const r = live && Store.get('MaterialLot', tr.dataset.row); if (!r) return;
      const no = esc(r.LotNumber || '');
      if (r.Status === 'Available') rowBtn(td, 'orange', 'Tạm giữ', () => askReason(`Tạm giữ lô <b>${no}</b>? Lô tạm giữ không được gợi ý và không xuất kho được.`, note => actInv('MaterialLot/Hold', { form: { Id: r.Id, Reason: note } }, 'Đã tạm giữ lô')));
      if (r.Status === 'OnHold') rowBtn(td, '', 'Mở giữ', () => UI.confirm(`Mở tạm giữ lô <b>${no}</b>? Lô trở lại khả dụng và được xuất kho.`, () => actInv('MaterialLot/Release', { form: { Id: r.Id } }, 'Đã mở tạm giữ lô'), { ok: 'Mở tạm giữ' }));
    });
    if (live) {
      el.querySelectorAll('[data-lf]').forEach(b => b.onclick = () => { lotS.f = b.dataset.lf; APP.refresh(); });
      headBtn(el, `<button class="btn" id="lotExp">${I('alert')} Xử lý lô hết hạn</button>`); el.querySelector('#lotExp').onclick = expireLots;
    }
    if (params.id && !(lotS.traced === params.id && Date.now() - lotS.tracedAt < 3000)) { lotS.traced = params.id; lotS.tracedAt = Date.now(); setTimeout(() => trace(params.id), 50); }
  });
  function trace(lid) {
    if (Store.isLive) return traceLive(lid);
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
    const live = Store.isLive;
    if (live) {
      // Phiếu kiểm kê tạo qua StockCount/Create (sinh dòng từ tồn hệ thống) – thay nút "Tạo mới" chung.
      const add = el.querySelector('[data-act="add"]'); if (add) add.remove();
      headBtn(el, `<button class="btn primary" id="scNew">${I('plus')} Tạo phiếu kiểm kê</button>`); el.querySelector('#scNew').onclick = countCreate;
    }
    APP.rowExtra(el, tr => {
      const r = Store.get('StockCount', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td || td.dataset.live) return; td.dataset.live = '1';
      if (live) rowBtn(td, '', 'In', () => printDoc('StockCount/Print', r.Id, 'biên bản kiểm kê'));
      if (r.Status === 'PendingApproval' && r.ApprovedBy) rowBtn(td, 'primary', 'Ghi sổ', () => postCountAsk(r));
      if (r.Status === 'PendingApproval' && !r.ApprovedBy) td.insertAdjacentHTML('afterbegin', `<a class="btn xs" href="#/wf-inbox">Chờ duyệt →</a> `);
      if (live && ['Draft', 'Counting'].includes(r.Status)) rowBtn(td, 'orange', 'Kiểm đếm', () => countModal(r.Id));
    });
  });
  // Ghi sổ kiểm kê: chế độ thật hỏi lại rồi gọi máy chủ; chế độ DEMO giữ cách ghi tại chỗ.
  function postCountAsk(r, after) {
    const go = () => Store.act('StockCount/Post', { form: { Id: r.Id }, tables: INV_TABLES, demo: () => postCount(r) }).then(x => { if (x.ok && !x.demo) { invDirty(); UI.toast(okText(x, 'Đã ghi sổ kiểm kê'), 'ok'); after && after(); APP.refresh(); } });
    if (!Store.isLive) return go();
    UI.confirm(`Ghi sổ phiếu kiểm kê <b>${esc(r.CountNumber || '')}</b>? Mỗi dòng lệch tạo một bút toán điều chỉnh, tồn kho bằng số đếm thực tế. Không hoàn tác được.`, go, { ok: 'Ghi sổ kiểm kê' });
  }
  function postCount(sc) {
    const ls = Store.by('StockCountLine', 'StockCountId', sc.Id).filter(l => l.VarianceQuantity);
    if (ls.some(l => !l.VarianceReason)) return UI.toast('Dòng có chênh lệch bắt buộc có VarianceReason', 'err');
    ls.forEach(l => { Store.add('InventoryTransaction', { TransactionNumber: 'GD-' + sc.CountNumber + '-' + ing(l.IngredientId).Code, TransactionType: 'ADJUSTMENT', WarehouseId: sc.WarehouseId, IngredientId: l.IngredientId, MaterialLotId: l.MaterialLotId, ReferenceType: 'StockCount', ReferenceId: sc.Id, Quantity: l.VarianceQuantity, QuantityIn: Math.max(0, l.VarianceQuantity), QuantityOut: Math.max(0, -l.VarianceQuantity), UnitId: l.UnitId, TransactionDate: U.iso(new Date()), Reason: l.VarianceReason }); const b = Store.rows('InventoryBalance').find(x => x.MaterialLotId === l.MaterialLotId); if (b) Store.update('InventoryBalance', b.Id, { OnHandQuantity: +(b.OnHandQuantity + l.VarianceQuantity).toFixed(3), AvailableQuantity: +(b.AvailableQuantity + l.VarianceQuantity).toFixed(3) }); });
    Store.update('StockCount', sc.Id, { Status: 'Posted' }); UI.toast(`Đã ghi sổ ${ls.length} dòng ADJUSTMENT`, 'ok'); APP.refresh();
  }
  // ---------- Xuất kho cho sản xuất: theo FEFO ----------
  APP.register('scm-issue', (el, f) => {
    if (Store.isLive) return issueScreenLive(el, f);
    APP.genericList(el, f, { cols: [{ k: 'IssueNumber', label: 'Số phiếu' }, { k: 'ProductionOrderId', label: 'Lệnh SX' }, { k: 'WarehouseId', label: 'Kho' }, { k: 'IssueDate', label: 'Ngày xuất' }, { k: 'n', label: 'Số dòng', col: false, num: true, render: r => Store.by('MaterialIssueLine', 'MaterialIssueId', r.Id).length }, { k: 'Status', label: 'Trạng thái' }], noAdd: true,
      top: `<div class="card panel mb"><div class="spread"><div><div class="panel-title">Lệnh sản xuất đã giữ chỗ – chờ xuất kho</div><div class="panel-desc">MaterialReservation (Reserved) → MaterialIssue + MaterialIssueLine theo FEFO → InventoryTransaction (ISSUE)</div></div></div><div class="row-flex mt">${Store.rows('ProductionOrder').filter(p => Store.by('MaterialReservation', 'ProductionOrderId', p.Id).some(r => r.Status === 'Reserved')).slice(0, 12).map(p => `<button class="btn sm" data-iss="${p.Id}">${esc(p.ProductionOrderNumber)} · ${esc(Store.label('Dishe', p.DishId))}</button>`).join('') || '<span class="muted small">Không có lệnh chờ xuất</span>'}</div></div>` });
    el.querySelectorAll('[data-iss]').forEach(b => b.onclick = () => issue(b.dataset.iss));
  });
  function issue(poid) {
    const po = Store.get('ProductionOrder', poid), res = Store.by('MaterialReservation', 'ProductionOrderId', poid).filter(r => r.Status === 'Reserved');
    const plan = res.map(r => { let need = r.ReservedQuantity; const picks = []; Store.by('InventoryBalance', 'IngredientId', r.IngredientId).map(b => ({ b, l: Store.get('MaterialLot', b.MaterialLotId) })).filter(x => x.l && x.l.QcStatus === 'PASS' && x.l.Status === 'Available').sort((a, b) => (a.l.ExpiryDate || '').localeCompare(b.l.ExpiryDate || '')).forEach(x => { if (need <= 0) return; const q = Math.min(need, x.b.OnHandQuantity); if (q > 0) { picks.push({ x, q }); need -= q; } }); return { r, picks, short: Math.max(0, need) }; });
    UI.modal({ title: 'Xuất kho cho ' + esc(po.ProductionOrderNumber), sub: 'Đề xuất lô theo FEFO (InventoryPolicie.UseFefo = 1)', size: 'lg', body: `<table class="table dc-table dt"><thead><tr><th>Nguyên liệu</th><th class="num">Đã giữ</th><th>Lô đề xuất (FEFO)</th><th class="num">Thiếu</th></tr></thead><tbody>${plan.map(p => `<tr><td>${esc(Store.label('Ingredient', p.r.IngredientId))}</td><td class="num">${U.num(p.r.ReservedQuantity, 3)}</td><td class="small">${p.picks.map(k => `${esc(k.x.l.LotNumber)} (HSD ${U.date(k.x.l.ExpiryDate)}): <b>${U.num(k.q, 3)}</b>`).join('<br>') || '<span class="bad">Không có lô đạt QC</span>'}</td><td class="num ${p.short ? 'bad' : ''}">${U.num(p.short, 3)}</td></tr>`).join('')}</tbody></table>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="doIss">Xác nhận xuất kho</button>',
      onMount: (m, close) => m.querySelector('#doIss').onclick = () => {
        const n = Store.rows('MaterialIssue').length + 1; const mi = Store.add('MaterialIssue', { IssueNumber: 'PX-' + String(n).padStart(4, '0'), ProductionOrderId: poid, WarehouseId: APP.rawWarehouseId(), IssueDate: U.iso(new Date()), Status: 'Issued' });
        plan.forEach(p => { p.picks.forEach(k => { Store.add('MaterialIssueLine', { MaterialIssueId: mi.Id, IngredientId: p.r.IngredientId, Quantity: +k.q.toFixed(3), MaterialLotId: k.x.l.Id, UnitId: p.r.UnitId }); Store.add('InventoryTransaction', { TransactionNumber: 'GD-' + mi.IssueNumber + '-' + ing(p.r.IngredientId).Code, TransactionType: 'ISSUE', WarehouseId: k.x.b.WarehouseId, StorageLocationId: k.x.b.StorageLocationId, IngredientId: p.r.IngredientId, MaterialLotId: k.x.l.Id, ReferenceType: 'MaterialIssue', ReferenceId: mi.Id, Quantity: -k.q, QuantityIn: 0, QuantityOut: +k.q.toFixed(3), UnitId: p.r.UnitId, UnitCost: k.x.l.UnitCost, TransactionDate: U.iso(new Date()) }); Store.update('InventoryBalance', k.x.b.Id, { OnHandQuantity: +(k.x.b.OnHandQuantity - k.q).toFixed(3), ReservedQuantity: Math.max(0, +(k.x.b.ReservedQuantity - k.q).toFixed(3)) }); }); Store.update('MaterialReservation', p.r.Id, { Status: p.short ? 'PartiallyIssued' : 'Issued' }); });
        if (po.Status === 'Released') Store.update('ProductionOrder', poid, { Status: 'InProgress', ActualStartDate: U.iso(new Date()) });
        close(); UI.toast(`Đã xuất kho ${esc(mi.IssueNumber)} – InventoryTransaction ISSUE theo FEFO`, 'ok'); APP.refresh();
      } });
  }

  // ================= CHẾ ĐỘ DỮ LIỆU THẬT – Kho theo docs/frontend-handoff/P4.md + BE2.1 / BE2.3 / BE2.4 =================
  // NCC tổng quan, sơ đồ kho, lô (lọc tồn / hạn dùng, tạm giữ, hết hạn, truy xuất), tồn kho (tổng hợp, theo lô, chuyển kho, điều chỉnh),
  // giữ chỗ, xuất kho theo gợi ý lô, kiểm kê (tạo, ghi số đếm, gửi duyệt, ghi sổ), chênh lệch tiêu hao, in phiếu.
  const INV_TABLES = ['MaterialLot', 'InventoryBalance', 'InventoryTransaction', 'MaterialIssue', 'MaterialIssueLine', 'MaterialReservation', 'StockCount', 'StockCountLine', 'ProductionOrder', 'ProductionBatche', 'ApprovalRequest'];
  const PRINTS = {
    GoodsReceipt: ['GoodsReceipt/Print', 'phiếu nhập kho', r => ['Accepted', 'PartiallyAccepted', 'Rejected', 'Posted'].includes(r.Status)],
    MaterialIssue: ['MaterialIssue/Print', 'phiếu xuất kho', r => r.Status === 'Issued'],
    StockCount: ['StockCount/Print', 'biên bản kiểm kê', () => true]
  };
  const lotS = { f: '', key: null, at: 0, ok: false, map: {}, busy: false, traced: null, tracedAt: 0 };
  const inv = { wh: '', low: false, key: null, at: 0, x: null, busy: false };
  const LOT_FILTERS = [['', 'Tất cả'], ['stock', 'Còn tồn'], ['d3', 'Hết hạn trong 3 ngày'], ['d7', 'Trong 7 ngày'], ['d30', 'Trong 30 ngày']];
  const LOT_QUERY = { '': {}, stock: { InStock: true }, d3: { ExpiringWithinDays: 3, InStock: true }, d7: { ExpiringWithinDays: 7, InStock: true }, d30: { ExpiringWithinDays: 30, InStock: true } };
  const LEVELS = { critical: ['red', 'Dưới tối thiểu'], reorder: ['orange', 'Cần đặt hàng'], ok: ['green', 'Đủ hàng'] };
  function invDirty() { lotS.at = 0; inv.at = 0; }
  const hasOverlay = () => !!document.querySelector('.overlay, .drawer-wrap');
  const okText = (r, def) => { const m = r.res && r.res.responseMessage; return esc(m && m !== 'Lưu thông tin thành công' ? m : def); };
  const listOf = x => (x.list && x.list.length ? x.list : Array.isArray(x.data) ? x.data : []);
  const actInv = (path, opt, def) => Store.act(path, Object.assign({ tables: INV_TABLES }, opt)).then(r => { if (r.ok && !r.demo) { invDirty(); UI.toast(okText(r, def), 'ok'); APP.refresh(); } return r; });
  const printDoc = (path, id, name) => Store.download(path, { id }).then(x => { if (x.ok) UI.toast('Đã tải ' + esc(x.fileName || name), 'ok'); });
  const headBtn = (el, html) => { const h = el.querySelector('.heading-actions'); if (h) h.insertAdjacentHTML('beforeend', html); };
  const warnHtml = w => (w && w.length ? `<div class="notice warn">${I('alert')}<div>${w.map(s => esc(s)).join('<br>')}</div></div>` : '');
  const tbl = (heads, rows, empty) => `<div class="table-wrap"><table class="table dc-table dt"><thead><tr>${heads.map(h => `<th class="${h[1] ? 'num' : ''}">${h[0]}</th>`).join('')}</tr></thead><tbody>${rows.join('') || `<tr><td colspan="${heads.length}" class="empty">${empty || 'Không có dữ liệu'}</td></tr>`}</tbody></table></div>`;
  const fld = (label, html, req, wide) => `<div class="field"${wide ? ' style="grid-column:1/-1"' : ''}><label>${label}${req ? ' <span class="req">*</span>' : ''}</label>${html}</div>`;
  const opts = (t, sel) => Store.rows(t).map(r => `<option value="${r.Id}" ${r.Id === sel ? 'selected' : ''}>${esc(Store.label(t, r))}</option>`).join('');
  const locOpts = (wid, sel) => '<option value="">Không chọn vị trí</option>' + Store.rows('StorageLocation').filter(l => l.WarehouseId === wid).map(l => `<option value="${l.Id}" ${l.Id === sel ? 'selected' : ''}>${esc(Store.label('StorageLocation', l))}</option>`).join('');
  const lotOpts = (iid, sel) => '<option value="">Không theo lô</option>' + (iid ? Store.rows('MaterialLot').filter(l => l.IngredientId === iid && (l.Status !== 'Consumed' || l.Id === sel)) : []).map(l => `<option value="${l.Id}" ${l.Id === sel ? 'selected' : ''}>${esc(l.LotNumber || '')} · HSD ${U.date(l.ExpiryDate)}</option>`).join('');
  const ensure = ts => Promise.all(ts.map(t => Store.ensure(t)));
  const has = v => v !== null && v !== undefined;
  const baseList = (el, f, opt) => (f.table ? APP.genericList(el, f, opt) : APP.gapScreen(el, f));
  const liveRow = (el, fn) => APP.rowExtra(el, tr => { const td = tr.querySelector('.actions'); if (!td || td.dataset.live) return; td.dataset.live = '1'; fn(tr.dataset.row, td); });

  // ---------- Nhà cung cấp: tổng quan (bảng giá, PO đang mở, sự cố, tỷ lệ 90 ngày) ----------
  APP.register('scm-supplier', (el, f) => {
    // UI-3 (D-67 a): bấm dòng → màn chi tiết NCC cả trang; nút Tổng quan giữ hộp xem nhanh.
    baseList(el, f, { onRow: id => APP.go('scm-supplier-360?id=' + encodeURIComponent(id)) });
    if (Store.isLive && f.table) liveRow(el, (id, td) => rowBtn(td, '', 'Tổng quan', () => supplierOverview(id)));
  });
  function supplierOverview(id) {
    Store.query('Supplier/GetOverview', { id }).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không tải được tổng quan nhà cung cấp'), 'err');
      const d = x.data, s = d.supplier || {}, mats = d.listMaterial || [], pos = d.listOpenPurchaseOrder || [], iss = d.listIssue || [];
      const pct = v => (has(v) ? U.num(v, 1) + '%' : '–');
      UI.modal({
        title: 'Tổng quan nhà cung cấp – ' + esc(s.name || ''), sub: esc(s.code || ''), size: 'lg',
        foot: `<button class="btn" data-close>Đóng</button><button class="btn primary" data-open360>${I('settings')} Mở hồ sơ đầy đủ</button>`,
        onMount: (m, close) => { const b = m.querySelector('[data-open360]'); if (b) b.onclick = () => { close(); APP.go('scm-supplier-360?id=' + encodeURIComponent(id)); }; },
        body: `<div class="kpis">${UI.kpi('Giao đúng hạn', pct(d.onTimeRate), '90 ngày', 'check', 'green')}${UI.kpi('Tỷ lệ trả hàng', pct(d.rejectRate), '90 ngày', 'alert', d.rejectRate > 0 ? 'red' : 'green')}${UI.kpi('Đơn mua đang mở', pos.length, '', 'clipboard', 'blue')}${UI.kpi('Sự cố gần đây', iss.length, '', 'alert', 'orange')}</div>
          <h3>Bảng giá nguyên liệu</h3>${tbl([['Nguyên liệu'], ['Đơn giá', 1], ['Hiệu lực từ'], ['Đến'], ['Ưu tiên'], ['Trạng thái']], mats.map(r => `<tr><td>${esc(r.ingredientName || '')}</td><td class="num">${U.money(r.unitPrice)}</td><td>${U.date(r.effectiveFrom)}</td><td>${U.date(r.effectiveTo)}</td><td>${r.isPreferred ? '<span class="tag green">Ưu tiên</span>' : ''}</td><td>${UI.badge(r.status, 'SupplierMaterial', 'Status')}</td></tr>`), 'Chưa có bảng giá')}
          <h3 class="mt">Đơn mua đang mở</h3>${tbl([['Số PO'], ['Ngày đặt'], ['Giao dự kiến'], ['Giá trị', 1], ['Trạng thái']], pos.map(r => `<tr><td><b>${esc(r.purchaseOrderNumber || '')}</b></td><td>${U.date(r.orderDate)}</td><td>${U.dt(r.expectedDeliveryDate)}</td><td class="num">${U.money(r.totalAmount)}</td><td>${UI.badge(r.status, 'PurchaseOrder', 'Status')}</td></tr>`), 'Không có đơn mua đang mở')}
          <h3 class="mt">Sự cố gần đây</h3>${tbl([['Ngày'], ['Loại'], ['Mức độ'], ['Nội dung'], ['Trạng thái']], iss.map(r => `<tr><td>${U.date(r.reportedAt)}</td><td>${esc(r.issueType || '')}</td><td>${esc(r.severity || '')}</td><td class="small">${esc(r.description || '')}</td><td>${UI.badge(r.issueStatus, 'SupplierIssue', 'IssueStatus')}</td></tr>`), 'Chưa ghi nhận sự cố')}`
      });
    });
  }

  // ---------- Kho: sơ đồ khu bảo quản → vị trí ----------
  APP.register('scm-warehouse', (el, f) => {
    baseList(el, f);
    if (Store.isLive && f.table) liveRow(el, (id, td) => rowBtn(td, '', 'Sơ đồ', () => warehouseLayout(id)));
  });
  function warehouseLayout(id) {
    Store.query('Warehouse/GetLayout', { id }).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không tải được sơ đồ kho'), 'err');
      const d = x.data, w = d.warehouse || {}, zones = d.listZone || [];
      const temp = z => (has(z.temperatureMin) || has(z.temperatureMax) ? `${has(z.temperatureMin) ? U.num(z.temperatureMin, 1) : '…'} đến ${has(z.temperatureMax) ? U.num(z.temperatureMax, 1) : '…'} °C` : 'Không quy định nhiệt độ');
      UI.modal({
        title: 'Sơ đồ kho – ' + esc(w.name || ''), sub: `${zones.length} khu bảo quản · ${U.sum(zones, z => (z.listLocation || []).length)} vị trí`, size: 'lg',
        body: zones.map(z => { const zn = z.zone || {}, ls = z.listLocation || []; return `<div class="card panel mb"><div class="spread"><div><b>${esc(zn.name || zn.code || '')}</b> <span class="muted small">${esc(zn.code || '')}</span></div><span class="small">${temp(zn)}</span></div><div class="row-flex mt">${ls.map(l => `<span class="chip" title="${esc(l.locationType || '')}">${esc(l.code || '')}${l.name && l.name !== l.code ? ' · ' + esc(l.name) : ''}</span>`).join('') || '<span class="muted small">Chưa có vị trí</span>'}</div></div>`; }).join('') || '<div class="empty">Kho chưa khai báo khu bảo quản</div>'
      });
    });
  }

  // ---------- Nhu cầu nguyên liệu: lập đề nghị mua từ các dòng thiếu được chọn + % dự phòng ----------
  function prFromShortage(d, done) {
    const items = (d.listItem || []).filter(r => r.purchaseQuantity > 0);
    UI.modal({
      title: 'Lập đề nghị mua từ nguyên liệu thiếu', sub: 'Kế hoạch ' + esc(d.planNumber || ''), size: 'lg',
      body: `<div class="form-grid mb">${fld('Dự phòng thêm (%)', '<input class="input" type="number" min="0" max="100" step="1" id="prBuf" value="0">')}${fld('Ghi chú', '<input class="input" id="prNote">')}</div>
        ${tbl([['<input type="checkbox" id="prAll" checked>'], ['Nguyên liệu'], ['Cần mua', 1], ['ĐVT'], ['NCC gợi ý']], items.map(r => `<tr><td><input type="checkbox" data-rid="${r.id}" checked></td><td><b>${esc(r.ingredientName || '')}</b><span class="sub">${esc(r.ingredientCode || '')}</span></td><td class="num">${U.num(r.purchaseQuantity, 2)}</td><td>${esc(r.unitName || '')}</td><td>${r.supplier ? esc(r.supplier.supplierName || '') : '<span class="muted">Chưa có NCC</span>'}</td></tr>`))}
        <div class="small muted mt">Số cần mua đã gồm tồn an toàn của nguyên liệu – mặc định không cộng thêm dự phòng (BIZ-1.2). Chỉ nhập % dự phòng khi thật cần; số đề nghị = số cần mua × (1 + % dự phòng). Đề nghị mua nháp của kế hoạch được cập nhật lại theo các dòng đã chọn.</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="prGo">Lập đề nghị mua</button>',
      onMount: (m, close) => {
        const boxes = () => [...m.querySelectorAll('[data-rid]')];
        m.querySelector('#prAll').onchange = e => boxes().forEach(b => { b.checked = e.target.checked; });
        m.querySelector('#prGo').onclick = () => {
          const ids = boxes().filter(b => b.checked).map(b => b.dataset.rid), buf = Number(m.querySelector('#prBuf').value) || 0;
          if (!ids.length) return UI.toast('Chọn ít nhất một nguyên liệu', 'err');
          if (buf < 0 || buf > 100) return UI.toast('Dự phòng trong khoảng 0–100%', 'err');
          Store.act('MaterialRequirement/CreatePurchaseRequest', { json: { productionPlanId: d.productionPlanId, listRequirementId: ids, bufferPercent: buf, note: m.querySelector('#prNote').value.trim() || null }, tables: PUR_TABLES }).then(r => {
            if (!r.ok) return; close(); UI.toast(okText(r, 'Đã lập đề nghị mua nháp'), 'ok'); done();
          });
        };
      }
    });
  }

  // ---------- Lô: số tồn / hạn dùng theo lô (MaterialLot/GetList), lô hết hạn, truy xuất ----------
  function lotLoad() {
    const key = lotS.f;
    if (lotS.busy || (lotS.key === key && Date.now() - lotS.at < 60000)) return;
    lotS.busy = true;
    Store.query('MaterialLot/GetList', LOT_QUERY[key] || {}).then(x => {
      lotS.busy = false; lotS.key = key; lotS.at = Date.now(); lotS.ok = x.ok; lotS.map = {};
      if (x.ok) listOf(x).forEach(i => { const id = i.lot && i.lot.id; if (id) lotS.map[id] = i; });
      if (APP.current === 'scm-lot' && !hasOverlay()) APP.refresh();
    });
  }
  function expireLots() {
    Store.act('MaterialLot/ProcessExpired', { form: { Preview: true } }).then(r => {
      if (!r.ok) return;
      const d = r.data || {}, lots = d.listLot || [], soon = d.listExpiring || [], todo = lots.filter(l => !l.skippedReason);
      const row = l => `<tr><td><b>${esc(l.lotNumber || '')}</b></td><td>${esc(l.ingredientName || '')}</td><td>${U.date(l.expiryDate)}</td><td class="num">${U.num(l.wasteQuantity, 2)} ${esc(l.unitName || '')}</td><td class="num">${U.money(l.wasteValue || 0)}</td><td class="small">${l.skippedReason ? `<span class="warn">${esc(l.skippedReason)}</span>` : ''}</td></tr>`;
      const heads = [['Số lô'], ['Nguyên liệu'], ['Hạn dùng'], ['Tồn', 1], ['Giá trị', 1], ['Ghi chú']];
      UI.modal({
        title: 'Xử lý lô hết hạn', sub: `${todo.length} lô sẽ chuyển sang Hết hạn · giá trị hủy ước tính ${U.money(d.totalWasteValue || 0)}`, size: 'lg',
        body: warnHtml(d.listWarning) + `<h3>Lô đã quá hạn dùng (${lots.length})</h3>${tbl(heads, lots.map(row), 'Không có lô nào quá hạn dùng')}${soon.length ? `<h3 class="mt">Lô sắp hết hạn (${soon.length})</h3>${tbl(heads, soon.map(row))}` : ''}`,
        foot: '<button class="btn" data-close>Đóng</button>' + (todo.length ? `<button class="btn danger" id="exGo">Chuyển hết hạn và hủy tồn ${todo.length} lô</button>` : ''),
        onMount: (m, close) => {
          const b = m.querySelector('#exGo'); if (!b) return;
          b.onclick = () => UI.confirm(`Chuyển <b>${todo.length}</b> lô sang Hết hạn và ghi hủy toàn bộ tồn của các lô này? Không hoàn tác được.`, note => actInv('MaterialLot/ProcessExpired', { form: { Preview: false, Note: note } }, 'Đã xử lý lô hết hạn').then(x => { if (x.ok) close(); }), { input: 'Ghi chú', danger: true, ok: 'Xác nhận hủy tồn' });
        }
      });
    });
  }
  const TRACE_STEPS = [['listSupplier', 'Nhà cung cấp'], ['listPurchaseOrder', 'Đơn mua'], ['listGoodsReceipt', 'Phiếu nhập'], ['listLot', 'Lô nguyên liệu'], ['listIssue', 'Phiếu xuất kho'], ['listProductionOrder', 'Lệnh sản xuất'], ['listBatch', 'Mẻ sản xuất'], ['listTemperature', 'Nhiệt độ đã đo'], ['listPackingOrder', 'Lệnh đóng gói'], ['listOrder', 'Đơn hàng'], ['listDeliveryOrder', 'Đơn giao hàng'], ['listCustomer', 'Khách hàng nhận suất ăn']];
  function traceModal(d, title) {
    const back = d.direction === 'BACKWARD', root = d.root || {};
    const node = (n, k) => `${esc(n.number || n.name || '')}${n.number && n.name ? ' – ' + esc(n.name) : ''}${has(n.quantity) ? ' · ' + U.num(n.quantity, 2) : ''}${n.date ? ' · ' + U.date(n.date) : ''}${n.inferred ? ' <span class="badge orange" title="Suy ra theo ngày và ca sản xuất">suy ra</span>' : ''}${k === 'listBatch' && !back ? ` <a href="javascript:void 0" data-bt="${n.id}">truy ngược mẻ</a>` : ''}`;
    const steps = (back ? TRACE_STEPS.slice().reverse() : TRACE_STEPS).map(([k, lb]) => [k, lb, d[k] || []]).filter(s => s[2].length || !back);
    UI.modal({
      title, sub: (back ? 'Truy ngược: mẻ → phiếu xuất → lô → phiếu nhập → nhà cung cấp' : 'Truy xuôi: lô → phiếu xuất → lệnh sản xuất → mẻ → giao hàng → khách hàng') + (root.name ? ' · ' + esc(root.name) : ''), size: 'lg',
      body: warnHtml(d.listWarning) + `<div class="timeline">${steps.map(([k, lb, items]) => `<div class="tl ${items.length ? '' : 'orange'}"><b>${lb} <span class="muted small">${items.length || ''}</span></b><small>${items.length ? items.slice(0, 12).map(n => node(n, k)).join('<br>') + (items.length > 12 ? `<br>… và ${items.length - 12} dòng khác` : '') : 'Không có dữ liệu'}</small></div>`).join('')}</div>`,
      onMount: m => m.querySelectorAll('[data-bt]').forEach(a => a.onclick = () => batchTrace(a.dataset.bt))
    });
  }
  function traceLive(lid) {
    Store.query('MaterialLot/Trace', { id: lid }).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không truy xuất được lô'), 'err');
      traceModal(x.data, 'Truy xuất lô ' + esc((x.data.root && x.data.root.number) || (Store.get('MaterialLot', lid) || {}).LotNumber || ''));
    });
  }
  function batchTrace(bid) {
    Store.query('ProductionBatch/Trace', { id: bid }).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không truy xuất được mẻ'), 'err');
      traceModal(x.data, 'Truy xuất mẻ ' + esc((x.data.root && x.data.root.number) || ''));
    });
  }

  // ---------- Tồn kho: tổng hợp theo kho × nguyên liệu, tồn theo lô, chuyển kho, điều chỉnh, tính lại tồn ----------
  const invKey = () => inv.wh + '|' + inv.low;
  function invDraw(body) {
    const x = inv.x;
    if (!x || inv.key !== invKey()) { body.innerHTML = `<div class="card panel empty">${I('database')}<h2 class="mt">Đang tải…</h2></div>`; return; }
    if (!x.ok) { body.innerHTML = `<div class="notice gap">${I('alert')}<div><b>Không tải được tồn kho</b>${esc(x.message || '')}</div></div>`; return; }
    const rows = listOf(x).map(r => Object.assign({ Id: r.warehouseId + '|' + r.ingredientId }, r));
    const n = lv => rows.filter(r => r.level === lv).length;
    body.innerHTML = `<div class="kpis">${UI.kpi('Nguyên liệu có tồn', U.num(rows.filter(r => r.onHandQuantity > 0).length), '', 'package', 'green')}${UI.kpi('Dưới tồn tối thiểu', U.num(n('critical')), n('critical') ? '<span class="bad">Cần xử lý ngay</span>' : 'Không có', 'alert', 'red')}${UI.kpi('Dưới điểm đặt hàng', U.num(n('reorder')), '', 'down', 'orange')}${UI.kpi('Lô sắp hết hạn', U.num(U.sum(rows, 'expiringLotCount')), U.sum(rows, 'expiredLotCount') ? `<span class="bad">${U.num(U.sum(rows, 'expiredLotCount'))} lô đã hết hạn</span>` : 'Xuất theo hạn dùng gần nhất', 'lot', 'purple')}</div><div class="card panel" id="ivT"></div>`;
    const q = k => r => U.num(r[k], 2);
    UI.table(body.querySelector('#ivT'), { rows, pageSize: 15, onRow: id => { const r = rows.find(z => z.Id === id); if (r) lotsLive(r); },
      cols: [{ k: 'ingredientCode', label: 'Mã' }, { k: 'ingredientName', label: 'Nguyên liệu' }, { k: 'warehouseName', label: 'Kho' }, { k: 'onHandQuantity', label: 'Tồn thực', num: true, render: q('onHandQuantity') }, { k: 'reservedQuantity', label: 'Đã giữ', num: true, render: q('reservedQuantity') },
        { k: 'usableQuantity', label: 'Xuất được', num: true, render: r => `<b>${U.num(r.usableQuantity, 2)}</b>${r.blockedQuantity ? `<span class="sub">bị chặn ${U.num(r.blockedQuantity, 2)}</span>` : ''}` }, { k: 'unitName', label: 'ĐVT' },
        { k: 'minimumStock', label: 'Tối thiểu / an toàn / đặt hàng', nosort: true, render: r => [r.minimumStock, r.safetyStock, r.reorderPoint].map(v => (has(v) ? U.num(v) : '–')).join(' / ') },
        { k: 'level', label: 'Mức tồn', render: r => { const lv = LEVELS[r.level] || LEVELS.ok; return `<span class="badge ${lv[0]}">${lv[1]}</span>`; } }, { k: 'lotCount', label: 'Số lô', num: true },
        { k: 'expiringLotCount', label: 'Sắp hết hạn', num: true, render: r => (r.expiringLotCount ? `<span class="bad">${r.expiringLotCount}</span>` : '0') + (r.nearestExpiryDate ? `<span class="sub">${U.date(r.nearestExpiryDate)}</span>` : '') }] });
  }
  function invLoad() {
    const key = invKey();
    if (inv.busy || (inv.key === key && Date.now() - inv.at < 60000)) return;
    inv.busy = true;
    Store.query('InventoryBalance/GetSummary', { warehouseId: inv.wh, belowMin: inv.low ? true : null }).then(x => {
      inv.busy = false; inv.key = key; inv.at = Date.now(); inv.x = x;
      const b = document.getElementById('ivBody'); if (!b || APP.current !== 'scm-inv-balance') return;
      invDraw(b); invLoad();
    });
  }
  function invLive(el, f) {
    const admin = !!(window.DC_HOST && window.DC_HOST.user && window.DC_HOST.user.isAdmin);
    el.innerHTML = APP.header(f, `${admin ? `<button class="btn" id="ivRb">${I('calc')} Tính lại tồn</button>` : ''}<button class="btn" id="ivTr">${I('route')} Chuyển kho</button><button class="btn" id="ivAdj">${I('edit')} Điều chỉnh tồn</button><a class="btn" href="#/scm-inv-txn">${I('history')} Sổ cái giao dịch</a><a class="btn" href="#/scm-stockcount">${I('check')} Kiểm kê</a>`, { noNote: true }) + `
    <div class="card panel mb"><div class="row-flex"><select class="select" id="ivWh" style="width:auto"><option value="">Tất cả kho</option>${opts('Warehouse', inv.wh)}</select><label class="small"><input type="checkbox" id="ivLow" ${inv.low ? 'checked' : ''}> Chỉ nguyên liệu dưới mức tồn</label><span class="small muted">Tồn kho tính từ sổ cái giao dịch, không sửa trực tiếp. Bấm một dòng để xem tồn theo lô.</span></div></div><div id="ivBody"></div>`;
    el.querySelector('#ivWh').onchange = e => { inv.wh = e.target.value; APP.refresh(); };
    el.querySelector('#ivLow').onchange = e => { inv.low = e.target.checked; APP.refresh(); };
    el.querySelector('#ivTr').onclick = () => stockForm(true);
    el.querySelector('#ivAdj').onclick = () => stockForm(false);
    const rb = el.querySelector('#ivRb');
    if (rb) rb.onclick = () => UI.confirm(`Tính lại toàn bộ số tồn ${inv.wh ? 'của kho <b>' + esc(Store.label('Warehouse', inv.wh)) + '</b>' : 'của <b>mọi kho</b>'} từ sổ cái giao dịch? Số tồn hiện tại sẽ được ghi đè theo sổ.`, () => Store.act('InventoryBalance/RebuildBalance', { form: { WarehouseId: inv.wh }, tables: ['InventoryBalance'] }).then(r => {
      if (!r.ok) return; const d = r.data || {}; invDirty(); UI.toast(`Đã tính lại ${U.num(d.rows || 0)} dòng tồn, ${U.num(d.changed || 0)} dòng thay đổi`, 'ok'); APP.refresh();
    }), { ok: 'Tính lại tồn' });
    invDraw(el.querySelector('#ivBody')); invLoad();
  }
  function lotsLive(s) {
    Store.query('InventoryBalance/GetByLot', { ingredientId: s.ingredientId, warehouseId: s.warehouseId }).then(x => {
      if (!x.ok) return UI.toast(esc(x.message || 'Không tải được tồn theo lô'), 'err');
      const rows = listOf(x);
      UI.modal({
        title: 'Tồn theo lô – ' + esc(s.ingredientName || ''), sub: esc(s.warehouseName || '') + ' · sắp theo thứ tự nên xuất trước', size: 'lg',
        body: tbl([['Lô'], ['Kho / vị trí'], ['Hạn dùng'], ['Tồn thực', 1], ['Đã giữ', 1], ['Khả dụng', 1], ['QC'], ['']], rows.map((b, i) => `<tr><td>${b.materialLotId ? `<a href="#/scm-lot?id=${b.materialLotId}" data-close>${esc(b.lotNumber || '')}</a>` : '<span class="muted">Không theo lô</span>'}${b.pickRank === 1 ? ' <span class="tag green">Xuất trước</span>' : ''}${b.blockReason ? `<span class="sub bad">${esc(b.blockReason)}</span>` : ''}</td><td class="small">${esc(b.warehouseName || '')}${b.storageLocationName ? ' · ' + esc(b.storageLocationName) : ''}</td><td><span class="${b.isExpired ? 'bad' : b.isExpiring ? 'warn' : ''}">${U.date(b.expiryDate)}</span>${has(b.daysToExpiry) ? `<span class="sub">${b.daysToExpiry < 0 ? 'quá hạn ' + (-b.daysToExpiry) + ' ngày' : 'còn ' + b.daysToExpiry + ' ngày'}</span>` : ''}</td><td class="num">${U.num(b.onHandQuantity, 2)}</td><td class="num">${U.num(b.reservedQuantity, 2)}</td><td class="num"><b>${U.num(b.availableQuantity, 2)}</b> ${esc(b.unitName || '')}</td><td>${UI.badge(b.qcStatus, 'MaterialLot', 'QcStatus')}</td><td class="actions"><button class="btn xs" data-tf="${i}">Chuyển</button> <button class="btn xs" data-adj="${i}">Điều chỉnh</button></td></tr>`), 'Không có tồn'),
        onMount: (m, close) => {
          const pre = i => { const b = rows[i]; return { ingredientId: b.ingredientId, materialLotId: b.materialLotId, warehouseId: b.warehouseId, storageLocationId: b.storageLocationId }; };
          m.querySelectorAll('[data-tf]').forEach(b => b.onclick = () => { close(); stockForm(true, pre(+b.dataset.tf)); });
          m.querySelectorAll('[data-adj]').forEach(b => b.onclick = () => { close(); stockForm(false, pre(+b.dataset.adj)); });
        }
      });
    });
  }
  // Chuyển kho / vị trí (cặp bút toán chuyển đi – chuyển đến) và điều chỉnh tồn tay (số có dấu, có thể phải qua duyệt).
  function stockForm(isTransfer, p = {}) {
    ensure(['Warehouse', 'StorageLocation', 'Ingredient', 'MaterialLot']).then(() => {
      const wh0 = p.warehouseId || inv.wh || APP.rawWarehouseId() || '';
      UI.modal({
        title: isTransfer ? 'Chuyển kho / vị trí' : 'Điều chỉnh tồn kho', sub: isTransfer ? 'Chuyển một nguyên liệu (theo lô) sang kho hoặc vị trí khác.' : 'Số dương = tăng tồn, số âm = giảm tồn. Điều chỉnh có thể phải được duyệt trước khi ghi sổ.',
        body: `<div class="form-grid">${fld('Nguyên liệu', `<select class="select" id="sfIng"><option value="">Chọn nguyên liệu</option>${opts('Ingredient', p.ingredientId)}</select>`, true)}${fld('Lô', `<select class="select" id="sfLot">${lotOpts(p.ingredientId, p.materialLotId)}</select>`)}
          ${fld(isTransfer ? 'Từ kho' : 'Kho', `<select class="select" id="sfWh">${opts('Warehouse', wh0)}</select>`, true)}${fld(isTransfer ? 'Từ vị trí' : 'Vị trí', `<select class="select" id="sfLoc">${locOpts(wh0, p.storageLocationId)}</select>`)}
          ${isTransfer ? fld('Đến kho', `<select class="select" id="sfWh2">${opts('Warehouse', wh0)}</select>`, true) + fld('Đến vị trí', `<select class="select" id="sfLoc2">${locOpts(wh0)}</select>`) : ''}
          ${fld(isTransfer ? 'Số lượng chuyển' : 'Số lượng điều chỉnh (+ tăng / − giảm)', '<input class="input" type="number" step="0.001" id="sfQty">', true)}${isTransfer ? '' : fld('Đơn giá (khi nhập thêm)', '<input class="input" type="number" step="1" min="0" id="sfCost">')}
          ${fld('Lý do', '<textarea class="input" rows="2" id="sfReason"></textarea>', true, true)}${isTransfer ? '' : fld('Ghi chú', '<input class="input" id="sfNote">', false, true)}</div>`,
        foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="sfGo">${isTransfer ? 'Chuyển kho' : 'Ghi điều chỉnh'}</button>`,
        onMount: (m, close) => {
          const v = id => { const e = m.querySelector('#' + id); return e ? e.value.trim() : ''; };
          m.querySelector('#sfIng').onchange = () => { m.querySelector('#sfLot').innerHTML = lotOpts(v('sfIng')); };
          m.querySelector('#sfWh').onchange = () => { m.querySelector('#sfLoc').innerHTML = locOpts(v('sfWh')); };
          if (isTransfer) m.querySelector('#sfWh2').onchange = () => { m.querySelector('#sfLoc2').innerHTML = locOpts(v('sfWh2')); };
          m.querySelector('#sfGo').onclick = () => {
            const q = Number(v('sfQty')), reason = v('sfReason');
            if (!v('sfIng') || !v('sfWh') || (isTransfer && !v('sfWh2'))) return UI.toast('Chọn nguyên liệu và kho', 'err');
            if (!q || (isTransfer && q < 0)) return UI.toast(isTransfer ? 'Số lượng chuyển phải lớn hơn 0' : 'Nhập số lượng điều chỉnh khác 0', 'err');
            if (reason.length < 3) return UI.toast('Nhập lý do (ít nhất 3 ký tự)', 'err');
            if (isTransfer && v('sfWh') === v('sfWh2') && v('sfLoc') === v('sfLoc2')) return UI.toast('Nơi chuyển đến phải khác nơi chuyển đi', 'err');
            const name = esc(Store.label('Ingredient', v('sfIng')));
            const send = () => (isTransfer
              ? actInv('InventoryTransaction/Transfer', { form: { FromWarehouseId: v('sfWh'), FromStorageLocationId: v('sfLoc'), ToWarehouseId: v('sfWh2'), ToStorageLocationId: v('sfLoc2'), IngredientId: v('sfIng'), MaterialLotId: v('sfLot'), Quantity: q, Reason: reason } }, 'Đã chuyển kho')
              : actInv('InventoryTransaction/Adjust', { form: { WarehouseId: v('sfWh'), StorageLocationId: v('sfLoc'), IngredientId: v('sfIng'), MaterialLotId: v('sfLot'), Quantity: q, UnitCost: v('sfCost'), Reason: reason, Note: v('sfNote') } }, 'Đã ghi nhận điều chỉnh tồn')
            ).then(r => { if (r.ok) close(); });
            UI.confirm(isTransfer ? `Chuyển <b>${U.num(q, 3)}</b> ${name} sang <b>${esc(Store.label('Warehouse', v('sfWh2')))}</b>? Bút toán kho đã ghi không sửa, không xóa được.` : `${q > 0 ? 'Tăng' : 'Giảm'} tồn <b>${U.num(Math.abs(q), 3)}</b> ${name} tại <b>${esc(Store.label('Warehouse', v('sfWh')))}</b>? Bút toán kho đã ghi không sửa, không xóa được.`, send, { ok: 'Xác nhận' });
          };
        }
      });
    });
  }
  // ---------- Sổ cái kho: chuyển kho, điều chỉnh tồn (sổ chỉ ghi thêm – không tạo bút toán thô) ----------
  APP.register('scm-inv-txn', (el, f) => {
    baseList(el, f);
    if (!Store.isLive || !f.table) return;
    const add = el.querySelector('[data-act="add"]'); if (add) add.remove();
    headBtn(el, `<button class="btn" id="txTr">${I('route')} Chuyển kho</button><button class="btn primary" id="txAdj">${I('edit')} Điều chỉnh tồn</button>`);
    el.querySelector('#txTr').onclick = () => stockForm(true);
    el.querySelector('#txAdj').onclick = () => stockForm(false);
  });

  // ---------- Giữ chỗ nguyên liệu: theo lệnh sản xuất / theo kế hoạch, giữ chỗ bổ sung ----------
  const resRow = (r, withOrder) => `<tr>${withOrder ? `<td><a href="javascript:void 0" data-ro="${r.productionOrderId}">${esc(r.productionOrderNumber || '')}</a></td>` : ''}<td><b>${esc(r.ingredientName || '')}</b><span class="sub">${esc(r.ingredientCode || '')}</span></td><td class="num">${U.num(r.requiredQuantity, 3)}</td><td class="num">${U.num(r.reservedQuantity, 3)}</td><td class="num ${r.shortQuantity > 0 ? 'bad' : ''}">${U.num(r.shortQuantity, 3)}</td><td>${esc(r.unitName || '')}</td><td>${UI.badge(r.status, 'MaterialReservation', 'Status')}</td></tr>`;
  const RES_HEADS = [['Nguyên liệu'], ['Cần', 1], ['Đang giữ', 1], ['Chưa giữ được', 1], ['ĐVT'], ['Trạng thái']];
  APP.register('scm-reservation', (el, f) => {
    if (!Store.isLive || !f.table) return baseList(el, f);
    const plans = Store.rows('ProductionPlan').filter(p => !['Draft', 'Cancelled'].includes(p.Status) && U.day(p.ProductionDate) >= U.addDays(Store.ctx.date, -1)).sort((a, b) => String(a.ProductionDate).localeCompare(String(b.ProductionDate))).slice(0, 8);
    APP.genericList(el, f, { noAdd: true, noTransit: true, top: `<div class="card panel mb"><div class="panel-title">Giữ chỗ theo kế hoạch sản xuất</div><div class="panel-desc">Nguyên liệu được giữ khi phát hành kế hoạch. Chọn kế hoạch để xem toàn bộ phần giữ chỗ, hoặc bấm "Lệnh SX" trên từng dòng để giữ bổ sung sau khi hàng về.</div><div class="row-flex mt">${plans.map(p => `<button class="btn sm" data-rp="${p.Id}">${esc(p.PlanNumber || '')} · ${U.date(p.ProductionDate)}</button>`).join('') || '<span class="muted small">Không có kế hoạch đã phát hành</span>'}</div></div>` });
    el.querySelectorAll('[data-rp]').forEach(b => b.onclick = () => planReservations(b.dataset.rp));
    liveRow(el, (id, td) => { const r = Store.get('MaterialReservation', id); if (r && r.ProductionOrderId) rowBtn(td, '', 'Lệnh SX', () => orderReservations(r.ProductionOrderId)); });
  });
  function orderReservations(poid) {
    Store.query('ProductionOrder/GetDetail', { id: poid }).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không tải được lệnh sản xuất'), 'err');
      const d = x.data, po = d.productionOrder || {}, res = d.listReservation || [], acts = d.allowedActions || [], no = esc(po.productionOrderNumber || '');
      UI.modal({
        title: 'Giữ chỗ nguyên liệu – lệnh ' + no, sub: UI.badge(po.status, 'ProductionOrder', 'Status'), size: 'lg',
        body: warnHtml(d.listWarning) + tbl(RES_HEADS, res.map(r => resRow(r)), 'Lệnh chưa giữ chỗ nguyên liệu'),
        foot: '<button class="btn" data-close>Đóng</button>' + (res.some(r => r.reservedQuantity > 0) ? '<button class="btn" id="roIss">Xuất kho cho lệnh</button>' : '') + (acts.includes('reserve') ? '<button class="btn primary" id="roRes">Giữ chỗ bổ sung</button>' : ''),
        onMount: (m, close) => {
          const bi = m.querySelector('#roIss'), br = m.querySelector('#roRes');
          if (bi) bi.onclick = () => { close(); issueLive(poid); };
          if (br) br.onclick = () => UI.confirm(`Giữ chỗ bổ sung nguyên liệu cho lệnh <b>${no}</b>? Phần còn thiếu được giữ thêm từ tồn khả dụng hiện có.`, () => actInv('ProductionOrder/Reserve', { form: { Id: poid } }, 'Đã giữ chỗ bổ sung').then(r => { if (r.ok) { close(); orderReservations(poid); } }), { ok: 'Giữ chỗ bổ sung' });
        }
      });
    });
  }
  function planReservations(pid) {
    Store.query('ProductionPlan/GetDetail', { id: pid }).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không tải được kế hoạch sản xuất'), 'err');
      const d = x.data, pl = d.productionPlan || {}, res = d.listReservation || [];
      UI.modal({
        title: 'Giữ chỗ nguyên liệu – kế hoạch ' + esc(pl.planNumber || ''), sub: `${U.date(pl.productionDate)} · ${res.length} dòng giữ chỗ · ${res.filter(r => r.shortQuantity > 0).length} dòng chưa giữ đủ`, size: 'lg',
        body: warnHtml(d.listWarning) + tbl([['Lệnh SX']].concat(RES_HEADS), res.map(r => resRow(r, true)), 'Kế hoạch chưa giữ chỗ nguyên liệu'),
        onMount: (m, close) => m.querySelectorAll('[data-ro]').forEach(a => a.onclick = () => { close(); orderReservations(a.dataset.ro); })
      });
    });
  }

  // ---------- Xuất kho: gợi ý lô theo hạn dùng, tạo phiếu nháp, ghi sổ, hủy, in ----------
  function issueScreenLive(el, f) {
    const wait = Store.rows('ProductionOrder').filter(p => ['Released', 'InProgress'].includes(p.Status) && Store.by('MaterialReservation', 'ProductionOrderId', p.Id).some(r => ['Reserved', 'PartiallyIssued'].includes(r.Status))).slice(0, 12);
    ingList(el, f, { noAdd: true, noTransit: true, cols: [{ k: 'IssueNumber', label: 'Số phiếu' }, { k: 'ProductionOrderId', label: 'Lệnh SX' }, { k: 'WarehouseId', label: 'Kho' }, { k: 'IssueDate', label: 'Ngày xuất' }, { k: 'n', label: 'Số dòng', col: false, num: true, render: r => Store.by('MaterialIssueLine', 'MaterialIssueId', r.Id).length }, { k: 'Status', label: 'Trạng thái' }],
      top: `<div class="card panel mb"><div class="panel-title">Lệnh sản xuất đã giữ chỗ – chờ xuất kho</div><div class="panel-desc">Chọn lệnh để xuất phần nguyên liệu đang giữ. Lô được gợi ý theo hạn dùng gần nhất; phiếu tạo ở trạng thái Nháp, ghi sổ mới trừ tồn.</div><div class="row-flex mt">${wait.map(p => `<button class="btn sm" data-iss="${p.Id}">${esc(p.ProductionOrderNumber || '')} · ${esc(Store.label('Dishe', p.DishId))}</button>`).join('') || '<span class="muted small">Không có lệnh chờ xuất</span>'}</div></div>` });
    headBtn(el, `<button class="btn primary" id="isNew">${I('plus')} Tạo phiếu xuất</button>`);
    el.querySelector('#isNew').onclick = () => issueLive(null);
    el.querySelectorAll('[data-iss]').forEach(b => b.onclick = () => issueLive(b.dataset.iss));
    liveRow(el, (id, td) => {
      const r = Store.get('MaterialIssue', id); if (!r) return;
      const no = esc(r.IssueNumber || '');
      if (r.Status === 'Issued') rowBtn(td, '', 'In', () => printDoc('MaterialIssue/Print', r.Id, 'phiếu xuất kho'));
      if (['Draft', 'Issued'].includes(r.Status)) rowBtn(td, 'danger', 'Hủy', () => askReason(r.Status === 'Issued' ? `Hủy phiếu xuất <b>${no}</b>? Phiếu đã xuất sẽ được ghi bút toán đảo, tồn kho trở lại.` : `Hủy phiếu xuất nháp <b>${no}</b>?`, note => actInv('MaterialIssue/Cancel', { form: { Id: r.Id, Reason: note } }, 'Đã hủy phiếu xuất')));
      if (r.Status === 'Draft') rowBtn(td, 'primary', 'Ghi sổ', () => postIssue(r.Id, no));
    });
  }
  const postIssue = (id, no) => UI.confirm(`Ghi sổ phiếu xuất <b>${no}</b>? Tồn kho bị trừ theo từng lô, không sửa được sau khi ghi.`, () => actInv('MaterialIssue/Post', { form: { Id: id } }, 'Đã ghi sổ xuất kho'), { ok: 'Ghi sổ xuất kho' });
  // Sau khi tạo phiếu nháp: báo cảnh báo (nếu có) và hỏi ghi sổ ngay.
  function offerPost(d) {
    const mi = (d && d.materialIssue) || {}, w = (d && d.listWarning) || [];
    invDirty(); APP.refresh();
    if (!mi.id) return UI.toast('Đã tạo phiếu xuất nháp', 'ok');
    UI.confirm(`Đã tạo phiếu xuất nháp <b>${esc(mi.issueNumber || '')}</b>.${w.length ? `<div class="small warn mt">${w.map(s => esc(s)).join('<br>')}</div>` : ''}<div class="mt">Ghi sổ xuất kho ngay? Tồn kho bị trừ theo từng lô, không sửa được sau khi ghi.</div>`, () => actInv('MaterialIssue/Post', { form: { Id: mi.id } }, 'Đã ghi sổ xuất kho'), { title: 'Phiếu xuất đã tạo', ok: 'Ghi sổ xuất kho' });
  }
  // poid có giá trị = xuất phần đang giữ chỗ của lệnh sản xuất; null = phiếu xuất tay (không gắn lệnh).
  function issueLive(poid) {
    Promise.all([poid ? Store.query('ProductionOrder/GetDetail', { id: poid }) : Promise.resolve(null), ensure(['Warehouse', 'Ingredient'])]).then(([x]) => {
      if (x && (!x.ok || !x.data)) return UI.toast(esc(x.message || 'Không tải được lệnh sản xuất'), 'err');
      const d = (x && x.data) || {}, po = d.productionOrder || {};
      const lines = poid ? (d.listReservation || []).filter(r => r.reservedQuantity > 0).map(r => ({ ingredientId: r.ingredientId, name: r.ingredientName, unitId: r.unitId, unitName: r.unitName, qty: r.reservedQuantity })) : [{ ingredientId: '', qty: '' }];
      const batches = poid ? (d.listBatch || []).filter(b => ['Planned', 'Cooking'].includes(b.status)) : [];
      if (poid && !lines.length && !batches.length) return UI.toast('Lệnh này không còn nguyên liệu đang giữ chỗ để xuất', 'warn');
      const ingSel = sel => `<select class="select" data-f="ing"><option value="">Chọn nguyên liệu</option>${opts('Ingredient', sel)}</select>`;
      const rowHtml = (l, i) => `<tr data-i="${i}"><td>${poid ? `<b>${esc(l.name || '')}</b>` : ingSel(l.ingredientId)}</td><td><input class="input" type="number" min="0" step="0.001" data-f="q" value="${has(l.qty) ? l.qty : ''}" style="width:120px"></td><td>${esc(l.unitName || '')}</td><td class="small" data-sg>${l.sg || '<span class="muted">Bấm "Gợi ý lô" để xem lô nên xuất</span>'}</td></tr>`;
      UI.modal({
        title: poid ? 'Xuất kho cho lệnh ' + esc(po.productionOrderNumber || '') : 'Tạo phiếu xuất kho', sub: poid ? 'Xuất phần nguyên liệu đang giữ chỗ của lệnh. Không chọn lô – hệ thống tự phân bổ theo hạn dùng gần nhất.' : 'Phiếu xuất không gắn lệnh sản xuất. Không chọn lô – hệ thống tự phân bổ theo hạn dùng gần nhất.', size: 'lg',
        body: `<div class="form-grid mb">${fld('Kho xuất', `<select class="select" id="isWh">${opts('Warehouse', APP.rawWarehouseId())}</select>`, true)}${fld('Ghi chú', '<input class="input" id="isNote">')}</div>
          ${lines.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Nguyên liệu</th><th>Số lượng xuất</th><th>ĐVT</th><th>Lô gợi ý</th></tr></thead><tbody id="isLines"></tbody></table></div>` : '<div class="muted small">Lệnh không còn nguyên liệu đang giữ chỗ.</div>'}
          ${poid ? '' : `<button class="btn sm mt" id="isAdd">${I('plus')} Thêm dòng</button>`}
          ${batches.length ? `<div class="card panel mt"><div class="panel-title">Xuất theo định mức của mẻ</div><div class="panel-desc">Số lượng tính theo định mức × số phần của mẻ; mỗi mẻ một phiếu xuất nháp.</div><div class="row-flex mt">${batches.map(b => `<button class="btn sm" data-bi="${b.id}">${esc(b.batchNumber || '')} · ${U.num(b.plannedQuantity)} phần</button>`).join('')}</div></div>` : ''}`,
        foot: `<button class="btn" data-close>Hủy</button>${lines.length ? '<button class="btn" id="isSg">Gợi ý lô</button><button class="btn primary" id="isGo">Tạo phiếu xuất</button>' : ''}`,
        onMount: (m, close) => {
          const tb = m.querySelector('#isLines'), wh = () => m.querySelector('#isWh').value;
          const sync = () => { if (tb) tb.querySelectorAll('tr[data-i]').forEach(tr => { const l = lines[+tr.dataset.i], s = tr.querySelector('[data-f="ing"]'); l.qty = tr.querySelector('[data-f="q"]').value; if (s && s.value !== l.ingredientId) { l.ingredientId = s.value; l.sg = ''; } }); };
          const draw = () => { if (tb) tb.innerHTML = lines.map(rowHtml).join(''); };
          const valid = () => { sync(); return lines.filter(l => l.ingredientId && Number(l.qty) > 0); };
          draw();
          const ba = m.querySelector('#isAdd'); if (ba) ba.onclick = () => { sync(); lines.push({ ingredientId: '', qty: '' }); draw(); };
          const bs = m.querySelector('#isSg');
          if (bs) bs.onclick = () => {
            const ls = valid(); if (!wh() || !ls.length) return UI.toast('Chọn kho, nguyên liệu và nhập số lượng xuất', 'err');
            Promise.all(ls.map(l => Store.query('MaterialIssue/SuggestLots', { warehouseId: wh(), ingredientId: l.ingredientId, quantity: Number(l.qty) }).then(s => {
              const g = s.data || {}, picks = g.listPick || [], blocked = g.listBlocked || [];
              l.sg = !s.ok ? `<span class="bad">${esc(s.message || 'Không gợi ý được lô')}</span>`
                : (picks.map(k => `${esc(k.lotNumber || 'Không theo lô')}${k.expiryDate ? ' (HSD ' + U.date(k.expiryDate) + ')' : ''}: <b>${U.num(k.quantity, 3)}</b>`).join('<br>') || '<span class="bad">Không có lô xuất được</span>')
                + (g.shortQuantity > 0 ? `<br><span class="bad">Thiếu ${U.num(g.shortQuantity, 3)}</span>` : '') + (blocked.length ? `<br><span class="muted">${blocked.length} lô còn tồn nhưng bị chặn xuất</span>` : '') + (s.ok && g.method ? `<br><span class="muted">${g.method === 'FIFO' ? 'Nhập trước xuất trước' : 'Hạn dùng gần nhất xuất trước'}</span>` : '');
            }))).then(() => { sync(); draw(); });
          };
          const bg = m.querySelector('#isGo');
          if (bg) bg.onclick = () => {
            const ls = valid(); if (!wh() || !ls.length) return UI.toast('Chọn kho, nguyên liệu và nhập số lượng xuất', 'err');
            Store.act('MaterialIssue/Create', { json: { warehouseId: wh(), productionOrderId: poid || null, note: m.querySelector('#isNote').value.trim() || null, listLine: ls.map(l => ({ ingredientId: l.ingredientId, quantity: Number(l.qty), unitId: l.unitId || null })) }, tables: INV_TABLES }).then(r => { if (!r.ok) return; close(); offerPost(r.data); });
          };
          m.querySelectorAll('[data-bi]').forEach(b => b.onclick = () => {
            if (!wh()) return UI.toast('Chọn kho xuất', 'err');
            UI.confirm(`Tạo phiếu xuất nháp theo định mức cho mẻ <b>${esc(b.textContent)}</b>?`, () => Store.act('ProductionBatch/CreateMaterialIssue', { form: { Id: b.dataset.bi, WarehouseId: wh(), Post: false, Note: m.querySelector('#isNote').value.trim() }, tables: INV_TABLES }).then(r => { if (!r.ok) return; close(); offerPost(r.data); }), { ok: 'Tạo phiếu xuất' });
          });
        }
      });
    });
  }

  // ---------- Kiểm kê: tạo phiếu, ghi số đếm, gửi duyệt, ghi sổ ----------
  function countCreate() {
    ensure(['Warehouse', 'Employee', 'Ingredient']).then(() => UI.modal({
      title: 'Tạo phiếu kiểm kê', sub: 'Hệ thống lấy mọi nguyên liệu và lô đang có tồn trong kho làm dòng kiểm kê.',
      body: `<div class="form-grid">${fld('Kho kiểm kê', `<select class="select" id="scWh">${opts('Warehouse', APP.rawWarehouseId())}</select>`, true)}${fld('Loại kiểm kê', '<select class="select" id="scType"><option value="ADHOC">Đột xuất</option><option value="DAILY">Hằng ngày</option><option value="WEEKLY">Hằng tuần</option><option value="MONTHLY">Hằng tháng</option></select>')}
        ${fld('Ngày kiểm kê', `<input class="input" type="date" id="scDate" value="${Store.ctx.date}">`)}${fld('Người phụ trách', `<select class="select" id="scEmp"><option value="">Chưa phân công</option>${opts('Employee')}</select>`)}
        ${fld('Chỉ kiểm một nguyên liệu', `<select class="select" id="scIng"><option value="">Mọi nguyên liệu có tồn</option>${opts('Ingredient')}</select>`, false, true)}${fld('Ghi chú', '<input class="input" id="scNote">', false, true)}</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="scGo">Tạo phiếu</button>',
      onMount: (m, close) => m.querySelector('#scGo').onclick = () => {
        const v = id => m.querySelector('#' + id).value.trim();
        if (!v('scWh')) return UI.toast('Chọn kho kiểm kê', 'err');
        Store.act('StockCount/Create', { form: { WarehouseId: v('scWh'), CountType: v('scType'), CountDate: v('scDate'), ResponsibleEmployeeId: v('scEmp'), IngredientId: v('scIng'), Note: v('scNote') }, tables: ['StockCount', 'StockCountLine'] }).then(r => {
          if (!r.ok) return; close(); UI.toast(okText(r, 'Đã tạo phiếu kiểm kê'), 'ok'); APP.refresh();
          const sc = r.data && r.data.stockCount; if (sc && sc.id) countModal(sc.id);
        });
      }
    }));
  }
  function countModal(id) {
    Store.query('StockCount/GetDetail', { id }).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không tải được phiếu kiểm kê'), 'err');
      const d = x.data, sc = d.stockCount || {}, lines = d.listLine || [], acts = d.allowedActions || [], edit = acts.includes('recordCounts'), no = esc(sc.countNumber || '');
      const vr = l => `<span class="${l.overThreshold ? 'bad' : ''}">${U.num(l.varianceQuantity, 3)}${l.varianceQuantity ? ` (${U.num(l.variancePercent, 1)}%)` : ''}</span>`;
      UI.modal({
        title: 'Kiểm đếm – phiếu ' + no, sub: `${esc(d.warehouseName || '')} · ${esc(d.statusName || '')} · lệch lớn nhất ${U.num(d.maxVariancePercent || 0, 1)}% (ngưỡng duyệt ${U.num(d.varianceThresholdPercent || 0, 1)}%)`, size: 'lg',
        body: (d.isApproved ? `<div class="notice info">${I('check')}<div><b>Phiếu đã được duyệt</b>Bấm Ghi sổ để điều chỉnh tồn theo số đếm.</div></div>` : d.needsApproval ? `<div class="notice warn">${I('alert')}<div><b>Có dòng lệch vượt ngưỡng</b>Phiếu phải gửi duyệt và được duyệt xong mới ghi sổ được.</div></div>` : '')
          + tbl([['Nguyên liệu'], ['Lô'], ['Tồn hệ thống', 1], ['Số đếm thực tế'], ['Chênh lệch', 1], ['Lý do chênh lệch']], lines.map(l => `<tr data-l="${l.id}" data-sys="${l.systemQuantity}"><td><b>${esc(l.ingredientName || '')}</b><span class="sub">${esc(l.unitName || '')}</span></td><td class="small">${esc(l.lotNumber || '–')}${l.expiryDate ? `<span class="sub">HSD ${U.date(l.expiryDate)}</span>` : ''}</td><td class="num">${U.num(l.systemQuantity, 3)}</td><td><input class="input" type="number" min="0" step="0.001" data-f="q" value="${l.physicalQuantity}" style="width:120px" ${edit ? '' : 'disabled'}></td><td class="num" data-v>${vr(l)}</td><td><input class="input" data-f="r" value="${esc(l.varianceReason || '')}" placeholder="Bắt buộc khi có chênh lệch" ${edit ? '' : 'disabled'}></td></tr>`), 'Phiếu không có dòng kiểm kê')
          + `<div class="small muted mt">Tổng giá trị chênh lệch: <b>${U.money(d.totalVarianceValue || 0)}</b>.${edit ? ' Lưu số đếm trước khi gửi duyệt hoặc ghi sổ.' : ''}</div>`,
        foot: `<button class="btn" data-close>Đóng</button><button class="btn" id="cmPrint">${I('export')} In</button>${edit ? '<button class="btn primary" id="cmSave">Lưu số đếm</button>' : ''}${acts.includes('submit') && d.needsApproval ? '<button class="btn orange" id="cmSubmit">Gửi duyệt</button>' : ''}${acts.includes('post') ? '<button class="btn primary" id="cmPost">Ghi sổ</button>' : ''}`,
        onMount: (m, close) => {
          const next = ['#cmSubmit', '#cmPost'].map(s => m.querySelector(s)).filter(Boolean);
          m.querySelectorAll('tr[data-l]').forEach(tr => tr.querySelectorAll('input').forEach(i => i.oninput = () => {
            next.forEach(b => { b.disabled = true; b.title = 'Lưu số đếm trước'; });
            const q = tr.querySelector('[data-f="q"]').value, dv = q === '' ? 0 : Number(q) - Number(tr.dataset.sys);
            tr.querySelector('[data-v]').textContent = U.num(dv, 3);
          }));
          m.querySelector('#cmPrint').onclick = () => printDoc('StockCount/Print', id, 'biên bản kiểm kê');
          const bs = m.querySelector('#cmSave');
          if (bs) bs.onclick = () => {
            const listLine = [...m.querySelectorAll('tr[data-l]')].map(tr => ({ id: tr.dataset.l, q: tr.querySelector('[data-f="q"]').value, sys: Number(tr.dataset.sys), r: tr.querySelector('[data-f="r"]').value.trim() }));
            if (listLine.some(l => l.q === '' || Number(l.q) < 0)) return UI.toast('Số đếm thực tế phải từ 0 trở lên', 'err');
            if (listLine.some(l => Number(l.q) !== l.sys && !l.r)) return UI.toast('Dòng có chênh lệch phải ghi lý do', 'err');
            Store.act('StockCount/RecordCounts', { method: 'PUT', json: { id, listLine: listLine.map(l => ({ id: l.id, physicalQuantity: Number(l.q), varianceReason: l.r || null })) }, tables: ['StockCount', 'StockCountLine'] }).then(r => {
              if (!r.ok) return; close(); UI.toast(okText(r, 'Đã lưu số đếm'), 'ok'); APP.refresh(); countModal(id);
            });
          };
          const bsu = m.querySelector('#cmSubmit');
          if (bsu) bsu.onclick = () => UI.confirm(`Gửi duyệt phiếu kiểm kê <b>${no}</b>? Sau khi gửi không sửa được số đếm cho tới khi có kết quả duyệt.`, () => actInv('StockCount/Submit', { form: { Id: id } }, 'Đã gửi duyệt phiếu kiểm kê').then(r => { if (r.ok) close(); }), { ok: 'Gửi duyệt' });
          const bp = m.querySelector('#cmPost');
          if (bp) bp.onclick = () => postCountAsk({ Id: id, CountNumber: sc.countNumber }, close);
        }
      });
    });
  }

  // ---------- Chênh lệch tiêu hao: định mức so với thực xuất (sinh khi đóng lệnh sản xuất) ----------
  const cv = { from: null, to: null, over: false, key: null, x: null, busy: false };
  const cvKey = () => cv.from + '|' + cv.to + '|' + cv.over;
  function cvDraw(body) {
    const x = cv.x;
    if (!x || cv.key !== cvKey()) { body.innerHTML = `<div class="card panel empty">${I('database')}<h2 class="mt">Đang tải…</h2></div>`; return; }
    if (!x.ok) { body.innerHTML = `<div class="notice gap">${I('alert')}<div><b>Không tải được chênh lệch tiêu hao</b>${esc(x.message || '')}</div></div>`; return; }
    const rows = listOf(x).map((r, i) => Object.assign({ Id: r.id || 'cv' + i }, r)), over = rows.filter(r => r.isOverThreshold).length;
    const q = k => r => U.num(r[k], 3);
    body.innerHTML = `<div class="kpis">${UI.kpi('Dòng chênh lệch', U.num(rows.length), '', 'clipboard', 'green')}${UI.kpi('Vượt ngưỡng cảnh báo', U.num(over), over ? '<span class="bad">Cần giải trình</span>' : 'Không có', 'alert', over ? 'red' : 'green')}${UI.kpi('Giá trị chênh lệch', U.money(U.sum(rows, 'varianceCost')), '', 'calc', 'orange')}</div><div class="card panel" id="cvT"></div>`;
    UI.table(body.querySelector('#cvT'), { rows, pageSize: 15,
      cols: [{ k: 'productionOrderNumber', label: 'Lệnh SX' }, { k: 'productionDate', label: 'Ngày SX', render: r => U.date(r.productionDate) }, { k: 'dishName', label: 'Món' }, { k: 'ingredientName', label: 'Nguyên liệu' }, { k: 'standardQuantity', label: 'Định mức', num: true, render: q('standardQuantity') }, { k: 'actualQuantity', label: 'Thực xuất', num: true, render: q('actualQuantity') },
        { k: 'varianceQuantity', label: 'Chênh lệch', num: true, render: r => `<span class="${r.isOverThreshold ? 'bad' : ''}">${U.num(r.varianceQuantity, 3)}</span>` }, { k: 'unitName', label: 'ĐVT' }, { k: 'variancePercent', label: '% lệch', num: true, render: r => `<span class="${r.isOverThreshold ? 'bad' : ''}">${has(r.variancePercent) ? U.num(r.variancePercent, 1) + '%' : '–'}</span>` },
        { k: 'varianceCost', label: 'Giá trị lệch', num: true, render: r => U.money(r.varianceCost) }, { k: 'reason', label: 'Lý do' }] });
  }
  function cvLoad() {
    const key = cvKey();
    if (cv.busy || cv.key === key) return;
    cv.busy = true;
    Store.query('MaterialConsumptionVariance/GetList', { From: cv.from, To: cv.to, OverThresholdOnly: cv.over ? true : null }).then(x => {
      cv.busy = false; cv.key = key; cv.x = x;
      const b = document.getElementById('cvBody'); if (!b || APP.current !== 'scm-consumption-var') return;
      cvDraw(b); cvLoad();
    });
  }
  APP.register('scm-consumption-var', (el, f) => {
    if (!Store.isLive) return baseList(el, f);
    if (!cv.from) { cv.to = Store.ctx.date; cv.from = U.addDays(Store.ctx.date, -30); }
    el.innerHTML = APP.header(f, `<input type="date" class="input" id="cvFrom" value="${cv.from}" style="width:auto"><input type="date" class="input" id="cvTo" value="${cv.to}" style="width:auto"><button class="btn" id="cvReload">${I('history')} Tải lại</button>`, { noNote: true, sub: 'So sánh nguyên liệu thực xuất với định mức theo công thức. Số liệu sinh khi đóng lệnh sản xuất.' }) + `
    <div class="card panel mb"><label class="small"><input type="checkbox" id="cvOver" ${cv.over ? 'checked' : ''}> Chỉ dòng vượt ngưỡng cảnh báo</label></div><div id="cvBody"></div>`;
    el.querySelector('#cvFrom').onchange = e => { if (e.target.value) { cv.from = e.target.value; APP.refresh(); } };
    el.querySelector('#cvTo').onchange = e => { if (e.target.value) { cv.to = e.target.value; APP.refresh(); } };
    el.querySelector('#cvOver').onchange = e => { cv.over = e.target.checked; APP.refresh(); };
    el.querySelector('#cvReload').onclick = () => { cv.key = null; APP.refresh(); };
    cvDraw(el.querySelector('#cvBody')); cvLoad();
  });
})();
