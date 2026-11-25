/* UI-6.34 (D-115): "Chia suất & đóng gói" (log-packing-order) + "Điều phối giao hàng" (log-route) – chỉ giao khi điểm giao đủ toàn bộ thực đơn.
 * - Chia suất: ngày + ca (◀ ▶), dải 4 bước, 4 chỉ số; ma trận dòng = điểm giao (nhóm theo chuyến, cuối "Chưa gán chuyến"), cột = món của ca
 *   (x/y mẻ đạt, "Xong món này" / "Chờ đủ mẻ"); ô chờ / sẵn sàng / đã chia / hỏng-thiếu / không áp dụng; bấm ô ghi hỏng; "Xong điểm giao" – PackingOrder/GetBoard + RecordResultMany.
 * - Điều phối: cột = chuyến (+ "Chưa gán chuyến"), thẻ điểm giao (Đủ thực đơn / thiếu món), "Chuyển xe", "Xếp lên xe" (chỉ mở khi đủ; xếp có điều kiện cần lý do),
 *   "Xuất bến" (nhiệt độ) – DeliveryRoute/GetDispatchBoard, MovePoint, Load, Dispatch.
 * Danh sách cũ (lệnh theo mẻ / danh sách chuyến) giữ ở nút "Tra cứu …" cho việc ngoại lệ. Chế độ DEMO giữ màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const st = { day: null, shift: null, legacyPack: false, legacyRoute: false };
  const today = () => U.day(Store.ctx.date || new Date());
  const hhmm = v => String(v || '').slice(11, 16);
  const TABLES = ['PackingOrder', 'PackingResult', 'ProductionBatche', 'DeliveryRoute', 'DeliveryOrder', 'DeliveryStop', 'DeliveryLoading'];
  const switcher = active => `<div class="seg mb" style="display:inline-flex">${[['log-packing-order', 'Chia suất & đóng gói'], ['log-route', 'Điều phối giao hàng']].map(([id, l]) => `<a class="seg-b ${active === id ? 'on' : ''}" href="${APP.href(id)}">${l}</a>`).join('')}</div>`;
  const dayNav = day => `<div class="row-flex" style="gap:6px"><button class="btn sm" data-dd="-1" aria-label="Ngày trước">◀</button><input type="date" class="input" id="ldDay" value="${day}" style="width:150px"><button class="btn sm" data-dd="1" aria-label="Ngày sau">▶</button>${day === today() ? '' : '<button class="btn sm" data-dd="0">Hôm nay</button>'}</div>`;
  const bindDay = (el, day, re) => { el.querySelectorAll('[data-dd]').forEach(b => b.onclick = () => { const n = +b.dataset.dd; st.day = n === 0 ? null : U.addDays(day, n); re(); }); const d = el.querySelector('#ldDay'); if (d) d.onchange = e => { st.day = e.target.value || null; re(); }; };
  const steps = `<div class="row-flex mb" style="gap:8px;flex-wrap:wrap">${['Chốt đơn → kế hoạch → mẻ (tự động)', 'Món đủ mẻ → mở chia', 'Điểm giao đủ thực đơn → sẵn sàng', 'Chuyến đủ mọi điểm → Xếp xe → Xuất bến'].map((t, i) => `<div class="card" style="padding:8px 12px;flex:1;min-width:200px"><span class="tag ${['green', 'green', 'orange', 'blue'][i]}">${i + 1}</span> ${t}</div>`).join('')}</div>`;

  // ================= Chia suất & đóng gói =================
  const basePack = APP.SCREENS['log-packing-order'];
  APP.register('log-packing-order', (el, f, p) => {
    if (!Store.isLive) return basePack(el, f, p);
    if (p && p.date) st.day = p.date;
    const re = () => APP.SCREENS['log-packing-order'](el, f, {});
    if (st.legacyPack) {
      basePack(el, f, p);
      const h = el.querySelector('.page-heading'); if (h) h.insertAdjacentHTML('afterend', `<div class="mb">${switcher('log-packing-order')} <button class="btn sm" id="ldBack">← Bảng chia suất</button></div>`);
      const b = el.querySelector('#ldBack'); if (b) b.onclick = () => { st.legacyPack = false; re(); };
      el.querySelectorAll('[data-act="add"]').forEach(x => x.remove());
      return;
    }
    const day = st.day || today();
    el.innerHTML = APP.header(f, '<button class="btn" id="ldLegacy">' + I('list') + ' Tra cứu lệnh theo mẻ</button>') + `<div class="small muted mb">Món chỉ mở chia khi đủ mọi mẻ của món. Điểm giao "Đủ thực đơn" khi mọi món đã chia; chỉ khi đó mới được lên xe. Bấm "Xong món này" / "Xong điểm giao"; chỉ ghi số khi hỏng / thiếu.</div>
      <div class="row-flex mb" style="gap:10px;flex-wrap:wrap">${switcher('log-packing-order')}${dayNav(day)}<span id="ldShifts"></span></div>${steps}<div id="ldBody"><div class="card panel small muted">Đang tải…</div></div>`;
    el.querySelector('#ldLegacy').onclick = () => { st.legacyPack = true; re(); };
    bindDay(el, day, re);
    Store.query('PackingOrder/GetBoard', { date: day, shiftId: st.shift || null }).then(x => {
      const body = el.querySelector('#ldBody'); if (!body) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được bảng chia suất')}</div>`; return; }
      const b = x.data || {}, dishes = b.listDish || [], rows = b.listRow || [], canEdit = Store.perm('log-packing-order').edit !== false;
      el.querySelector('#ldShifts').innerHTML = (b.listShift || []).map(s => `<button class="chip ${s.id === b.shiftId ? 'active' : ''}" data-sh="${s.id}">${esc(s.name || '')}</button>`).join(' ');
      el.querySelectorAll('[data-sh]').forEach(c => c.onclick = () => { st.shift = c.dataset.sh; re(); });
      const pct = b.portionsTotal ? Math.round(100 * b.portionsPacked / b.portionsTotal) : 0;
      const groups = {}; rows.forEach(r => { const k = r.routeName || ''; (groups[k] = groups[k] || []).push(r); });
      const cellHtml = (r, c) => {
        if (c.status === 'NA') return '<span class="small muted">không áp dụng</span>';
        const cls = { PACKED: 'background:#eef6e8;border-color:#b9d79e', READY: 'background:#fff;border-color:#7DA33C', WAITING: 'background:#f1f2ef;border-color:#ddd;color:#999', DAMAGED: 'background:#fdecec;border-color:#e3a3a3;color:#b42318' }[c.status];
        const txt = c.status === 'WAITING' ? 'chờ' : U.num(c.quantity);
        return `<button class="btn sm" style="min-width:64px;${cls}" ${c.status === 'READY' && canEdit ? `data-cell="${r.orderId}|${c.dishId}"` : 'disabled'}>${txt}</button>${c.status === 'DAMAGED' ? `<div class="small bad">thiếu ${U.num(c.damaged)}</div>` : ''}`;
      };
      body.innerHTML = `<div class="kpis">${UI.kpi('Món sẵn sàng chia', U.num(b.dishesReady || 0), `${U.num(dishes.length - (b.dishesReady || 0))} món chờ đủ mẻ`, 'check', 'green')}${UI.kpi('Điểm giao đủ thực đơn', `${U.num(b.pointsComplete || 0)} / ${U.num(b.pointsTotal || 0)}`, 'mọi món của thực đơn đã chia', 'truck', 'blue')}${UI.kpi('Phần đã chia', U.pct(pct), `${U.num(b.portionsPacked || 0)} / ${U.num(b.portionsTotal || 0)} phần`, 'package', 'orange')}${UI.kpi('Hỏng / thiếu khi chia', U.num(b.damagedTotal || 0), '', 'alert', 'red')}</div>
        <div class="card panel">${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Chuyến · điểm giao</th>${dishes.map(d => `<th style="text-align:center"><b>${esc(d.dishName || '')}</b><br><span class="small muted">${U.num(d.batchesPassed)}/${U.num(d.batchesTotal)} mẻ đạt</span><br>${d.allPacked ? UI.tag('Đã chia', 'green') : d.ready && canEdit ? `<button class="btn xs primary" data-dish="${d.dishId}">Xong món này</button>` : d.ready ? '' : UI.tag('Chờ đủ mẻ', '')}</th>`).join('')}<th>Điểm giao</th></tr></thead><tbody>
          ${Object.entries(groups).map(([g, list]) => `<tr><td colspan="${dishes.length + 2}" style="background:#f7f8f4"><b>${g ? esc(g) : 'Chưa gán chuyến'}</b> <span class="small muted">${U.num(list.filter(r => r.complete).length)}/${U.num(list.length)} điểm giao đủ thực đơn</span></td></tr>${list.map(r => `<tr><td><b>${esc(r.customerName || '')}</b>${r.locationName ? ' · ' + esc(r.locationName) : ''}<br><span class="small muted">${esc(r.orderNumber || '')}</span></td>${r.listCell.map(c => `<td style="text-align:center">${cellHtml(r, c)}</td>`).join('')}
            <td>${r.complete ? UI.tag('Đủ thực đơn', 'green') : `<span class="small">${U.num(r.dishesDone)}/${U.num(r.dishesTotal)} món</span><div style="height:5px;background:#eee;border-radius:3px;margin:3px 0"><div style="width:${r.dishesTotal ? 100 * r.dishesDone / r.dishesTotal : 0}%;height:100%;background:#7DA33C;border-radius:3px"></div></div>${canEdit && r.listCell.some(c => c.status === 'READY') ? `<button class="btn xs" data-point="${r.orderId}">Xong điểm giao</button>` : ''}`}</td></tr>`).join('')}`).join('')}
        </tbody></table></div><div class="small muted mt">Lệnh đóng gói vẫn tự sinh theo mẻ (dữ liệu nền) – màn này ghi kết quả hàng loạt theo món × điểm giao, mặc định đủ số, hỏng = 0.</div>` : '<div class="empty">Chưa có đơn đã chốt cho ngày / ca này.</div>'}</div>`;
      const send = (cells, label) => Store.act('PackingOrder/RecordResultMany', { json: { date: day, shiftId: b.shiftId, listCell: cells }, tables: TABLES }).then(r => { if (r.ok) { UI.toast(esc(r.res && r.res.responseMessage || label), 'ok'); re(); } });
      body.querySelectorAll('[data-dish]').forEach(bt => bt.onclick = () => UI.confirm('Ghi đủ số cho mọi điểm giao của món này (hỏng = 0)?', () => send(rows.filter(r => (r.listCell.find(c => c.dishId === bt.dataset.dish) || {}).status === 'READY').map(r => ({ orderId: r.orderId, dishId: bt.dataset.dish })), 'Đã chia xong món'), { ok: 'Xong món này' }));
      body.querySelectorAll('[data-point]').forEach(bt => bt.onclick = () => { const r = rows.find(z => z.orderId === bt.dataset.point); send(r.listCell.filter(c => c.status === 'READY').map(c => ({ orderId: r.orderId, dishId: c.dishId })), 'Đã chia xong điểm giao'); });
      body.querySelectorAll('[data-cell]').forEach(bt => bt.onclick = () => {
        const [orderId, dishId] = bt.dataset.cell.split('|'), r = rows.find(z => z.orderId === orderId), c = r.listCell.find(z => z.dishId === dishId), d = dishes.find(z => z.dishId === dishId);
        UI.modal({ title: esc(d.dishName || '') + ' – ' + esc(r.customerName || ''), sub: `${U.num(c.quantity)} phần · mặc định chia đủ`, size: 'sm',
          body: `<div class="field"><label>Hỏng / thiếu (phần)</label><input class="input" type="number" min="0" max="${c.quantity}" id="pcD" value="0"></div><div class="field mt"><label>Lý do (khi có hỏng)</label><input class="input" id="pcR"></div>`,
          foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="pcGo">Ghi đã chia</button>',
          onMount: (m, close) => m.querySelector('#pcGo').onclick = () => { const dmg = Number(m.querySelector('#pcD').value) || 0, reason = m.querySelector('#pcR').value.trim(); if (dmg > 0 && reason.length < 2) return UI.toast('Ghi lý do hỏng / thiếu', 'err'); close(); send([{ orderId, dishId, damaged: dmg || null, reason: reason || null }], 'Đã ghi'); } });
      });
    });
  });

  // ================= Điều phối giao hàng =================
  const baseRoute = APP.SCREENS['log-route'];
  APP.register('log-route', (el, f, p) => {
    if (!Store.isLive) return baseRoute(el, f, p);
    if (p && p.date) st.day = p.date;
    const re = () => APP.SCREENS['log-route'](el, f, {});
    if (st.legacyRoute) {
      baseRoute(el, f, p);
      const h = el.querySelector('.page-heading'); if (h) h.insertAdjacentHTML('afterend', `<div class="mb">${switcher('log-route')} <button class="btn sm" id="ldBack">← Bảng điều phối</button></div>`);
      const b = el.querySelector('#ldBack'); if (b) b.onclick = () => { st.legacyRoute = false; re(); };
      return;
    }
    const day = st.day || today();
    el.innerHTML = APP.header(f, '<button class="btn" id="ldLegacy">' + I('list') + ' Danh sách chuyến (thêm chuyến, lập theo khu vực)</button>') + `<div class="small muted mb">Chuyến chỉ xếp lên xe khi mọi điểm giao đủ thực đơn. Chuyển điểm giao giữa các chuyến bằng nút "Chuyển xe".</div>
      <div class="row-flex mb" style="gap:10px;flex-wrap:wrap">${switcher('log-route')}${dayNav(day)}</div><div id="ldBody"><div class="card panel small muted">Đang tải…</div></div>`;
    el.querySelector('#ldLegacy').onclick = () => { st.legacyRoute = true; re(); };
    bindDay(el, day, re);
    Store.query('DeliveryRoute/GetDispatchBoard', { date: day }).then(x => {
      const body = el.querySelector('#ldBody'); if (!body) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được bảng điều phối')}</div>`; return; }
      const b = x.data || {}, routes = b.listRoute || [], canEdit = Store.perm('log-route').edit !== false;
      const opts = cur => routes.filter(r => ['Planned', 'Loading'].includes(r.status) && r.id !== cur).map(r => `<option value="${r.id}">${esc(r.routeNo || '')} ${r.vehicleName ? '· ' + esc(r.vehicleName) : ''}</option>`).join('');
      const point = (pt, cur) => `<div style="border:1px solid #e5e7eb;border-left:4px solid ${pt.complete ? '#7DA33C' : '#E8892B'};border-radius:10px;background:#fff;padding:8px 10px;margin-bottom:8px"><div class="spread"><b>${esc(pt.customerName || '')}</b>${pt.complete ? UI.tag('Đủ thực đơn', 'green') : UI.tag('Còn thiếu', 'orange')}</div>
        <div class="small muted">${esc(pt.locationName || '')} · ${U.num(pt.meals)} suất · giao ${hhmm(pt.plannedTime)}</div>${pt.complete ? '' : `<div class="small warn">Thiếu: ${pt.listMissing.map(esc).join(', ')}</div>`}
        ${canEdit && (routes.length > 1 || !cur) && opts(cur) ? `<div class="row-flex mt" style="gap:4px"><select class="select" data-mv="${pt.deliveryOrderId}" style="width:auto;font-size:12px"><option value="">Chuyển xe…</option>${opts(cur)}</select></div>` : ''}</div>`;
      body.innerHTML = `<div class="kpis">${UI.kpi('Chuyến', U.num(routes.length), `${U.num(b.routesReady || 0)} chuyến đủ hàng`, 'truck', 'blue')}${UI.kpi('Điểm giao đủ thực đơn', `${U.num(b.pointsComplete || 0)} / ${U.num(b.pointsTotal || 0)}`, '', 'check', 'green')}${UI.kpi('Chưa gán chuyến', U.num((b.listUnassigned || []).length), '', 'alert', 'orange')}${UI.kpi('Đã xếp xe', U.num(routes.filter(r => r.loaded).length), '', 'package', 'purple')}</div>
        <style>.ld-cols{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:12px}</style><div class="ld-cols">
        ${routes.map(r => `<div class="card panel" style="background:#fafbf8"><div class="spread"><b>${esc(r.routeNo || '')}</b>${UI.tag(r.statusName || r.status || '', r.status === 'InTransit' ? 'blue' : '')}</div>
          <div class="small muted">${esc(r.vehicleName || 'Chưa có xe')} · ${esc(r.driverName || 'Chưa có tài xế')}${r.plannedStart ? ' · xuất bến ' + hhmm(r.plannedStart) : ''}</div>
          <div class="small mt"><b>${U.num(r.pointsComplete)}/${U.num(r.listPoint.length)}</b> điểm đủ thực đơn</div>
          <div class="row-flex mt mb" style="gap:6px">${canEdit && ['Planned', 'Loading'].includes(r.status) && !r.loaded ? `<button class="btn sm ${r.canLoad ? 'primary' : ''}" data-load="${r.id}" title="${r.canLoad ? '' : 'Còn điểm chưa đủ thực đơn'}">Xếp lên xe</button>` : ''}${canEdit && r.loaded && ['Planned', 'Loading'].includes(r.status) ? `<button class="btn sm orange" data-go="${r.id}">Xuất bến</button>` : ''}</div>
          ${r.listPoint.map(pt => point(pt, r.id)).join('') || '<div class="small muted">Chưa có điểm giao</div>'}</div>`).join('')}
        <div class="card panel" style="background:#fff8f2"><div class="spread"><b>Chưa gán chuyến</b>${UI.tag(U.num((b.listUnassigned || []).length), 'orange')}</div><div class="small muted mb">Đơn chốt muộn / bổ sung, hoặc thiếu xe khi tự lập chuyến.</div>${(b.listUnassigned || []).map(pt => point(pt, null)).join('') || '<div class="small muted">Không có</div>'}</div>
        ${(b.listRedeliver || []).length ? `<div class="card panel" style="background:#fdf2f2"><div class="spread"><b>Cần giao lại</b>${UI.tag(U.num(b.listRedeliver.length), 'red')}</div><div class="small muted mb">Giao thất bại – chọn chuyến khác để giao lại.</div>${b.listRedeliver.map(pt => point(pt, null)).join('')}</div>` : ''}</div>`;
      body.querySelectorAll('[data-mv]').forEach(s => s.onchange = () => { if (!s.value) return; Store.act('DeliveryRoute/MovePoint', { form: { DeliveryOrderId: s.dataset.mv, DeliveryRouteId: s.value }, tables: TABLES }).then(r => { if (r.ok) { UI.toast('Đã chuyển điểm giao', 'ok'); re(); } else re(); }); });
      body.querySelectorAll('[data-load]').forEach(bt => bt.onclick = () => {
        const r = routes.find(z => z.id === bt.dataset.load);
        if (r.canLoad) return Store.act('DeliveryRoute/Load', { json: { id: r.id }, tables: TABLES }).then(x2 => { if (x2.ok) { UI.toast('Đã xếp lên xe', 'ok'); re(); } });
        const lacks = r.listPoint.filter(pt => !pt.complete).map(pt => `${esc(pt.customerName || '')}: thiếu ${pt.listMissing.map(esc).join(', ')}`);
        UI.modal({ title: 'Chuyến chưa đủ thực đơn', sub: esc(r.routeNo || ''), size: 'md', body: `<div class="notice warn">${I('alert')}<div>${lacks.join('<br>')}</div></div><div class="field mt"><label>Xếp có điều kiện – lý do (chỉ người có quyền duyệt điều phối)</label><textarea class="input" rows="2" id="ldWhy"></textarea></div>`,
          foot: '<button class="btn" data-close>Đóng</button><button class="btn danger" id="ldForce">Xếp có điều kiện</button>',
          onMount: (m, close) => m.querySelector('#ldForce').onclick = () => { const why = m.querySelector('#ldWhy').value.trim(); if (why.length < 3) return UI.toast('Ghi lý do xếp có điều kiện', 'err'); Store.act('DeliveryRoute/Load', { json: { id: r.id, overrideReason: why }, tables: TABLES }).then(x2 => { if (x2.ok) { UI.toast('Đã xếp lên xe có điều kiện', 'warn'); close(); re(); } }); } });
      });
      body.querySelectorAll('[data-go]').forEach(bt => bt.onclick = () => UI.modal({ title: 'Xuất bến', size: 'sm', body: '<div class="field"><label>Nhiệt độ lúc xuất (°C)</label><input class="input" type="number" step="0.1" id="dgT"></div><div class="field mt"><label>Hành động khắc phục (khi dưới ngưỡng)</label><input class="input" id="dgC"></div>',
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="dgGo">Xuất bến</button>',
        onMount: (m, close) => m.querySelector('#dgGo').onclick = () => { const t = m.querySelector('#dgT').value; Store.act('DeliveryRoute/Dispatch', { form: { Id: bt.dataset.go, TemperatureC: t === '' ? '' : Number(t), CorrectiveAction: m.querySelector('#dgC').value }, tables: TABLES }).then(x2 => { if (x2.ok) { UI.toast('Đã xuất bến', 'ok'); close(); re(); } }); } }));
    });
  });
})();
