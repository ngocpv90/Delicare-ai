/* UI-6.24 (D-105): "Bảng sản xuất ngày" trên trang chi tiết kế hoạch (#/prod-plan-detail?id=) – thay cho màn Lệnh sản xuất / Sản lượng riêng.
 * Ma trận Món (nhóm theo nhóm món, mở / gập + dòng tổng) × Ca + cột Cả ngày; một số mỗi ô = suất, chữ nhỏ "x/y mẻ xong", màu theo trạng thái mẻ + chú thích.
 * Ô không sửa trực tiếp; bấm ô → hộp chi tiết lệnh (mẻ, giữ chỗ, phiếu xuất, chênh lệch + giá thành khi đã đóng) và nút theo allowedActions của lệnh:
 * Giữ chỗ bổ sung · Hủy lệnh · Tính giá thành · Mở trang lệnh. Tab phụ: Nguyên liệu & giữ chỗ (gộp cả ngày) · Chênh lệch & giá thành.
 * API: ProductionPlan/GetDayBoard (chỉ đọc), ProductionOrder/GetDetail · Reserve · Cancel · CalculateCost (APP.prodOrder – modules/production.js). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const nil = v => v === null || v === undefined || v === '';
  const cache = {};
  const closed = {};
  const CLS = { planned: 'miss', waiting: 'reg', cooking: 'draft', qc: 'qc', passed: 'pub', problem: 'held' };
  const hm = v => (v ? U.time(v) : '');
  const canEdit = () => Store.perm('prod-plan').edit !== false;

  function load(id, fresh) {
    const c = cache[id];
    if (!fresh && c && Date.now() - c.at < 20000) return c.p;
    const p = Store.query('ProductionPlan/GetDayBoard', { id });
    cache[id] = { at: Date.now(), p };
    return p;
  }
  const fail = (x, what) => `<div class="muted small">${esc((x && x.res && x.res.responseMessage) || 'Không tải được ' + what)}</div>`;

  function legend() {
    return `<div class="fw-legend"><span><span class="fw-sw miss"></span>Kế hoạch chưa ra lệnh</span><span><span class="fw-sw reg"></span>Chờ nấu</span><span><span class="fw-sw draft"></span>Đang nấu</span>
      <span><span class="fw-sw qc"></span>Chờ QC</span><span><span class="fw-sw pub"></span>Đạt / đã đóng gói</span><span><span class="fw-sw held"></span>Có mẻ loại / tạm giữ</span><span>Bấm ô để xem lệnh của món × ca</span></div>`;
  }

  function matrixHtml(b) {
    const sh = b.listShift || [];
    const tip = (c, s) => [`${c.listOrder.length ? c.listOrder.length + ' lệnh' : 'Chưa ra lệnh'}`, c.lineName, c.startAt ? `${hm(c.startAt)} – ${hm(c.endAt)}` : '', c.shortQuantityLines ? `${c.shortQuantityLines} dòng thiếu nguyên liệu` : '', s.shiftName].filter(Boolean).join(' · ');
    const cellHtml = (row, s, gi, ri) => {
      const c = (row.listCell || []).find(x => x.shiftId === s.shiftId);
      if (!c) return '<td><span class="fw-q lock">–</span></td>';
      return `<td><div class="fw-cell"><span class="fw-q pp ${CLS[c.state] || 'miss'}" data-cell="${gi}:${ri}:${esc(s.shiftId)}" title="${esc(c.stateName + ' · ' + tip(c, s))}">${U.num(c.portions)}</span>
        <span class="fw-hint${c.state === 'problem' || c.shortQuantityLines ? ' bad' : ''}">${c.batchTotal ? `${c.batchDone}/${c.batchTotal} mẻ xong` : esc(c.stateName)}</span></div></td>`;
    };
    const rowTotal = row => U.sum(row.listCell || [], 'portions');
    let h = `<colgroup><col class="name">${sh.map(() => '<col>').join('')}<col class="tot"></colgroup>
      <thead><tr><th class="l">Món</th>${sh.map(s => `<th><b>${esc(s.shiftName || s.shiftCode || '')}</b>${s.released ? UI.tag('Đã ra lệnh', 'green') : UI.tag('Chưa ra lệnh', 'orange')}</th>`).join('')}<th>Cả ngày</th></tr></thead><tbody>`;
    (b.listGroup || []).forEach((g, gi) => {
      const key = b.planId + ':' + gi, open = !closed[key];
      const byShift = sh.map(s => U.sum(g.listRow, r => ((r.listCell || []).find(c => c.shiftId === s.shiftId) || {}).portions || 0));
      h += `<tr class="fw-cus ${open ? '' : 'closed'}" data-grp="${esc(key)}"><td class="l"><span class="fw-chev">▾</span> ${esc(g.categoryName)} <span class="muted small">· ${g.listRow.length} món</span></td>${byShift.map(v => `<td>${v ? U.num(v) : '<span class="muted">–</span>'}</td>`).join('')}<td class="tot">${U.num(U.sum(byShift, v => v))}</td></tr>`;
      if (open) g.listRow.forEach((r, ri) => { h += `<tr class="fw-sub"><td class="l fw-name" title="${esc(r.dishName || '')}">${esc(r.dishName || '')}</td>${sh.map(s => cellHtml(r, s, gi, ri)).join('')}<td class="tot">${U.num(rowTotal(r))}</td></tr>`; });
    });
    const all = sh.map(s => U.sum(b.listGroup || [], g => U.sum(g.listRow, r => ((r.listCell || []).find(c => c.shiftId === s.shiftId) || {}).portions || 0)));
    h += `</tbody><tfoot><tr><td class="l">Tổng suất</td>${all.map(v => `<td>${U.num(v)}</td>`).join('')}<td>${U.num(U.sum(all, v => v))}</td></tr></tfoot>`;
    return h;
  }

  // Trang kế hoạch – tổng quan: dòng mục đích + ma trận Món × Ca.
  function render(box, planId) {
    box.innerHTML = '<div class="muted small">Đang tải bảng sản xuất ngày…</div>';
    load(planId).then(x => {
      if (!box.isConnected) return;
      if (!x.ok || !x.data) { box.innerHTML = fail(x, 'bảng sản xuất ngày'); return; }
      const b = x.data;
      if (!(b.listGroup || []).length) { box.innerHTML = '<div class="muted small">Kế hoạch chưa có món nào có suất.</div>'; return; }
      box.innerHTML = `<div class="small muted mb">Theo dõi cả ngày sản xuất: mỗi ô là số suất của món trong ca, chữ nhỏ là số mẻ đã xong.${b.autoClose ? ' Lệnh tự đóng khi mẻ cuối đóng gói / loại (tính chênh lệch tiêu hao, giá thành, trả nguyên liệu giữ chỗ dư).' : ''}</div>
        ${legend()}<div class="table-wrap pp-wrap"><table class="fw-table pp-table pb-table">${matrixHtml(b)}</table></div>`;
      box.querySelectorAll('[data-grp]').forEach(t => t.onclick = () => { closed[t.dataset.grp] = !closed[t.dataset.grp]; render(box, planId); });
      box.querySelectorAll('[data-cell]').forEach(el => el.onclick = () => {
        const [gi, ri, sid] = el.dataset.cell.split(':');
        const row = b.listGroup[+gi].listRow[+ri], cell = row.listCell.find(c => c.shiftId === sid), s = b.listShift.find(x => x.shiftId === sid);
        orderBox(row, cell, s, () => { load(planId, true); render(box, planId); });
      });
    });
  }

  // Hộp chi tiết lệnh (món × ca).
  function orderBox(row, cell, s, done) {
    const title = `${esc(row.dishName || '')} · ${esc(s.shiftName || s.shiftCode || '')}`;
    if (!cell.listOrder.length) {
      return UI.modal({ title, size: 'sm', body: `<div class="notice info">${I('help')}<div>${U.num(cell.portions)} suất – kế hoạch <b>chưa ra lệnh</b> ca này. Bấm <b>Ra lệnh SX</b> ở đầu trang để hệ thống chia mẻ, giữ chỗ nguyên liệu và lập phiếu xuất.</div></div>`, foot: '<button class="btn" data-close>Đóng</button>' });
    }
    const o = cell.listOrder[0], more = cell.listOrder.slice(1);
    const acts = o.allowedActions || [], edit = canEdit();
    const btn = [];
    if (edit && acts.includes('reserve')) btn.push('<button class="btn" data-o="reserve">Giữ chỗ bổ sung</button>');
    if (edit && acts.includes('calculateCost')) btn.push(`<button class="btn" data-o="cost">${I('calc')} Tính giá thành</button>`);
    if (edit && acts.includes('cancel')) btn.push('<button class="btn danger" data-o="cancel">Hủy lệnh</button>');
    btn.push(`<button class="btn primary" data-o="open">Mở trang lệnh</button>`);
    UI.modal({ title: `${title} · ${esc(o.number || '')}`, size: 'lg',
      body: `<div class="small muted mb">${U.num(cell.portions)} suất · ${UI.tag(cell.stateName, ({ passed: 'green', problem: 'red', qc: 'orange', cooking: 'orange', waiting: 'blue' })[cell.state] || '')}${cell.lineName ? ' · ' + esc(cell.lineName) : ''}${cell.startAt ? ` · ${hm(cell.startAt)} – ${hm(cell.endAt)}` : ''} · lệnh ${esc(o.statusName || '')}${more.length ? ` · thêm ${more.length} lệnh bổ sung: ${more.map(m => `<a href="#/prod-order-detail?id=${esc(m.id)}">${esc(m.number || '')}</a>`).join(', ')}` : ''}</div><div id="pbOrder"><div class="muted small">Đang tải lệnh…</div></div>`,
      foot: `<button class="btn" data-close>Đóng</button>${btn.join('')}`,
      onMount: (m, close) => {
        const after = () => { close(); done(); };
        m.querySelectorAll('[data-o]').forEach(b => b.onclick = () => {
          const a = b.dataset.o;
          if (a === 'open') { close(); location.hash = '#/prod-order-detail?id=' + encodeURIComponent(o.id); return; }
          if (a === 'reserve') return UI.confirm(`Giữ chỗ bổ sung nguyên liệu cho lệnh <b>${esc(o.number || '')}</b> từ tồn kho hiện có?`, () => Store.act('ProductionOrder/Reserve', { form: { Id: o.id }, tables: ['MaterialReservation'] }).then(r => {
            if (!r.ok) return; const w = (r.data && r.data.listWarning) || []; UI.toast(w.length ? esc(w[0]) : 'Đã giữ chỗ bổ sung', w.length ? 'warn' : 'ok'); after(); }), { ok: 'Giữ chỗ' });
          if (APP.prodOrder) APP.prodOrder.act(a, { Id: o.id, ProductionOrderNumber: o.number, Status: o.status }, after);
        });
        Store.query('ProductionOrder/GetDetail', { id: o.id }).then(x => {
          const host = m.querySelector('#pbOrder'); if (!host) return;
          if (!x.ok || !x.data) { host.innerHTML = fail(x, 'lệnh'); return; }
          const d = x.data, mini = (head, rows) => rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr>${head.map(h => `<th>${h}</th>`).join('')}</tr></thead><tbody>${rows.map(r => `<tr>${r.map(c => `<td>${c}</td>`).join('')}</tr>`).join('')}</tbody></table></div>` : '<div class="muted small mb">Chưa có</div>';
          host.innerHTML = `<h3>Mẻ</h3>${mini(['Số mẻ', 'Suất', 'Giờ', 'Trạng thái', 'QC'], (d.listBatch || []).map(bt => [`<a href="#/prod-batch-detail?id=${esc(bt.id)}">${esc(bt.batchNumber || '')}</a>`, U.num(bt.plannedQuantity), esc([hm(bt.actualStart || bt.plannedStart), hm(bt.actualEnd || bt.plannedEnd)].filter(Boolean).join(' – ')), esc(bt.statusName || bt.status || ''), bt.qcStatus ? UI.badge(bt.qcStatus, 'ProductionBatche', 'QcStatus') : '']))}
            <h3 class="mt">Giữ chỗ nguyên liệu</h3>${mini(['Nguyên liệu', 'Cần', 'Đã giữ', 'Thiếu'], (d.listReservation || []).map(r => [esc(r.ingredientName || r.ingredientCode || ''), U.num(r.requiredQuantity, 3) + ' ' + esc(r.unitName || ''), U.num(r.reservedQuantity, 3), r.shortQuantity > 0 ? `<b class="bad">${U.num(r.shortQuantity, 3)}</b>` : '0']))}
            <h3 class="mt">Phiếu xuất nguyên liệu</h3>${mini(['Số phiếu', 'Ngày', 'Trạng thái'], (d.listIssue || []).map(i => [esc(i.issueNumber || ''), esc(U.date(i.issueDate)), esc(i.statusName || i.status || '')]))}
            ${(d.productionOrder || {}).status === 'Closed' && APP.prodOrder ? '<div class="mt">' + APP.prodOrder.costHtml(d) + '</div>' : ''}`;
        });
      } });
  }

  // Tab Nguyên liệu & giữ chỗ (gộp theo nguyên liệu cả ngày).
  function materials(box, planId) {
    box.innerHTML = '<div class="muted small">Đang tải…</div>';
    load(planId).then(x => {
      if (!box.isConnected) return;
      if (!x.ok) { box.innerHTML = fail(x, 'nguyên liệu'); return; }
      const rows = x.data.listMaterial || [];
      box.innerHTML = rows.length ? `<div class="small muted mb">Gộp giữ chỗ của mọi lệnh trong ngày theo nguyên liệu. Thiếu → kho giữ chỗ bổ sung khi hàng về (nút Giữ chỗ bổ sung ở ô lệnh).</div><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Nguyên liệu</th><th class="num">Cần</th><th class="num">Đã giữ</th><th class="num">Thiếu</th><th class="num">Số lệnh</th></tr></thead><tbody>${rows.map(r => `<tr><td><b>${esc(r.ingredientName || '')}</b></td><td class="num">${U.num(r.required, 3)} ${esc(r.unitName || '')}</td><td class="num">${U.num(r.reserved, 3)}</td><td class="num">${r.short > 0 ? `<b class="bad">${U.num(r.short, 3)}</b>` : '0'}</td><td class="num">${r.orderCount}</td></tr>`).join('')}</tbody></table></div>`
        : '<div class="muted small">Chưa có giữ chỗ nguyên liệu (kế hoạch chưa ra lệnh).</div>';
    });
  }

  // Tab Chênh lệch & giá thành.
  function variance(box, planId) {
    box.innerHTML = '<div class="muted small">Đang tải…</div>';
    load(planId).then(x => {
      if (!box.isConnected) return;
      if (!x.ok) { box.innerHTML = fail(x, 'chênh lệch'); return; }
      const v = x.data.listVariance || [], c = x.data.listCost || [];
      box.innerHTML = `<h3>Chênh lệch tiêu hao</h3>${v.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Lệnh</th><th>Món</th><th>Nguyên liệu</th><th class="num">Định mức</th><th class="num">Thực tế</th><th class="num">%</th><th class="num">Giá trị</th></tr></thead><tbody>${v.map(r => `<tr><td><a href="#/prod-order-detail?id=${esc(r.productionOrderId)}">${esc(r.orderNumber || '')}</a></td><td>${esc(r.dishName || '')}</td><td>${esc(r.ingredientName || '')}</td><td class="num">${U.num(r.standardQuantity, 3)}</td><td class="num">${U.num(r.actualQuantity, 3)}</td><td class="num">${nil(r.variancePercent) ? '' : (r.isOverThreshold ? `<b class="bad">${U.pct(r.variancePercent)}</b>` : U.pct(r.variancePercent))}</td><td class="num">${nil(r.varianceCost) ? '' : U.money(r.varianceCost)}</td></tr>`).join('')}</tbody></table></div>` : '<div class="muted small mb">Chưa có – chênh lệch tính khi lệnh đóng.</div>'}
        <h3 class="mt">Giá thành thực tế theo lệnh</h3>${c.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Lệnh</th><th>Món</th><th>Ca</th><th>Trạng thái</th><th class="num">Suất đạt</th><th class="num">Giá thành</th><th class="num">/ suất</th></tr></thead><tbody>${c.map(r => `<tr><td><a href="#/prod-order-detail?id=${esc(r.productionOrderId)}">${esc(r.orderNumber || '')}</a></td><td>${esc(r.dishName || '')}</td><td>${esc(r.shiftCode || '')}</td><td>${esc(r.status || '')}</td><td class="num">${U.num(r.acceptedPortions)}</td><td class="num">${U.money(r.totalCost)}</td><td class="num">${nil(r.costPerPortion) ? '–' : U.money(r.costPerPortion)}</td></tr>`).join('')}</tbody></table></div>` : '<div class="muted small">Chưa tính giá thành.</div>'}`;
    });
  }

  APP.prodBoard = { render, materials, variance, reload: id => load(id, true) };
})();
