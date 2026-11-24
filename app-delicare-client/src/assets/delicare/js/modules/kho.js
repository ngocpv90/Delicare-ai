/* UI-6.29 (D-110): Kho gọn 12 → 5 mục + "Nhận hàng & kiểm tra" một form + "Sổ kiểm tra hàng nhập" chỉ xem.
 * - Nhận hàng (scm-gr): khối "Đơn mua hẹn giao" (PurchaseOrder/GetDueForReceiving, chọn ngày ◀ ▶) + danh sách phiếu (không có "Tạo mới" chung).
 *   Form một trang cho mỗi dòng đơn mua còn chờ nhận: số nhận, lô NCC, hạn dùng, nhiệt độ (báo vùng ngay), cảm quan, bao bì, kết quả gợi ý, số đạt, nhận xét.
 *   Có quyền Duyệt "Kiểm tra hàng nhập" → "Kết luận & nhập kho" (GoodsReceipt/Create rồi QcInspection/Conclude – tự ghi sổ); không có → "Gửi QA".
 *   Mọi dòng tự đạt → máy chủ tự ghi sổ ngay. Không còn tự chuyển sang màn kiểm tra.
 * - Hàng chờ QA (#/wf-inbox?type=qc-inspection): phiếu chờ kết luận → mở CÙNG form đã điền sẵn số liệu kho nhập, QA chỉ xác nhận / sửa dòng có vấn đề.
 * - Tồn kho: tab Theo nguyên liệu · Theo lô & hạn dùng · Sổ kho · Định mức theo kho (Chuyển kho / Điều chỉnh tồn chỉ ở tab đầu).
 * - Kho & vị trí: tab Kho · Khu bảo quản · Vị trí kệ (In QR vị trí). Xuất kho sản xuất: tab Phiếu xuất · Giữ chỗ nguyên liệu.
 * - Sổ kiểm tra hàng nhập (qc-inspection): QcInspection/GetBook + GetSupplierSummary – gom theo ngày, lọc, 4 chỉ số, ngăn chi tiết + "Lô này đã đi đâu".
 * Chế độ DEMO giữ màn cũ. Tải SAU scm.js, qc.js, approval.js, ghep.js, prod-menu.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const has = v => v !== null && v !== undefined && v !== '';
  const st = { inv: 'ing', wh: 'wh', iss: 'doc', dueDay: null, book: { tab: 'book', period: '7d', from: null, to: null, q: '', result: '', storage: '', supplier: '', out: false } };
  const STORAGE = { CHILLED: 'Mát', FROZEN: 'Đông lạnh', DRY: 'Khô', AMBIENT: 'Khô' };
  const RESULT = { DAT: ['green', 'Đạt'], DAT_MOT_PHAN: ['orange', 'Đạt một phần'], KHONG_DAT: ['red', 'Không đạt'] };
  const ZONE = { ACCEPT: ['green', 'Đạt nhiệt độ'], CONDITIONAL: ['orange', 'Nhận có điều kiện'], REJECT: ['red', 'Vượt ngưỡng – loại'], MISSING: ['orange', 'Chưa đo'], NOT_CHECKED: ['', 'Không kiểm'] };
  const APPEAR = ['Đạt', 'Bẩn / lẫn tạp chất', 'Biến màu / mùi lạ', 'Dập / hư hỏng', 'Khác'];
  const PACK = ['Đạt', 'Rách / thủng', 'Phồng / hở', 'Sai / thiếu nhãn', 'Khác'];
  const PUR_TABLES = ['GoodsReceipt', 'GoodsReceiptLine', 'QCInspection', 'QCInspectionLine', 'PurchaseOrder', 'PurchaseOrderLine', 'MaterialLot', 'InventoryBalance', 'InventoryTransaction', 'SupplierIssue'];
  const today = () => U.day(Store.ctx.date || new Date());
  const dayAdd = (d, n) => U.addDays(d, n);
  const resultTag = r => (r && RESULT[r] ? UI.tag(RESULT[r][1], RESULT[r][0]) : UI.tag('Chờ kết luận', 'orange'));
  const zoneTag = z => (z && ZONE[z] && z !== 'NOT_CHECKED' ? UI.tag(ZONE[z][1], ZONE[z][0]) : '');
  const tabsHtml = (key, list) => `<div class="tabs mb" data-khtabs>${list.map(([k, l]) => `<button class="tab ${st[key] === k ? 'active' : ''}" data-kh="${k}">${l}</button>`).join('')}</div>`;
  const placeTabs = (el, html) => { const h = el.querySelector('.page-heading'); if (h && !el.querySelector('[data-khtabs]')) h.insertAdjacentHTML('afterend', html); };
  const bindTabs = (el, key, rerender) => el.querySelectorAll('[data-kh]').forEach(b => b.onclick = () => { st[key] = b.dataset.kh; rerender(); });
  const asList = (el, f, id, top) => {
    const meta = Object.assign({}, APP.FN[id], { name: f.name });
    const draw = () => { (APP.SCREENS[id] || ((e, ff) => APP.genericList(e, ff, { noAdd: UI.viewOnly(meta.table) })))(el, meta, {}); placeTabs(el, top); if (APP.afterRender) APP.afterRender(el, id); };
    return meta.table ? Store.ensure(meta.table).then(draw, draw) : Promise.resolve(draw());
  };

  // ================= Ngưỡng nhiệt độ nhận (Ingredient/GetFormOptions – BusinessRules:ReceivingTemp) =================
  let rules = null;
  const loadRules = () => (rules ? Promise.resolve(rules) : Store.query('Ingredient/GetFormOptions', {}).then(x => {
    rules = {}; ((x.ok && x.data && x.data.listStorageType) || []).forEach(s => { if (has(s.acceptMax)) rules[String(s.code).toUpperCase()] = s; });
    return rules;
  }, () => (rules = {})));
  const ruleOf = storage => (storage && rules ? rules[String(storage).toUpperCase()] : null);
  const zoneOf = (storage, t) => { const r = ruleOf(storage); if (!r) return 'NOT_CHECKED'; if (!has(t)) return 'MISSING'; const v = Number(t); return v <= r.acceptMax ? 'ACCEPT' : v <= r.conditionalMax ? 'CONDITIONAL' : 'REJECT'; };
  const needsTemp = storage => !!ruleOf(storage) || ['CHILLED', 'FROZEN'].includes(String(storage || '').toUpperCase());
  const suggest = (zone, expiry, appearance, packaging) => {
    if (zone === 'REJECT' || (expiry && expiry < today())) return 'KHONG_DAT';
    if ((appearance && appearance !== 'Đạt') || (packaging && packaging !== 'Đạt')) return 'KHONG_DAT';
    return 'DAT';
  };

  // ================= Form "Nhận hàng & kiểm tra" =================
  // src = { po } (nhận mới theo đơn mua) | { inspectionId } (QA xác nhận phiếu đã nhận – số liệu kho điền sẵn).
  function receiveForm(src, done) {
    const qa = Store.canApprove('qc-inspection');
    const load = src.inspectionId
      ? Store.query('QcInspection/GetDetail', { id: src.inspectionId }).then(x => (x.ok ? { mode: 'qa', d: x.data || {} } : Promise.reject(x.message)))
      : Store.query('PurchaseOrder/GetDetail', { id: src.po.Id || src.po.purchaseOrderId }).then(x => (x.ok ? { mode: 'new', d: x.data || {} } : Promise.reject(x.message)));
    Promise.all([load, loadRules(), Store.ensure('Warehouse'), Store.ensure('Ingredient')]).then(([o]) => {
      const d = o.d, isQa = o.mode === 'qa';
      const remain = l => (has(l.remainingQuantity) ? l.remainingQuantity : l.quantity);
      const lines = isQa
        ? (d.listLine || []).map(l => ({ key: l.id, grlId: l.id, ingredientId: l.ingredientId, name: l.ingredientName, unit: l.unitName, storage: l.storageType || (Store.get('Ingredient', l.ingredientId) || {}).StorageType, qty: l.receivedQuantity, lot: l.supplierLotNumber, exp: l.expiryDate ? U.day(l.expiryDate) : '', t: l.temperature, app: l.appearance, pack: l.packaging, res: l.qualityStatus, remark: l.remark }))
        : (d.listLine || []).filter(l => remain(l) > 0).map(l => { const g = Store.get('Ingredient', l.ingredientId) || {}; return { key: l.id, poLineId: l.id, ingredientId: l.ingredientId, name: l.ingredientName, unit: l.unitName, storage: g.StorageType, qcRequired: g.QcRequired, qty: remain(l), remain: remain(l) }; });
      if (!lines.length) return UI.toast(isQa ? 'Phiếu không có dòng cần kiểm tra' : 'Đơn mua này không còn dòng nào chờ nhận', 'warn');
      const gr = d.goodsReceipt || {}, whs = Store.rows('Warehouse'), defWh = APP.rawWarehouseId() || (whs.find(w => /nguyên liệu|NL/i.test(w.Name || '')) || whs[0] || {}).Id;
      const title = isQa ? 'Kiểm tra hàng nhập – ' + esc(gr.receiptNumber || gr.ReceiptNumber || d.inspectionNumber || '') : 'Nhận hàng & kiểm tra – ' + esc(d.purchaseOrderNumber || (d.purchaseOrder || {}).purchaseOrderNumber || src.po.PurchaseOrderNumber || '');
      const sel = (list, v, f) => `<select class="select" data-f="${f}" style="width:130px">${list.map(x => `<option ${x === (v || 'Đạt') ? 'selected' : ''}>${esc(x)}</option>`).join('')}${v && !list.includes(v) ? `<option selected>${esc(v)}</option>` : ''}</select>`;
      const row = l => `<tr data-k="${l.key}">
        <td><b>${esc(l.name || '')}</b><br><span class="small muted">${esc(STORAGE[String(l.storage || '').toUpperCase()] || 'Khô')}${l.unit ? ' · ' + esc(l.unit) : ''}</span></td>
        <td>${isQa ? `<b>${U.num(l.qty, 2)}</b>` : `<input class="input" type="number" step="0.01" min="0" data-f="q" value="${l.qty}" style="width:90px"><br><span class="small muted">còn ${U.num(l.remain, 2)}</span>`}</td>
        <td>${isQa ? esc(l.lot || '–') : '<input class="input" data-f="lot" placeholder="Lô NCC" style="width:110px">'}</td>
        <td>${isQa ? U.date(l.exp) : '<input class="input" type="date" data-f="exp" style="width:140px">'}</td>
        <td><input class="input" type="number" step="0.1" data-f="t" value="${has(l.t) ? l.t : ''}" style="width:80px" ${needsTemp(l.storage) ? 'placeholder="bắt buộc"' : ''}><br><span class="small" data-z></span></td>
        <td>${sel(APPEAR, l.app, 'app')}</td><td>${sel(PACK, l.pack, 'pack')}</td>
        <td><select class="select" data-f="res" style="width:130px">${Object.keys(RESULT).map(k => `<option value="${k}" ${l.res === k ? 'selected' : ''}>${RESULT[k][1]}</option>`).join('')}</select>
          <input class="input mt" type="number" min="0" step="any" data-f="aq" placeholder="Số đạt" style="display:none;width:110px">
          ${String(l.storage || '').toUpperCase() === 'FROZEN' ? '<label class="small mt" style="display:none;gap:4px;font-weight:400" data-nt><input type="checkbox" data-f="nt"> Không có dấu hiệu rã đông</label>' : ''}
          <br><span class="small muted" data-sug></span></td>
        <td><input class="input" data-f="rm" value="${esc(l.remark || '')}" placeholder="Bắt buộc khi không Đạt" style="width:170px"></td></tr>`;
      UI.modal({
        title, sub: esc(d.supplierName || '') + (isQa ? ' · số liệu kho đã nhập sẵn – chỉ sửa dòng có vấn đề' : ''), size: 'xl',
        body: `${isQa ? '' : `<div class="field mb" style="max-width:320px"><label>Kho nhận</label><select class="select" id="rfWh">${whs.map(w => `<option value="${w.Id}" ${w.Id === defWh ? 'selected' : ''}>${esc(Store.label('Warehouse', w))}</option>`).join('')}</select></div>`}
          <div class="table-wrap"><table class="table dc-table"><thead><tr><th>Nguyên liệu</th><th>Số nhận</th><th>Lô NCC</th><th>Hạn dùng</th><th>Nhiệt độ °C</th><th>Cảm quan</th><th>Bao bì</th><th>Kết quả</th><th>Nhận xét</th></tr></thead><tbody>${lines.map(row).join('')}</tbody></table></div>
          <div class="small muted mt">${isQa ? 'Kết luận xong hệ thống ghi sổ phần đạt, lập lô; dòng không đạt tự lập sự cố nhà cung cấp.' : 'Dòng không nhận lần này: số nhận = 0. Hạn dùng để trống = ngày nhận + hạn bảo quản. Hàng mát / đông lạnh bắt buộc đo nhiệt độ. Nguyên liệu không yêu cầu kiểm tra + nhiệt độ đạt → tự đạt, hệ thống nhập kho ngay.'}</div>`,
        foot: `<button class="btn" data-close>Hủy</button>${isQa || qa ? '<button class="btn primary" id="rfGo">Kết luận & nhập kho</button>' : '<button class="btn primary" id="rfGo">Gửi QA</button>'}`,
        onMount: (m, close) => {
          const tr = k => m.querySelector(`tr[data-k="${k}"]`), fv = (t, f) => { const e = t.querySelector(`[data-f="${f}"]`); return e ? (e.type === 'checkbox' ? e.checked : e.value.trim()) : ''; };
          const sync = l => {
            const t = tr(l.key), z = zoneOf(l.storage, fv(t, 't')), exp = isQa ? l.exp : fv(t, 'exp'), sug = suggest(z, exp, fv(t, 'app'), fv(t, 'pack'));
            t.querySelector('[data-z]').innerHTML = zoneTag(z) + (ruleOf(l.storage) ? `<br><span class="muted">đạt ≤ ${U.num(ruleOf(l.storage).acceptMax, 1)} · có điều kiện ≤ ${U.num(ruleOf(l.storage).conditionalMax, 1)}</span>` : '');
            const res = t.querySelector('[data-f="res"]');
            if (!res.dataset.touched) res.value = sug;
            t.querySelector('[data-sug]').textContent = 'Gợi ý: ' + RESULT[sug][1] + (!isQa && l.qcRequired === false && sug === 'DAT' && z !== 'MISSING' ? ' (tự đạt)' : '');
            t.querySelector('[data-f="aq"]').style.display = res.value === 'DAT_MOT_PHAN' ? 'block' : 'none';
            const nt = t.querySelector('[data-nt]'); if (nt) nt.style.display = z === 'CONDITIONAL' ? 'flex' : 'none';
          };
          lines.forEach(l => { const t = tr(l.key); t.querySelectorAll('input,select').forEach(e => e.addEventListener(e.tagName === 'SELECT' ? 'change' : 'input', () => { if (e.dataset.f === 'res') e.dataset.touched = '1'; sync(l); })); if (isQa && l.res) t.querySelector('[data-f="res"]').dataset.touched = '1'; sync(l); });
          const collect = () => {
            const out = [];
            for (const l of lines) {
              const t = tr(l.key), q = isQa ? Number(l.qty) : Number(fv(t, 'q')) || 0; if (q <= 0) continue;
              const temp = fv(t, 't'), res = fv(t, 'res'), rm = fv(t, 'rm'), aq = Number(fv(t, 'aq'));
              if (needsTemp(l.storage) && !has(temp)) throw new Error(`${l.name}: hàng ${STORAGE[String(l.storage).toUpperCase()] || ''} bắt buộc đo nhiệt độ`);
              if (res !== 'DAT' && rm.length < 3) throw new Error(`${l.name}: nhập nhận xét khi kết quả không phải Đạt`);
              if (res === 'DAT_MOT_PHAN' && !(aq > 0 && aq < q)) throw new Error(`${l.name}: số đạt phải lớn hơn 0 và nhỏ hơn số nhận (${U.num(q, 2)})`);
              const nt = t.querySelector('[data-nt]'); if (nt && nt.style.display !== 'none' && res !== 'KHONG_DAT' && !fv(t, 'nt')) throw new Error(`${l.name}: hàng đông lạnh nhận có điều kiện – xác nhận không có dấu hiệu rã đông`);
              out.push({ l, q, temp: has(temp) ? Number(temp) : null, res, rm, aq, app: fv(t, 'app'), pack: fv(t, 'pack'), lot: isQa ? l.lot : fv(t, 'lot'), exp: isQa ? l.exp : fv(t, 'exp'), nt: !!fv(t, 'nt') });
            }
            if (!out.length) throw new Error('Nhập số nhận cho ít nhất một dòng');
            return out;
          };
          const conclude = (inspectionId, rows, grlOf) => Store.act('QcInspection/Conclude', { json: { id: inspectionId, note: null, listLine: rows.map(x => ({ goodsReceiptLineId: grlOf(x), result: x.res, temperature: x.temp, appearance: x.app, packaging: x.pack, remark: x.rm || null, acceptedQuantity: x.res === 'DAT_MOT_PHAN' ? x.aq : null, noThawConfirmed: x.nt || null })) }, tables: PUR_TABLES });
          const report = (r, sentToQa) => {
            const data = r.data || {}, g = data.goodsReceipt || {}, w = data.listWarning || [], rej = (data.listLine || []).some(l => l.qualityStatus === 'KHONG_DAT');
            const msg = g.status === 'Posted' || g.Status === 'Posted' ? 'Đã nhập kho – lập lô, cập nhật đơn mua' : sentToQa ? 'Đã gửi QA – phiếu chờ kết luận ở Phê duyệt › Kiểm tra hàng nhập' : 'Đã kết luận kiểm tra';
            UI.toast(msg + (rej ? '<br>Dòng không đạt → đã lập sự cố nhà cung cấp' : '') + (w.length ? '<br><span class="small">' + w.map(esc).join('<br>') + '</span>' : ''), w.length || rej ? 'warn' : 'ok');
            close(); if (done) done(); else APP.refresh();
          };
          m.querySelector('#rfGo').onclick = () => {
            let rows; try { rows = collect(); } catch (e) { return UI.toast(esc(e.message), 'err'); }
            if (isQa) return conclude(src.inspectionId, rows, x => x.l.grlId).then(r => { if (r.ok) report(r, false); });
            const json = { purchaseOrderId: src.po.Id || src.po.purchaseOrderId, warehouseId: m.querySelector('#rfWh').value, receiptDate: U.iso(new Date()),
              listLine: rows.map(x => ({ purchaseOrderLineId: x.l.poLineId, receivedQuantity: x.q, supplierLotNumber: x.lot || null, expiryDate: x.exp || null, temperatureC: x.temp })) };
            m.querySelector('#rfGo').disabled = true;
            Store.act('GoodsReceipt/Create', { json, tables: PUR_TABLES }).then(r => {
              if (!r.ok) { m.querySelector('#rfGo').disabled = false; return; }
              const data = r.data || {}, g = data.goodsReceipt || {};
              if (g.status === 'Posted' || !qa || data.qcResult) return report(r, !qa && g.status !== 'Posted');
              // QA tại cửa kho: kết luận ngay trên cùng phiếu (dòng phiếu nhập ↔ dòng đơn mua).
              const byPo = {}; (data.listLine || []).forEach(l => { byPo[l.purchaseOrderLineId] = l.id; });
              conclude(data.qcInspectionId, rows, x => byPo[x.l.poLineId]).then(c => {
                if (c.ok) return report(c, false);
                UI.toast('Đã tạo phiếu nhập nhưng chưa kết luận được – phiếu chờ ở Phê duyệt › Kiểm tra hàng nhập', 'warn'); close(); APP.refresh();
              });
            });
          };
        }
      });
    }).catch(msg => UI.toast(esc(msg || 'Không tải được dữ liệu nhận hàng'), 'err'));
  }
  APP.khoReceive = receiveForm;

  // ================= Nhận hàng (scm-gr) =================
  const baseGr = APP.SCREENS['scm-gr'];
  APP.register('scm-gr', (el, f, p) => {
    if (!Store.isLive) return baseGr(el, f, p);
    baseGr(el, f, p);
    const add = el.querySelector('[data-act="add"]'); if (add) add.remove();
    const note = el.querySelector('.notice.info'); if (note) note.remove();
    const h = el.querySelector('.page-heading');
    if (h && !el.querySelector('#grDue')) h.insertAdjacentHTML('afterend', '<div class="card panel mb" id="grDue"><div class="small muted">Đang tải đơn mua hẹn giao…</div></div>');
    drawDue(el);
    // Phiếu chờ kết luận: QA mở cùng form; kho chỉ thấy trạng thái.
    const qa = Store.canApprove('qc-inspection');
    APP.rowExtra(el, tr => tr.querySelectorAll('.actions button').forEach(b => {
      if (b.textContent.trim() !== 'Mở QC') return;
      if (!qa) { b.remove(); return; }
      b.textContent = 'Kiểm tra & kết luận';
      b.onclick = e => { e.stopPropagation(); const q = Store.one('QCInspection', 'GoodsReceiptId', tr.dataset.row); if (q) receiveForm({ inspectionId: q.Id }); else Store.ensure('QCInspection').then(() => { const q2 = Store.one('QCInspection', 'GoodsReceiptId', tr.dataset.row); if (q2) receiveForm({ inspectionId: q2.Id }); }); };
    }));
  });
  function drawDue(el) {
    const box = el.querySelector('#grDue'); if (!box) return;
    const day = st.dueDay || today();
    Store.query('PurchaseOrder/GetDueForReceiving', { date: day }).then(x => {
      if (!box.isConnected) return;
      const rows = x.ok ? (x.list && x.list.length ? x.list : Array.isArray(x.data) ? x.data : []) : [];
      const isToday = day === today();
      box.innerHTML = `<div class="spread mb"><div><div class="panel-title">Đơn mua hẹn giao ${isToday ? 'hôm nay' : U.date(day)} + quá hẹn chưa nhận đủ</div><div class="panel-desc">Bấm "Nhận hàng" để nhập số nhận, nhiệt độ, lô và kết quả kiểm tra trên cùng một form.</div></div>
        <div class="row-flex"><button class="btn sm" data-dd="-1" aria-label="Ngày trước">◀</button><input type="date" class="input" id="grDay" value="${day}" style="width:150px"><button class="btn sm" data-dd="1" aria-label="Ngày sau">▶</button>${isToday ? '' : '<button class="btn sm" data-dd="0">Hôm nay</button>'}</div></div>
        ${!x.ok ? `<div class="muted small">${esc(x.message || 'Không tải được đơn mua hẹn giao')}</div>` : rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Đơn mua</th><th>Nhà cung cấp</th><th>Hẹn giao</th><th class="num">Dòng còn chờ</th><th class="num">Giá trị</th><th></th></tr></thead><tbody>${rows.map((r, i) => `<tr><td><b>${esc(r.purchaseOrderNumber || '')}</b>${r.overdue ? ' ' + UI.tag('Quá hẹn', 'red') : ''}</td><td>${esc(r.supplierName || '')}</td><td>${r.expectedDate ? U.date(r.expectedDate) : '<span class="muted">Chưa hẹn</span>'}</td><td class="num">${U.num(r.remainingLines)} / ${U.num(r.lineCount)}</td><td class="num">${has(r.totalAmount) ? U.money(r.totalAmount) : '–'}</td><td><button class="btn xs primary" data-rcv="${i}">Nhận hàng</button></td></tr>`).join('')}</tbody></table></div>` : '<div class="muted small">Không có đơn mua hẹn giao / quá hẹn chưa nhận.</div>'}
        <div class="row-flex mt"><span class="small muted">Đơn mua khác:</span><input class="input" id="grFind" list="grPoList" placeholder="Gõ số đơn mua" style="width:220px"><datalist id="grPoList"></datalist><button class="btn sm" id="grFindGo">Nhận hàng</button></div>`;
      box.querySelectorAll('[data-dd]').forEach(b => b.onclick = () => { const n = +b.dataset.dd; st.dueDay = n === 0 ? null : dayAdd(day, n); drawDue(el); });
      box.querySelector('#grDay').onchange = e => { st.dueDay = e.target.value || null; drawDue(el); };
      box.querySelectorAll('[data-rcv]').forEach(b => b.onclick = () => { const r = rows[+b.dataset.rcv]; receiveForm({ po: { Id: r.purchaseOrderId, PurchaseOrderNumber: r.purchaseOrderNumber } }, () => APP.refresh()); });
      Store.ensure('PurchaseOrder').then(() => {
        const open = Store.rows('PurchaseOrder').filter(r => ['Approved', 'Sent', 'PartiallyReceived'].includes(r.Status));
        const dl = box.querySelector('#grPoList'); if (dl) dl.innerHTML = open.map(r => `<option value="${esc(r.PurchaseOrderNumber || '')}">${esc(Store.label('Supplier', r.SupplierId))}</option>`).join('');
        const go = box.querySelector('#grFindGo'); if (go) go.onclick = () => {
          const v = box.querySelector('#grFind').value.trim().toUpperCase(), r = open.find(z => String(z.PurchaseOrderNumber || '').toUpperCase() === v);
          if (!r) return UI.toast('Không tìm thấy đơn mua đang chờ nhận với số này', 'err');
          receiveForm({ po: r }, () => APP.refresh());
        };
      }, () => {});
    });
  }
  // Nút "Nhận hàng" ở màn Đơn mua / trang chi tiết đơn mua → cùng form.
  if (APP.scmPo) APP.scmPo.receive = r => receiveForm({ po: r });
  if (APP.scmGr) APP.scmGr.qc = r => { const q = r && Store.one('QCInspection', 'GoodsReceiptId', r.Id); if (q && Store.canApprove('qc-inspection')) receiveForm({ inspectionId: q.Id }); else UI.toast('Phiếu đang chờ QA kết luận', 'warn'); };

  // ================= Hàng chờ QA – Phê duyệt › Kiểm tra hàng nhập =================
  const baseInbox = APP.SCREENS['wf-inbox'];
  APP.register('wf-inbox', (el, f, p) => {
    if (!Store.isLive || !p || p.type !== 'qc-inspection') return baseInbox(el, f, p);
    el.innerHTML = APP.header(Object.assign({}, f, { name: 'Kiểm tra hàng nhập – chờ kết luận' }), `<a class="btn" href="${APP.href('wf-inbox')}">${I('approve')} Tất cả phê duyệt</a><a class="btn" href="${APP.href('qc-inspection')}">${I('eye')} Sổ kiểm tra hàng nhập</a>`, { noNote: true }) + '<div class="card panel" id="qaQ"><div class="small muted">Đang tải…</div></div>';
    const to = today();
    Store.query('QcInspection/GetBook', { From: dayAdd(to, -31), To: to, Result: 'PENDING' }).then(x => {
      const box = el.querySelector('#qaQ'); if (!box) return;
      if (!x.ok) { box.innerHTML = `<div class="muted">${esc(x.message || 'Không tải được hàng chờ')}</div>`; return; }
      const rows = ((x.data || {}).listDay || []).flatMap(d => d.listRow || []), by = {};
      rows.forEach(r => { (by[r.inspectionId] = by[r.inspectionId] || { r, lines: [] }).lines.push(r); });
      const list = Object.values(by);
      box.innerHTML = list.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Phiếu nhập</th><th>Nhà cung cấp</th><th>Nhận lúc</th><th>Nguyên liệu chờ kết luận</th><th></th></tr></thead><tbody>${list.map((g, i) => `<tr><td><b>${esc(g.r.receiptNumber || '')}</b><br><span class="small muted">${esc(g.r.inspectionNumber || '')}</span></td><td>${esc(g.r.supplierName || '')}</td><td>${U.date(g.r.inspectionDate)} ${String(g.r.inspectionDate || '').slice(11, 16)}</td><td>${g.lines.map(l => `${esc(l.ingredientName || '')} ${zoneTag(l.tempZone)}`).join('<br>')}</td><td><button class="btn xs primary" data-qa="${i}">Kiểm tra & kết luận</button></td></tr>`).join('')}</tbody></table></div>`
        : `<div class="empty">${I('check')}<p class="mt">Không có phiếu nhận hàng nào chờ kết luận.</p></div>`;
      box.querySelectorAll('[data-qa]').forEach(b => b.onclick = () => receiveForm({ inspectionId: list[+b.dataset.qa].r.inspectionId }, () => APP.SCREENS['wf-inbox'](el, f, p)));
    });
  });

  // ================= Tồn kho – 4 tab =================
  const baseInv = APP.SCREENS['scm-inv-balance'];
  APP.register('scm-inv-balance', (el, f, p) => {
    if (!Store.isLive) return baseInv(el, f, p);
    if (p && ['ing', 'lot', 'txn', 'policy'].includes(p.tab)) st.inv = p.tab;
    const tabs = tabsHtml('inv', [['ing', 'Theo nguyên liệu'], ['lot', 'Theo lô & hạn dùng'], ['txn', 'Sổ kho'], ['policy', 'Định mức theo kho']]);
    const rerender = () => APP.SCREENS['scm-inv-balance'](el, f, {});
    if (st.inv === 'lot') return asList(el, f, 'scm-lot', tabs).then(() => { bindTabs(el, 'inv', rerender); if (p && p.id && APP.scmLot) setTimeout(() => APP.scmLot.trace(p.id), 50); });
    if (st.inv === 'txn') return asList(el, f, 'scm-inv-txn', tabs).then(() => { ['#txTr', '#txAdj'].forEach(s => { const b = el.querySelector(s); if (b) b.remove(); }); bindTabs(el, 'inv', rerender); });
    if (st.inv === 'policy') return asList(el, f, 'scm-inv-policy', tabs + '<div class="notice info mb">' + I('info') + '<div>Chỉ cần khai báo khi một kho cần mức tồn tối thiểu / an toàn khác mức chung của nguyên liệu; trống = dùng mức của nguyên liệu.</div></div>').then(() => bindTabs(el, 'inv', rerender));
    baseInv(el, f, p);
    el.querySelectorAll('.heading-actions a[href="#/scm-inv-txn"]').forEach(a => a.remove());
    placeTabs(el, tabs); bindTabs(el, 'inv', rerender);
  });

  // ================= Kho & vị trí – 3 tab =================
  const baseWh = APP.SCREENS['scm-warehouse'];
  APP.register('scm-warehouse', (el, f, p) => {
    if (!Store.isLive) return baseWh(el, f, p);
    if (p && ['wh', 'zone', 'location'].includes(p.tab)) st.wh = p.tab;
    const tabs = tabsHtml('wh', [['wh', 'Kho'], ['zone', 'Khu bảo quản'], ['location', 'Vị trí kệ']]);
    const rerender = () => APP.SCREENS['scm-warehouse'](el, f, {});
    if (st.wh === 'zone') return asList(el, f, 'scm-zone', tabs).then(() => bindTabs(el, 'wh', rerender));
    if (st.wh === 'location') return asList(el, f, 'scm-location', tabs).then(() => bindTabs(el, 'wh', rerender));
    baseWh(el, f, p);
    placeTabs(el, tabs); bindTabs(el, 'wh', rerender);
  });

  // ================= Xuất kho sản xuất – tab Giữ chỗ nguyên liệu =================
  const baseIss = APP.SCREENS['scm-issue'];
  APP.register('scm-issue', (el, f, p) => {
    if (!Store.isLive) return baseIss(el, f, p);
    if (p && ['doc', 'res'].includes(p.tab)) st.iss = p.tab;
    const tabs = tabsHtml('iss', [['doc', 'Phiếu xuất'], ['res', 'Giữ chỗ nguyên liệu']]);
    const rerender = () => APP.SCREENS['scm-issue'](el, f, {});
    if (st.iss === 'res') return asList(el, f, 'scm-reservation', tabs).then(() => bindTabs(el, 'iss', rerender));
    baseIss(el, f, p);
    placeTabs(el, tabs); bindTabs(el, 'iss', rerender);
  });

  // ================= Sổ kiểm tra hàng nhập (qc-inspection) – chỉ xem =================
  const baseQc = APP.SCREENS['qc-inspection'];
  const bk = st.book;
  const range = () => {
    const t = today();
    if (bk.period === 'today') return [t, t];
    if (bk.period === 'month') return [t.slice(0, 8) + '01', t];
    if (bk.period === 'custom' && bk.from && bk.to) return [bk.from, bk.to];
    return [dayAdd(t, -6), t];
  };
  const bookParams = () => { const [a, b] = range(); return { From: a, To: b, Keyword: bk.q || null, Result: bk.result || null, StorageType: bk.storage || null, SupplierId: bk.supplier || null, OnlyTempOut: bk.out ? true : null }; };
  const hhmm = v => String(v || '').slice(11, 16);
  const tempCell = r => (has(r.temperature) ? `<b class="${r.tempZone === 'REJECT' ? 'bad' : r.tempZone === 'CONDITIONAL' ? 'warn' : ''}">${U.num(r.temperature, 1)} °C</b>` : r.tempZone === 'MISSING' ? '<span class="bad">Chưa đo</span>' : '<span class="muted">–</span>')
    + (ruleOf(r.storageType) ? `<br><span class="small muted">≤ ${U.num(ruleOf(r.storageType).acceptMax, 1)} / ≤ ${U.num(ruleOf(r.storageType).conditionalMax, 1)}</span>` : '');
  const resCell = r => resultTag(r.result) + (r.conditional ? '<br>' + UI.tag('Có điều kiện', 'orange') : '');
  const who = r => (r.autoPass ? '<span class="muted">Tự đạt</span>' : esc(r.inspector || (r.result ? '–' : '')));
  APP.register('qc-inspection', (el, f, p) => {
    if (!Store.isLive) return baseQc(el, f, p);
    if (p && ['book', 'supplier'].includes(p.tab)) bk.tab = p.tab;
    loadRules().then(() => drawBook(el, f));
  });
  function drawBook(el, f) {
    const [a, b] = range(), canExp = Store.perm('qc-inspection').exp !== false;
    el.innerHTML = APP.header(f, `<button class="btn" id="qbPrint">${I('print')} In sổ kiểm thực bước 1</button>${canExp ? `<button class="btn" id="qbXls">${I('download')} Xuất Excel</button>` : ''}`) + `
      <div id="qbQa"></div>
      <div class="tabs mb">${[['book', 'Sổ theo ngày'], ['supplier', 'Theo nhà cung cấp']].map(([k, l]) => `<button class="tab ${bk.tab === k ? 'active' : ''}" data-qbt="${k}">${l}</button>`).join('')}</div>
      <div class="card panel mb"><div class="row-flex" style="flex-wrap:wrap;gap:8px">
        <input class="input" type="search" id="qbQ" placeholder="Tìm nguyên liệu, NCC, số lô, số phiếu" value="${esc(bk.q)}" style="width:260px">
        ${[['today', 'Hôm nay'], ['7d', '7 ngày'], ['month', 'Tháng này'], ['custom', 'Chọn khoảng']].map(([k, l]) => `<button class="chip ${bk.period === k ? 'active' : ''}" data-qbp="${k}">${l}</button>`).join('')}
        <span id="qbRange" style="display:${bk.period === 'custom' ? 'inline-flex' : 'none'};gap:4px"><input type="date" class="input" id="qbFrom" value="${a}" style="width:145px"><input type="date" class="input" id="qbTo" value="${b}" style="width:145px"></span>
        ${bk.tab === 'book' ? `<select class="select" id="qbRes" style="width:auto"><option value="">Mọi kết quả</option>${[['DAT', 'Đạt'], ['DAT_MOT_PHAN', 'Đạt một phần'], ['KHONG_DAT', 'Không đạt'], ['CONDITIONAL', 'Nhận có điều kiện'], ['PENDING', 'Chờ kết luận']].map(([k, l]) => `<option value="${k}" ${bk.result === k ? 'selected' : ''}>${l}</option>`).join('')}</select>` : ''}
        <select class="select" id="qbSt" style="width:auto"><option value="">Mọi loại bảo quản</option>${[['CHILLED', 'Mát'], ['FROZEN', 'Đông lạnh'], ['DRY', 'Khô']].map(([k, l]) => `<option value="${k}" ${bk.storage === k ? 'selected' : ''}>${l}</option>`).join('')}</select>
        <select class="select" id="qbSup" style="width:auto"><option value="">Mọi nhà cung cấp</option>${Store.rows('Supplier').map(s => `<option value="${s.Id}" ${bk.supplier === s.Id ? 'selected' : ''}>${esc(Store.label('Supplier', s))}</option>`).join('')}</select>
        ${bk.tab === 'book' ? `<label class="small"><input type="checkbox" id="qbOut" ${bk.out ? 'checked' : ''}> Chỉ nhiệt độ ngoài ngưỡng</label>` : ''}
      </div></div><div id="qbBody"><div class="card panel small muted">Đang tải…</div></div>`;
    const redraw = () => drawBook(el, f);
    el.querySelectorAll('[data-qbt]').forEach(x => x.onclick = () => { bk.tab = x.dataset.qbt; redraw(); });
    el.querySelectorAll('[data-qbp]').forEach(x => x.onclick = () => { bk.period = x.dataset.qbp; if (bk.period === 'custom') { bk.from = bk.from || a; bk.to = bk.to || b; } redraw(); });
    const on = (id, ev, fn) => { const e = el.querySelector(id); if (e) e.addEventListener(ev, fn); };
    let tq; on('#qbQ', 'input', e => { clearTimeout(tq); tq = setTimeout(() => { bk.q = e.target.value.trim(); loadBody(el); }, 350); });
    on('#qbFrom', 'change', e => { bk.from = e.target.value; loadBody(el); }); on('#qbTo', 'change', e => { bk.to = e.target.value; loadBody(el); });
    on('#qbRes', 'change', e => { bk.result = e.target.value; loadBody(el); }); on('#qbSt', 'change', e => { bk.storage = e.target.value; loadBody(el); });
    on('#qbSup', 'change', e => { bk.supplier = e.target.value; loadBody(el); }); on('#qbOut', 'change', e => { bk.out = e.target.checked; loadBody(el); });
    if (!Store.rows('Supplier').length) Store.ensure('Supplier').then(() => { if (Store.rows('Supplier').length) redraw(); }, () => {});
    // QA: phiếu chờ kết luận → hàng chờ ở Phê duyệt (sổ không có nút Kết luận).
    if (Store.canApprove('qc-inspection')) Store.query('QcInspection/GetBook', { From: dayAdd(today(), -31), To: today(), Result: 'PENDING' }).then(x => {
      const n = x.ok ? ((x.data || {}).summary || {}).receipts || 0 : 0, box = el.querySelector('#qbQa');
      if (box && n) box.innerHTML = `<div class="notice warn mb">${I('alert')}<div><b>${U.num(n)} phiếu nhận hàng chờ kết luận.</b> <a href="${APP.href('wf-inbox?type=qc-inspection')}">Mở hàng chờ kiểm tra →</a></div></div>`;
    });
    loadBody(el);
  }
  let lastBook = null;
  function loadBody(el) {
    const body = el.querySelector('#qbBody'); if (!body) return;
    const prm = bookParams();
    if (bk.tab === 'supplier') {
      return Store.query('QcInspection/GetSupplierSummary', { From: prm.From, To: prm.To, StorageType: prm.StorageType, SupplierId: prm.SupplierId, Keyword: prm.Keyword }).then(x => {
        if (!body.isConnected) return;
        if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được')}</div>`; return; }
        const rows = x.list && x.list.length ? x.list : Array.isArray(x.data) ? x.data : [];
        lastBook = { supplier: rows };
        body.innerHTML = `<div class="card panel">${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Nhà cung cấp</th><th class="num">Phiếu</th><th class="num">Dòng kiểm</th><th class="num">Đạt</th><th class="num">Có điều kiện</th><th class="num">Không đạt</th><th class="num">Nhiệt độ ngoài ngưỡng</th><th class="num">Tỷ lệ đạt</th><th>Lần kiểm gần nhất</th></tr></thead><tbody>${rows.map(r => `<tr data-sup="${r.supplierId}" style="cursor:pointer"><td><b>${esc(r.supplierName || '')}</b></td><td class="num">${U.num(r.receipts)}</td><td class="num">${U.num(r.lines)}</td><td class="num">${U.num(r.dat)}</td><td class="num">${U.num(r.conditional)}</td><td class="num">${r.khongDat ? `<b class="bad">${U.num(r.khongDat)}</b>` : '0'}</td><td class="num">${r.tempOut ? `<b class="warn">${U.num(r.tempOut)}</b>` : '0'}</td><td class="num">${has(r.passPct) ? UI.tag(U.pct(r.passPct), r.passPct >= 95 ? 'green' : r.passPct >= 85 ? 'orange' : 'red') : '–'}</td><td>${U.date(r.lastDate)}</td></tr>`).join('')}</tbody></table></div><div class="small muted mt">Bấm một nhà cung cấp để xem các dòng kiểm trong sổ.</div>` : '<div class="empty">Không có lượt kiểm nào trong kỳ.</div>'}</div>`;
        body.querySelectorAll('[data-sup]').forEach(tr => tr.onclick = () => { bk.supplier = tr.dataset.sup; bk.tab = 'book'; drawBook(el, APP.FN['qc-inspection'] || {}); });
      });
    }
    Store.query('QcInspection/GetBook', prm).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được sổ')}</div>`; return; }
      const d = x.data || {}, s = d.summary || {}, days = d.listDay || [];
      lastBook = d;
      const card = r => `<div class="qb-card" data-row="${r.inspectionLineId}"><div class="spread"><b>${esc(r.ingredientName || '')}</b>${resCell(r)}</div><div class="small muted">${hhmm(r.inspectionDate)} · ${esc(r.supplierName || '')} · ${esc(r.receiptNumber || '')}</div><div class="small">${tempCell(r)} · HSD ${U.date(r.expiryDate)} · nhận ${U.num(r.receivedQuantity, 2)} / đạt ${U.num(r.acceptedQuantity, 2)}</div></div>`;
      body.innerHTML = `<style>.qb-cards{display:none}.qb-card{border:1px solid var(--line,#e5e7eb);border-radius:10px;padding:10px;margin-bottom:8px;cursor:pointer}@media(max-width:640px){.qb-table{display:none}.qb-cards{display:block}}</style>
        <div class="kpis">${UI.kpi('Lượt kiểm', U.num(s.lines || 0), `${U.num(s.receipts || 0)} phiếu · ${U.num(s.suppliers || 0)} NCC`, 'shield', 'blue')}${UI.kpi('Tỷ lệ đạt', has(s.passPct) ? U.pct(s.passPct) : '–', s.pending ? `${U.num(s.pending)} dòng chờ kết luận` : 'trên dòng đã kết luận', 'check', 'green')}${UI.kpi('Có điều kiện / Không đạt', `${U.num(s.conditional || 0)} / ${U.num(s.khongDat || 0)}`, s.khongDat ? '<span class="bad">đã lập sự cố NCC</span>' : '', 'alert', 'orange')}${UI.kpi('Nhiệt độ ngoài ngưỡng', U.num(s.tempOut || 0), 'có điều kiện, vượt ngưỡng hoặc chưa đo', 'thermo', 'red')}</div>
        ${days.length ? days.map(g => `<details class="card panel mb" open><summary class="spread" style="cursor:pointer"><span><b>${U.date(g.date)}</b> <span class="small muted">· ${new Set(g.listRow.map(r => r.goodsReceiptId)).size} phiếu · ${U.num(g.lines)} dòng${g.khongDat ? ` · <span class="bad">${U.num(g.khongDat)} không đạt</span>` : ''}${g.tempOut ? ` · ${U.num(g.tempOut)} nhiệt độ ngoài ngưỡng` : ''}</span></span><button class="btn xs" data-pday="${U.day(g.date)}">${I('print')} In ngày</button></summary>
          <div class="table-wrap qb-table mt"><table class="table dc-table"><thead><tr><th>Giờ</th><th>Nguyên liệu</th><th>Nhà cung cấp</th><th>Lô kho</th><th>Hạn dùng</th><th class="num">Nhận / đạt</th><th>Nhiệt độ</th><th>Cảm quan / bao bì</th><th>Kết quả</th><th>Người kiểm</th><th></th></tr></thead><tbody>${g.listRow.map(r => `<tr data-row="${r.inspectionLineId}" style="cursor:pointer"><td>${hhmm(r.inspectionDate)}</td><td><b>${esc(r.ingredientName || '')}</b><br><span class="small muted">${esc(STORAGE[String(r.storageType || '').toUpperCase()] || 'Khô')}</span></td><td>${esc(r.supplierName || '')}<br><span class="small muted">${esc(r.receiptNumber || '')}</span></td><td>${esc(r.lotNo || '–')}</td><td>${U.date(r.expiryDate)}</td><td class="num">${U.num(r.receivedQuantity, 2)} / ${U.num(r.acceptedQuantity, 2)}</td><td>${tempCell(r)}</td><td>${esc(r.appearance || (r.result ? 'Đạt' : '–'))}<br><span class="small muted">${esc(r.packaging || (r.result ? 'Đạt' : '–'))}</span></td><td>${resCell(r)}</td><td>${who(r)}</td><td><button class="btn xs" data-prc="${r.goodsReceiptId}" title="In phiếu kiểm tra">${I('print')}</button></td></tr>`).join('')}</tbody></table></div>
          <div class="qb-cards mt">${g.listRow.map(card).join('')}</div></details>`).join('') : '<div class="card panel empty">Không có dòng kiểm tra nào khớp bộ lọc.</div>'}`;
      const all = days.flatMap(g => g.listRow);
      body.querySelectorAll('[data-row]').forEach(tr => tr.onclick = e => { if (e.target.closest('button')) return; const r = all.find(z => z.inspectionLineId === tr.dataset.row); if (r) bookDrawer(r); });
      body.querySelectorAll('[data-prc]').forEach(btn => btn.onclick = e => { e.stopPropagation(); printReceipt(btn.dataset.prc); });
      body.querySelectorAll('[data-pday]').forEach(btn => btn.onclick = e => { e.preventDefault(); e.stopPropagation(); printBook(days.filter(g => U.day(g.date) === btn.dataset.pday)); });
      const pr = el.querySelector('#qbPrint'); if (pr) pr.onclick = () => printBook(days);
      const xl = el.querySelector('#qbXls'); if (xl) xl.onclick = () => exportCsv(all);
    });
  }
  const printReceipt = id => Store.download('GoodsReceipt/Print', { id }).then(x => { if (x.ok) UI.toast('Đã tải ' + esc(x.fileName || 'phiếu nhập kho'), 'ok'); });
  function bookDrawer(r) {
    UI.drawer({
      title: esc(r.ingredientName || ''), badge: resultTag(r.result), width: 560,
      tabs: [
        { label: 'Kiểm tra', html: `<div class="kv-grid" style="display:grid;grid-template-columns:150px 1fr;gap:6px 12px">
          ${[['Phiếu nhập', esc(r.receiptNumber || '')], ['Phiếu kiểm tra', esc(r.inspectionNumber || '')], ['Nhận lúc', U.date(r.inspectionDate) + ' ' + hhmm(r.inspectionDate)], ['Nhà cung cấp', esc(r.supplierName || '')], ['Bảo quản', esc(STORAGE[String(r.storageType || '').toUpperCase()] || 'Khô')],
            ['Nhiệt độ', tempCell(r) + ' ' + zoneTag(r.tempZone)], ['Hạn dùng', U.date(r.expiryDate)], ['Cảm quan', esc(r.appearance || (r.result ? 'Đạt' : '–'))], ['Bao bì', esc(r.packaging || (r.result ? 'Đạt' : '–'))],
            ['Nhận / đạt / loại', `${U.num(r.receivedQuantity, 2)} / ${U.num(r.acceptedQuantity, 2)} / ${U.num(r.rejectedQuantity, 2)}`], ['Kết quả', resCell(r)], ['Người kiểm', who(r)], ['Nhận xét', esc(r.remark || '–')], ['Lô kho', esc(r.lotNo || 'Chưa nhập kho')]]
            .map(([k, v]) => `<div class="muted small">${k}</div><div>${v}</div>`).join('')}</div>
          <div class="small muted mt">Hồ sơ kiểm tra không sửa được. Sai sau khi nhập kho → Tạm giữ lô, sự cố nhà cung cấp hoặc Điều chỉnh tồn (có lý do).</div>` },
        { label: 'Lô này đã đi đâu', html: pane => { if (!r.lotId) return '<div class="muted">Dòng chưa nhập kho – chưa có lô.</div>'; Store.query('MaterialLot/Trace', { id: r.lotId }).then(x => { if (!pane.isConnected) return; if (!x.ok || !x.data) { pane.innerHTML = `<div class="muted">${esc(x.message || 'Không truy xuất được lô')}</div>`; return; } const d = x.data, steps = [['listIssue', 'Phiếu xuất kho'], ['listProductionOrder', 'Lệnh sản xuất'], ['listBatch', 'Mẻ sản xuất'], ['listPackingOrder', 'Lệnh đóng gói'], ['listOrder', 'Đơn đặt suất'], ['listCustomer', 'Khách hàng']]; pane.innerHTML = `<div class="timeline">${steps.map(([k, lb]) => { const it = d[k] || []; return `<div class="tl ${it.length ? '' : 'orange'}"><b>${lb} <span class="muted small">${it.length || ''}</span></b><small>${it.length ? it.slice(0, 8).map(n => esc(n.number || n.name || '') + (n.date ? ' · ' + U.date(n.date) : '')).join('<br>') : 'Chưa có'}</small></div>`; }).join('')}</div>`; }); return '<div class="small muted">Đang truy xuất…</div>'; } },
      ],
      foot: `<a class="btn" href="${APP.href('scm-gr-detail?id=' + encodeURIComponent(r.goodsReceiptId))}">Mở phiếu nhập</a>${r.lotId ? '<button class="btn" data-dtr>Truy xuất lô</button>' : ''}<button class="btn primary" data-dpr>${I('print')} In phiếu kiểm tra</button>`,
      onMount: (d, close) => {
        const t = d.parentNode.querySelector('[data-dtr]'); if (t) t.onclick = () => { close(); if (APP.scmLot) APP.scmLot.trace(r.lotId); };
        const p = d.parentNode.querySelector('[data-dpr]'); if (p) p.onclick = () => printReceipt(r.goodsReceiptId);
        d.parentNode.querySelectorAll('a[href]').forEach(a => a.addEventListener('click', () => close()));
      }
    });
  }
  // In sổ kiểm thực bước 1 (kiểm tra trước khi nhập nguyên liệu) theo khoảng ngày – bản in trình duyệt; mẫu PDF chính thức chờ biểu mẫu của anh (PROGRESS).
  function printBook(days) {
    const [a, b] = range(), rows = days.flatMap(g => g.listRow);
    if (!rows.length) return UI.toast('Không có dòng nào để in', 'warn');
    const w = window.open('', '_blank'); if (!w) return UI.toast('Trình duyệt chặn cửa sổ in – cho phép cửa sổ bật lên rồi thử lại', 'err');
    const txt = v => esc(v == null ? '' : String(v));
    w.document.write(`<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Sổ kiểm thực bước 1</title><style>body{font:12px Arial,sans-serif;margin:18px}h1{font-size:16px;margin:0 0 4px}table{border-collapse:collapse;width:100%;margin-top:10px}th,td{border:1px solid #999;padding:4px 5px;vertical-align:top}th{background:#eee}.r{text-align:right}</style></head><body>
      <h1>SỔ KIỂM THỰC BA BƯỚC – BƯỚC 1: KIỂM TRA TRƯỚC KHI NHẬP NGUYÊN LIỆU</h1><div>Từ ngày ${U.date(a)} đến ngày ${U.date(b)} · ${rows.length} dòng kiểm tra</div>
      <table><thead><tr><th>Ngày giờ</th><th>Nguyên liệu</th><th>Nhà cung cấp / phiếu nhập</th><th>Lô</th><th>Hạn dùng</th><th class="r">Nhận</th><th class="r">Đạt</th><th>Nhiệt độ</th><th>Cảm quan</th><th>Bao bì</th><th>Kết quả</th><th>Người kiểm</th><th>Nhận xét</th></tr></thead><tbody>
      ${rows.map(r => `<tr><td>${U.date(r.inspectionDate)} ${hhmm(r.inspectionDate)}</td><td>${txt(r.ingredientName)}</td><td>${txt(r.supplierName)}<br>${txt(r.receiptNumber)}</td><td>${txt(r.lotNo)}</td><td>${U.date(r.expiryDate)}</td><td class="r">${U.num(r.receivedQuantity, 2)}</td><td class="r">${U.num(r.acceptedQuantity, 2)}</td><td>${has(r.temperature) ? U.num(r.temperature, 1) + ' °C' : txt(ZONE[r.tempZone] ? ZONE[r.tempZone][1] : '')}</td><td>${txt(r.appearance || (r.result ? 'Đạt' : ''))}</td><td>${txt(r.packaging || (r.result ? 'Đạt' : ''))}</td><td>${txt(r.result && RESULT[r.result] ? RESULT[r.result][1] : 'Chờ kết luận')}${r.conditional ? ' (có điều kiện)' : ''}</td><td>${r.autoPass ? 'Tự đạt' : txt(r.inspector)}</td><td>${txt(r.remark)}</td></tr>`).join('')}
      </tbody></table><p style="margin-top:24px">Người kiểm tra ký: ........................................ &nbsp;&nbsp;&nbsp; Người phụ trách ký: ........................................</p><script>window.onload=function(){window.print()}<\/script></body></html>`);
    w.document.close();
  }
  function exportCsv(rows) {
    if (!rows.length) return UI.toast('Không có dòng nào để xuất', 'warn');
    const q = v => '"' + String(v == null ? '' : v).replace(/"/g, '""') + '"';
    const head = ['Ngày', 'Giờ', 'Nguyên liệu', 'Bảo quản', 'Nhà cung cấp', 'Phiếu nhập', 'Phiếu kiểm tra', 'Lô', 'Hạn dùng', 'Nhận', 'Đạt', 'Loại', 'Nhiệt độ °C', 'Vùng nhiệt độ', 'Cảm quan', 'Bao bì', 'Kết quả', 'Người kiểm', 'Nhận xét'];
    const lines = rows.map(r => [U.date(r.inspectionDate), hhmm(r.inspectionDate), r.ingredientName, STORAGE[String(r.storageType || '').toUpperCase()] || 'Khô', r.supplierName, r.receiptNumber, r.inspectionNumber, r.lotNo, U.date(r.expiryDate), r.receivedQuantity, r.acceptedQuantity, r.rejectedQuantity, has(r.temperature) ? r.temperature : '', ZONE[r.tempZone] ? ZONE[r.tempZone][1] : '', r.appearance || '', r.packaging || '', r.result && RESULT[r.result] ? RESULT[r.result][1] : 'Chờ kết luận', r.autoPass ? 'Tự đạt' : r.inspector || '', r.remark || ''].map(q).join(','));
    const blob = new Blob(['﻿' + [head.map(q).join(',')].concat(lines).join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a'); a.href = URL.createObjectURL(blob); const [x, y] = range(); a.download = `so-kiem-tra-hang-nhap_${x}_${y}.csv`; document.body.appendChild(a); a.click(); setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
    UI.toast('Đã xuất ' + rows.length + ' dòng', 'ok');
  }
})();
