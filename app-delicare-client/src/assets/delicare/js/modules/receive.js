/* UI-6.50 (D-132 – anh chốt 2026-11-26): Nhận hàng theo nhà cung cấp – như mô phỏng docs/frontend-handoff/assets/receive-mockup.html.
 * - Màn Nhận hàng (scm-gr): ◀ ngày ▶ · ô tìm "Tìm NCC, số đơn mua, số hóa đơn NCC…" (Enter → form gộp của NCC đó; KHÔNG trình quét QR trong web) · 4 chỉ số ·
 *   thẻ theo NCC (số đơn, số dòng, giờ hẹn, quá hẹn đỏ, đã nhận đủ làm mờ + Xem phiếu) – thay khối "Đơn mua hẹn giao" của UI-6.29. API PurchaseOrder/GetDueBySupplier.
 * - Form "Nhận hàng – <NCC>": một form cho mọi đơn còn mở của NCC (tick bỏ đơn không giao hôm nay), dòng theo nguyên liệu (cộng các đơn), thanh chung: kho nhận ·
 *   số hóa đơn NCC ("[HĐ NCC]" trong ghi chú) · nhiệt độ thùng xe · Excel phiếu giao NCC (tải mẫu / tải lên điền form, không lưu tệp) · "✓ Nhận đủ – Đạt tất cả".
 *   Lô NCC / hạn dùng chỉ khi nguyên liệu quản lý lô / hạn ("↓ áp dòng dưới"); nhiệt độ "theo xe" / sửa; kết quả + nhận xét. Lưu → GoodsReceipt/CreateMany
 *   (máy chủ chia về từng đơn – đơn cũ trước, mỗi đơn 1 phiếu, tự đạt nhập kho ngay, cần QA gửi QA; QA tại cửa kho: "Kết luận & nhập kho").
 * - Điện thoại 390: thẻ NCC → form dạng thẻ (CSS .rcv-*), nút to "Nhận đủ – Đạt tất cả". Không dùng camera trên web (QR in trên đơn mua mở bot).
 * Phiếu chờ QA vẫn mở form kiểm tra của kho.js (APP.khoReceive). Tải SAU kho.js. */
(function () {
  if (!window.APP || !window.Store) return;
  const U = UTIL, I = U.icon, esc = U.esc;
  const has = v => v !== null && v !== undefined && v !== '';
  const STORAGE = { CHILLED: 'Mát', FROZEN: 'Đông lạnh', DRY: 'Khô', AMBIENT: 'Khô' };
  const RESULT = [['DAT', 'Đạt'], ['DAT_MOT_PHAN', 'Đạt một phần'], ['KHONG_DAT', 'Không đạt']];
  const TABLES = ['GoodsReceipt', 'GoodsReceiptLine', 'QCInspection', 'QCInspectionLine', 'PurchaseOrder', 'PurchaseOrderLine', 'MaterialLot', 'InventoryBalance', 'InventoryTransaction', 'SupplierIssue'];
  const st = { day: null, q: '' };
  const today = () => U.day(Store.ctx.date || new Date());
  const shortNo = n => String(n || '').split('-').pop();
  const hhmm = d => { if (!d) return ''; const t = new Date(d); return t.getHours() || t.getMinutes() ? String(t.getHours()).padStart(2, '0') + ':' + String(t.getMinutes()).padStart(2, '0') : U.date(d).slice(0, 5); };
  const late = m => (m >= 1440 ? `quá hẹn ${Math.floor(m / 1440)} ngày` : m >= 120 ? `quá hẹn ${Math.floor(m / 60)} giờ${m % 60 ? ' ' + (m % 60) + ' phút' : ''}` : `quá hẹn ${m} phút`);
  const num = v => (has(v) && v !== '' ? Number(String(v).replace(',', '.')) : null);

  // ================= Màn Nhận hàng: thẻ NCC + ô tìm + 4 chỉ số =================
  APP.rcvDue = el => {
    let box = el.querySelector('#grDue');
    const h = el.querySelector('.page-heading');
    if (!box && h) { h.insertAdjacentHTML('afterend', '<div class="card panel mb" id="grDue"></div>'); box = el.querySelector('#grDue'); }
    if (!box) return;
    const day = st.day || today(), isToday = day === today();
    box.innerHTML = `<div class="rcv-top"><div class="row-flex" style="gap:6px"><button class="btn sm" data-dd="-1" aria-label="Ngày trước">◀</button><b>${isToday ? 'Hôm nay ' : ''}${U.date(day).slice(0, 5)}</b><button class="btn sm" data-dd="1" aria-label="Ngày sau">▶</button>${isToday ? '' : '<button class="btn sm" data-dd="0">Hôm nay</button>'}</div>
      <div class="rcv-search">${I('search')}<input class="input" id="rcvQ" placeholder="Tìm NCC, số đơn mua, số hóa đơn NCC…" value="${esc(st.q)}"><button class="btn primary" id="rcvGo">Nhận hàng</button></div></div>
      <div id="rcvKpi" class="mt"><div class="muted small">Đang tải…</div></div><div id="rcvCards" class="rcv-cards mt"></div>
      <div class="small muted mt">Đơn mua của NCC khác / ngày khác: gõ tên NCC, số đơn mua hoặc số hóa đơn NCC ở ô tìm – hệ thống tự gom các đơn còn mở của cùng NCC. NCC mang đơn mua in theo → quét QR trên phiếu bằng camera điện thoại → mở bot nhận hàng.</div>`;
    box.querySelectorAll('[data-dd]').forEach(b => b.onclick = () => { const n = +b.dataset.dd; st.day = n === 0 ? null : U.addDays(day, n); APP.rcvDue(el); });
    const q = box.querySelector('#rcvQ'), go = () => { st.q = q.value.trim(); if (!st.q) return UI.toast('Gõ tên NCC, số đơn mua hoặc số hóa đơn NCC', 'warn'); APP.rcvOpen({ keyword: st.q, date: day }); };
    q.onkeydown = e => { if (e.key === 'Enter') go(); };
    box.querySelector('#rcvGo').onclick = go;
    Store.query('PurchaseOrder/GetDueBySupplier', { date: day }).then(x => {
      if (!box.isConnected) return;
      if (!x.ok || !x.data) { box.querySelector('#rcvKpi').innerHTML = `<div class="muted small">${esc(x.message || 'Không tải được NCC hẹn giao')}</div>`; return; }
      const d = x.data, cards = d.listSupplier || [], over = cards.filter(c => has(c.overdueMinutes)), full = cards.filter(c => c.receivedFull);
      box.querySelector('#rcvKpi').innerHTML = UI.kpis([
        UI.kpi(`NCC hẹn giao ${isToday ? 'hôm nay' : U.date(day).slice(0, 5)}`, U.num(d.supplierCount || 0), `${U.num(d.orderCount || 0)} đơn mua · ${U.num(d.lineCount || 0)} dòng`, 'truck', 'blue'),
        UI.kpi('Đã nhận đủ', U.num(d.receivedFullCount || 0), full.map(c => c.supplierName).slice(0, 2).join(', '), 'check', 'green'),
        UI.kpi('Quá hẹn chưa nhận', U.num(d.overdueCount || 0), over.slice(0, 1).map(c => `${c.supplierName} · ${late(c.overdueMinutes)}`).join(''), 'alert', d.overdueCount ? 'red' : 'green'),
        UI.kpi('Chờ QA kết luận', U.num(d.qcPendingCount || 0), 'phiếu', 'shield', d.qcPendingCount ? 'orange' : 'green'),
      ]);
      box.querySelector('#rcvCards').innerHTML = cards.length ? cards.map((c, i) => `<div class="rcv-card ${c.receivedFull ? 'done' : ''} ${has(c.overdueMinutes) ? 'late' : ''}">
          <div class="spread"><b>${esc(c.supplierName || '')}</b>${c.earliestExpected ? `<span class="small">${hhmm(c.earliestExpected)}</span>` : ''}</div>
          <div class="small muted">${c.receivedFull ? `Đã nhận đủ · ${U.num(c.receiptCount)} phiếu` : `${U.num(c.orderCount)} đơn · ${U.num(c.lineCount)} dòng · ${esc((c.listOrderNumber || []).map(shortNo).join(', '))}`}</div>
          ${has(c.overdueMinutes) ? `<div class="small" style="color:var(--red,#c62828);font-weight:600">${late(c.overdueMinutes)}</div>` : ''}
          <div class="mt">${c.receivedFull ? (c.lastReceiptId ? `<a class="btn sm" href="${esc(APP.detailHref ? APP.detailHref('GoodsReceipt', c.lastReceiptId) : '#/scm-gr')}">Xem phiếu</a>` : '') : `<button class="btn sm primary" data-rcv="${i}">${I('package')} Nhận hàng</button>`}</div></div>`).join('')
        : '<div class="muted small">Không có NCC nào hẹn giao / quá hẹn chưa nhận.</div>';
      box.querySelectorAll('[data-rcv]').forEach(b => b.onclick = () => APP.rcvOpen({ supplierId: cards[+b.dataset.rcv].supplierId, date: day }));
    });
  };

  // ================= Form "Nhận hàng – <NCC>" (gộp nhiều đơn mua) =================
  APP.rcvOpen = src => {
    const qa = Store.canApprove('qc-inspection');
    Promise.all([Store.query('PurchaseOrder/GetReceivingBundle', src), Store.ensure('Warehouse')]).then(([x]) => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không tải được đơn mua của NCC'), 'err');
      const b = x.data, orders = b.listOrder || [];
      const sel = new Set(orders.filter(o => o.due || o.matched).map(o => o.purchaseOrderId));
      if (!sel.size && orders.length) sel.add(orders[0].purchaseOrderId);
      const rows = (b.listLine || []).map(l => ({ l, q: '', lot: '', exp: '', mfg: '', temp: null, tempOwn: false, res: 'DAT', aq: '', rm: '', touched: false }));
      const whs = Store.rows('Warehouse').filter(w => !w.IsDeleted && (!w.Status || w.Status === 'Active'));
      const defWh = b.defaultWarehouseId || APP.rawWarehouseId && APP.rawWarehouseId() || (whs.find(w => /nguyên liệu|NL/i.test(w.Name || '')) || whs[0] || {}).Id;
      const partsOf = r => (r.l.listPart || []).filter(p => sel.has(p.purchaseOrderId));
      const remainOf = r => partsOf(r).reduce((s, p) => s + p.remaining, 0);
      const visible = () => rows.filter(r => partsOf(r).length);
      const cold = r => has(r.l.tempAcceptMax);
      const matchedOther = (b.matchedBy === 'order' || b.matchedBy === 'invoice' || b.matchedBy === 'keyword') && b.matchedText ? orders.filter(o => !o.matched).length : 0;
      // Số phiếu sẽ lập = số đơn mua nhận được hàng (chia đơn cũ trước như máy chủ).
      const receiptCount = () => {
        const pos = new Set();
        visible().forEach(r => { let left = num(r.q) || 0; partsOf(r).forEach((p, i, arr) => { const take = i === arr.length - 1 ? left : Math.min(left, p.remaining); if (take > 0) pos.add(p.purchaseOrderId); left -= take; }); });
        return pos.size;
      };
      UI.modal({
        title: 'Nhận hàng – ' + esc(b.supplierName || ''), size: 'xl',
        body: `<div id="rcvHead"></div>
          <div class="rcv-bar mb">
            <label class="small">Kho nhận<select class="select" id="rcvWh">${whs.map(w => `<option value="${w.Id}" ${w.Id === defWh ? 'selected' : ''}>${esc(Store.label('Warehouse', w))}</option>`).join('')}</select></label>
            <label class="small">Số hóa đơn / phiếu giao NCC<input class="input" id="rcvInv" placeholder="tùy chọn"></label>
            ${b.needsVehicleTemperature ? '<label class="small">Nhiệt độ thùng xe (°C)<input class="input" id="rcvVt" type="number" step="0.1" placeholder="áp cho hàng mát / đông"></label>' : ''}
            <div class="small"><div>Phiếu giao của NCC (Excel)</div><div class="row-flex" style="gap:6px"><button class="btn sm" id="rcvTpl">${I('down')} Mẫu</button><button class="btn sm" id="rcvUp">${I('up')} Tải lên</button><input type="file" id="rcvFile" accept=".xlsx" hidden></div></div>
            <button class="btn primary rcv-all" id="rcvAll">${I('check')} Nhận đủ – Đạt tất cả</button>
          </div>
          <div id="rcvXls"></div><div id="rcvBody"></div>
          <div class="small muted mt" id="rcvNote"></div>`,
        foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="rcvSave"></button>`,
        onMount: (m, close) => {
          const $ = s => m.querySelector(s);
          const paintHead = () => {
            const vis = visible();
            $('#rcvHead').innerHTML = (matchedOther ? `<div class="notice info mb">${I('search')}<div>Tìm <b>${esc(b.matchedText)}</b> → tự gom ${matchedOther} đơn còn mở khác của cùng NCC. Bỏ chọn đơn không giao hôm nay.</div></div>` : '')
              + `<div class="small mb">Gộp <b>${sel.size}</b> đơn mua · ${vis.reduce((s, r) => s + partsOf(r).length, 0)} dòng → <b>${vis.length}</b> nguyên liệu</div>`
              + `<div class="rcv-orders mb">${orders.map(o => `<label class="rcv-chip ${sel.has(o.purchaseOrderId) ? 'on' : ''}"><input type="checkbox" data-po="${o.purchaseOrderId}" ${sel.has(o.purchaseOrderId) ? 'checked' : ''}> ${esc(o.purchaseOrderNumber || '')}<span class="muted"> · ${o.expectedDate ? 'hẹn ' + esc(U.date(o.expectedDate).slice(0, 5)) : 'chưa hẹn'} · ${U.num(o.lineCount)} dòng</span></label>`).join('')}</div>`;
            m.querySelectorAll('[data-po]').forEach(c => c.onchange = () => { if (c.checked) sel.add(c.dataset.po); else sel.delete(c.dataset.po); paintHead(); paint(); });
          };
          const lotCell = (r, i) => (r.l.lotControl ? `<input class="input" data-f="lot" data-i="${i}" value="${esc(r.lot)}" placeholder="Lô NCC (trống = lô nội bộ)"><a href="javascript:void 0" class="small" data-apply="${i}">↓ áp dòng dưới</a>` : '<span class="small muted">không quản lý lô</span>');
          const expCell = (r, i) => (r.l.expiryControl ? `<input class="input" type="date" data-f="exp" data-i="${i}" value="${esc(r.exp)}">` : '<span class="small muted">tự tính</span>');
          const tempCell = (r, i) => (!cold(r) ? '<span class="small muted">–</span>' : r.tempOwn ? `<input class="input" type="number" step="0.1" data-f="temp" data-i="${i}" value="${has(r.temp) ? r.temp : ''}" style="width:80px">`
            : `<span class="small">theo xe</span> <a href="javascript:void 0" class="small" data-own="${i}">sửa</a>`);
          const resCell = (r, i) => `<select class="select" data-f="res" data-i="${i}">${RESULT.map(([k, l]) => `<option value="${k}" ${r.res === k ? 'selected' : ''}>${l}</option>`).join('')}</select>${r.res === 'DAT_MOT_PHAN' ? `<input class="input mt" type="number" min="0" step="any" data-f="aq" data-i="${i}" value="${esc(r.aq)}" placeholder="Số đạt">` : ''}`;
          const cls = r => { const q = num(r.q) || 0, need = (r.l.lotControl && !r.lot) || (r.l.expiryControl && !r.exp); return r.res !== 'DAT' || (q > 0 && q < remainOf(r)) ? 'rcv-bad' : q > 0 && need ? 'rcv-need' : ''; };
          const paint = () => {
            const vis = visible();
            $('#rcvBody').innerHTML = vis.length ? `<div class="table-wrap rcv-table"><table class="table dc-table"><thead><tr><th>Nguyên liệu</th><th>Đơn mua</th><th class="num">Số đặt (còn)</th><th>Số nhận</th><th>Lô NCC</th><th>Hạn dùng</th><th>Nhiệt độ</th><th>Kết quả</th><th>Nhận xét</th></tr></thead><tbody>`
              + vis.map(r => { const i = rows.indexOf(r), parts = partsOf(r); return `<tr class="${cls(r)}" data-row="${i}">
                <td data-l="Nguyên liệu"><b>${esc(r.l.ingredientName || '')}</b><div class="small muted">${esc(STORAGE[String(r.l.storageType || '').toUpperCase()] || 'Khô')} · ${esc(r.l.unitName || '')}${r.l.qcRequired ? ' · cần QA' : ''}</div></td>
                <td data-l="Đơn mua" class="small">${esc(parts.map(p => shortNo(p.purchaseOrderNumber)).join(' + '))}</td>
                <td data-l="Số đặt (còn)" class="num">${U.num(remainOf(r), 2)}</td>
                <td data-l="Số nhận"><input class="input" type="number" min="0" step="any" data-f="q" data-i="${i}" value="${esc(r.q)}" placeholder="0" style="width:90px"></td>
                <td data-l="Lô NCC">${lotCell(r, i)}</td><td data-l="Hạn dùng">${expCell(r, i)}</td><td data-l="Nhiệt độ">${tempCell(r, i)}</td>
                <td data-l="Kết quả">${resCell(r, i)}</td><td data-l="Nhận xét"><input class="input" data-f="rm" data-i="${i}" value="${esc(r.rm)}" placeholder="${r.res === 'DAT' ? '' : 'bắt buộc'}"></td></tr>`; }).join('')
              + '</tbody></table></div>' : '<div class="muted small">Chọn ít nhất một đơn mua.</div>';
            const lotRows = vis.filter(r => r.l.lotControl || r.l.expiryControl).length, n = receiptCount();
            $('#rcvNote').innerHTML = `Chỉ nguyên liệu có quản lý lô / hạn dùng mới hỏi Lô NCC và Hạn dùng (${lotRows} / ${vis.length} dòng); lô trống = lô nội bộ, hạn trống = ngày nhận + hạn bảo quản. Dòng vàng = cần nhập, đỏ = thiếu / không đạt. Lưu xong: tách ${n || 'N'} phiếu nhập theo đơn mua (đơn cũ trước), tự đạt nhập kho ngay, cần QA gửi QA, thiếu / không đạt lập sự cố NCC.`;
            $('#rcvSave').textContent = `${qa ? 'Kết luận & nhập kho' : 'Nhận hàng'} (${n} phiếu)`;
            $('#rcvSave').disabled = !n;
            m.querySelectorAll('[data-f]').forEach(e => e.addEventListener(e.tagName === 'SELECT' ? 'change' : 'input', () => {
              const r = rows[+e.dataset.i], f = e.dataset.f; r[f] = e.value; if (f === 'q') r.touched = true;
              if (f === 'res' || f === 'q') { const pos = e.selectionStart; paint(); const again = m.querySelector(`[data-f="${f}"][data-i="${e.dataset.i}"]`); if (again && f === 'q') { again.focus(); try { again.setSelectionRange(pos, pos); } catch (_) { /* number input */ } } }
              else { const tr = e.closest('tr'); if (tr) tr.className = cls(r); }
            }));
            m.querySelectorAll('[data-own]').forEach(a => a.onclick = () => { const r = rows[+a.dataset.own]; r.tempOwn = true; r.temp = num(($('#rcvVt') || {}).value); paint(); });
            m.querySelectorAll('[data-apply]').forEach(a => a.onclick = () => {
              const i = +a.dataset.apply, src = rows[i], after = visible().filter(r => rows.indexOf(r) > i && (r.l.lotControl || r.l.expiryControl));
              after.forEach(r => { if (r.l.lotControl) r.lot = src.lot; if (r.l.expiryControl && src.exp) r.exp = src.exp; });
              paint(); UI.toast(`Đã chép lô / hạn dùng cho ${after.length} dòng dưới`, 'ok');
            });
          };
          paintHead(); paint();
          $('#rcvAll').onclick = () => { visible().forEach(r => { if (!r.touched) { r.q = String(remainOf(r)); r.res = 'DAT'; } }); paint(); UI.toast('Đã đặt mọi dòng = số còn phải nhận, kết quả Đạt (giữ dòng đã sửa)', 'ok'); };
          $('#rcvTpl').onclick = () => Store.download('PurchaseOrder/DeliveryNoteTemplate', { supplierId: b.supplierId });
          $('#rcvUp').onclick = () => $('#rcvFile').click();
          $('#rcvFile').onchange = () => {
            const file = $('#rcvFile').files[0]; $('#rcvFile').value = ''; if (!file) return;
            const fd = new FormData(); fd.append('file', file); fd.append('supplierId', b.supplierId);
            Store.act('PurchaseOrder/ParseDeliveryNote', { json: fd }).then(r => {
              if (!r.ok) return;
              const list = (r.res && r.res.data) || r.data || [], bad = [], sum = {};
              list.forEach(d => {
                const row = d.purchaseOrderLineId && rows.find(x => (x.l.listPart || []).some(p => p.purchaseOrderLineId === d.purchaseOrderLineId));
                if (!row || d.problem) { bad.push(d); return; }
                (row.l.listPart || []).forEach(p => { if (p.purchaseOrderLineId === d.purchaseOrderLineId) sel.add(p.purchaseOrderId); });
                sum[rows.indexOf(row)] = (sum[rows.indexOf(row)] || 0) + (Number(d.quantity) || 0);
                if (d.supplierLotNumber && row.l.lotControl) row.lot = d.supplierLotNumber;
                if (d.expiryDate && row.l.expiryControl) row.exp = U.day(d.expiryDate);
              });
              Object.entries(sum).forEach(([i, q]) => { rows[+i].q = String(q); rows[+i].touched = true; });
              $('#rcvXls').innerHTML = `<div class="notice ${bad.length ? 'warn' : 'ok'} mb">${I('file')}<div>Phiếu giao NCC: điền ${Object.keys(sum).length} nguyên liệu${bad.length ? ` · <b>${bad.length} dòng không khớp</b> – nhập tay:<br>` + bad.slice(0, 8).map(d => `Dòng ${d.row}: ${esc([d.purchaseOrderNumber, d.itemCode, d.itemName].filter(Boolean).join(' · '))} – ${esc(d.problem || 'không khớp')}`).join('<br>') : ''}</div></div>`;
              paintHead(); paint();
            });
          };
          $('#rcvSave').onclick = () => {
            const vt = num(($('#rcvVt') || {}).value), out = [];
            for (const r of visible()) {
              const q = num(r.q) || 0; if (q <= 0) continue;
              const name = r.l.ingredientName || '';
              if (q > remainOf(r) * 1.5) return UI.toast(`${esc(name)}: số nhận ${U.num(q, 2)} vượt xa số còn phải nhận (${U.num(remainOf(r), 2)})`, 'err');
              if (cold(r) && !has(r.tempOwn ? r.temp : vt)) return UI.toast(`${esc(name)}: hàng ${STORAGE[String(r.l.storageType).toUpperCase()] || 'mát'} – nhập nhiệt độ thùng xe hoặc đo riêng dòng này`, 'err');
              if (r.res !== 'DAT' && String(r.rm || '').trim().length < 3) return UI.toast(`${esc(name)}: nhập nhận xét khi kết quả không phải Đạt`, 'err');
              const aq = num(r.aq);
              if (r.res === 'DAT_MOT_PHAN' && !(aq > 0 && aq < q)) return UI.toast(`${esc(name)}: số đạt phải lớn hơn 0 và nhỏ hơn số nhận (${U.num(q, 2)})`, 'err');
              out.push({ ingredientId: r.l.ingredientId, listPurchaseOrderLineId: partsOf(r).map(p => p.purchaseOrderLineId), receivedQuantity: q, supplierLotNumber: r.l.lotControl && r.lot ? r.lot : null,
                expiryDate: r.l.expiryControl && r.exp ? r.exp : null, temperatureC: cold(r) && r.tempOwn ? num(r.temp) : null, result: r.res, acceptedQuantity: r.res === 'DAT_MOT_PHAN' ? aq : null, remark: r.rm || null });
            }
            if (!out.length) return UI.toast('Nhập số nhận cho ít nhất một dòng (hoặc bấm "Nhận đủ – Đạt tất cả")', 'err');
            $('#rcvSave').disabled = true;
            Store.act('GoodsReceipt/CreateMany', { json: { warehouseId: $('#rcvWh').value, receiptDate: U.iso(new Date()), vehicleTemperatureC: vt, supplierInvoiceNo: $('#rcvInv').value.trim() || null, conclude: qa, listLine: out }, tables: TABLES }).then(r => {
              if (!r.ok) { $('#rcvSave').disabled = false; return; }
              const d = (r.res && r.res.data) || r.data || {}, w = d.listWarning || [];
              UI.toast(esc((r.res && r.res.responseMessage) || 'Đã nhận hàng') + (d.issueCount ? '<br>Dòng thiếu / không đạt → đã lập sự cố nhà cung cấp' : '') + (w.length ? '<br><span class="small">' + w.slice(0, 6).map(esc).join('<br>') + '</span>' : ''), w.length || d.issueCount ? 'warn' : 'ok');
              close(); APP.refresh();
            });
          };
        },
      });
    });
  };
})();
