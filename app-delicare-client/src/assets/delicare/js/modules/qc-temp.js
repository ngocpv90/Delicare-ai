/* UI-6.31 (D-112): "Nhật ký nhiệt độ" (qc-temperature) theo mô phỏng temperature-log-mockup.html – 3 tab:
 * - Hôm nay: ngày ◀ ▶, nhãn ca đang chạy, nút "Ghi một lượt"; 4 chỉ số; "Cần xử lý ngay" (Đo lại & ghi khắc phục / Đo tay ngay); mỗi khu một thẻ
 *   gom theo kho (nhiệt độ mới nhất, ngưỡng, giờ đo, Cảm biến / Ghi tay, đường 24 giờ có dải ngưỡng, trạng thái) – TemperatureLog/GetDayBoard.
 * - Hộp "Ghi một lượt": khu ghi tay + khu cảm biến mất tín hiệu, ô nhiệt độ to, tô màu ngay, ngoài ngưỡng bắt buộc khắc phục, lưu một lần (RecordRound).
 * - Sổ nhiệt độ (chỉ xem): GetBook – gom theo ngày, lọc, In sổ / Xuất Excel. - Biểu đồ: MỘT biểu đồ GetSeries 48 giờ / 7 / 30 ngày cho khu đã chọn.
 * Thay màn qc.js + panel be3.js ở chế độ thật (bỏ biểu đồ trùng). Tải SAU be3.js. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const has = v => v !== null && v !== undefined && v !== '';
  const st = { tab: 'today', day: null, board: null, zone: null, range: '48h', book: { period: '7d', from: null, to: null, q: '', zone: '', source: '', out: false, process: false } };
  const today = () => U.day(Store.ctx.date || new Date());
  const hhmm = v => String(v || '').slice(11, 16);
  const num = v => (has(v) ? U.num(v, 1) : '–');
  const STATUS = { OK: ['green', 'Đạt'], OUT: ['red', 'Ngoài ngưỡng – chưa khắc phục'], DUE: ['orange', 'Đến giờ chưa đo'], SILENT: ['gray', 'Cảm biến mất tín hiệu'], NONE: ['', 'Chưa có số đo'] };
  const COLOR = { green: '#5E9B3A', red: '#D64545', orange: '#E8892B', gray: '#8A9099', '': '#C9CED6' };
  const rangeText = (min, max) => (has(min) && has(max) ? `${num(min)} – ${num(max)} °C` : has(min) ? `≥ ${num(min)} °C` : has(max) ? `≤ ${num(max)} °C` : 'không quy định');
  const inRange = (z, t) => (!has(z.temperatureMin) || t >= z.temperatureMin) && (!has(z.temperatureMax) || t <= z.temperatureMax);

  const base = APP.SCREENS['qc-temperature'];
  APP.register('qc-temperature', (el, f, p) => {
    if (!Store.isLive) return base(el, f, p);
    if (p && ['today', 'book', 'chart'].includes(p.tab)) st.tab = p.tab;
    if (p && p.zone) { st.zone = p.zone; st.tab = 'chart'; }
    const tabs = `<div class="tabs mb">${[['today', 'Hôm nay'], ['book', 'Sổ nhiệt độ'], ['chart', 'Biểu đồ']].map(([k, l]) => `<button class="tab ${st.tab === k ? 'active' : ''}" data-qtt="${k}">${l}</button>`).join('')}</div>`;
    const re = () => APP.SCREENS['qc-temperature'](el, f, {});
    (st.tab === 'book' ? drawBook : st.tab === 'chart' ? drawChart : drawToday)(el, f, tabs, re);
    el.querySelectorAll('[data-qtt]').forEach(b => b.onclick = () => { st.tab = b.dataset.qtt; re(); });
  });

  // Đường 24 giờ (SVG gọn) + dải ngưỡng.
  function spark(z) {
    const pts = z.listPoint || [], W = 300, H = 34;
    if (!pts.length) return `<svg viewBox="0 0 ${W} ${H}" width="100%" height="${H}" aria-hidden="true"><rect x="0" y="0" width="${W}" height="${H}" fill="#f4f5f2"/></svg>`;
    const vals = pts.map(p => p.t).concat([z.temperatureMin, z.temperatureMax].filter(has));
    let lo = Math.min(...vals), hi = Math.max(...vals); if (hi - lo < 1) { hi += 0.5; lo -= 0.5; }
    const t0 = new Date(pts[0].at).getTime(), t1 = Math.max(t0 + 1, new Date(pts[pts.length - 1].at).getTime());
    const x = a => ((new Date(a).getTime() - t0) / (t1 - t0)) * (W - 4) + 2, y = v => H - 3 - ((v - lo) / (hi - lo)) * (H - 6);
    const band = has(z.temperatureMin) || has(z.temperatureMax) ? `<rect x="0" y="${y(has(z.temperatureMax) ? z.temperatureMax : hi)}" width="${W}" height="${Math.max(1, y(has(z.temperatureMin) ? z.temperatureMin : lo) - y(has(z.temperatureMax) ? z.temperatureMax : hi))}" fill="#eef2e6"/>` : '';
    const line = pts.length === 1 ? `<circle cx="${W / 2}" cy="${y(pts[0].t)}" r="3" fill="${pts[0].ok === false ? '#D64545' : '#5E9B3A'}"/>`
      : `<polyline fill="none" stroke="${pts.some(p => p.ok === false) ? '#D64545' : '#5E9B3A'}" stroke-width="1.6" points="${pts.map(p => x(p.at).toFixed(1) + ',' + y(p.t).toFixed(1)).join(' ')}"/>`;
    return `<svg viewBox="0 0 ${W} ${H}" width="100%" height="${H}" preserveAspectRatio="none" aria-hidden="true">${band}${line}</svg>`;
  }

  // ================= Hôm nay =================
  function drawToday(el, f, tabs, re) {
    el.innerHTML = APP.header(f, '') + tabs + '<div id="qtBody"><div class="card panel small muted">Đang tải…</div></div>';
    Store.query('TemperatureLog/GetDayBoard', { date: st.day || null }).then(x => {
      const body = el.querySelector('#qtBody'); if (!body) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được nhật ký nhiệt độ')}</div>`; return; }
      const b = st.board = x.data || {}, zones = b.listZone || [], day = U.day(b.date || st.day || today()), srvToday = U.day(b.now || today());
      const canAdd = Store.perm('qc-temperature').add !== false;
      const act = zones.filter(z => ['OUT', 'SILENT', 'DUE'].includes(z.status));
      let groups = {}; zones.forEach(z => { (groups[z.warehouseName || 'Khác'] = groups[z.warehouseName || 'Khác'] || []).push(z); });
      // Nhiều kho, mỗi kho ít khu → một lưới chung, tên kho trong thẻ.
      const flat = Object.keys(groups).length > 6 && zones.length / Object.keys(groups).length < 3; if (flat) groups = { '': zones };
      body.innerHTML = `<style>.qt-cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:12px}.qt-card{border:1px solid var(--line,#e5e7eb);border-left:5px solid #ccc;border-radius:12px;padding:12px 14px;background:#fff;cursor:pointer}.qt-card .t{font-size:26px;font-weight:700}.qt-act{display:flex;justify-content:space-between;align-items:center;gap:8px;padding:8px 0;border-bottom:1px dashed #f1c9c9}.qt-act:last-child{border-bottom:0}</style>
        <div class="card panel mb"><div class="spread"><div class="row-flex"><button class="btn sm" data-dd="-1" aria-label="Ngày trước">◀</button><input type="date" class="input" id="qtDay" value="${day}" style="width:150px"><button class="btn sm" data-dd="1" aria-label="Ngày sau">▶</button>${day === srvToday ? '' : '<button class="btn sm" data-dd="0">Hôm nay</button>'}${b.shiftName ? ' ' + UI.tag('Ca ' + b.shiftName, 'orange') : ''}</div>
          ${canAdd ? `<button class="btn primary" id="qtRound" style="font-size:16px;padding:10px 18px">${I('thermo')} Ghi một lượt</button>` : ''}</div>
          <div class="small muted mt">Nhiệt độ kho, tủ, khu chia suất – cảm biến ghi tự động, khu ghi tay đo mỗi ca. Sổ chỉ ghi thêm, không sửa / xóa. Nhiệt độ lõi mẻ, giao hàng, nhận hàng ghi ở mẻ / chuyến / nhận hàng.</div></div>
        <div class="kpis">${UI.kpi('Khu đang ngoài ngưỡng', U.num(b.zonesOut || 0), `trên ${zones.length} khu theo dõi`, 'alert', 'red')}${UI.kpi('Đến giờ chưa đo (ca này)', U.num(b.dueNotMeasured || 0), 'khu ghi tay · hạn 60 phút đầu ca', 'clock', 'orange')}${UI.kpi('Cảm biến mất tín hiệu', U.num(b.silentSensors || 0), `${zones.filter(z => z.hasSensor).length} khu có cảm biến`, 'alert', 'gray')}${UI.kpi('Lần ngoài ngưỡng 7 ngày', U.num((b.out7dResolved || 0) + (b.out7dOpen || 0)), `${U.num(b.out7dResolved || 0)} đã khắc phục · ${U.num(b.out7dOpen || 0)} còn mở`, 'thermo', 'blue')}</div>
        ${act.length ? `<div class="card panel mb" style="border-color:#f1c9c9;background:#fff7f7"><div class="panel-title bad">Cần xử lý ngay (${act.length})</div>${act.map(z => `<div class="qt-act"><div><b>${esc(z.zoneName || '')}</b> — ${z.status === 'OUT' ? `<span class="bad">${num(z.lastTemperature)} °C lúc ${hhmm(z.lastAt)} (ngưỡng ${rangeText(z.temperatureMin, z.temperatureMax)}) – chưa ghi khắc phục</span>` : z.status === 'SILENT' ? `cảm biến ${esc((z.listDevice || []).join(', '))} ${z.silentSince ? 'không gửi số từ ' + hhmm(z.silentSince) : 'chưa gửi số đo'} – kiểm tra cảm biến, đo tay tạm thời` : `<span class="warn">${z.lastAt ? 'lần đo gần nhất ' + hhmm(z.lastAt) : 'chưa có lần đo'} – ca này chưa đo</span>`}</div>${canAdd ? `<button class="btn xs ${z.status === 'OUT' ? 'orange' : ''}" data-fix="${z.storageZoneId}">${z.status === 'OUT' ? 'Đo lại & ghi khắc phục' : 'Đo tay ngay'}</button>` : ''}</div>`).join('')}</div>` : ''}
        <div class="row-flex small mb" style="flex-wrap:wrap;gap:12px">${['OK', 'OUT', 'DUE', 'SILENT'].map(k => `<span><span style="display:inline-block;width:12px;height:12px;border-radius:3px;background:${COLOR[STATUS[k][0]]};vertical-align:middle"></span> ${STATUS[k][1].replace(' – chưa khắc phục', ' chưa khắc phục')}</span>`).join('')}<span class="muted">Bấm thẻ xem biểu đồ của khu · dải xanh = ngưỡng</span></div>
        ${zones.length ? Object.entries(groups).map(([g, list]) => `${g ? `<div class="small muted mb" style="text-transform:uppercase;font-weight:600;margin-top:10px">${esc(g)}</div>` : ''}<div class="qt-cards mb">${list.map(z => { const s = STATUS[z.status] || STATUS.NONE; return `<div class="qt-card" data-zone="${z.storageZoneId}" style="border-left-color:${COLOR[s[0]]}"><div class="spread"><b>${esc(z.zoneName || '')}</b>${UI.tag(z.hasSensor ? 'Cảm biến' : 'Ghi tay', z.hasSensor ? 'blue' : '')}</div>
          <div class="t ${z.lastWithin === false ? 'bad' : ''}">${num(z.lastTemperature)} <span class="small muted" style="font-size:13px">°C</span></div><div class="small muted">${flat && z.warehouseName ? esc(z.warehouseName) + ' · ' : ''}Ngưỡng ${rangeText(z.temperatureMin, z.temperatureMax)}${z.lastAt ? ' · đo lúc ' + hhmm(z.lastAt) : ''}${z.lastBy ? ' · ' + esc(z.lastBy) : ''}</div>
          <div class="mt">${spark(z)}</div><div class="mt">${UI.tag(z.status === 'SILENT' && z.silentSince ? s[1] + ' ' + Math.round((new Date(b.now) - new Date(z.silentSince)) / 60000) + '′' : s[1], s[0] || 'gray')}</div></div>`; }).join('')}</div>`).join('') : '<div class="card panel empty">Chưa có khu bảo quản nào – thêm ở Kho › Kho & vị trí › Khu bảo quản.</div>'}`;
      const go = d => { st.day = d; re(); };
      body.querySelectorAll('[data-dd]').forEach(bt => bt.onclick = () => { const n = +bt.dataset.dd; go(n === 0 ? null : U.addDays(day, n)); });
      body.querySelector('#qtDay').onchange = e => go(e.target.value || null);
      const rb = body.querySelector('#qtRound'); if (rb) rb.onclick = () => roundForm(zones, null, re);
      body.querySelectorAll('[data-fix]').forEach(bt => bt.onclick = () => roundForm(zones, bt.dataset.fix, re));
      body.querySelectorAll('[data-zone]').forEach(c => c.onclick = () => { st.zone = c.dataset.zone; st.tab = 'chart'; re(); });
    });
  }

  // ================= Ghi một lượt =================
  function roundForm(zones, focus, done) {
    const fz = focus && zones.find(z => z.storageZoneId === focus);
    const list = fz ? [fz] : zones.filter(z => !z.hasSensor || z.status === 'SILENT');
    if (!list.length) return UI.toast('Không có khu ghi tay nào – mọi khu đang có cảm biến', 'warn');
    const fixMode = !!(fz && fz.status === 'OUT');
    UI.modal({
      title: fz ? (fixMode ? 'Đo lại & ghi khắc phục – ' : 'Đo tay – ') + esc(fz.zoneName || '') : 'Ghi một lượt', sub: fz ? 'Ghi lần đo mới (không sửa lần cũ). Giờ đo = lúc lưu.' : 'Mỗi khu một nhiệt độ – để trống = chưa đo khu đó. Giờ đo = lúc lưu; loại đo theo loại khu.', size: 'lg',
      body: `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Khu</th><th>Ngưỡng</th><th>Lần trước</th><th>Nhiệt độ °C</th><th>Hành động khắc phục</th></tr></thead><tbody>${list.map(z => `<tr data-z="${z.storageZoneId}"><td><b>${esc(z.zoneName || '')}</b><br><span class="small muted">${esc(z.warehouseName || '')}${z.status === 'SILENT' ? ' · cảm biến mất tín hiệu' : ''}</span></td><td class="small">${rangeText(z.temperatureMin, z.temperatureMax)}</td><td class="small">${z.lastAt ? `${num(z.lastTemperature)} °C · ${hhmm(z.lastAt)}` : '–'}</td>
        <td><input class="input" type="number" inputmode="decimal" step="0.1" data-t style="width:110px;font-size:20px;font-weight:600"></td><td><input class="input" data-c placeholder="${fixMode ? 'Đã làm gì để khắc phục' : 'Bắt buộc khi ngoài ngưỡng'}" style="width:240px;display:${fixMode ? 'block' : 'none'}"></td></tr>`).join('')}</tbody></table></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="qtSave">Lưu cả lượt</button>',
      onMount: (m, close) => {
        const rows = [...m.querySelectorAll('tr[data-z]')];
        rows.forEach(tr => { const z = list.find(q => q.storageZoneId === tr.dataset.z), inp = tr.querySelector('[data-t]'), c = tr.querySelector('[data-c]');
          inp.oninput = () => { const v = inp.value === '' ? null : Number(inp.value), ok = v === null || inRange(z, v); inp.style.background = v === null ? '' : ok ? '#eef6e8' : '#fdecec'; inp.style.color = v === null || ok ? '' : '#b42318'; if (!fixMode) c.style.display = ok ? 'none' : 'block'; }; });
        if (rows[0]) rows[0].querySelector('[data-t]').focus();
        m.querySelector('#qtSave').onclick = () => {
          const items = [];
          for (const tr of rows) {
            const z = list.find(q => q.storageZoneId === tr.dataset.z), v = tr.querySelector('[data-t]').value, c = tr.querySelector('[data-c]').value.trim();
            if (v === '') continue;
            if (!inRange(z, Number(v)) && c.length < 3) return UI.toast(esc(z.zoneName || '') + ': ngoài ngưỡng – ghi hành động khắc phục', 'err');
            items.push({ storageZoneId: z.storageZoneId, temperatureC: Number(v), correctiveAction: c || null });
          }
          if (!items.length) return UI.toast('Chưa nhập nhiệt độ khu nào', 'err');
          Store.act('TemperatureLog/RecordRound', { json: { listItem: items }, tables: ['TemperatureLog'] }).then(r => {
            if (!r.ok) return;
            const d = r.data || {};
            UI.toast(`Đã ghi nhiệt độ ${U.num(d.saved || items.length)} khu` + (d.outOfRange ? ` · <b>${U.num(d.outOfRange)} khu ngoài ngưỡng</b> – đã báo trưởng ca + QA` : ''), d.outOfRange ? 'warn' : 'ok');
            close(); if (done) done();
          });
        };
      }
    });
  }

  // ================= Sổ nhiệt độ =================
  const bk = st.book;
  const range = () => {
    const t = today();
    if (bk.period === 'today') return [t, t];
    if (bk.period === 'month') return [t.slice(0, 8) + '01', t];
    if (bk.period === 'custom' && bk.from && bk.to) return [bk.from, bk.to];
    return [U.addDays(t, -6), t];
  };
  function drawBook(el, f, tabs, re) {
    const [a, b] = range(), canExp = Store.perm('qc-temperature').exp !== false;
    el.innerHTML = APP.header(f, `<button class="btn" id="qbPrint">${I('print')} In sổ nhiệt độ</button>${canExp ? `<button class="btn" id="qbXls">${I('download')} Xuất Excel</button>` : ''}`) + tabs + `
      <div class="card panel mb"><div class="row-flex" style="flex-wrap:wrap;gap:8px">
        <input class="input" type="search" id="qbQ" placeholder="Tìm khu, mẻ, lô, người đo, khắc phục" value="${esc(bk.q)}" style="width:250px">
        ${[['today', 'Hôm nay'], ['7d', '7 ngày'], ['month', 'Tháng này'], ['custom', 'Chọn khoảng']].map(([k, l]) => `<button class="chip ${bk.period === k ? 'active' : ''}" data-qbp="${k}">${l}</button>`).join('')}
        <span style="display:${bk.period === 'custom' ? 'inline-flex' : 'none'};gap:4px"><input type="date" class="input" id="qbFrom" value="${a}" style="width:145px"><input type="date" class="input" id="qbTo" value="${b}" style="width:145px"></span>
        <select class="select" id="qbZone" style="width:auto"><option value="">Mọi khu</option>${Store.rows('StorageZone').map(z => `<option value="${z.Id}" ${bk.zone === z.Id ? 'selected' : ''}>${esc(z.Name || z.Code || '')}</option>`).join('')}</select>
        <select class="select" id="qbSrc" style="width:auto"><option value="">Mọi nguồn</option><option value="IOT" ${bk.source === 'IOT' ? 'selected' : ''}>Cảm biến</option><option value="MANUAL" ${bk.source === 'MANUAL' ? 'selected' : ''}>Ghi tay</option></select>
        <label class="small"><input type="checkbox" id="qbOut" ${bk.out ? 'checked' : ''}> Chỉ ngoài ngưỡng</label>
        <label class="small"><input type="checkbox" id="qbProc" ${bk.process ? 'checked' : ''}> Gồm lõi mẻ / giao hàng / nhận hàng</label>
      </div></div><div id="qbBody"><div class="card panel small muted">Đang tải…</div></div>`;
    el.querySelectorAll('[data-qbp]').forEach(x => x.onclick = () => { bk.period = x.dataset.qbp; if (bk.period === 'custom') { bk.from = bk.from || a; bk.to = bk.to || b; } re(); });
    const on = (id, ev, fn) => { const e = el.querySelector(id); if (e) e.addEventListener(ev, fn); };
    let tq; on('#qbQ', 'input', e => { clearTimeout(tq); tq = setTimeout(() => { bk.q = e.target.value.trim(); loadBook(el); }, 350); });
    on('#qbFrom', 'change', e => { bk.from = e.target.value; loadBook(el); }); on('#qbTo', 'change', e => { bk.to = e.target.value; loadBook(el); });
    on('#qbZone', 'change', e => { bk.zone = e.target.value; loadBook(el); }); on('#qbSrc', 'change', e => { bk.source = e.target.value; loadBook(el); });
    on('#qbOut', 'change', e => { bk.out = e.target.checked; loadBook(el); }); on('#qbProc', 'change', e => { bk.process = e.target.checked; loadBook(el); });
    if (!Store.rows('StorageZone').length) Store.ensure('StorageZone').then(() => { if (Store.rows('StorageZone').length) re(); }, () => {});
    loadBook(el);
  }
  function loadBook(el) {
    const body = el.querySelector('#qbBody'); if (!body) return;
    const [a, b] = range();
    Store.query('TemperatureLog/GetBook', { From: a, To: b, Keyword: bk.q || null, StorageZoneId: bk.zone || null, Source: bk.source || null, OnlyOutOfRange: bk.out ? true : null, IncludeProcess: bk.process ? true : null }).then(x => {
      if (!body.isConnected) return;
      if (!x.ok) { body.innerHTML = `<div class="card panel muted">${esc(x.message || 'Không tải được sổ')}</div>`; return; }
      const d = x.data || {}, days = d.listDay || [];
      const res = r => (r.isWithinRange === false ? UI.tag(r.resolved ? 'Ngoài ngưỡng – đã khắc phục' : 'Ngoài ngưỡng', r.resolved ? 'orange' : 'red') : r.isWithinRange === true ? UI.tag('Đạt', 'green') : '<span class="muted small">Không có ngưỡng</span>');
      const target = r => esc(r.target || '–') + (r.productionBatchId ? ` <a href="${APP.href('prod-batch-detail?id=' + r.productionBatchId)}" class="small">mở mẻ</a>` : '') + (r.materialLotId ? ` <a href="${APP.href('scm-lot-detail?id=' + r.materialLotId)}" class="small">mở lô</a>` : '');
      body.innerHTML = `<style>.qb-cards{display:none}.qb-card{border:1px solid var(--line,#e5e7eb);border-radius:10px;padding:10px;margin-bottom:8px}@media(max-width:640px){.qb-table{display:none}.qb-cards{display:block}}</style>
        <div class="kpis">${UI.kpi('Lần đo', U.num(d.count || 0), '', 'thermo', 'blue')}${UI.kpi('Ngoài ngưỡng', U.num(d.outOfRange || 0), '', 'alert', 'orange')}${UI.kpi('Chưa khắc phục', U.num(d.openOutOfRange || 0), d.openOutOfRange ? '<span class="bad">đo lại & ghi khắc phục</span>' : 'Không có', 'shield', 'red')}${UI.kpi('Tỷ lệ trong ngưỡng', d.count ? U.pct(100 * (d.count - d.outOfRange) / d.count) : '–', '', 'check', 'green')}</div>
        ${days.length ? days.map(g => `<details class="card panel mb" open><summary class="spread" style="cursor:pointer"><span><b>${U.date(g.date)}</b> <span class="small muted">· ${U.num(g.count)} lần đo${g.outOfRange ? ` · <span class="bad">${U.num(g.outOfRange)} ngoài ngưỡng</span>` : ''}</span></span><button class="btn xs" data-pday="${U.day(g.date)}">${I('print')} In ngày</button></summary>
          <div class="table-wrap qb-table mt"><table class="table dc-table"><thead><tr><th>Giờ</th><th>Khu vực / đối tượng</th><th>Loại đo</th><th class="num">Nhiệt độ</th><th>Ngưỡng</th><th>Kết quả</th><th>Người đo / nguồn</th><th>Khắc phục</th></tr></thead><tbody>${g.listRow.map(r => `<tr><td>${hhmm(r.measuredAt)}</td><td>${target(r)}</td><td class="small">${esc(r.measurementName || '')}</td><td class="num"><b class="${r.isWithinRange === false ? 'bad' : ''}">${num(r.temperatureC)} °C</b></td><td class="small">${rangeText(r.minAllowedC, r.maxAllowedC)}</td><td>${res(r)}</td><td class="small">${esc(r.by || '')}</td><td class="small">${esc(r.correctiveAction || '–')}</td></tr>`).join('')}</tbody></table></div>
          <div class="qb-cards mt">${g.listRow.map(r => `<div class="qb-card"><div class="spread"><b>${esc(r.target || '')}</b>${res(r)}</div><div class="small muted">${hhmm(r.measuredAt)} · ${esc(r.measurementName || '')} · ${esc(r.by || '')}</div><div><b>${num(r.temperatureC)} °C</b> <span class="small muted">ngưỡng ${rangeText(r.minAllowedC, r.maxAllowedC)}</span></div>${r.correctiveAction ? `<div class="small">Khắc phục: ${esc(r.correctiveAction)}</div>` : ''}</div>`).join('')}</div></details>`).join('') : '<div class="card panel empty">Không có lần đo nào khớp bộ lọc.</div>'}`;
      body.querySelectorAll('[data-pday]').forEach(btn => btn.onclick = e => { e.preventDefault(); e.stopPropagation(); printBook(days.filter(g => U.day(g.date) === btn.dataset.pday)); });
      const pr = el.querySelector('#qbPrint'); if (pr) pr.onclick = () => printBook(days);
      const xl = el.querySelector('#qbXls'); if (xl) xl.onclick = () => exportCsv(days.flatMap(g => g.listRow));
    });
  }
  // In sổ nhiệt độ (bản in trình duyệt theo khoảng ngày) – mẫu PDF chính thức chờ biểu mẫu của anh (PROGRESS).
  function printBook(days) {
    const [a, b] = range(), rows = days.flatMap(g => g.listRow);
    if (!rows.length) return UI.toast('Không có dòng nào để in', 'warn');
    const w = window.open('', '_blank'); if (!w) return UI.toast('Trình duyệt chặn cửa sổ in – cho phép cửa sổ bật lên rồi thử lại', 'err');
    const t = v => esc(v == null ? '' : String(v));
    w.document.write(`<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Sổ nhiệt độ</title><style>body{font:12px Arial,sans-serif;margin:18px}h1{font-size:16px;margin:0 0 4px}table{border-collapse:collapse;width:100%;margin-top:10px}th,td{border:1px solid #999;padding:4px 5px;vertical-align:top}th{background:#eee}</style></head><body>
      <h1>SỔ THEO DÕI NHIỆT ĐỘ BẢO QUẢN</h1><div>Từ ngày ${U.date(a)} đến ngày ${U.date(b)} · ${rows.length} lần đo</div>
      <table><thead><tr><th>Ngày giờ</th><th>Khu vực / đối tượng</th><th>Loại đo</th><th>Nhiệt độ</th><th>Ngưỡng</th><th>Kết quả</th><th>Người đo / nguồn</th><th>Khắc phục</th></tr></thead><tbody>
      ${rows.map(r => `<tr><td>${U.date(r.measuredAt)} ${hhmm(r.measuredAt)}</td><td>${t(r.target)}</td><td>${t(r.measurementName)}</td><td>${num(r.temperatureC)} °C</td><td>${rangeText(r.minAllowedC, r.maxAllowedC)}</td><td>${r.isWithinRange === false ? 'Ngoài ngưỡng' + (r.resolved ? ' (đã khắc phục)' : '') : r.isWithinRange === true ? 'Đạt' : ''}</td><td>${t(r.by)}</td><td>${t(r.correctiveAction)}</td></tr>`).join('')}
      </tbody></table><p style="margin-top:24px">Người kiểm tra ký: ........................................ &nbsp;&nbsp;&nbsp; Người phụ trách ký: ........................................</p><script>window.onload=function(){window.print()}<\/script></body></html>`);
    w.document.close();
  }
  function exportCsv(rows) {
    if (!rows.length) return UI.toast('Không có dòng nào để xuất', 'warn');
    const q = v => '"' + String(v == null ? '' : v).replace(/"/g, '""') + '"';
    const head = ['Ngày', 'Giờ', 'Khu vực / đối tượng', 'Loại đo', 'Nhiệt độ °C', 'Ngưỡng', 'Kết quả', 'Người đo / nguồn', 'Khắc phục'];
    const lines = rows.map(r => [U.date(r.measuredAt), hhmm(r.measuredAt), r.target, r.measurementName, r.temperatureC, rangeText(r.minAllowedC, r.maxAllowedC), r.isWithinRange === false ? 'Ngoài ngưỡng' + (r.resolved ? ' (đã khắc phục)' : '') : r.isWithinRange === true ? 'Đạt' : '', r.by, r.correctiveAction].map(q).join(','));
    const blob = new Blob(['﻿' + [head.map(q).join(',')].concat(lines).join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const [x, y] = range(), a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = `so-nhiet-do_${x}_${y}.csv`; document.body.appendChild(a); a.click(); setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
    UI.toast('Đã xuất ' + rows.length + ' dòng', 'ok');
  }

  // ================= Biểu đồ (một biểu đồ duy nhất) =================
  function drawChart(el, f, tabs, re) {
    el.innerHTML = APP.header(f, '') + tabs + '<div id="qcBody"><div class="card panel small muted">Đang tải…</div></div>';
    Store.ensure('StorageZone').then(() => {
      const body = el.querySelector('#qcBody'); if (!body) return;
      const zones = Store.rows('StorageZone').filter(z => z.Status !== 'Inactive');
      if (!zones.length) { body.innerHTML = '<div class="card panel empty">Chưa có khu bảo quản nào.</div>'; return; }
      if (!st.zone || !zones.some(z => z.Id === st.zone)) st.zone = zones[0].Id;
      const now = new Date(), to = U.iso(now), hours = { '48h': 48, '7d': 168, '30d': 720 }[st.range] || 48, from = U.iso(new Date(now.getTime() - hours * 3600000));
      body.innerHTML = `<div class="card panel"><div class="row-flex mb" style="flex-wrap:wrap;gap:8px"><select class="select" id="qcZone" style="width:auto">${zones.map(z => `<option value="${z.Id}" ${z.Id === st.zone ? 'selected' : ''}>${esc(z.Name || z.Code || '')}</option>`).join('')}</select>${[['48h', '48 giờ'], ['7d', '7 ngày'], ['30d', '30 ngày']].map(([k, l]) => `<button class="chip ${st.range === k ? 'active' : ''}" data-rg="${k}">${l}</button>`).join('')}</div><div id="qcChart" class="small muted">Đang tải…</div></div>`;
      body.querySelector('#qcZone').onchange = e => { st.zone = e.target.value; re(); };
      body.querySelectorAll('[data-rg]').forEach(b => b.onclick = () => { st.range = b.dataset.rg; re(); });
      Store.query('TemperatureLog/GetSeries', { StorageZoneId: st.zone, From: from, To: to }).then(x => {
        const box = el.querySelector('#qcChart'); if (!box) return;
        if (!x.ok) { box.innerHTML = esc(x.message || 'Không tải được biểu đồ'); return; }
        const d = x.data || {}, pts = d.points || [];
        if (!pts.length) { box.innerHTML = '<div class="empty">Khu này chưa có lần đo trong khoảng đã chọn.</div>'; return; }
        const lbl = p => (st.range === '48h' ? hhmm(p.measuredAt) : U.date(p.measuredAt).slice(0, 5) + ' ' + hhmm(p.measuredAt));
        const band = has(d.temperatureMin) || has(d.temperatureMax) ? { min: has(d.temperatureMin) ? d.temperatureMin : -999, max: has(d.temperatureMax) ? d.temperatureMax : 999 } : null;
        box.innerHTML = `<div class="small muted mb">${esc(d.zoneName || '')} · ngưỡng ${rangeText(d.temperatureMin, d.temperatureMax)} · ${U.num(d.measurementCount || 0)} lần đo, <span class="${d.outOfRangeCount ? 'bad' : ''}">${U.num(d.outOfRangeCount || 0)} ngoài ngưỡng</span>${d.bucketMinutes ? ` · gộp mỗi ${U.num(d.bucketMinutes)} phút` : ''}</div>`
          + UI.chart({ type: 'line', title: 'Nhiệt độ ' + (d.zoneName || ''), labels: pts.map(lbl), unit: '°C', band, series: [{ name: 'Nhiệt độ', values: pts.map(p => p.temperatureC) }], h: 300 });
      });
    });
  }
})();
