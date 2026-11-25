/* UI-6.35 (D-116): "Theo dõi giao hàng" (log-execution) + "Khay hộp theo khách" (log-packaging-ledger).
 * - Điều phối: ngày ◀ ▶, 4 chỉ số, mỗi dòng một chuyến + dải điểm (chờ / đã đến / đã giao / thất bại / trễ) – DeliveryRoute/GetTrackBoard.
 *   Tab "Sổ giao nhận" (GetPodBook, in phiếu) và "Bản đồ" (vị trí theo sự kiện – DeliveryEvent/GetVehiclePositions).
 * - Tài xế (người không có quyền điều phối, hoặc ?mode=driver): chuyến của tôi (GetMyTrack), "Đã đến" (gửi tọa độ nếu máy cho phép),
 *   "Bàn giao" (số suất + người nhận + thu khay kỳ trước ngay tại điểm – DeliveryStop/GetHandover + Deliver ListReturn), "Giao thất bại" (lý do theo danh mục).
 * - Khay hộp theo khách: còn ở khách / quá hạn thu (GetBalance), phiếu thu hồi (ContainerReturn/GetList – chỉ xem; thu khay ghi khi tài xế bàn giao),
 *   sổ bao bì (chỉ xem; ghi tay chỉ còn "Thất thoát"). Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { day: null, tab: 'board', mode: null, ltab: 'balance' };
  const today = () => U.day(Store.ctx.date || new Date());
  const hhmm = v => String(v || '').slice(11, 16);
  const ref = n => (Store.refList ? Store.refList(n) : []) || [];
  const pkName = c => (ref('PackagingType').find(x => x.code === c) || {}).name || c || '';
  const TABLES = ['DeliveryRoute', 'DeliveryOrder', 'DeliveryStop', 'DeliveryEvent', 'ProofOfDeliverie', 'ContainerReturn', 'PackagingLedger'];
  const dayNav = day => `<div class="row-flex" style="gap:6px"><button class="btn sm" data-dd="-1" aria-label="Ngày trước">◀</button><input type="date" class="input" id="ltDay" value="${day}" style="width:150px"><button class="btn sm" data-dd="1" aria-label="Ngày sau">▶</button>${day === today() ? '' : '<button class="btn sm" data-dd="0">Hôm nay</button>'}</div>`;
  const bindDay = (el, day, re) => { el.querySelectorAll('[data-dd]').forEach(b => b.onclick = () => { const n = +b.dataset.dd; st.day = n === 0 ? null : U.addDays(day, n); re(); }); const d = el.querySelector('#ltDay'); if (d) d.onchange = e => { st.day = e.target.value || null; re(); }; };
  const tabs = (list, cur, attr) => `<div class="seg mb" style="display:inline-flex">${list.map(([k, l]) => `<button class="seg-b ${cur === k ? 'on' : ''}" ${attr}="${k}">${l}</button>`).join('')}</div>`;
  const DOT = { PLANNED: ['#f1f2ef', '#bbb', 'Chờ'], ARRIVED: ['#e8f0fb', '#4C83B6', 'Đã đến'], DELIVERED: ['#eef6e8', '#7DA33C', 'Đã giao'], FAILED: ['#fdecec', '#D95C4F', 'Thất bại'] };
  const dot = s => { const [bg, bd] = DOT[s.status] || DOT.PLANNED; return `<span title="#${s.sequenceNo} ${esc(s.customerName || '')} – ${DOT[s.status] ? DOT[s.status][2] : ''}${s.late ? ' (trễ)' : ''}" style="display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:26px;border-radius:13px;background:${bg};border:2px solid ${s.late && s.status !== 'DELIVERED' ? '#E8892B' : bd};font-size:11px;margin:2px">${s.sequenceNo}</span>`; };
  const stopTag = s => UI.tag((DOT[s.status] || DOT.PLANNED)[2] + (s.late && s.status !== 'DELIVERED' ? ' · trễ' : ''), { DELIVERED: 'green', FAILED: 'red', ARRIVED: 'blue' }[s.status] || (s.late ? 'orange' : ''));

  // ================= Theo dõi giao hàng =================
  const baseExec = APP.SCREENS['log-execution'];
  APP.register('log-execution', (el, f, p) => {
    if (!Store.isLive) return baseExec(el, f, p);
    if (p && p.date) st.day = p.date;
    if (p && ['board', 'pod', 'map'].includes(p.tab)) st.tab = p.tab;
    if (p && p.mode) st.mode = p.mode === 'driver' ? 'driver' : 'board';
    const re = () => APP.SCREENS['log-execution'](el, f, {});
    const dispatcher = Store.perm('log-route').view !== false;
    const mode = st.mode || (dispatcher ? 'board' : 'driver');
    const day = st.day || today();
    const modeSeg = dispatcher ? tabs([['board', 'Điều phối viên'], ['driver', 'Chuyến của tôi (tài xế)']], mode, 'data-md') : '';
    if (mode === 'driver') return driver(el, f, day, re, modeSeg);
    el.innerHTML = APP.header(f, '') + `<div class="small muted mb">Mỗi dòng một chuyến; dải điểm theo thứ tự giao. Điểm trễ (quá ${'giờ hẹn'}) viền cam; giao thất bại hiện ở cột "Cần giao lại" của Điều phối giao hàng.</div>
      <div class="row-flex mb" style="gap:10px;flex-wrap:wrap">${modeSeg}${tabs([['board', 'Chuyến hôm nay'], ['pod', 'Sổ giao nhận'], ['map', 'Bản đồ']], st.tab, 'data-tb')}${dayNav(day)}</div><div id="ltBody"><div class="card panel small muted">Đang tải…</div></div>`;
    el.querySelectorAll('[data-md]').forEach(b => b.onclick = () => { st.mode = b.dataset.md; re(); });
    el.querySelectorAll('[data-tb]').forEach(b => b.onclick = () => { st.tab = b.dataset.tb; re(); });
    bindDay(el, day, re);
    const body = el.querySelector('#ltBody');
    if (st.tab === 'pod') return podBook(body, day);
    if (st.tab === 'map') return mapTab(body, day);
    Store.query('DeliveryRoute/GetTrackBoard', { date: day }).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được bảng theo dõi')}</div>`; return; }
      const b = x.data || {}, routes = b.listRoute || [];
      body.innerHTML = `<div class="kpis">${UI.kpi('Chuyến đang chạy', U.num(b.routesInTransit || 0), `${U.num(routes.length)} chuyến trong ngày`, 'truck', 'blue')}${UI.kpi('Điểm đã giao', `${U.num(b.pointsDelivered || 0)} / ${U.num(b.pointsTotal || 0)}`, '', 'check', 'green')}${UI.kpi('Điểm trễ', U.num(b.pointsLate || 0), 'quá giờ hẹn, chưa giao', 'clock', 'orange')}${UI.kpi('Giao thất bại', U.num(b.pointsFailed || 0), 'cần gán chuyến khác', 'alert', 'red')}</div>
        <div class="card panel">${routes.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Chuyến</th><th>Xe · tài xế</th><th>Xuất bến</th><th>Điểm giao</th><th>Vị trí gần nhất</th><th></th></tr></thead><tbody>
          ${routes.map(r => `<tr><td><b>${esc(r.routeNo || '')}</b><br>${UI.tag(r.statusName || r.status || '', r.status === 'InTransit' ? 'blue' : r.status === 'Completed' ? 'green' : '')}</td><td>${esc(r.vehicleName || 'Chưa có xe')}<br><span class="small muted">${esc(r.driverName || 'Chưa có tài xế')}</span></td>
            <td>${r.actualStart ? hhmm(r.actualStart) : `<span class="muted">dự kiến ${hhmm(r.plannedStart) || '–'}</span>`}${r.dispatchTemperature != null ? `<br><span class="small muted">${U.num(r.dispatchTemperature, 1)} °C</span>` : ''}</td>
            <td>${(r.listStop || []).map(dot).join('') || '<span class="small muted">Chưa có điểm</span>'}<div class="small muted">${U.num((r.listStop || []).filter(s => s.status === 'DELIVERED').length)}/${U.num((r.listStop || []).length)} đã giao</div></td>
            <td class="small">${esc(r.lastPosition || '–')}${r.lastPositionAt ? `<br><span class="muted">${hhmm(r.lastPositionAt)}</span>` : ''}</td><td><button class="btn xs" data-rt="${r.id}">Chi tiết</button></td></tr>`).join('')}
        </tbody></table></div><div class="row-flex small muted mt" style="gap:12px">${Object.values(DOT).map(([bg, bd, l]) => `<span><span style="display:inline-block;width:12px;height:12px;border-radius:6px;background:${bg};border:2px solid ${bd}"></span> ${l}</span>`).join('')}<span><span style="display:inline-block;width:12px;height:12px;border-radius:6px;border:2px solid #E8892B"></span> Trễ</span></div>` : '<div class="empty">Chưa có chuyến giao cho ngày này.</div>'}</div>`;
      body.querySelectorAll('[data-rt]').forEach(bt => bt.onclick = () => {
        const r = routes.find(z => z.id === bt.dataset.rt);
        UI.drawer({ title: 'Chuyến ' + esc(r.routeNo || ''), sub: esc((r.vehicleName || '') + ' · ' + (r.driverName || '')), width: 720,
          tabs: [{ label: 'Điểm giao', html: `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>#</th><th>Điểm giao</th><th>Hẹn</th><th>Đến</th><th class="num">Suất</th><th class="num">Nhận</th><th class="num">Khay thu</th><th>Trạng thái</th></tr></thead><tbody>${(r.listStop || []).map(s => `<tr><td>${s.sequenceNo}</td><td><b>${esc(s.customerName || '')}</b>${s.locationName ? '<br><span class="small muted">' + esc(s.locationName) + '</span>' : ''}</td><td>${hhmm(s.plannedArrival)}${s.windowEnd ? '–' + hhmm(s.windowEnd) : ''}</td><td>${hhmm(s.actualArrival) || '–'}</td><td class="num">${U.num(s.plannedMeals)}</td><td class="num">${s.accepted == null ? '–' : U.num(s.accepted)}</td><td class="num">${U.num(s.traysReturned || 0)}</td><td>${stopTag(s)}${s.failReason ? `<div class="small bad">${esc(s.failReason)}</div>` : ''}</td></tr>`).join('')}</tbody></table></div>` }] });
      });
    });
  });

  function podBook(body, day) {
    Store.query('DeliveryRoute/GetPodBook', { from: day, to: day }).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được sổ giao nhận')}</div>`; return; }
      const rows = x.data || [];
      body.innerHTML = `<div class="card panel"><div class="panel-head"><div><div class="panel-title">Sổ giao nhận ${U.date(day)}</div><div class="panel-desc">Ghi khi tài xế bấm "Bàn giao" tại điểm – chỉ xem, in phiếu giao nhận.</div></div></div>
        ${rows.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Lúc nhận</th><th>Chuyến</th><th>Đơn giao</th><th>Khách · điểm</th><th class="num">Giao</th><th class="num">Nhận</th><th class="num">Trả lại</th><th>Người nhận</th><th class="num">°C</th><th></th></tr></thead><tbody>${rows.map(r => `<tr><td>${hhmm(r.receivedAt)}</td><td>${esc(r.routeNo || '')}</td><td>${esc(r.deliveryNumber || '')}</td><td><b>${esc(r.customerName || '')}</b>${r.locationName ? '<br><span class="small muted">' + esc(r.locationName) + '</span>' : ''}</td><td class="num">${U.num(r.delivered)}</td><td class="num">${U.num(r.accepted)}</td><td class="num ${r.rejected > 0 ? 'bad' : ''}">${U.num(r.rejected)}</td><td>${esc(r.receiverName || '')}</td><td class="num">${r.temperature == null ? '–' : U.num(r.temperature, 1)}</td><td><button class="btn xs" data-pp="${r.id}">${I('print')} In</button></td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Chưa có xác nhận giao trong ngày.</div>'}</div>`;
      body.querySelectorAll('[data-pp]').forEach(b => b.onclick = () => Store.download('ProofOfDelivery/Print', { id: b.dataset.pp }));
    });
  }
  function mapTab(body, day) {
    Store.query('DeliveryEvent/GetVehiclePositions', { DeliveryDate: day }).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được vị trí xe')}</div>`; return; }
      const latest = (x.data && x.data.listLatest) || [];
      body.innerHTML = `<div class="card panel"><div class="panel-head"><div><div class="panel-title">Vị trí xe</div><div class="panel-desc">Theo sự kiện có tọa độ (đến điểm, giao thất bại, tài xế gửi vị trí) – không phải định vị liên tục.</div></div></div>
        ${latest.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Chuyến</th><th>Xe</th><th>Tài xế</th><th>Sự kiện gần nhất</th><th>Lúc</th><th>Tọa độ</th></tr></thead><tbody>${latest.map(e => `<tr><td><b>${esc(e.routeNumber || '')}</b></td><td>${esc(e.vehiclePlate || '')}</td><td>${esc(e.driverName || '')}</td><td>${esc(e.eventTypeName || e.eventType || '')}${e.stopSequenceNo ? ' · điểm #' + e.stopSequenceNo : ''}</td><td>${hhmm(e.eventAt)}</td><td><a target="_blank" rel="noopener" href="https://www.google.com/maps?q=${e.latitude},${e.longitude}">${U.num(e.latitude, 5)}, ${U.num(e.longitude, 5)}</a></td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Chưa có chuyến nào ghi tọa độ trong ngày.</div>'}</div>`;
    });
  }

  // ---------- Chế độ tài xế ----------
  const geo = () => new Promise(res => { if (!navigator.geolocation) return res({}); navigator.geolocation.getCurrentPosition(p => res({ Latitude: p.coords.latitude.toFixed(6), Longitude: p.coords.longitude.toFixed(6) }), () => res({}), { timeout: 6000, maximumAge: 60000 }); });
  function driver(el, f, day, re, modeSeg) {
    el.innerHTML = APP.header(f, '') + `<div class="row-flex mb" style="gap:10px;flex-wrap:wrap">${modeSeg}${dayNav(day)}</div><div id="ltBody" style="max-width:640px"><div class="card panel small muted">Đang tải…</div></div>`;
    el.querySelectorAll('[data-md]').forEach(b => b.onclick = () => { st.mode = b.dataset.md; re(); });
    bindDay(el, day, re);
    const body = el.querySelector('#ltBody');
    Store.query('DeliveryRoute/GetMyTrack', { date: day }).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được chuyến của bạn')}</div>`; return; }
      const routes = (x.data && x.data.listRoute) || [];
      if (!routes.length) { body.innerHTML = '<div class="card panel empty">Ngày này bạn không có chuyến giao nào.</div>'; return; }
      body.innerHTML = routes.map(r => `<div class="card panel mb"><div class="spread"><b style="font-size:16px">${esc(r.routeNo || '')}</b>${UI.tag(r.statusName || r.status || '', r.status === 'InTransit' ? 'blue' : '')}</div><div class="small muted mb">${esc(r.vehicleName || '')}${r.actualStart ? ' · xuất bến ' + hhmm(r.actualStart) : r.plannedStart ? ' · dự kiến ' + hhmm(r.plannedStart) : ''}</div>
        ${r.status !== 'InTransit' && r.status !== 'Completed' ? '<div class="notice">' + I('info') + '<div>Chuyến chưa xuất bến – điều phối bấm "Xuất bến" trước khi giao.</div></div>' : ''}
        ${(r.listStop || []).map(s => `<div style="border:1px solid #e5e7eb;border-left:4px solid ${(DOT[s.status] || DOT.PLANNED)[1]};border-radius:10px;padding:10px;margin-top:8px"><div class="spread"><b>#${s.sequenceNo} ${esc(s.customerName || '')}</b>${stopTag(s)}</div>
          <div class="small muted">${esc(s.locationName || '')}${s.address ? ' · ' + esc(s.address) : ''}</div><div class="small">Hẹn ${hhmm(s.plannedArrival) || '–'}${s.windowEnd ? '–' + hhmm(s.windowEnd) : ''} · ${U.num(s.plannedMeals)} suất${s.accepted != null ? ' · khách nhận ' + U.num(s.accepted) : ''}${s.traysReturned ? ' · thu ' + U.num(s.traysReturned) + ' khay' : ''}</div>
          ${s.failReason ? `<div class="small bad">${esc(s.failReason)}</div>` : ''}
          ${r.status === 'InTransit' && ['PLANNED', 'ARRIVED'].includes(s.status) ? `<div class="row-flex mt" style="gap:6px;flex-wrap:wrap">${s.status === 'PLANNED' ? `<button class="btn" data-arr="${s.stopId}">${I('pin')} Đã đến</button>` : ''}<button class="btn primary" data-hand="${s.stopId}">${I('check')} Bàn giao</button><button class="btn danger" data-fl="${s.stopId}">Giao thất bại</button>${s.address ? `<a class="btn" target="_blank" rel="noopener" href="https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(s.address)}">Chỉ đường</a>` : ''}</div>` : ''}</div>`).join('')}</div>`).join('');
      body.querySelectorAll('[data-arr]').forEach(b => b.onclick = () => { b.disabled = true; geo().then(g => Store.act('DeliveryStop/Arrive', { form: Object.assign({ Id: b.dataset.arr }, g), tables: TABLES }).then(r => { if (r.ok) { UI.toast('Đã đến điểm giao' + (g.Latitude ? '' : ' (máy không gửi được vị trí)'), 'ok'); re(); } else b.disabled = false; })); });
      body.querySelectorAll('[data-fl]').forEach(b => b.onclick = () => UI.modal({ title: 'Giao thất bại', size: 'sm',
        body: `<div class="field"><label>Lý do *</label><select class="select" id="flR"><option value="">— Chọn lý do —</option>${ref('DeliveryFailReason').map(z => `<option value="${esc(z.name || z.code)}">${esc(z.name || z.code)}</option>`).join('')}</select></div><div class="field mt"><label>Ghi chú</label><input class="input" id="flN" maxlength="500"></div><div class="small muted mt">Điểm chuyển sang cột "Cần giao lại" của Điều phối giao hàng; điều phối được báo ngay.</div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn danger" id="flGo">Ghi giao thất bại</button>',
        onMount: (m, close) => m.querySelector('#flGo').onclick = () => { const r = m.querySelector('#flR').value, n = m.querySelector('#flN').value.trim(); if (!r) return UI.toast('Chọn lý do giao thất bại', 'err');
          geo().then(g => Store.act('DeliveryStop/Fail', { form: Object.assign({ Id: b.dataset.fl, Reason: n ? r + ' – ' + n : r }, g), tables: TABLES }).then(x2 => { if (x2.ok) { UI.toast('Đã ghi giao thất bại – điều phối sẽ gán chuyến khác', 'warn'); close(); re(); } })); } }));
      body.querySelectorAll('[data-hand]').forEach(b => b.onclick = () => handover(b.dataset.hand, re));
    });
  }
  function handover(stopId, re) {
    Store.query('DeliveryStop/GetHandover', { id: stopId }).then(x => {
      if (!x.ok) return UI.toast(esc(x.message || 'Không tải được dữ liệu bàn giao'), 'err');
      const h = x.data || {}, trays = h.listTray || [];
      UI.modal({ title: 'Bàn giao – ' + esc(h.customerName || ''), sub: esc(h.locationName || ''), size: 'md',
        body: `<div class="form-grid"><div class="field"><label>Số suất giao *</label><input class="input" type="number" min="0" id="hvD" value="${h.plannedMeals || 0}"></div><div class="field"><label>Khách nhận *</label><input class="input" type="number" min="0" id="hvA" value="${h.plannedMeals || 0}"></div>
          <div class="field"><label>Người nhận *</label><input class="input" id="hvN" value="${esc(h.contactName || '')}" maxlength="500"></div><div class="field"><label>Điện thoại</label><input class="input" id="hvP" value="${esc(h.contactPhone || '')}" maxlength="100"></div>
          <div class="field"><label>Nhiệt độ lúc giao (°C)</label><input class="input" type="number" step="0.1" id="hvT"></div><div class="field"><label>Ghi chú</label><input class="input" id="hvR" maxlength="2000"></div></div>
          <div class="panel-title mt">Thu khay kỳ trước</div><div class="small muted mb">Số điền sẵn = khay còn ở khách. Sửa theo số thực thu; ghi số hỏng nếu có. Để 0 khi không thu.</div>
          ${trays.length ? `<table class="table dc-table"><thead><tr><th>Loại</th><th class="num">Còn ở khách</th><th class="num">Thu về</th><th class="num">Trong đó hỏng</th></tr></thead><tbody>${trays.map((t, i) => `<tr><td>${esc(t.label || t.packagingType)}</td><td class="num">${U.num(t.atCustomer)}</td><td class="num"><input class="input" style="width:80px" type="number" min="0" max="${t.atCustomer}" data-rq="${i}" value="${t.atCustomer}"></td><td class="num"><input class="input" style="width:80px" type="number" min="0" data-dq="${i}" value="0"></td></tr>`).join('')}</tbody></table>` : '<div class="small muted">Khách không còn khay nào cần thu.</div>'}`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="hvGo">Ghi bàn giao</button>',
        onMount: (m, close) => m.querySelector('#hvGo').onclick = () => {
          const v = id => m.querySelector('#' + id).value, dq = Number(v('hvD')), aq = Number(v('hvA')), rn = v('hvN').trim(), t = v('hvT');
          if (!rn) return UI.toast('Nhập người nhận', 'err'); if (!(dq >= 0) || !(aq >= 0) || aq > dq) return UI.toast('Số khách nhận phải từ 0 đến số suất giao', 'err');
          const form = { Id: stopId, DeliveredQuantity: dq, AcceptedQuantity: aq, ReceiverName: rn, ReceiverPhone: v('hvP'), Remark: v('hvR'), TemperatureC: t === '' ? '' : Number(t) };
          let bad = '', k = 0;
          trays.forEach((tr, i) => { const rq = Number(m.querySelector(`[data-rq="${i}"]`).value) || 0, dm = Number(m.querySelector(`[data-dq="${i}"]`).value) || 0; if (rq < 0 || dm < 0 || dm > rq) bad = 'Số hỏng phải từ 0 đến số thu về'; if (rq > tr.atCustomer) bad = `Thu ${rq} vượt số còn ở khách (${tr.atCustomer})`; if (rq > 0) { form[`ListReturn[${k}].PackagingType`] = tr.packagingType; form[`ListReturn[${k}].ReturnedQty`] = rq; form[`ListReturn[${k}].DamagedQty`] = dm; k++; } });
          if (bad) return UI.toast(bad, 'err');
          Store.act('DeliveryStop/Deliver', { form, tables: TABLES }).then(r => { if (!r.ok) return; close(); UI.toast(esc(r.res && r.res.responseMessage || 'Đã bàn giao'), 'ok'); re(); });
        } });
    });
  }

  // ================= Khay hộp theo khách =================
  const baseLedger = APP.SCREENS['log-packaging-ledger'];
  APP.register('log-packaging-ledger', (el, f, p) => {
    if (!Store.isLive) return baseLedger(el, f, p);
    Store.ensure('Customer');
    if (p && ['balance', 'returns', 'book'].includes(p.tab)) st.ltab = p.tab;
    const re = () => APP.SCREENS['log-packaging-ledger'](el, f, {});
    const tabBar = tabs([['balance', 'Còn ở khách'], ['returns', 'Phiếu thu hồi'], ['book', 'Sổ bao bì']], st.ltab, 'data-lt');
    const canLoss = Store.perm('log-packaging-ledger').add !== false;
    const bindTabs = () => el.querySelectorAll('[data-lt]').forEach(b => b.onclick = () => { st.ltab = b.dataset.lt; re(); });
    if (st.ltab === 'book') {
      baseLedger(el, f, p);
      el.querySelectorAll('#plNew').forEach(b => b.remove());
      const h = el.querySelector('.page-heading'); if (h) h.insertAdjacentHTML('afterend', `<div class="mb">${tabBar} ${canLoss ? '<button class="btn sm" id="plLoss">Ghi thất thoát</button>' : ''}</div>`);
      bindTabs(); const lb = el.querySelector('#plLoss'); if (lb) lb.onclick = () => lossForm(re);
      return;
    }
    el.innerHTML = APP.header(f, canLoss ? '<button class="btn" id="plLoss">Ghi thất thoát</button>' : '') + `<div class="small muted mb">Khay giao đi và thu về được ghi tự động khi tài xế bàn giao. Ghi tay chỉ còn thất thoát (mất / khách làm hỏng không trả).</div><div class="mb">${tabBar}</div><div id="plBody"><div class="card panel small muted">Đang tải…</div></div>`;
    bindTabs(); const lb = el.querySelector('#plLoss'); if (lb) lb.onclick = () => lossForm(re);
    const body = el.querySelector('#plBody');
    if (st.ltab === 'returns') return Store.query('ContainerReturn/GetList', { Page: 1, PageSize: 300 }).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được phiếu thu hồi')}</div>`; return; }
      const items = (x.data && x.data.listItem) || [], sc = (x.data && x.data.statusCounts) || {};
      body.innerHTML = `<div class="card panel"><div class="panel-head"><div><div class="panel-title">Phiếu thu hồi</div><div class="panel-desc">Mỗi lần giao có khay sinh một phiếu Chờ thu hồi; tài xế thu ở lần giao sau. ${Object.entries(sc).map(([k, n]) => esc(k) + ': ' + U.num(n)).join(' · ')}</div></div></div>
        ${items.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Đơn giao</th><th>Khách hàng</th><th>Loại</th><th>Ngày</th><th class="num">Phải thu</th><th class="num">Thu về</th><th class="num">Hỏng</th><th>Kiểm tra</th><th>Trạng thái</th></tr></thead><tbody>${items.map(i => { const r = i.return || {}; return `<tr><td>${esc(i.deliveryNumber || '')}</td><td>${esc(i.customerName || '')}</td><td>${esc(pkName(i.packagingType))}</td><td>${U.date(r.returnDate)}</td><td class="num">${U.num(r.expectedQuantity || 0)}</td><td class="num">${U.num(r.returnedQuantity || 0)}</td><td class="num">${U.num(r.damagedQuantity || 0)}</td><td>${esc(i.inspectionResultName || '')}</td><td>${UI.tag(i.statusName || r.status || '', r.status === 'Expected' ? 'orange' : 'green')}</td></tr>`; }).join('')}</tbody></table></div>` : '<div class="empty">Chưa có phiếu thu hồi.</div>'}</div>`;
    });
    Store.query('PackagingLedger/GetBalance', {}).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được số dư khay hộp')}</div>`; return; }
      const b = x.data || {}, items = b.listItem || [], customers = new Set(items.filter(i => i.outstanding > 0).map(i => i.customerId)).size;
      body.innerHTML = `<div class="kpis">${UI.kpi('Khay còn ở khách', U.num(b.totalOutstanding || 0), `${U.num(customers)} khách`, 'package', 'blue')}${UI.kpi('Quá hạn thu', U.num(b.overdueTotal || 0), 'quá số ngày thu hồi theo cấu hình', 'clock', 'orange')}${UI.kpi('Vượt ngưỡng thất thoát', U.num(b.overThresholdCount || 0), 'dòng khách × loại', 'alert', 'red')}${UI.kpi('Đã thu (lũy kế)', U.num(items.reduce((s, i) => s + (i.inQuantity || 0), 0)), `hỏng ${U.num(items.reduce((s, i) => s + (i.damagedInQuantity || 0), 0))}`, 'check', 'green')}</div>
        <div class="card panel">${items.length ? `<div class="table-wrap"><table class="table dc-table dt"><thead><tr><th>Khách hàng</th><th>Loại</th><th class="num">Đã giao</th><th class="num">Đã thu</th><th class="num">Hỏng</th><th class="num">Thất thoát</th><th class="num">Còn ở khách</th><th class="num">Quá hạn thu</th><th class="num">Tỷ lệ thất thoát</th></tr></thead><tbody>${items.map(i => `<tr><td>${esc(i.customerName || '')}${i.customerCode ? `<div class="small muted">${esc(i.customerCode)}</div>` : ''}</td><td>${esc(pkName(i.packagingType))}</td><td class="num">${U.num(i.outQuantity)}</td><td class="num">${U.num(i.inQuantity)}</td><td class="num">${U.num(i.damagedInQuantity)}</td><td class="num">${U.num(i.lossQuantity)}</td><td class="num"><b>${U.num(i.outstanding)}</b></td><td class="num ${i.overdueQuantity > 0 ? 'warn' : ''}">${U.num(i.overdueQuantity || 0)}</td><td class="num"><span class="${i.isOverThreshold ? 'bad' : ''}">${i.lossRatePct == null ? '–' : U.pct(i.lossRatePct, 2)}</span></td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">Chưa có khay hộp nào ở khách.</div>'}</div>`;
    });
  });
  function lossForm(re) {
    const cs = Store.rows('Customer');
    UI.modal({ title: 'Ghi thất thoát khay hộp', size: 'sm',
      body: `<div class="field"><label>Khách hàng *</label><select class="select" id="lsC">${cs.map(c => `<option value="${c.Id}">${esc(Store.label('Customer', c))}</option>`).join('')}</select></div><div class="field mt"><label>Loại *</label>${UI.refSelect('lsP', 'PackagingType', { required: true, placeholder: '— Chọn loại —' })}</div><div class="field mt"><label>Số lượng *</label><input class="input" type="number" min="1" id="lsQ"></div><div class="field mt"><label>Lý do *</label><textarea class="input" rows="2" id="lsR" maxlength="1000"></textarea></div><div class="small muted mt">Dòng sổ không sửa / xóa được.</div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn danger" id="lsGo">Ghi thất thoát</button>',
      onMount: (m, close) => m.querySelector('#lsGo').onclick = () => { const v = id => m.querySelector('#' + id).value, q = Number(v('lsQ')); if (!v('lsP')) return UI.toast('Chọn loại khay / hộp', 'err'); if (!(q > 0)) return UI.toast('Số lượng phải lớn hơn 0', 'err'); if (!v('lsR').trim()) return UI.toast('Nhập lý do', 'err');
        Store.act('PackagingLedger/Record', { form: { CustomerId: v('lsC'), TransactionType: 'LOSS', PackagingType: v('lsP'), Quantity: q, Reason: v('lsR').trim() }, tables: TABLES }).then(r => { if (r.ok) { UI.toast('Đã ghi thất thoát', 'ok'); close(); re(); } }); } });
  }
})();
