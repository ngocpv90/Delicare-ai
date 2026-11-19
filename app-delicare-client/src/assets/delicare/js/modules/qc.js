/* P6 – QC đầu vào (PASS/HOLD/REJECT), Checklist ATTP theo ca, Nhật ký nhiệt độ */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  // ---------- Phiếu QC đầu vào ----------
  APP.register('qc-inspection', (el, f, params) => {
    APP.genericList(el, f, { noTransit: true, noAdd: true,
      top: `<div class="notice info">${I('shield')}<div><b>Kết quả theo BRD: PASS / HOLD / REJECT.</b> Mở phiếu để kết luận từng dòng (QCInspectionLine). REJECT → tự lập Sự cố NCC (SupplierIssue). Chỉ dòng PASS được ghi sổ nhập kho.</div></div>`,
      cols: [{ k: 'InspectionNumber', label: 'Số phiếu' }, { k: 'GoodsReceiptId', label: 'Phiếu nhập' }, { k: 'InspectionDate', label: 'Ngày kiểm' }, { k: 'InspectorEmployeeId', label: 'Người kiểm', render: r => esc(Store.label('Employee', r.InspectorEmployeeId)) }, { k: 'n', label: 'Dòng PASS/HOLD/REJECT', col: false, nosort: true, render: r => { const g = U.group(Store.by('QCInspectionLine', 'QCInspectionId', r.Id), 'Result'); return ['PASS', 'HOLD', 'REJECT'].map(k => `${UI.badge(k)} ${(g[k] || []).length}`).join(' ') + (g.undefined ? ` <span class="muted small">${g.undefined.length} chưa kiểm</span>` : ''); } }, { k: 'Result', label: 'Kết luận' }] });
    APP.rowExtra(el, tr => { const r = Store.get('QCInspection', tr.dataset.row); if (!r.Result) { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-k>Kết luận</button> `); td.querySelector('[data-k]').onclick = () => inspect(r.Id); } });
    if (params.id && Store.get('QCInspection', params.id)) setTimeout(() => inspect(params.id), 50);
  });
  function inspect(id) {
    const qi = Store.get('QCInspection', id), gr = Store.get('GoodsReceipt', qi.GoodsReceiptId);
    const lines = Store.by('QCInspectionLine', 'QCInspectionId', id);
    UI.modal({ title: 'Kiểm tra chất lượng đầu vào – ' + esc(qi.InspectionNumber), sub: `${esc(gr.ReceiptNumber)} · ${esc(Store.label('Supplier', gr.SupplierId))} · QCInspection / QCInspectionLine`, size: 'lg',
      body: `<table class="table dc-table dt"><thead><tr><th>Nguyên liệu</th><th>Bảo quản</th><th class="num">Nhiệt độ °C<small>Temperature</small></th><th class="num">KL<small>Weight</small></th><th>Cảm quan<small>Appearance</small></th><th>Bao bì<small>Packaging</small></th><th>HSD</th><th>Kết quả<small>Result</small></th></tr></thead><tbody>${lines.map(l => { const g = Store.get('Ingredient', l.IngredientId); return `<tr><td>${esc(g.Name)}</td><td class="small">${esc(g.StorageCondition || '')}</td><td class="num"><input type="number" step="0.1" class="input" style="width:72px" data-t="${l.Id}" value="${l.Temperature ?? ''}"><div class="small muted" data-th="${l.Id}"></div></td><td class="num">${U.num(l.Weight)}</td><td><input class="input" style="width:90px" data-a="${l.Id}" value="${esc(l.Appearance || 'Đạt')}"></td><td><input class="input" style="width:110px" data-p="${l.Id}" value="${esc(l.Packaging || 'Nguyên vẹn')}"></td><td>${U.date(l.ExpiryDate)}</td><td><select class="select" data-r="${l.Id}"><option value="">—</option>${['PASS', 'HOLD', 'REJECT'].map(x => `<option ${l.Result === x ? 'selected' : ''}>${x}</option>`).join('')}</select></td></tr>`; }).join('')}</tbody></table><div id="qWarn"></div><div class="field mt"><label>Ghi chú / lý do HOLD-REJECT <code>QCInspectionLine.Remark</code></label><textarea class="input" id="rm" rows="2"></textarea></div><div class="small muted mt">Gợi ý: hàng lạnh > 5°C → HOLD; hàng đông > -18°C → REJECT (quy tắc PROPOSED – BA/PO CONFIRM).</div>`,
      foot: '<button class="btn" data-close>Đóng</button><button class="btn primary" id="qSave">Lưu kết luận</button>',
      onMount: (m, close) => {
        // QcInspection/GetDetail: ngưỡng nhiệt độ nhận hàng + kết quả gợi ý từng dòng (chỉ gợi ý – người kiểm vẫn quyết định).
        if (Store.isLive) Store.query('QcInspection/GetDetail', { id }).then(x => {
          if (!x.ok || !m.isConnected) return;
          const d = x.data || {}, SUG = { DAT: 'PASS', DAT_MOT_PHAN: 'HOLD', KHONG_DAT: 'REJECT', PASS: 'PASS', HOLD: 'HOLD', REJECT: 'REJECT' }, SUG_VI = { PASS: 'Đạt', HOLD: 'Tạm giữ', REJECT: 'Không đạt' };
          (d.listLine || []).forEach(dl => {
            const l = lines.find(k => k.Id === dl.qcInspectionLineId || k.GoodsReceiptLineId === dl.id); if (!l) return;
            const inp = m.querySelector(`[data-t="${l.Id}"]`), sel = m.querySelector(`[data-r="${l.Id}"]`), hint = m.querySelector(`[data-th="${l.Id}"]`), sug = SUG[dl.suggestedResult];
            if (inp && inp.value === '' && dl.temperature !== null && dl.temperature !== undefined) inp.value = dl.temperature;
            if (hint) hint.innerHTML = [dl.maxAllowedTemperature !== null && dl.maxAllowedTemperature !== undefined ? 'Tối đa ' + U.num(dl.maxAllowedTemperature, 1) + '°C' : '', esc(dl.temperatureZoneName || ''), sug ? 'Gợi ý: ' + SUG_VI[sug] : ''].filter(Boolean).join(' · ');
            if (sel && !sel.value && sug) sel.value = sug;
          });
          const warn = d.listWarning || [], box = m.querySelector('#qWarn');
          if (box && warn.length) box.innerHTML = `<div class="notice warn mt">${I('alert')}<div>${warn.map(esc).join('<br>')}</div></div>`;
        });
        m.querySelectorAll('[data-t]').forEach(inp => inp.oninput = () => { const l = Store.get('QCInspectionLine', inp.dataset.t), g = Store.get('Ingredient', l.IngredientId), v = +inp.value, sel = m.querySelector(`[data-r="${l.Id}"]`); if (g.StorageType === 'CHILLED') sel.value = v > 5 ? 'HOLD' : 'PASS'; if (g.StorageType === 'FROZEN') sel.value = v > -18 ? 'REJECT' : 'PASS'; });
        m.querySelector('#qSave').onclick = () => {
          const res = lines.map(l => ({ l, r: m.querySelector(`[data-r="${l.Id}"]`).value }));
          if (res.some(x => !x.r)) return UI.toast('Chưa kết luận đủ các dòng', 'err');
          const rm = m.querySelector('#rm').value;
          if (res.some(x => x.r !== 'PASS') && !rm) return UI.toast('Nhập lý do cho dòng HOLD/REJECT', 'err');
          if (Store.isLive) {
            // QcInspection/Conclude: backend ghi dòng QC, phiếu nhập, lô và sự cố NCC trong một giao dịch (PASS / HOLD / REJECT ↔ DAT / DAT_MOT_PHAN / KHONG_DAT).
            const num = v => v === '' || v === null || v === undefined ? null : Number(v);
            return Store.act('QcInspection/Conclude', { json: { id, inspectorEmployeeId: qi.InspectorEmployeeId || null, note: rm || null, listLine: res.map(({ l, r }) => ({ goodsReceiptLineId: l.GoodsReceiptLineId, result: r, temperature: num(m.querySelector(`[data-t="${l.Id}"]`).value), weight: l.Weight ?? null, appearance: m.querySelector(`[data-a="${l.Id}"]`).value, packaging: m.querySelector(`[data-p="${l.Id}"]`).value, remark: r !== 'PASS' ? rm : (l.Remark || null) })) }, tables: ['QCInspection', 'QCInspectionLine', 'GoodsReceipt', 'GoodsReceiptLine', 'MaterialLot', 'SupplierIssue'] })
              .then(x => { if (!x.ok) return; close(); UI.toast('Đã kết luận QC đầu vào', 'ok'); APP.refresh(); });
          }
          res.forEach(({ l, r }) => { Store.update('QCInspectionLine', l.Id, { Result: r, Temperature: Number(m.querySelector(`[data-t="${l.Id}"]`).value), Appearance: m.querySelector(`[data-a="${l.Id}"]`).value, Packaging: m.querySelector(`[data-p="${l.Id}"]`).value, Remark: r !== 'PASS' ? rm : l.Remark }); const gl = Store.get('GoodsReceiptLine', l.GoodsReceiptLineId); if (gl) Store.update('GoodsReceiptLine', gl.Id, { QualityStatus: r, AcceptedQuantity: r === 'PASS' ? gl.ReceivedQuantity : 0, RejectedQuantity: r === 'REJECT' ? gl.ReceivedQuantity : 0 }); });
          const all = res.map(x => x.r); const overall = all.every(x => x === 'PASS') ? 'PASS' : all.every(x => x === 'REJECT') ? 'REJECT' : all.includes('PASS') ? 'PASS' : 'HOLD';
          Store.update('QCInspection', id, { Result: overall, InspectorEmployeeId: qi.InspectorEmployeeId || (Store.rows('Employee').find(e => Store.label('Position', e.PositionId).includes('QC')) || {}).Id, Note: rm || qi.Note });
          Store.update('GoodsReceipt', gr.Id, { Status: all.every(x => x === 'PASS') ? 'Accepted' : all.includes('PASS') ? 'PartiallyAccepted' : 'Rejected' });
          if (all.includes('REJECT') || all.includes('HOLD')) Store.add('SupplierIssue', { SupplierId: gr.SupplierId, PurchaseOrderId: gr.PurchaseOrderId, GoodsReceiptId: gr.Id, IssueType: 'QUALITY', Severity: all.includes('REJECT') ? 'High' : 'Medium', Description: rm, ReportedAt: U.iso(new Date()), DueAt: U.addDays(Store.ctx.date, 2) + 'T17:00', IssueStatus: 'Open' });
          close(); UI.toast(`Đã kết luận QC: ${overall}. Phiếu nhập → ${Store.get('GoodsReceipt', gr.Id).Status}${all.some(x => x !== 'PASS') ? ' · đã lập Sự cố NCC' : ''}`, 'ok');
          APP.refresh();
        };
      } });
  }
  // ---------- Nhật ký nhiệt độ ----------
  const ts = { zone: null };
  const offRange = x => x.IsWithinRange === 0 || x.IsWithinRange === false;
  const MEASURE_TYPES = [['STORAGE', 'Kho / tủ bảo quản'], ['HOT_HOLDING', 'Giữ nóng thành phẩm'], ['BLAST_CHILL', 'Làm lạnh nhanh'], ['PACKING_AREA', 'Khu chia suất / đóng gói'], ['AMBIENT', 'Nhiệt độ phòng']];
  const fv = (m, sel) => { const e = m.querySelector(sel); return e ? String(e.value || '').trim() : ''; };
  const options = (t, rows, sel, label) => rows.map(r => `<option value="${esc(r.Id)}" ${r.Id === sel ? 'selected' : ''}>${esc(label ? label(r) : Store.label(t, r))}</option>`).join('');
  // Ghi một lần đo mới (TemperatureLog/Record). Sổ nhiệt độ chỉ ghi thêm: không có sửa / xóa; ngưỡng và đạt / không đạt do máy chủ tính.
  function recordTemperature(zoneId) {
    Promise.all([Store.ensure('StorageZone'), Store.ensure('ProductionBatche')]).then(() => {
      const zones = Store.rows('StorageZone'), batches = Store.rows('ProductionBatche').slice(0, 300);
      UI.modal({ title: 'Ghi nhiệt độ', sub: 'Mỗi lần đo là một dòng mới trong sổ, không sửa / xóa được sau khi ghi.',
        body: `<form class="form-grid" novalidate>
          <div class="field"><label>Khu vực bảo quản</label><select class="select" id="tZone"><option value="">—</option>${options('StorageZone', zones, zoneId)}</select></div>
          <div class="field"><label>Mẻ sản xuất</label><select class="select" id="tBatch"><option value="">—</option>${options('ProductionBatche', batches)}</select></div>
          <div class="field"><label>Loại đo <span class="req">*</span></label><select class="select" id="tType">${MEASURE_TYPES.map(([c, l]) => `<option value="${c}">${esc(l)}</option>`).join('')}</select></div>
          <div class="field"><label>Nhiệt độ °C <span class="req">*</span></label><input type="number" step="0.1" class="input" id="tTemp"></div>
          <div class="field"><label>Đo lúc</label><input type="datetime-local" class="input" id="tAt" value="${U.iso(new Date())}"></div>
          <div class="field"><label>Thiết bị đo</label><input class="input" id="tDev" maxlength="100"></div>
          <div class="field full"><label>Hành động khắc phục</label><textarea class="input" id="tFix" rows="2" maxlength="2000" placeholder="Bắt buộc khi nhiệt độ ngoài ngưỡng của khu vực"></textarea></div></form>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="tSave">Ghi nhiệt độ</button>',
        onMount: (m, close) => m.querySelector('#tSave').onclick = () => {
          const zid = fv(m, '#tZone'), bid = fv(m, '#tBatch'), raw = fv(m, '#tTemp'), fix = fv(m, '#tFix'), z = zid && Store.get('StorageZone', zid);
          if (!zid && !bid) return UI.toast('Chọn khu vực bảo quản hoặc mẻ sản xuất', 'err');
          if (raw === '' || isNaN(Number(raw))) return UI.toast('Nhập nhiệt độ đo được', 'err');
          const t = Number(raw), lo = z && z.TemperatureMin !== null && z.TemperatureMin !== undefined ? Number(z.TemperatureMin) : null, hi = z && z.TemperatureMax !== null && z.TemperatureMax !== undefined ? Number(z.TemperatureMax) : null;
          if (((lo !== null && t < lo) || (hi !== null && t > hi)) && !fix) return UI.toast('Nhiệt độ ngoài ngưỡng của khu vực – nhập hành động khắc phục', 'err');
          Store.act('TemperatureLog/Record', { form: { StorageZoneId: zid, ProductionBatchId: bid, MeasurementType: fv(m, '#tType'), TemperatureC: t, MeasuredAt: fv(m, '#tAt'), DeviceCode: fv(m, '#tDev'), SourceChannel: 'WEB', CorrectiveAction: fix }, tables: ['TemperatureLog'] }).then(r => {
            if (!r.ok) return;
            const d = r.data || {}, log = d.log || {}, out = log.isWithinRange === false, warn = (d.listWarning || []).map(esc).join('<br>');
            if (zid) ts.zone = zid;
            close(); UI.toast(out ? `Đã ghi ${U.num(t, 1)}°C – <b>ngoài ngưỡng</b>${warn ? '<br>' + warn : ''}` : `Đã ghi nhiệt độ ${U.num(t, 1)}°C${warn ? '<br>' + warn : ''}`, out || warn ? 'warn' : 'ok'); APP.refresh();
          });
        } });
    });
  }
  APP.register('qc-temperature', (el, f) => {
    const live = Store.isLive, perm = Store.perm(f.id);
    const used = Store.rows('StorageZone').filter(z => Store.by('TemperatureLog', 'StorageZoneId', z.Id).length);
    const zones = used.length || !live ? used : Store.rows('StorageZone');   // chế độ thật chưa có lần đo nào: vẫn hiện các khu vực để ghi lần đầu
    // Chưa có khu vực bảo quản nào (vd. mới chỉ có nhiệt độ lõi của mẻ): bỏ khối biểu đồ theo khu vực, vẫn hiện danh sách lần đo.
    if (zones.length && (!ts.zone || !zones.some(x => x.Id === ts.zone))) ts.zone = zones[0].Id;
    const z = zones.length ? Store.get('StorageZone', ts.zone) : null;
    const logs = !z ? [] : Store.by('TemperatureLog', 'StorageZoneId', ts.zone).sort((a, b) => a.MeasuredAt.localeCompare(b.MeasuredAt));
    const out = Store.rows('TemperatureLog').filter(offRange);
    APP.genericList(el, f, { noAdd: live,   // chế độ thật: ghi qua TemperatureLog/Record (nút "Ghi nhiệt độ")
      top: !z ? '' : `<div class="card panel mb"><div class="row-flex mb"><span class="small muted">Khu vực bảo quản:</span>${zones.map(x => `<button class="chip ${x.Id === ts.zone ? 'active' : ''}" data-z="${x.Id}">${esc(x.Name)}</button>`).join('')}</div>
        <div class="panel-head"><div><div class="panel-title">${esc(z.Name)} – 48 giờ gần nhất</div><div class="panel-desc">TemperatureLog.TemperatureC · ngưỡng StorageZone.TemperatureMin/Max = ${U.num(z.TemperatureMin)}…${U.num(z.TemperatureMax)}°C · điểm đỏ = IsWithinRange = 0</div></div><span class="badge ${logs.some(offRange) ? 'red' : 'green'}">${logs.filter(offRange).length} lần vượt ngưỡng</span></div>
        ${UI.lines(logs.map(l => U.dt(l.MeasuredAt).slice(6)), [{ name: z.Name, values: logs.map(l => +l.TemperatureC), color: '#4C83B6' }], { bands: [[+z.TemperatureMin, +z.TemperatureMax]], badFn: v => v < z.TemperatureMin || v > z.TemperatureMax, min: Math.min(z.TemperatureMin, ...logs.map(l => +l.TemperatureC)) - 1, max: Math.max(z.TemperatureMax, ...logs.map(l => +l.TemperatureC)) + 1 })}
        ${out.length ? `<div class="notice warn mt">${I('thermo')}<div><b>${out.length} lần đo ngoài ngưỡng</b>${out.slice(0, 3).map(o => `${U.dt(o.MeasuredAt)} · ${esc(Store.label('StorageZone', o.StorageZoneId) || Store.label('ProductionBatche', o.ProductionBatchId))}: ${U.num(o.TemperatureC)}°C → ${esc(o.CorrectiveAction || 'Chưa có hành động khắc phục')}`).join('<br>')}</div></div>` : ''}</div>`,
      cols: [{ k: 'MeasuredAt', label: 'Đo lúc' }, { k: 'StorageZoneId', label: 'Khu vực' }, { k: 'ProductionBatchId', label: 'Mẻ SX' }, { k: 'MeasurementType', label: 'Loại đo' }, { k: 'TemperatureC', label: '°C', num: true }, { k: 'IsWithinRange', label: 'Trong ngưỡng' }, { k: 'DeviceCode', label: 'Thiết bị' }, { k: 'CorrectiveAction', label: 'Khắc phục' }],
      rows: () => Store.rows('TemperatureLog').slice().sort((a, b) => b.MeasuredAt.localeCompare(a.MeasuredAt)),
      defaults: { MeasuredAt: U.iso(new Date()), SourceChannel: 'WEB' }
    });
    el.querySelectorAll('[data-z]').forEach(b => b.onclick = () => { ts.zone = b.dataset.z; APP.refresh(); });
    if (!live) return;
    const bar = el.querySelector('.heading-actions');
    if (bar && perm.add !== false) {
      bar.insertAdjacentHTML('beforeend', `<button class="btn primary" data-act="temp">${I('plus')} Ghi nhiệt độ</button>`);
      bar.querySelector('[data-act="temp"]').onclick = () => recordTemperature(ts.zone);
    }
    // Sổ chỉ ghi thêm: bỏ nút sửa / xóa trên dòng, ngăn chi tiết chỉ để xem.
    APP.rowExtra(el, tr => {
      tr.querySelectorAll('[data-edit],[data-del]').forEach(b => b.remove());
      const view = () => APP.openDetail('TemperatureLog', tr.dataset.row, f, { noEdit: true });
      tr.onclick = e => { if (e.target.closest('button,a,input,select')) return; view(); };
      const vb = tr.querySelector('[data-view]'); if (vb) vb.onclick = view;
    });
  });
  // ---------- Thực hiện checklist ATTP ----------
  const cs = { ck: null, shift: null, run: {}, busy: {} };
  APP.register('qc-checklist-result', (el, f) => {
    const cks = Store.rows('FoodSafetyChecklist').filter(c => c.Code !== 'VS-KHAY');
    if (!cs.ck) cs.ck = cks[0].Id; const ck = Store.get('FoodSafetyChecklist', cs.ck);
    const shifts = Store.rows('Shift'); if (!cs.shift || !Store.get('Shift', cs.shift)) cs.shift = (shifts[1] || shifts[0] || {}).Id;
    if (!cs.shift) return APP.genericList(el, f);
    const items = Store.by('FoodSafetyChecklistItem', 'ChecklistId', ck.Id).sort((a, b) => a.SortOrder - b.SortOrder);
    const today = Store.rows('FoodSafetyChecklistResult').filter(r => r.ChecklistId === ck.Id && U.day(r.CheckDate) === Store.ctx.date && r.ShiftId === cs.shift);
    const days = Array.from({ length: 7 }, (_, i) => U.addDays(Store.ctx.date, i - 6));
    // FoodSafetyChecklistResult/GetRun: lần chạy của ngày / ca – kết quả hiện tại từng tiêu chí, hạn làm, tiêu chí trọng yếu chưa đạt, tỷ lệ đạt 7 ngày.
    const runKey = [ck.Id, Store.ctx.date, cs.shift].join('|');
    let run = null;
    if (Store.isLive) {
      run = cs.run[runKey] || null;
      if (!run && !cs.busy[runKey]) {
        cs.busy[runKey] = true;
        Store.query('FoodSafetyChecklistResult/GetRun', { ChecklistId: ck.Id, CheckDate: Store.ctx.date, ShiftId: cs.shift }).then(x => { cs.busy[runKey] = false; cs.run[runKey] = x.ok && x.data ? x.data : { failed: true }; if (APP.current === 'qc-checklist-result') APP.refresh(); });
      }
      if (run && !Array.isArray(run.listItem)) run = null;
    }
    const cur = it => {
      if (!run) return today.find(x => x.ChecklistItemId === it.Id);
      const ri = run.listItem.find(x => x.item && x.item.id === it.Id), r = ri && ri.result;
      return r ? { ActualValue: r.actualValue, ResultCode: r.resultCode, CorrectiveAction: r.correctiveAction } : null;
    };
    const rate = run && (run.listPassRate7d || []).length ? run.listPassRate7d : null;
    const crit = run ? (run.listCriticalFailure || []) : [];
    const doneText = run ? (run.doneCount ? 'Đã ghi ' + run.doneCount + '/' + items.length + ' tiêu chí cho ca này' : 'Chưa thực hiện cho ca này') + (run.dueAt ? ' · Hạn làm: ' + U.dt(run.dueAt) : '') + (run.isOverdue ? ' <span class="tag red">Quá hạn</span>' : '') : (today.length ? 'Đã ghi ' + today.length + '/' + items.length + ' tiêu chí cho ca này' : 'Chưa thực hiện cho ca này');
    APP.genericList(el, f, { noAdd: true,
      top: `<div class="grid g-main mb"><div class="card panel"><div class="spread mb"><div><div class="panel-title">Thực hiện checklist: ${esc(ck.Name)}</div><div class="panel-desc">${esc(ck.Code)} · V${ck.VersionNo} · tần suất ${esc(ck.FrequencyCode)} · ngày ${U.date(Store.ctx.date)}</div></div><div class="row-flex">${cks.map(c => `<button class="chip ${c.Id === ck.Id ? 'active' : ''}" data-ck="${c.Id}">${esc(c.Code)}</button>`).join('')}</div></div>
        <div class="row-flex mb">${shifts.map(s => `<button class="chip ${s.Id === cs.shift ? 'active' : ''}" data-sh="${s.Id}">${esc(s.Code)}</button>`).join('')}</div>
        <table class="table dc-table dt"><thead><tr><th>Tiêu chí<small>ItemName</small></th><th>Chuẩn<small>ExpectedValue</small></th><th>Giá trị đo<small>ActualValue</small></th><th>Kết quả<small>ResultCode</small></th><th>Khắc phục<small>CorrectiveAction</small></th></tr></thead><tbody>${items.map(it => { const r = cur(it); return `<tr><td>${esc(it.ItemName)} ${it.IsCritical ? '<span class="tag red">Trọng yếu</span>' : ''}</td><td>${esc(it.ExpectedValue || '')}</td><td><input class="input" style="width:90px" data-v="${it.Id}" value="${esc(r ? r.ActualValue || '' : '')}"></td><td><select class="select" data-rc="${it.Id}"><option value="">—</option><option ${r && r.ResultCode === 'PASS' ? 'selected' : ''}>PASS</option><option ${r && r.ResultCode === 'FAIL' ? 'selected' : ''}>FAIL</option></select></td><td><input class="input" data-ca="${it.Id}" value="${esc(r ? r.CorrectiveAction || '' : '')}" placeholder="Bắt buộc nếu FAIL"></td></tr>`; }).join('')}</tbody></table>
        ${crit.length ? `<div class="notice warn mt">${I('alert')}<div><b>Tiêu chí trọng yếu đang không đạt</b>${crit.map(esc).join('<br>')}</div></div>` : ''}
        <div class="spread mt"><span class="small muted">${doneText}</span><button class="btn primary" id="ckSave">${I('ok')} Ghi kết quả checklist</button></div></div>
        <div class="card panel"><div class="panel-title mb">Tỷ lệ đạt 7 ngày</div>${rate ? UI.bars(rate.map(p => U.date(p.date).slice(0, 5)), [{ name: 'Đạt %', values: rate.map(p => Number(p.rate) || 0), color: '#91AF42' }], { h: 190 }) : UI.bars(days.map(d => U.date(d).slice(0, 5)), [{ name: 'Đạt %', values: days.map(d => { const rr = Store.rows('FoodSafetyChecklistResult').filter(r => r.ChecklistId === ck.Id && U.day(r.CheckDate) === d); return rr.length ? Math.round(rr.filter(r => r.ResultCode === 'PASS').length / rr.length * 100) : 0; }), color: '#91AF42' }], { h: 190 })}</div></div>`,
      cols: [{ k: 'CheckDate', label: 'Ngày' }, { k: 'ShiftId', label: 'Ca' }, { k: 'ChecklistItemId', label: 'Tiêu chí', render: r => esc((Store.get('FoodSafetyChecklistItem', r.ChecklistItemId) || {}).ItemName || '') }, { k: 'ActualValue', label: 'Giá trị' }, { k: 'ResultCode', label: 'Kết quả', render: r => UI.badge(r.ResultCode) }, { k: 'CorrectiveAction', label: 'Khắc phục' }],
      rows: () => Store.rows('FoodSafetyChecklistResult').filter(r => r.ChecklistId === ck.Id).slice().sort((a, b) => b.CheckDate.localeCompare(a.CheckDate)) });
    el.querySelectorAll('[data-ck]').forEach(b => b.onclick = () => { cs.ck = b.dataset.ck; APP.refresh(); });
    el.querySelectorAll('[data-sh]').forEach(b => b.onclick = () => { cs.shift = b.dataset.sh; APP.refresh(); });
    el.querySelector('#ckSave').onclick = () => {
      const vals = items.map(it => ({ it, v: el.querySelector(`[data-v="${it.Id}"]`).value, r: el.querySelector(`[data-rc="${it.Id}"]`).value, ca: el.querySelector(`[data-ca="${it.Id}"]`).value }));
      if (vals.some(x => !x.r)) return UI.toast('Chọn kết quả cho tất cả tiêu chí', 'err');
      if (vals.some(x => x.r === 'FAIL' && !x.ca)) return UI.toast('Tiêu chí FAIL bắt buộc nhập hành động khắc phục', 'err');
      if (Store.isLive) {
        // FoodSafetyChecklistResult/Record: ghi cả lượt checklist một lần; backend tự chuyển mẻ liên quan khi tiêu chí trọng yếu không đạt.
        const crit0 = vals.filter(x => x.r === 'FAIL' && x.it.IsCritical);
        return Store.act('FoodSafetyChecklistResult/Record', { json: { checklistId: ck.Id, checkDate: Store.ctx.date, shiftId: cs.shift || null, workstationId: (Store.rows('KitchenWorkstation')[2] || {}).Id || null, listItem: vals.map(x => ({ checklistItemId: x.it.Id, resultCode: x.r, actualValue: x.v || null, correctiveAction: x.ca || null })) }, tables: ['FoodSafetyChecklistResult', 'ProductionBatche'] })
          .then(r => { if (!r.ok) return; if (r.data && r.data.run) cs.run[runKey] = r.data.run; else delete cs.run[runKey]; UI.toast(crit0.length ? `Có ${crit0.length} tiêu chí TRỌNG YẾU không đạt – mẻ liên quan chuyển chờ QC` : 'Đã ghi kết quả checklist ATTP', crit0.length ? 'warn' : 'ok'); APP.refresh(); });
      }
      vals.forEach(x => { const ex = today.find(r => r.ChecklistItemId === x.it.Id); const data = { ResultCode: x.r, ActualValue: x.v, CorrectiveAction: x.ca || null, CheckedBy: Store.user.id, CheckedAt: U.iso(new Date()) }; if (ex) Store.update('FoodSafetyChecklistResult', ex.Id, data); else Store.add('FoodSafetyChecklistResult', Object.assign({ ChecklistId: ck.Id, ChecklistItemId: x.it.Id, CheckDate: Store.ctx.date, ShiftId: cs.shift, WorkstationId: (Store.rows('KitchenWorkstation')[2] || {}).Id }, data)); });
      const crit = vals.filter(x => x.r === 'FAIL' && x.it.IsCritical);
      UI.toast(crit.length ? `Có ${crit.length} tiêu chí TRỌNG YẾU không đạt – mẻ liên quan chuyển chờ QC` : 'Đã ghi kết quả checklist ATTP', crit.length ? 'warn' : 'ok'); APP.refresh();
    };
  });
  // ---------- Vệ sinh khay / hộp (chế độ dữ liệu thật) – docs/frontend-handoff/BE2.5.md mục 2, 3 ----------
  // Chuỗi: thu hồi → kiểm tra (tự lập mẻ) → 6 bước vệ sinh đi lần lượt → kết luận Đạt (khay sẵn sàng dùng lại) / Không đạt (vệ sinh lại bằng mẻ mới).
  const CLEAN_TABLES = ['CleaningBatche', 'CleaningChecklistResult', 'PackagingLedger'];
  const RETURN_RESULTS = [['OK', 'Đủ'], ['SHORTAGE', 'Thiếu'], ['DAMAGED', 'Hỏng'], ['DIRTY', 'Bẩn'], ['REJECTED', 'Không đạt']];
  const returnLabel = c => [Store.label('Customer', c.CustomerId), U.date(c.ReturnDate), 'trả ' + U.num(c.ReturnedQuantity) + (c.DamagedQuantity ? ', hỏng ' + U.num(c.DamagedQuantity) : '')].filter(Boolean).join(' · ');
  const empOptions = () => '<option value="">—</option>' + options('Employee', Store.rows('Employee').slice(0, 400));
  const numOrNull = v => v === '' || isNaN(Number(v)) ? null : Number(v);
  // Mẻ vệ sinh: xem 6 bước (CleaningBatch/GetDetail), ghi bước (CleaningBatch/RecordResults), kết luận (CleaningBatch/Complete).
  function openCleaning(id, loaded) {
    (loaded ? Promise.resolve({ ok: true, data: loaded }) : Store.query('CleaningBatch/GetDetail', { id })).then(x => {
      if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không tải được mẻ vệ sinh'), 'err');
      const d = x.data, b = d.batch || {}, steps = d.listStep || [], can = a => (d.allowedActions || []).includes(a);
      const canRec = can('recordResults') && Store.perm('qc-cleaning-result').add !== false, canDone = can('complete') && Store.perm('qc-cleaning').edit !== false;
      const no = esc(b.cleaningBatchNumber || '');
      const stepRow = s => {
        const it = s.item || {}, r = s.result || {}, rc = r.resultCode || '';
        const now = rc ? `<span class="badge ${rc === 'PASS' ? 'green' : 'red'}">${esc(s.resultName || (rc === 'PASS' ? 'Đạt' : 'Không đạt'))}</span><div class="small muted">${esc([s.checkedByName, U.dt(r.checkedAt)].filter(Boolean).join(' · '))}${s.historyCount > 1 ? ' · lần ' + s.historyCount : ''}</div>` : '<span class="muted">Chưa làm</span>';
        if (!canRec) return `<tr><td>${esc(it.itemName || '')}</td><td>${now}</td><td>${esc(r.actualValue || '')}</td><td>${esc(r.chemicalUsed || '')}</td><td>${esc(r.correctiveAction || '')}</td></tr>`;
        return `<tr data-s="${esc(it.id)}"><td>${esc(it.itemName || '')}${it.isRequired === false ? '' : ' <span class="req">*</span>'}<div class="small">${now}</div></td><td><select class="select" data-f="rc"><option value="">—</option><option value="PASS" ${rc === 'PASS' ? 'selected' : ''}>Đạt</option><option value="FAIL" ${rc === 'FAIL' ? 'selected' : ''}>Không đạt</option></select></td><td><input class="input" data-f="av" style="width:110px" maxlength="500" value="${esc(r.actualValue || '')}" placeholder="${esc(it.expectedValue || '')}"></td><td>${s.requiresChemical ? `<input class="input" data-f="ch" style="width:130px" maxlength="255" value="${esc(r.chemicalUsed || '')}" placeholder="Bắt buộc khi đạt">` : '<span class="muted">–</span>'}</td><td><input class="input" data-f="ca" maxlength="2000" value="${esc(r.correctiveAction || '')}" placeholder="Bắt buộc khi không đạt"></td></tr>`;
      };
      const info = [['Khách hàng', d.customerName], ['Đơn giao', d.deliveryNumber], ['Loại khay / hộp', d.packagingType], ['Số lượng', U.num(b.containerQuantity)], ['Phụ trách', d.responsibleName], ['Bắt đầu', U.dt(b.startDate)], ['Kết thúc', U.dt(b.endDate)], ['Kết quả', d.qcResultName]].filter(p => p[1]);
      UI.modal({ title: 'Mẻ vệ sinh – ' + no, sub: esc(d.statusName || '') + ` · ${d.passedStepCount || 0}/${d.stepCount || steps.length} bước đạt`, size: 'lg',
        body: `<div class="kv mb">${info.map(([l, v]) => `<div><label>${l}</label><span>${esc(v)}</span></div>`).join('')}</div>
          ${(d.listWarning || []).length ? `<div class="notice warn mb">${I('alert')}<div>${d.listWarning.map(esc).join('<br>')}</div></div>` : ''}
          <div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Bước</th><th>Kết quả</th><th>Giá trị đo</th><th>Hóa chất</th><th>Khắc phục</th></tr></thead><tbody>${steps.map(stepRow).join('') || '<tr><td colspan="5" class="empty">Chưa có bộ tiêu chí vệ sinh</td></tr>'}</tbody></table></div>
          ${canRec ? '<div class="small muted mt">Các bước làm lần lượt: bước sau chỉ ghi được khi các bước bắt buộc phía trước đã đạt. Mỗi lần ghi là một dòng mới, kết quả mới nhất là kết quả hiện tại.</div>' : ''}`,
        foot: `<button class="btn" data-close>Đóng</button>${canRec ? '<button class="btn primary" id="cbSave">Lưu các bước</button>' : ''}${canDone ? `<button class="btn danger" id="cbReject">Không đạt</button><button class="btn primary" id="cbPass" ${d.canPass ? '' : 'disabled title="Còn bước bắt buộc chưa đạt"'}>${I('ok')} Đạt – sẵn sàng dùng lại</button>` : ''}`,
        onMount: (m, close) => {
          const on = (sel, fn) => { const e = m.querySelector(sel); if (e) e.onclick = fn; };
          on('#cbSave', () => {
            const list = [];
            for (const s of steps) {
              const tr = m.querySelector(`tr[data-s="${(s.item || {}).id}"]`); if (!tr) continue;
              const r = s.result || {}, g = k => { const e = tr.querySelector(`[data-f="${k}"]`); return e ? e.value.trim() : ''; };
              const rc = g('rc'), av = g('av'), ch = g('ch'), ca = g('ca');
              if (!rc) continue;
              if (r.resultCode === rc && (r.actualValue || '') === av && (r.chemicalUsed || '') === ch && (r.correctiveAction || '') === ca) continue;   // không đổi → không ghi dòng mới
              if (rc === 'FAIL' && !ca) return UI.toast(`Bước "${esc(s.item.itemName || '')}" không đạt – nhập hành động khắc phục`, 'err');
              if (rc === 'PASS' && s.requiresChemical && !ch) return UI.toast(`Bước "${esc(s.item.itemName || '')}" – nhập hóa chất đã dùng`, 'err');
              list.push({ checklistItemId: s.item.id, resultCode: rc, actualValue: av || null, chemicalUsed: ch || null, correctiveAction: ca || null });
            }
            if (!list.length) return UI.toast('Chưa có bước nào được ghi mới', 'warn');
            // JSON vì có danh sách bước.
            Store.act('CleaningBatch/RecordResults', { json: { id, checklistId: d.checklistId || null, listItem: list }, tables: CLEAN_TABLES }).then(r => {
              if (!r.ok) return;
              close(); UI.toast(`Đã ghi ${list.length} bước vệ sinh`, 'ok'); APP.refresh();
              openCleaning(id, r.data && r.data.batch ? r.data : null);
            });
          });
          const complete = (qc, note) => Store.act('CleaningBatch/Complete', { form: { Id: id, QcResult: qc, Note: note || null }, tables: CLEAN_TABLES }).then(r => {
            if (!r.ok) return;
            close(); UI.toast(qc === 'PASS' ? `Mẻ ${no} đạt – ${U.num(b.containerQuantity)} khay / hộp sẵn sàng dùng lại` : `Mẻ ${no} không đạt – cần lập mẻ mới để vệ sinh lại`, qc === 'PASS' ? 'ok' : 'warn'); APP.refresh();
          });
          on('#cbPass', () => UI.confirm(`Kết luận mẻ <b>${no}</b> <b>ĐẠT</b>? ${U.num(b.containerQuantity)} khay / hộp được ghi sẵn sàng dùng lại. Sau khi kết luận không sửa được.`, () => complete('PASS'), { ok: 'Kết luận đạt' }));
          on('#cbReject', () => UI.confirm(`Kết luận mẻ <b>${no}</b> <b>KHÔNG ĐẠT</b>? Khay / hộp phải vệ sinh lại bằng mẻ mới. Sau khi kết luận không sửa được.`, note => complete('REJECT', note), { input: 'Lý do không đạt', required: true, danger: true, ok: 'Kết luận không đạt' }));
        } });
    });
  }
  // Kiểm tra khay / hộp đã thu hồi (ContainerReturn/Inspect) – khay dùng được tự vào mẻ vệ sinh mới.
  function inspectReturn() {
    Promise.all(['ContainerReturn', 'Customer', 'Employee'].map(t => Store.ensure(t))).then(() => {
      const list = Store.rows('ContainerReturn').filter(c => c.Status === 'Returned').sort((a, b) => String(b.ReturnDate || '').localeCompare(String(a.ReturnDate || '')));
      if (!list.length) return UI.toast('Không có phiếu thu hồi nào đang chờ kiểm tra', 'warn');
      UI.modal({ title: 'Kiểm tra khay / hộp thu hồi', sub: 'Khay / hộp dùng được sau kiểm tra tự vào mẻ vệ sinh mới.',
        body: `<form class="form-grid" novalidate>
          <div class="field full"><label>Phiếu thu hồi <span class="req">*</span></label><select class="select" id="riRet">${options('ContainerReturn', list, null, returnLabel)}</select></div>
          <div class="field"><label>Kết quả kiểm tra <span class="req">*</span></label><select class="select" id="riRes">${RETURN_RESULTS.map(([c, l]) => `<option value="${c}">${l}</option>`).join('')}</select></div>
          <div class="field"><label>Người kiểm</label><select class="select" id="riInsp">${empOptions()}</select></div>
          <div class="field"><label>Số hỏng</label><input type="number" min="0" step="1" class="input" id="riDmg" placeholder="Để trống = số hỏng lúc thu hồi"></div>
          <div class="field"><label>Số thiếu</label><input type="number" min="0" step="1" class="input" id="riMiss" placeholder="Để trống = phải thu − trả về"></div>
          <div class="field full"><label>Người phụ trách vệ sinh</label><select class="select" id="riResp">${empOptions()}</select></div>
          <div class="field full"><label>Ghi chú</label><textarea class="input" id="riNote" rows="2" maxlength="1000" placeholder="Bắt buộc khi kết quả khác Đủ"></textarea></div></form>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="riSave">Ghi kết quả kiểm tra</button>',
        onMount: (m, close) => m.querySelector('#riSave').onclick = () => {
          const rid = fv(m, '#riRet'), res = fv(m, '#riRes'), note = fv(m, '#riNote'), c = Store.get('ContainerReturn', rid);
          if (!c) return UI.toast('Chọn phiếu thu hồi', 'err');
          if (res !== 'OK' && !note) return UI.toast('Kết quả khác Đủ – nhập ghi chú', 'err');
          UI.confirm(`Ghi kết quả kiểm tra <b>${esc((RETURN_RESULTS.find(p => p[0] === res) || [])[1] || res)}</b> cho phiếu thu hồi <b>${esc(returnLabel(c))}</b>? Sau khi ghi không sửa được.`, () =>
            Store.act('ContainerReturn/Inspect', { form: { Id: rid, InspectionResult: res, DamagedQty: numOrNull(fv(m, '#riDmg')), MissingQty: numOrNull(fv(m, '#riMiss')), InspectorEmployeeId: fv(m, '#riInsp'), ResponsibleEmployeeId: fv(m, '#riResp'), Note: note }, tables: ['ContainerReturn', 'ReturnInspection'].concat(CLEAN_TABLES) }).then(r => {
              if (!r.ok) return;
              const made = ((r.data && r.data.listCleaningBatch) || []).map(x => x.cleaningBatchNumber).filter(Boolean), warn = ((r.data && r.data.listWarning) || []).map(esc).join('<br>');
              close(); UI.toast((made.length ? 'Đã kiểm tra – mẻ vệ sinh ' + esc(made.join(', ')) : 'Đã ghi kết quả kiểm tra – không có khay / hộp nào vào mẻ vệ sinh') + (warn ? '<br>' + warn : ''), warn ? 'warn' : 'ok'); APP.refresh();
            }), { ok: 'Ghi kết quả' });
        } });
    });
  }
  // Lập mẻ vệ sinh bằng tay từ phiếu kiểm tra (CleaningBatch/Create) – dùng khi vệ sinh lại sau mẻ không đạt.
  function createCleaning() {
    Promise.all(['ReturnInspection', 'ContainerReturn', 'Customer', 'Employee', 'Shift'].map(t => Store.ensure(t))).then(() => {
      const list = Store.rows('ReturnInspection').filter(r => r.InspectionResult !== 'REJECTED').sort((a, b) => String(b.InspectionDate || '').localeCompare(String(a.InspectionDate || '')));
      if (!list.length) return UI.toast('Chưa có phiếu kiểm tra thu hồi nào để lập mẻ vệ sinh', 'warn');
      const label = r => { const c = Store.get('ContainerReturn', r.ContainerReturnId); return [U.date(r.InspectionDate), c ? returnLabel(c) : '', (RETURN_RESULTS.find(p => p[0] === r.InspectionResult) || [])[1] || ''].filter(Boolean).join(' · '); };
      UI.modal({ title: 'Lập mẻ vệ sinh', sub: 'Mẻ thường được lập tự động khi kiểm tra khay / hộp thu hồi. Lập tay khi cần vệ sinh lại.',
        body: `<form class="form-grid" novalidate>
          <div class="field full"><label>Phiếu kiểm tra thu hồi <span class="req">*</span></label><select class="select" id="ccIns">${options('ReturnInspection', list, null, label)}</select></div>
          <div class="field"><label>Số khay / hộp</label><input type="number" min="1" step="1" class="input" id="ccQty" placeholder="Để trống = toàn bộ số dùng được còn lại"></div>
          <div class="field"><label>Ca</label><select class="select" id="ccShift"><option value="">Theo giờ hiện tại</option>${options('Shift', Store.rows('Shift'))}</select></div>
          <div class="field full"><label>Người phụ trách</label><select class="select" id="ccResp">${empOptions()}</select></div></form>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="ccSave">Lập mẻ</button>',
        onMount: (m, close) => m.querySelector('#ccSave').onclick = () => {
          const iid = fv(m, '#ccIns'), qty = fv(m, '#ccQty');
          if (!iid) return UI.toast('Chọn phiếu kiểm tra thu hồi', 'err');
          if (qty !== '' && !(Number(qty) > 0)) return UI.toast('Số khay / hộp phải lớn hơn 0', 'err');
          Store.act('CleaningBatch/Create', { form: { ReturnInspectionId: iid, ContainerQuantity: qty === '' ? null : Math.round(Number(qty)), ResponsibleEmployeeId: fv(m, '#ccResp'), ShiftId: fv(m, '#ccShift') }, tables: CLEAN_TABLES }).then(r => {
            if (!r.ok) return;
            const nb = (r.data && r.data.batch) || {};
            close(); UI.toast('Đã lập mẻ vệ sinh ' + esc(nb.cleaningBatchNumber || ''), 'ok'); APP.refresh();
            if (nb.id) openCleaning(nb.id, r.data);
          });
        } });
    });
  }
  APP.register('qc-cleaning', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    const perm = Store.perm(f.id);
    APP.genericList(el, f, { noAdd: true });   // mẻ chỉ lập qua nghiệp vụ (kiểm tra thu hồi / lập tay), không nhập tay dòng mẻ
    const bar = el.querySelector('.heading-actions');
    if (bar && Store.perm('log-return-inspection').add !== false) {
      bar.insertAdjacentHTML('beforeend', `<button class="btn" data-act="insp">${I('eye')} Kiểm tra khay thu hồi</button>`);
      bar.querySelector('[data-act="insp"]').onclick = inspectReturn;
    }
    if (bar && perm.add !== false) {
      bar.insertAdjacentHTML('beforeend', `<button class="btn primary" data-act="mk">${I('plus')} Lập mẻ vệ sinh</button>`);
      bar.querySelector('[data-act="mk"]').onclick = createCleaning;
    }
    APP.rowExtra(el, tr => {
      const r = Store.get('CleaningBatche', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td) return;
      const ed = td.querySelector('[data-edit]'); if (ed) ed.remove();   // sửa tay dòng mẻ bị máy chủ chặn
      const doing = r.Status === 'InProgress';
      td.insertAdjacentHTML('afterbegin', `<button class="btn xs ${doing ? 'orange' : ''}" data-cb>${doing ? 'Ghi bước' : 'Các bước'}</button> `);
      td.querySelector('[data-cb]').onclick = () => openCleaning(r.Id);
    });
  });
  APP.register('qc-cleaning-result', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    APP.genericList(el, f, { noAdd: true });   // kết quả từng bước chỉ ghi theo mẻ (CleaningBatch/RecordResults)
    const bar = el.querySelector('.heading-actions');
    if (bar && Store.perm(f.id).add !== false) {
      bar.insertAdjacentHTML('beforeend', `<button class="btn primary" data-act="rec">${I('plus')} Ghi bước vệ sinh</button>`);
      bar.querySelector('[data-act="rec"]').onclick = () => Store.ensure('CleaningBatche').then(() => {
        const open = Store.rows('CleaningBatche').filter(b => b.Status === 'InProgress');
        if (!open.length) return UI.toast('Không có mẻ nào đang vệ sinh', 'warn');
        if (open.length === 1) return openCleaning(open[0].Id);
        UI.modal({ title: 'Chọn mẻ đang vệ sinh', body: `<div class="table-wrap"><table class="table dc-table dt"><tbody>${open.map(b => `<tr><td><b>${esc(b.CleaningBatchNumber || '')}</b></td><td>${esc(U.dt(b.StartDate))}</td><td class="num">${U.num(b.ContainerQuantity)}</td><td class="actions"><button class="btn xs orange" data-pick="${esc(b.Id)}">Ghi bước</button></td></tr>`).join('')}</tbody></table></div>`,
          onMount: (m, close) => m.querySelectorAll('[data-pick]').forEach(b => b.onclick = () => { close(); openCleaning(b.dataset.pick); }) });
      });
    }
    APP.rowExtra(el, tr => {
      tr.querySelectorAll('[data-edit],[data-del]').forEach(b => b.remove());   // sửa / xóa tay kết quả bị máy chủ chặn
      const r = Store.get('CleaningChecklistResult', tr.dataset.row), td = tr.querySelector('.actions');
      if (r && r.CleaningBatchId && td) { td.insertAdjacentHTML('afterbegin', '<button class="btn xs" data-cb>Mẻ vệ sinh</button> '); td.querySelector('[data-cb]').onclick = () => openCleaning(r.CleaningBatchId); }
    });
  });
  // ---------- Sự cố nhà cung cấp (chế độ dữ liệu thật) – BE2.5.md mục 5 ----------
  // SupplierIssue/GetList: quá hạn, người phụ trách, bước chuyển được phép. SupplierIssue/Transition: Mới → Đang xử lý → Đã xử lý → Đóng (hoặc mở lại).
  const si = { data: null, at: 0, busy: false, sig: '' };
  const ISSUE_NEXT = { Open: ['InProgress', 'Resolved'], InProgress: ['Resolved'], Resolved: ['Closed', 'InProgress'] };   // dùng khi chưa tải được danh sách từ máy chủ
  function issueTransit(r, to) {
    const who = esc(Store.label('Supplier', r.SupplierId));
    const run = resolution => Store.act('SupplierIssue/Transition', { form: { Id: r.Id, To: to, Resolution: resolution || null }, tables: ['SupplierIssue'] }).then(x => {
      if (!x.ok) return;
      si.at = 0; UI.toast('Sự cố đã chuyển sang ' + esc((x.data && x.data.statusName) || UI.statusVi('SupplierIssue', 'IssueStatus', to)), 'ok'); APP.refresh();
    });
    if (to === 'Resolved') return UI.confirm(`Xác nhận <b>đã xử lý</b> sự cố của <b>${who}</b>?`, run, { input: 'Cách xử lý', required: true, ok: 'Đã xử lý' });
    if (to === 'Closed') return UI.confirm(`<b>Đóng</b> sự cố của <b>${who}</b>? Sau khi đóng không sửa và không mở lại được.`, () => run(), { danger: true, ok: 'Đóng sự cố' });
    if (r.IssueStatus === 'Resolved') return UI.confirm(`<b>Mở lại</b> sự cố của <b>${who}</b> để xử lý tiếp?`, () => run(), { ok: 'Mở lại' });
    UI.confirm(`Nhận xử lý sự cố của <b>${who}</b>? Bạn sẽ là người phụ trách.`, () => run(), { ok: 'Nhận xử lý' });
  }
  APP.register('qc-supplier-issue', (el, f) => {
    if (!Store.isLive) return APP.genericList(el, f);
    const perm = Store.perm(f.id);
    if (!si.busy && Date.now() - si.at > 30000) {
      si.busy = true;
      Store.query('SupplierIssue/GetList', { Page: 1, PageSize: 500 }).then(x => {
        si.busy = false; si.at = Date.now();
        const data = x.ok && x.data ? x.data : null, sig = data ? JSON.stringify((data.listItem || []).map(i => [i.issue && i.issue.id, i.issue && i.issue.issueStatus, i.isOverdue, i.ownerName])) : '';
        const changed = sig !== si.sig || !data !== !si.data; si.data = data; si.sig = sig;
        if (changed && APP.current === 'qc-supplier-issue') APP.refresh();   // chỉ vẽ lại khi dữ liệu đổi (không làm mất ô tìm / bộ lọc đang dùng)
      });
    }
    const info = {}; ((si.data && si.data.listItem) || []).forEach(i => { if (i.issue) info[i.issue.id] = i; });
    const late = r => !!(info[r.Id] && info[r.Id].isOverdue), overdue = si.data ? si.data.overdueCount || 0 : 0;
    const BTN = { InProgress: 'Nhận xử lý', Resolved: 'Đã xử lý', Closed: 'Đóng' };
    APP.genericList(el, f, {
      top: overdue ? `<div class="notice warn">${I('alert')}<div><b>${overdue} sự cố quá hạn xử lý</b>Sự cố quá hạn được xếp lên đầu danh sách.</div></div>` : '',
      cols: [{ k: 'SupplierId', label: 'Nhà cung cấp' }, { k: 'GoodsReceiptId', label: 'Phiếu nhập' }, { k: 'IssueType', label: 'Loại' }, { k: 'Severity', label: 'Mức độ' }, { k: 'ReportedAt', label: 'Ghi nhận' },
        { k: 'DueAt', label: 'Hạn xử lý', render: r => esc(U.dt(r.DueAt)) + (late(r) ? ' <span class="tag red">Quá hạn</span>' : '') },
        { k: 'OwnerId', label: 'Phụ trách', col: false, nosort: true, render: r => esc((info[r.Id] && info[r.Id].ownerName) || (r.OwnerId ? Store.label('User', r.OwnerId) : '')) }, { k: 'IssueStatus', label: 'Trạng thái' }],
      rows: () => Store.rows('SupplierIssue').slice().sort((a, b) => (late(b) - late(a)) || String(b.ReportedAt || '').localeCompare(String(a.ReportedAt || '')))
    });
    if (perm.edit === false) return;
    APP.rowExtra(el, tr => {
      const r = Store.get('SupplierIssue', tr.dataset.row), td = tr.querySelector('.actions'); if (!r || !td) return;
      const next = info[r.Id] ? info[r.Id].allowedTransitions || [] : ISSUE_NEXT[r.IssueStatus || 'Open'] || [];
      td.insertAdjacentHTML('afterbegin', next.map(to => `<button class="btn xs ${to === 'Resolved' ? 'orange' : ''}" data-to="${esc(to)}">${to === 'InProgress' && r.IssueStatus === 'Resolved' ? 'Mở lại' : BTN[to] || esc(to)}</button> `).join(''));
      td.querySelectorAll('[data-to]').forEach(b => b.onclick = () => issueTransit(r, b.dataset.to));
    });
  });
})();
