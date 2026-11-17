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
      body: `<table class="dt"><thead><tr><th>Nguyên liệu</th><th>Bảo quản</th><th class="num">Nhiệt độ °C<small>Temperature</small></th><th class="num">KL<small>Weight</small></th><th>Cảm quan<small>Appearance</small></th><th>Bao bì<small>Packaging</small></th><th>HSD</th><th>Kết quả<small>Result</small></th></tr></thead><tbody>${lines.map(l => { const g = Store.get('Ingredient', l.IngredientId); return `<tr><td>${esc(g.Name)}</td><td class="small">${esc(g.StorageCondition || '')}</td><td class="num"><input type="number" step="0.1" class="input" style="width:72px" data-t="${l.Id}" value="${l.Temperature ?? ''}"></td><td class="num">${U.num(l.Weight)}</td><td><input class="input" style="width:90px" data-a="${l.Id}" value="${esc(l.Appearance || 'Đạt')}"></td><td><input class="input" style="width:110px" data-p="${l.Id}" value="${esc(l.Packaging || 'Nguyên vẹn')}"></td><td>${U.date(l.ExpiryDate)}</td><td><select class="select" data-r="${l.Id}"><option value="">—</option>${['PASS', 'HOLD', 'REJECT'].map(x => `<option ${l.Result === x ? 'selected' : ''}>${x}</option>`).join('')}</select></td></tr>`; }).join('')}</tbody></table><div class="field mt"><label>Ghi chú / lý do HOLD-REJECT <code>QCInspectionLine.Remark</code></label><textarea class="input" id="rm" rows="2"></textarea></div><div class="small muted mt">Gợi ý: hàng lạnh > 5°C → HOLD; hàng đông > -18°C → REJECT (quy tắc PROPOSED – BA/PO CONFIRM).</div>`,
      foot: '<button class="btn" data-close>Đóng</button><button class="btn primary" id="qSave">Lưu kết luận</button>',
      onMount: (m, close) => {
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
  APP.register('qc-temperature', (el, f) => {
    const zones = Store.rows('StorageZone').filter(z => Store.by('TemperatureLog', 'StorageZoneId', z.Id).length);
    if (!ts.zone) ts.zone = zones[0].Id;
    const z = Store.get('StorageZone', ts.zone);
    const logs = Store.by('TemperatureLog', 'StorageZoneId', ts.zone).sort((a, b) => a.MeasuredAt.localeCompare(b.MeasuredAt));
    const out = Store.rows('TemperatureLog').filter(x => x.IsWithinRange === 0);
    APP.genericList(el, f, {
      top: `<div class="card panel mb"><div class="row-flex mb"><span class="small muted">Khu vực bảo quản:</span>${zones.map(x => `<button class="chip ${x.Id === ts.zone ? 'active' : ''}" data-z="${x.Id}">${esc(x.Name)}</button>`).join('')}</div>
        <div class="panel-head"><div><div class="panel-title">${esc(z.Name)} – 48 giờ gần nhất</div><div class="panel-desc">TemperatureLog.TemperatureC · ngưỡng StorageZone.TemperatureMin/Max = ${U.num(z.TemperatureMin)}…${U.num(z.TemperatureMax)}°C · điểm đỏ = IsWithinRange = 0</div></div><span class="badge ${logs.some(l => l.IsWithinRange === 0) ? 'red' : 'green'}">${logs.filter(l => l.IsWithinRange === 0).length} lần vượt ngưỡng</span></div>
        ${UI.lines(logs.map(l => U.dt(l.MeasuredAt).slice(6)), [{ name: z.Name, values: logs.map(l => +l.TemperatureC), color: '#4C83B6' }], { bands: [[+z.TemperatureMin, +z.TemperatureMax]], badFn: v => v < z.TemperatureMin || v > z.TemperatureMax, min: Math.min(z.TemperatureMin, ...logs.map(l => +l.TemperatureC)) - 1, max: Math.max(z.TemperatureMax, ...logs.map(l => +l.TemperatureC)) + 1 })}
        ${out.length ? `<div class="notice warn mt">${I('thermo')}<div><b>${out.length} lần đo ngoài ngưỡng</b>${out.slice(0, 3).map(o => `${U.dt(o.MeasuredAt)} · ${esc(Store.label('StorageZone', o.StorageZoneId) || Store.label('ProductionBatche', o.ProductionBatchId))}: ${U.num(o.TemperatureC)}°C → ${esc(o.CorrectiveAction || 'Chưa có hành động khắc phục')}`).join('<br>')}</div></div>` : ''}</div>`,
      cols: [{ k: 'MeasuredAt', label: 'Đo lúc' }, { k: 'StorageZoneId', label: 'Khu vực' }, { k: 'ProductionBatchId', label: 'Mẻ SX' }, { k: 'MeasurementType', label: 'Loại đo' }, { k: 'TemperatureC', label: '°C', num: true }, { k: 'IsWithinRange', label: 'Trong ngưỡng' }, { k: 'DeviceCode', label: 'Thiết bị' }, { k: 'CorrectiveAction', label: 'Khắc phục' }],
      rows: () => Store.rows('TemperatureLog').slice().sort((a, b) => b.MeasuredAt.localeCompare(a.MeasuredAt)),
      defaults: { MeasuredAt: U.iso(new Date()), SourceChannel: 'WEB' }
    });
    el.querySelectorAll('[data-z]').forEach(b => b.onclick = () => { ts.zone = b.dataset.z; APP.refresh(); });
  });
  // ---------- Thực hiện checklist ATTP ----------
  const cs = { ck: null, shift: null };
  APP.register('qc-checklist-result', (el, f) => {
    const cks = Store.rows('FoodSafetyChecklist').filter(c => c.Code !== 'VS-KHAY');
    if (!cs.ck) cs.ck = cks[0].Id; const ck = Store.get('FoodSafetyChecklist', cs.ck);
    const shifts = Store.rows('Shift'); if (!cs.shift || !Store.get('Shift', cs.shift)) cs.shift = (shifts[1] || shifts[0] || {}).Id;
    if (!cs.shift) return APP.genericList(el, f);
    const items = Store.by('FoodSafetyChecklistItem', 'ChecklistId', ck.Id).sort((a, b) => a.SortOrder - b.SortOrder);
    const today = Store.rows('FoodSafetyChecklistResult').filter(r => r.ChecklistId === ck.Id && U.day(r.CheckDate) === Store.ctx.date && r.ShiftId === cs.shift);
    const days = Array.from({ length: 7 }, (_, i) => U.addDays(Store.ctx.date, i - 6));
    APP.genericList(el, f, { noAdd: true,
      top: `<div class="grid g-main mb"><div class="card panel"><div class="spread mb"><div><div class="panel-title">Thực hiện checklist: ${esc(ck.Name)}</div><div class="panel-desc">${esc(ck.Code)} · V${ck.VersionNo} · tần suất ${esc(ck.FrequencyCode)} · ngày ${U.date(Store.ctx.date)}</div></div><div class="row-flex">${cks.map(c => `<button class="chip ${c.Id === ck.Id ? 'active' : ''}" data-ck="${c.Id}">${esc(c.Code)}</button>`).join('')}</div></div>
        <div class="row-flex mb">${shifts.map(s => `<button class="chip ${s.Id === cs.shift ? 'active' : ''}" data-sh="${s.Id}">${esc(s.Code)}</button>`).join('')}</div>
        <table class="dt"><thead><tr><th>Tiêu chí<small>ItemName</small></th><th>Chuẩn<small>ExpectedValue</small></th><th>Giá trị đo<small>ActualValue</small></th><th>Kết quả<small>ResultCode</small></th><th>Khắc phục<small>CorrectiveAction</small></th></tr></thead><tbody>${items.map(it => { const r = today.find(x => x.ChecklistItemId === it.Id); return `<tr><td>${esc(it.ItemName)} ${it.IsCritical ? '<span class="tag red">Trọng yếu</span>' : ''}</td><td>${esc(it.ExpectedValue || '')}</td><td><input class="input" style="width:90px" data-v="${it.Id}" value="${esc(r ? r.ActualValue : '')}"></td><td><select class="select" data-rc="${it.Id}"><option value="">—</option><option ${r && r.ResultCode === 'PASS' ? 'selected' : ''}>PASS</option><option ${r && r.ResultCode === 'FAIL' ? 'selected' : ''}>FAIL</option></select></td><td><input class="input" data-ca="${it.Id}" value="${esc(r ? r.CorrectiveAction || '' : '')}" placeholder="Bắt buộc nếu FAIL"></td></tr>`; }).join('')}</tbody></table>
        <div class="spread mt"><span class="small muted">${today.length ? 'Đã ghi ' + today.length + '/' + items.length + ' tiêu chí cho ca này' : 'Chưa thực hiện cho ca này'}</span><button class="btn primary" id="ckSave">${I('ok')} Ghi kết quả checklist</button></div></div>
        <div class="card panel"><div class="panel-title mb">Tỷ lệ đạt 7 ngày</div>${UI.bars(days.map(d => U.date(d).slice(0, 5)), [{ name: 'Đạt %', values: days.map(d => { const rr = Store.rows('FoodSafetyChecklistResult').filter(r => r.ChecklistId === ck.Id && U.day(r.CheckDate) === d); return rr.length ? Math.round(rr.filter(r => r.ResultCode === 'PASS').length / rr.length * 100) : 0; }), color: '#91AF42' }], { h: 190 })}</div></div>`,
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
          .then(r => { if (!r.ok) return; UI.toast(crit0.length ? `Có ${crit0.length} tiêu chí TRỌNG YẾU không đạt – mẻ liên quan chuyển chờ QC` : 'Đã ghi kết quả checklist ATTP', crit0.length ? 'warn' : 'ok'); APP.refresh(); });
      }
      vals.forEach(x => { const ex = today.find(r => r.ChecklistItemId === x.it.Id); const data = { ResultCode: x.r, ActualValue: x.v, CorrectiveAction: x.ca || null, CheckedBy: Store.user.id, CheckedAt: U.iso(new Date()) }; if (ex) Store.update('FoodSafetyChecklistResult', ex.Id, data); else Store.add('FoodSafetyChecklistResult', Object.assign({ ChecklistId: ck.Id, ChecklistItemId: x.it.Id, CheckDate: Store.ctx.date, ShiftId: cs.shift, WorkstationId: (Store.rows('KitchenWorkstation')[2] || {}).Id }, data)); });
      const crit = vals.filter(x => x.r === 'FAIL' && x.it.IsCritical);
      UI.toast(crit.length ? `Có ${crit.length} tiêu chí TRỌNG YẾU không đạt – mẻ liên quan chuyển chờ QC` : 'Đã ghi kết quả checklist ATTP', crit.length ? 'warn' : 'ok'); APP.refresh();
    };
  });
})();
