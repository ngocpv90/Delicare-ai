/* UI-6.25 (D-106): Mẻ nấu – 1 chạm mỗi mẻ ("Xong mẻ"), chế độ Dây chuyền / Bếp trưởng (mô phỏng docs/frontend-handoff/assets/production-batch-v2-mockup.html).
 * Một ngày (◀ ngày ▶) · 4 thẻ ca (giờ xuất bếp, x/y mẻ xong, trễ, tiến độ) · công tắc Dây chuyền / Bếp trưởng (nhớ theo máy).
 * Dây chuyền: ĐANG NẤU (Xong mẻ · Ghi hao hụt) hoặc Bắt đầu nấu mẻ kế tiếp; Mẻ chờ QC – cần bổ sung; Tiếp theo · Đã xong trong ca.
 * Bếp trưởng: 4 chỉ số · Cần làm ngay (Bắt đầu cả n mẻ) · tab Theo giờ (thanh mẻ theo dây chuyền, vạch Bây giờ / Hạn xong nấu) · Sản lượng & hao hụt ca.
 * API: ProductionBatch/GetBoard · Complete (một giao dịch) · StartMany · Start · RecordWaste · RecordSampleRetained; QA dùng APP.prodBatch.act (Đạt / Tạm giữ / Loại).
 * Tải SAU production.js, TRƯỚC prod-menu.js (prod-menu bọc thêm tab Sản lượng). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const LS = { get: k => { try { return localStorage.getItem(k); } catch (e) { return null; } }, set: (k, v) => { try { localStorage.setItem(k, v); } catch (e) { /* máy chặn lưu */ } } };
  const st = { date: null, shift: null, mode: LS.get('dc.batchMode') || 'line', line: LS.get('dc.batchLine') || '', tab: 'time', data: null };
  const WASTE = [['SPOILAGE', 'Hư hỏng'], ['PREP_LOSS', 'Hao hụt sơ chế'], ['PACKING_DAMAGE', 'Hỏng khi đóng gói']];
  const hm = v => (v ? U.time(v) : '');
  const ms = v => (v ? new Date(String(v).replace(' ', 'T')).getTime() : NaN);
  const canEdit = () => Store.perm('prod-batch').edit !== false;
  const canQa = () => Store.canApprove('prod-batch');
  const CLS = { Planned: 'wait', Cooking: 'cook', QCPending: 'qc', Passed: 'ok', Packed: 'ok', Failed: 'bad', Cancelled: 'off' };
  const LBL = { Planned: 'Chờ nấu', Cooking: 'Đang nấu', QCPending: 'Chờ QC', Passed: 'Đạt', Packed: 'Đã đóng gói', Failed: 'Không đạt', Cancelled: 'Hủy' };
  const late = b => b.lateStart || b.lateFinish || b.qcWaitLong;
  const lineKey = b => b.productionLineId || '';

  function load(el, f) {
    return Store.query('ProductionBatch/GetBoard', { ProductionDate: st.date }).then(x => {
      if (!el.isConnected) return;
      st.data = x.ok ? x.data : null;
      if (!st.data) { el.querySelector('#bbBody').innerHTML = `<div class="muted small">${esc((x.res && x.res.responseMessage) || 'Không tải được bảng mẻ')}</div>`; return; }
      const sh = st.data.listShift || [];
      if (!sh.some(s => s.shiftId === st.shift)) st.shift = (sh.find(s => s.batchDone < s.batchTotal) || sh[0] || {}).shiftId || null;
      const lines = st.data.listLine || [];
      if (!lines.some(l => (l.id || '') === st.line)) st.line = lines.length ? lines[0].id || '' : '';
      draw(el, f);
    });
  }
  const inShift = () => (st.data.listBatch || []).filter(b => !st.shift || b.shiftId === st.shift);

  function shiftCards() {
    return `<div class="bb-shifts">${(st.data.listShift || []).map(s => `<button class="bb-shift ${s.shiftId === st.shift ? 'on' : ''}" data-shift="${esc(s.shiftId)}">
      <b>${esc(s.shiftName || s.shiftCode || '')}</b><span class="small muted">Xuất bếp ${esc(hm(s.dispatchAt)) || '–'}</span>
      <span class="small">${s.batchDone}/${s.batchTotal} mẻ xong${s.lateCount ? ` · <b class="bad">${s.lateCount} trễ</b>` : ''}</span>
      <span class="bb-bar"><i style="width:${s.batchTotal ? Math.round(100 * s.batchDone / s.batchTotal) : 0}%"></i></span></button>`).join('') || '<div class="muted small">Ngày này chưa có mẻ nấu (kế hoạch chưa ra lệnh).</div>'}</div>`;
  }

  // ---------- Chế độ Dây chuyền
  function lineMode() {
    const lines = st.data.listLine || [], all = inShift().filter(b => lineKey(b) === st.line);
    const cooking = all.filter(b => b.status === 'Cooking'), next = all.filter(b => b.status === 'Planned').sort((a, b) => ms(a.plannedStart) - ms(b.plannedStart));
    const qc = all.filter(b => b.status === 'QCPending'), done = all.filter(b => ['Passed', 'Packed', 'Failed'].includes(b.status));
    const sample = b => (b.sampleRetained ? `<span class="tag green">Đã lưu mẫu ${esc(hm(b.sampleRetainedAt))}</span>` : '<span class="tag orange">Món này trong ca chưa lưu mẫu</span>');
    const edit = canEdit();
    const cur = cooking.map(b => `<div class="bb-now"><div><span class="small muted">ĐANG NẤU · ${esc(b.batchNumber || '')}${b.isExtra ? ' · nấu thêm' : ''}</span><h3>${esc(b.dishName || '')}</h3>
        <div class="small">${U.num(b.plannedQuantity)} suất · ${esc(hm(b.plannedStart))} – ${esc(hm(b.plannedEnd))}${b.lateFinish ? ' · <b class="bad">quá giờ xong</b>' : ''} · ${sample(b)}</div></div>
        ${edit ? `<div class="bb-acts"><button class="btn primary bb-big" data-done="${esc(b.id)}">${I('check')} Xong mẻ</button><button class="btn" data-waste="${esc(b.id)}">Ghi hao hụt</button></div>` : ''}</div>`).join('');
    const start = !cooking.length && next.length ? `<div class="bb-now idle"><div><span class="small muted">MẺ KẾ TIẾP · ${esc(next[0].batchNumber || '')}</span><h3>${esc(next[0].dishName || '')}</h3><div class="small">${U.num(next[0].plannedQuantity)} suất · bắt đầu ${esc(hm(next[0].plannedStart))}${next[0].lateStart ? ' · <b class="bad">đã trễ</b>' : ''}</div></div>
        ${edit ? `<button class="btn primary bb-big" data-start="${esc(next[0].id)}">${I('flow')} Bắt đầu nấu</button>` : ''}</div>` : '';
    const qcBox = qc.length ? `<div class="card panel mb"><div class="panel-title mb">Mẻ chờ QC – cần bổ sung</div>${qc.map(b => `<div class="bb-qc"><div><b>${esc(b.dishName || '')}</b> <span class="small muted">${esc(b.batchNumber || '')}${b.qcWaitLong ? ' · <b class="bad">chờ quá ' + st.data.batchQcWaitWarnMinutes + ' phút</b>' : ''}</span>
        <div class="small bad">${(b.listAutoPassMissing || []).map(esc).join(' · ') || 'Chờ QA xem'}</div></div><div class="bb-acts">${edit && !b.sampleRetained ? `<button class="btn xs" data-sample="${esc(b.id)}">Đã lưu mẫu</button>` : ''}
        ${canQa() ? `<button class="btn xs primary" data-qa="pass:${esc(b.id)}">Đạt</button><button class="btn xs" data-qa="hold:${esc(b.id)}">Tạm giữ</button><button class="btn xs danger" data-qa="reject:${esc(b.id)}">Loại</button>` : ''}<a class="btn xs" href="#/prod-batch-detail?id=${esc(b.id)}">Mở mẻ</a></div></div>`).join('')}</div>` : '';
    const row = b => `<tr class="${late(b) ? 'bb-late' : ''}"><td>${esc(hm(b.plannedStart))}</td><td><b>${esc(b.dishName || '')}</b><br><span class="small muted">${esc(b.batchNumber || '')}</span></td><td class="num">${U.num(b.plannedQuantity)}</td><td>${b.lateStart ? '<span class="tag red">Trễ</span>' : ''}</td></tr>`;
    const doneRow = b => `<tr><td><b>${esc(b.dishName || '')}</b><br><span class="small muted">${esc(b.batchNumber || '')}</span></td><td class="num">${U.num(b.actualQuantity ?? b.plannedQuantity)}</td><td><span class="tag ${CLS[b.status] === 'bad' ? 'red' : 'green'}">${LBL[b.status] || ''}</span></td><td class="num">${b.lastCoreTemperatureC != null ? U.num(b.lastCoreTemperatureC, 1) + '°C' : ''}</td></tr>`;
    return `<div class="row-flex mb bb-lines">${lines.map(l => `<button class="chip ${(l.id || '') === st.line ? 'active' : ''}" data-line="${esc(l.id || '')}">${esc(l.name || '')}</button>`).join('')}</div>
      ${cur || start || '<div class="notice ok mb">' + I('check') + '<div>Dây chuyền này không còn mẻ nào trong ca.</div></div>'}${qcBox}
      <div class="grid g2"><div class="card panel"><div class="panel-title mb">Tiếp theo</div>${next.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Giờ</th><th>Món</th><th class="num">Suất</th><th></th></tr></thead><tbody>${next.map(row).join('')}</tbody></table></div>` : '<div class="muted small">Không còn mẻ chờ nấu</div>'}</div>
      <div class="card panel"><div class="panel-title mb">Đã xong trong ca</div>${done.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Món</th><th class="num">Suất</th><th>Kết quả</th><th class="num">Lõi</th></tr></thead><tbody>${done.map(doneRow).join('')}</tbody></table></div>` : '<div class="muted small">Chưa có mẻ xong</div>'}</div></div>`;
  }

  // ---------- Chế độ Bếp trưởng
  function chefMode() {
    const all = inShift(), now = ms(st.data.now);
    const cooking = all.filter(b => b.status === 'Cooking').length, auto = all.filter(b => ['Passed', 'Packed'].includes(b.status)).length, qa = all.filter(b => b.status === 'QCPending').length;
    const todo = all.filter(late).sort((a, b) => ms(a.plannedStart) - ms(b.plannedStart));
    const startable = todo.filter(b => b.lateStart);
    const kpis = UI.kpis([UI.kpi('Mẻ của ca', U.num(all.length), '', 'chef', 'blue'), UI.kpi('Đang nấu', U.num(cooking), '', 'flow', 'orange'),
      UI.kpi('Tự Đạt QC / cần QA xem', `${U.num(auto)} / ${U.num(qa)}`, '', 'check', qa ? 'orange' : 'green'), UI.kpi('Trễ / cần xử lý', U.num(todo.length), '', 'alert', todo.length ? 'red' : 'green')], 'compact');
    const why = b => (b.lateStart ? 'Đến giờ chưa bắt đầu' : b.lateFinish ? 'Quá giờ xong' : 'Chờ QC quá ' + st.data.batchQcWaitWarnMinutes + ' phút');
    const todoBox = `<div class="card panel mb"><div class="spread mb"><div class="panel-title">Cần làm ngay</div>${startable.length > 1 && canEdit() ? `<button class="btn sm primary" id="bbStartAll">Bắt đầu cả ${startable.length} mẻ</button>` : ''}</div>
      ${todo.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Mẻ</th><th>Dây chuyền</th><th>Giờ</th><th>Việc</th><th></th></tr></thead><tbody>${todo.map(b => `<tr class="bb-late"><td><b>${esc(b.dishName || '')}</b><br><span class="small muted">${esc(b.batchNumber || '')}</span></td><td>${esc(b.productionLineName || 'Chưa gán')}</td><td>${esc(hm(b.plannedStart))} – ${esc(hm(b.plannedEnd))}</td><td>${why(b)}</td>
        <td class="actions">${b.lateStart && canEdit() ? `<button class="btn xs primary" data-start="${esc(b.id)}">Bắt đầu</button>` : b.lateFinish && canEdit() ? `<button class="btn xs primary" data-done="${esc(b.id)}">Xong mẻ</button>` : `<a class="btn xs" href="#/prod-batch-detail?id=${esc(b.id)}">Mở mẻ</a>`}</td></tr>`).join('')}</tbody></table></div>` : '<div class="muted small">Không có mẻ trễ – mọi việc đúng giờ.</div>'}</div>`;
    const tabs = `<div class="tabs mb"><button class="tab ${st.tab === 'time' ? 'active' : ''}" data-bbt="time">Theo giờ</button><button class="tab ${st.tab === 'out' ? 'active' : ''}" data-bbt="out">Sản lượng & hao hụt ca</button></div>`;
    return kpis + todoBox + `<div class="card panel">${tabs}${st.tab === 'time' ? timeline(all, now) : outputTab(all)}</div>`;
  }

  function timeline(all, now) {
    if (!all.length) return '<div class="muted small">Ca này chưa có mẻ</div>';
    const shift = (st.data.listShift || []).find(s => s.shiftId === st.shift) || {};
    // Thanh theo giờ KẾ HOẠCH (giờ thực tế có thể lệch xa – vd dữ liệu thử); nhãn giờ tối đa ~12 vạch.
    const d0 = ms(st.date + 'T00:00') - 6 * 3600000, d1 = d0 + 36 * 3600000, inDay = t => t >= d0 && t <= d1;
    const starts = all.map(b => ms(b.plannedStart)).filter(inDay), ends = all.map(b => ms(b.plannedEnd || b.plannedStart)).concat([ms(shift.cookDeadline)]).filter(t => !isNaN(t) && inDay(t));
    const t0 = starts.length ? Math.min(...starts) : d0 + 6 * 3600000, t1 = Math.max(t0 + 3600000, ...ends);
    const clip = t => Math.min(Math.max(t, t0), t1);
    const span = Math.max(t1 - t0, 3600000), pos = t => ((t - t0) / span * 100).toFixed(2) + '%';
    const lines = [...new Set(all.map(lineKey))].map(k => ({ k, name: (all.find(b => lineKey(b) === k) || {}).productionLineName || 'Chưa gán dây chuyền' }));
    const stepH = Math.max(1, Math.ceil(span / 3600000 / 12)) * 3600000, hours = []; for (let t = Math.ceil(t0 / 3600000) * 3600000; t <= t1; t += stepH) hours.push(t);
    const rowHtml = l => {
      const bs = all.filter(b => lineKey(b) === l.k).sort((a, b) => ms(a.plannedStart) - ms(b.plannedStart));
      const lanes = []; bs.forEach(b => { const s = ms(b.plannedStart), e = ms(b.plannedEnd || b.plannedStart); let i = lanes.findIndex(x => x <= s); if (i < 0) { i = lanes.length; lanes.push(0); } lanes[i] = e; b._lane = i; });
      const h = Math.max(1, lanes.length) * 30 + 6;
      return `<div class="bb-tl-row"><div class="bb-tl-name">${esc(l.name)}</div><div class="bb-tl-track" style="height:${h}px">${bs.map(b => { const s = clip(ms(b.plannedStart)), e = Math.max(clip(ms(b.plannedEnd || b.plannedStart)), s + 300000);
        return `<a class="bb-tl-bar ${CLS[b.status] || 'wait'} ${late(b) ? 'late' : ''} ${b.isExtra ? 'extra' : ''}" style="left:${pos(s)};width:calc(${pos(e)} - ${pos(s)});top:${3 + b._lane * 30}px" href="#/prod-batch-detail?id=${esc(b.id)}" title="${esc(`${b.dishName} · ${b.batchNumber} · ${hm(b.plannedStart)}–${hm(b.plannedEnd)} · ${LBL[b.status] || ''}`)}">${esc(b.dishName || '')}</a>`; }).join('')}</div></div>`;
    };
    const mark = (t, cls, label) => (t >= t0 && t <= t1 ? `<div class="bb-tl-mark ${cls}" style="left:calc(180px + (100% - 180px) * ${((t - t0) / span).toFixed(4)})"><span>${label}</span></div>` : '');
    return `<div class="fw-legend"><span><span class="bb-sw wait"></span>Chờ nấu</span><span><span class="bb-sw cook"></span>Đang nấu</span><span><span class="bb-sw qc"></span>Chờ QC</span><span><span class="bb-sw ok"></span>Đạt / đóng gói</span><span><span class="bb-sw bad"></span>Không đạt</span><span>Viền đỏ: trễ · viền đứt: nấu thêm</span></div>
      <div class="table-wrap"><div class="bb-tl"><div class="bb-tl-row bb-tl-head"><div class="bb-tl-name"></div><div class="bb-tl-track">${hours.map(t => `<span class="bb-tl-hour" style="left:${pos(t)}">${String(new Date(t).getHours()).padStart(2, "0")}:00</span>`).join('')}</div></div>
      ${lines.map(rowHtml).join('')}${mark(now, 'now', 'Bây giờ')}${mark(ms(shift.cookDeadline), 'dl', 'Hạn xong nấu')}</div></div>`;
  }

  function outputTab(all) {
    const by = U.group(all, 'dishId');
    const rows = Object.values(by).map(bs => {
      const doneB = bs.filter(b => ['Passed', 'Packed', 'Failed'].includes(b.status));
      const plan = U.sum(bs, 'plannedQuantity'), made = U.sum(doneB, b => b.actualQuantity ?? 0), ok = U.sum(bs.filter(b => ['Passed', 'Packed'].includes(b.status)), b => b.acceptedQuantity ?? b.actualQuantity ?? 0);
      const rej = U.sum(bs.filter(b => b.status === 'Failed'), b => b.actualQuantity ?? b.plannedQuantity), waste = U.sum(bs, b => b.wasteMeals ?? 0);
      return { dish: bs[0].dishName, done: doneB.length, total: bs.length, plan, made, ok, rej, waste };
    });
    const tot = k => U.sum(rows, k), pct = r => (r.made ? U.pct(100 * (r.rej + r.waste) / r.made) : '–');
    return rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Món</th><th class="num">Mẻ xong</th><th class="num">Kế hoạch</th><th class="num">Nấu được</th><th class="num">Đạt</th><th class="num">Loại QC</th><th class="num">Hao hụt khác</th><th class="num">% hao hụt</th></tr></thead><tbody>
      ${rows.map(r => `<tr><td><b>${esc(r.dish || '')}</b></td><td class="num">${r.done}/${r.total}</td><td class="num">${U.num(r.plan)}</td><td class="num">${U.num(r.made)}</td><td class="num">${U.num(r.ok)}</td><td class="num">${U.num(r.rej)}</td><td class="num">${U.num(r.waste)}</td><td class="num">${pct(r)}</td></tr>`).join('')}</tbody>
      <tfoot><tr><td>Tổng ca</td><td class="num">${tot('done')}/${tot('total')}</td><td class="num">${U.num(tot('plan'))}</td><td class="num">${U.num(tot('made'))}</td><td class="num">${U.num(tot('ok'))}</td><td class="num">${U.num(tot('rej'))}</td><td class="num">${U.num(tot('waste'))}</td><td class="num">${pct({ made: tot('made'), rej: tot('rej'), waste: tot('waste') })}</td></tr></tfoot></table></div>` : '<div class="muted small">Ca này chưa có mẻ</div>';
  }

  // ---------- Hộp "Xong mẻ"
  function doneBox(el, f, b) {
    const minC = Number(st.data.coreTempMinC || 70);
    const next = inShift().filter(x => x.status === 'Planned' && b.productionLineId && x.productionLineId === b.productionLineId).sort((x, y) => ms(x.plannedStart) - ms(y.plannedStart))[0];
    const keys = ['7', '8', '9', '4', '5', '6', '1', '2', '3', ',', '0', '⌫'];
    UI.modal({ title: 'Xong mẻ ' + esc(b.batchNumber || '') + ' · ' + esc(b.dishName || ''), size: 'md',
      body: `<div class="bb-done"><div><label class="small muted">Nhiệt độ lõi (°C) <span class="req">*</span> – đạt khi ≥ ${U.num(minC)} °C</label><div class="bb-temp"><input class="input" id="bdT" inputmode="decimal" autocomplete="off" placeholder="vd 78"><span id="bdTs" class="tag"></span></div>
          <div class="bb-pad">${keys.map(k => `<button type="button" class="btn" data-k="${k}">${k}</button>`).join('')}</div>
          <div class="field mt hide" id="bdCa"><label>Hành động khắc phục <span class="req">*</span></label><textarea class="input" id="bdCaT" rows="2" placeholder="vd Gia nhiệt lại 10 phút, đo lại"></textarea></div></div>
        <div><div class="field"><label>Số suất nấu được</label><div class="bb-qty"><button type="button" class="btn" data-q="-1">−</button><input class="input" id="bdQ" type="number" min="1" value="${esc(b.plannedQuantity)}"><button type="button" class="btn" data-q="1">+</button></div><span class="small muted">Kế hoạch ${U.num(b.plannedQuantity)} suất</span></div>
          <div class="field mt"><label>Lưu mẫu món này trong ca</label>${b.sampleRetained ? `<div class="small"><span class="tag green">Đã lưu ở mẻ trước ${esc(hm(b.sampleRetainedAt))}</span></div>` : '<label class="pl-cat"><input type="checkbox" id="bdS"> <span>Đã lưu mẫu (hồ sơ ATTP – chỉ tích khi đã lưu thật)</span></label>'}</div>
          <div class="field mt"><label>Hao hụt trong mẻ (nếu có)</label><div class="row-flex">${WASTE.map(([c, l]) => `<button type="button" class="chip" data-w="${c}">${l}</button>`).join('')}</div><input class="input mt hide" id="bdW" type="number" min="0" placeholder="Số suất hao hụt"></div></div></div>`,
      foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="bdOk">${I('check')} Xong mẻ</button>${next ? `<button class="btn primary" id="bdNext">Xong &amp; bắt đầu ${esc(next.dishName || '')} (${esc(hm(next.plannedStart))})</button>` : ''}`,
      onMount: (m, close) => {
        const t = m.querySelector('#bdT'), ts = m.querySelector('#bdTs'), ca = m.querySelector('#bdCa');
        let waste = null;
        const sync = () => { const v = parseFloat(String(t.value).replace(',', '.')); const ok = !isNaN(v) && v >= minC; ts.textContent = isNaN(v) ? '' : ok ? 'Đạt' : 'Dưới ngưỡng'; ts.className = 'tag ' + (isNaN(v) ? '' : ok ? 'green' : 'red'); ca.classList.toggle('hide', isNaN(v) || ok); };
        t.oninput = sync; setTimeout(() => t.focus(), 50);
        m.querySelectorAll('[data-k]').forEach(k => k.onclick = () => { const c = k.dataset.k; t.value = c === '⌫' ? t.value.slice(0, -1) : t.value + (c === ',' ? '.' : c); sync(); });
        m.querySelectorAll('[data-q]').forEach(k => k.onclick = () => { const q = m.querySelector('#bdQ'); q.value = Math.max(1, (+q.value || 0) + +k.dataset.q); });
        m.querySelectorAll('[data-w]').forEach(k => k.onclick = () => { m.querySelectorAll('[data-w]').forEach(x => x.classList.toggle('active', x === k && waste !== k.dataset.w)); waste = waste === k.dataset.w ? null : k.dataset.w; m.querySelector('#bdW').classList.toggle('hide', !waste); });
        const send = startNext => {
          const v = parseFloat(String(t.value).replace(',', '.'));
          if (isNaN(v)) return UI.toast('Nhập nhiệt độ lõi', 'err');
          const caT = m.querySelector('#bdCaT').value.trim();
          if (v < minC && !caT) return UI.toast('Dưới ngưỡng – nhập hành động khắc phục', 'err');
          const wq = +m.querySelector('#bdW').value || 0, sEl = m.querySelector('#bdS');
          Store.act('ProductionBatch/Complete', { json: { id: b.id, coreTemperatureC: v, correctiveAction: caT || null, retainSample: !!(sEl && sEl.checked), producedQuantity: +m.querySelector('#bdQ').value || null,
            listWaste: waste && wq > 0 ? [{ wasteType: waste, quantityMeals: wq }] : [], startNextBatchId: startNext && next ? next.id : null }, tables: ['ProductionBatche', 'ProductionOrder', 'TemperatureLog', 'ProductionOutput', 'MealWasteRecord'] })
            .then(r => {
              if (!r.ok) return; close();
              const d = r.data || {}, bt = d.batch || {}, w = d.listWarning || [];
              if (bt.status === 'Passed' || bt.status === 'Packed') UI.toast(`Xong mẻ ${esc(bt.batchNumber || '')} – hệ thống tự Đạt QC${w.length ? ' · ' + esc(w.filter(x => !/tự Đạt/.test(x))[0] || '') : ''}`, 'ok');
              else UI.toast(`Xong mẻ – Chờ QC: ${esc((bt.listAutoPassMissing || []).join('; ') || w.join('; ') || 'chờ QA xem')}`, 'warn');
              load(el, f);
            });
        };
        m.querySelector('#bdOk').onclick = () => send(false);
        const nb = m.querySelector('#bdNext'); if (nb) nb.onclick = () => send(true);
      } });
  }
  function wasteBox(el, f, b) {
    UI.modal({ title: 'Ghi hao hụt · ' + esc(b.dishName || ''), size: 'sm',
      body: `<div class="row-flex mb">${WASTE.map(([c, l], i) => `<label class="chip"><input type="radio" name="wt" value="${c}" ${i === 0 ? 'checked' : ''}> ${l}</label>`).join('')}</div>
        <div class="field"><label>Số suất <span class="req">*</span></label><input class="input" id="wq" type="number" min="1"></div><div class="field mt"><label>Lý do</label><input class="input" id="wr" maxlength="300"></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="wOk">Lưu</button>',
      onMount: (m, close) => m.querySelector('#wOk').onclick = () => {
        const q = +m.querySelector('#wq').value; if (!(q > 0)) return UI.toast('Nhập số suất hao hụt', 'err');
        Store.act('ProductionBatch/RecordWaste', { form: { Id: b.id, WasteType: m.querySelector('[name=wt]:checked').value, QuantityMeals: q, Reason: m.querySelector('#wr').value.trim() }, tables: ['MealWasteRecord', 'ProductionBatche'] })
          .then(r => { if (r.ok) { UI.toast('Đã ghi hao hụt', 'ok'); close(); load(el, f); } });
      } });
  }

  function draw(el, f) {
    const body = el.querySelector('#bbBody'); if (!body || !st.data) return;
    body.innerHTML = shiftCards() + `<div class="spread mb"><div class="seg"><button class="seg-b ${st.mode === 'line' ? 'on' : ''}" data-mode="line">Dây chuyền</button><button class="seg-b ${st.mode === 'chef' ? 'on' : ''}" data-mode="chef">Bếp trưởng</button></div></div>`
      + (st.mode === 'chef' ? chefMode() : lineMode());
    const byId = id => (st.data.listBatch || []).find(b => b.id === id);
    body.querySelectorAll('[data-shift]').forEach(b => b.onclick = () => { st.shift = b.dataset.shift; draw(el, f); });
    body.querySelectorAll('[data-mode]').forEach(b => b.onclick = () => { st.mode = b.dataset.mode; LS.set('dc.batchMode', st.mode); draw(el, f); });
    body.querySelectorAll('[data-line]').forEach(b => b.onclick = () => { st.line = b.dataset.line; LS.set('dc.batchLine', st.line); draw(el, f); });
    body.querySelectorAll('[data-bbt]').forEach(b => b.onclick = () => { st.tab = b.dataset.bbt; draw(el, f); });
    body.querySelectorAll('[data-done]').forEach(b => b.onclick = () => doneBox(el, f, byId(b.dataset.done)));
    body.querySelectorAll('[data-waste]').forEach(b => b.onclick = () => wasteBox(el, f, byId(b.dataset.waste)));
    body.querySelectorAll('[data-start]').forEach(b => b.onclick = () => Store.act('ProductionBatch/Start', { form: { Id: b.dataset.start }, tables: ['ProductionBatche', 'ProductionOrder'] }).then(r => { if (r.ok) { UI.toast('Đã bắt đầu nấu', 'ok'); load(el, f); } }));
    body.querySelectorAll('[data-sample]').forEach(b => b.onclick = () => UI.confirm('Xác nhận đã lưu mẫu món này trong ca? (hồ sơ ATTP)', () => Store.act('ProductionBatch/RecordSampleRetained', { form: { Id: b.dataset.sample, ActualValue: 'Đã lưu mẫu' }, tables: ['FoodSafetyChecklistResult'] }).then(r => { if (r.ok) { UI.toast('Đã ghi lưu mẫu', 'ok'); load(el, f); } }), { ok: 'Đã lưu mẫu' }));
    body.querySelectorAll('[data-qa]').forEach(b => b.onclick = () => { const [a, id] = b.dataset.qa.split(':'); if (APP.prodBatch) APP.prodBatch.act(a, id, null); setTimeout(() => load(el, f), 1500); });
    const all = body.querySelector('#bbStartAll');
    if (all) all.onclick = () => { const ids = inShift().filter(b => b.lateStart).map(b => b.id); Store.act('ProductionBatch/StartMany', { json: { listId: ids }, tables: ['ProductionBatche', 'ProductionOrder'] }).then(r => { if (!r.ok) return; UI.toast(esc((r.res && r.res.responseMessage) || 'Đã bắt đầu'), 'ok'); load(el, f); }); };
  }

  const base = APP.SCREENS['prod-batch'];
  APP.register('prod-batch', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (!st.date) st.date = Store.ctx.date;
    if (p && /^\d{4}-\d{2}-\d{2}$/.test(p.date || '')) st.date = p.date;
    const plan = Store.rows('ProductionPlan').find(x => !x.IsDeleted && U.day(x.ProductionDate) === st.date);
    el.innerHTML = APP.header(f, `<a class="btn" href="${plan ? '#/prod-plan-detail?id=' + encodeURIComponent(plan.Id) : '#/prod-plan'}">${I('calendar')} Bảng sản xuất ngày</a>`, { noNote: true })
      + `<div class="small muted mb">Mỗi mẻ một chạm: bấm <b>Xong mẻ</b> → nhập nhiệt độ lõi, lưu mẫu (một lần cho món trong ca), số suất, hao hụt – hệ thống tự Đạt QC khi đủ điều kiện.</div>
      <div class="card panel mb"><div class="fw-week"><button class="btn sm" id="bbPrev" aria-label="Ngày trước">◀</button><span>${esc(U.date(st.date))}${st.date === Store.ctx.date ? ' · hôm nay' : ''}</span><button class="btn sm" id="bbNext" aria-label="Ngày sau">▶</button><input type="date" class="input" id="bbDate" value="${esc(st.date)}" style="max-width:170px"></div></div>
      <div id="bbBody"><div class="muted small">Đang tải mẻ nấu…</div></div>`;
    const go = d => { st.date = d; st.data = null; APP.SCREENS['prod-batch'](el, f, {}); };
    el.querySelector('#bbPrev').onclick = () => go(U.addDays(st.date, -1));
    el.querySelector('#bbNext').onclick = () => go(U.addDays(st.date, 1));
    el.querySelector('#bbDate').onchange = e => { if (e.target.value) go(e.target.value); };
    if (st.data && U.day(st.data.productionDate) === st.date) draw(el, f);
    load(el, f);
  });
})();
