/* RPT-1 (D-140): tab "Kết quả kinh doanh" trong Bảng điều hành (#/dash-exec?tab=pl&date=…) – mục quyền ẩn dash-pl (CEO, COO, FIN-IT, FIN-ACC).
 * Tháng = tháng của ngày đang chọn ở bộ lọc chung (D-139); ô Ca mờ. Lớp số: Tạm tính (Delicare – cập nhật hằng ngày) / Chính thức (AMIS – sau khi nhập sổ).
 * Một đơn vị hiển thị: triệu đồng (trđ, 1 chữ số thập phân) – rê chuột xem số đầy đủ (D-140 f). API: Dashboard/GetPl(month, branchId). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const nil = v => v === null || v === undefined;
  const trd = v => (nil(v) ? '–' : U.num(v / 1e6, 1));
  const full = v => (nil(v) ? '' : U.num(Math.round(v)) + ' đ');
  const MEAL = { SANG: 'Sáng', TRUA: 'Trưa', TOI: 'Tối', DEM: 'Đêm', KHAC: 'Khác' };
  const monthName = m => `${+m.slice(5)}/${m.slice(0, 4)}`;
  const valTxt = c => (nil(c.value) ? '' : c.unit === 'PCT' ? U.num(c.value, 1) + '%' : c.unit === 'MEAL' ? U.num(c.value) : trd(c.value));
  /** So sánh: ▲ / ▼ % (đổi màu theo tốt / xấu); % chỉ tiêu tỷ lệ thì so điểm %. */
  function delta(c, base, label) {
    if (nil(c.value) || nil(base)) return `<span class="muted">${esc(label)}: –</span>`;
    const d = c.unit === 'PCT' ? c.value - base : base === 0 ? null : (c.value - base) / Math.abs(base) * 100;
    if (nil(d)) return `<span class="muted">${esc(label)}: –</span>`;
    const good = c.lowerIsBetter ? d < 0 : d > 0, cls = Math.abs(d) < 0.05 ? 'muted' : good ? 'up' : 'warn';
    return `<span class="${cls}" title="${esc(label)}: ${esc(c.unit === 'PCT' ? U.num(base, 1) + '%' : c.unit === 'MEAL' ? U.num(base) + ' suất' : full(base))}">${d > 0 ? '▲' : d < 0 ? '▼' : '='} ${U.num(Math.abs(d), 1)}${c.unit === 'PCT' ? ' điểm' : '%'} ${esc(label)}</span>`;
  }
  function card(c) {
    const amis = c.source === 'AMIS';
    return `<div class="card rp-card ${amis ? 'amis' : ''}"><div class="spread"><span class="rp-n">${esc(c.name)}</span><span class="rp-src ${amis ? 'amis' : ''}">${amis ? 'Chính thức · AMIS' : 'Tạm tính'}</span></div>
      ${nil(c.value) ? `<div class="rp-v muted">${amis ? 'Chờ số AMIS' : '–'}</div>` : `<div class="rp-v" title="${esc(c.unit === 'VND' ? full(c.value) : valTxt(c))}">${esc(valTxt(c))}${c.unit === 'VND' ? '<small> trđ</small>' : c.unit === 'MEAL' ? '<small> suất</small>' : ''}</div>`}
      <div class="rp-d small">${delta(c, c.prev, 'so tháng trước')}<br>${delta(c, c.yearAgo, 'cùng kỳ')}</div>${c.note ? `<div class="small muted">${esc(c.note)}</div>` : ''}</div>`;
  }

  function render(el, p) {
    const date = (p && p._date) || U.day(U.iso(new Date())), month = date.slice(0, 7);
    el.innerHTML = `<div class="card panel empty">${I('chart')}<h2 class="mt">Đang tính kết quả kinh doanh tháng ${esc(monthName(month))}…</h2></div>`;
    Store.query('Dashboard/GetPl', { month, branchId: Store.ctx.branch || null }).then(x => {
      if (!el.isConnected) return;
      if (!x.ok || !x.data) { el.innerHTML = `<div class="notice warn">${I('alert')}<div>${esc(x.message || 'Không tải được kết quả kinh doanh')}</div></div>`; return; }
      const d = x.data, rows = d.listRevenue || [], meals = d.meals || [];
      // Gọn: 12 nhóm lớn nhất + 1 dòng "N nhóm khác" (Pareto vẫn tính trên toàn bộ nhóm).
      const TOP = 12, allG = d.listGroup || [], rest = allG.slice(TOP);
      const groups = rest.length ? allG.slice(0, TOP).concat([{ group: rest.length + ' nhóm khác', value: U.sum(rest, 'value'), prev: U.sum(rest, 'prev'), cumPct: 100, other: true }]) : allG;
      const allM = d.listGroupMonth || [], restM = allM.slice(15);
      const gm = restM.length ? allM.slice(0, 15).concat([{ group: restM.length + ' nhóm khác', values: (d.months || []).map((m, i) => U.sum(restM.map(g => ({ v: g.values[i] })), 'v')) }]) : allM;
      const totRev = U.sum(rows, 'total'), unconf = rows.filter(r => r.unconfirmed);
      el.innerHTML = `<div class="spread mb" style="flex-wrap:wrap;gap:8px"><div><b>Kết quả kinh doanh tháng ${esc(monthName(d.month))}</b> <span class="small muted">· so tháng ${esc(monthName(d.prevMonth))} và cùng kỳ ${esc(monthName(d.yearAgoMonth))} · đơn vị triệu đồng (rê chuột xem số đủ)</span></div>
          <div class="row-flex" style="gap:6px;flex-wrap:wrap">${d.periodLocked ? UI.tag('Kỳ đã khóa', 'green') : UI.tag('Kỳ đang mở – số tạm tính cập nhật hằng ngày', 'orange')}${d.hasAmis ? UI.tag('Đã có số AMIS', 'green') : UI.tag('Chưa nhập sổ AMIS', '')}
            <button class="btn sm" id="rpAmis">${I('database')} Số AMIS tháng</button>${Store.perm('dash-pl').imp ? `<button class="btn sm primary" id="rpImp">${I('upload')} Nhập sổ AMIS</button>` : ''}<button class="btn sm" id="rpRules">${I('settings')} Quy tắc phân loại</button></div></div>
        <div class="rp-cards">${(d.listCard || []).map(card).join('')}</div>
        <div id="rpRep" class="mt"></div>
        <div class="rp-grid mt">
          <div class="card panel"><div class="panel-head"><div><div class="panel-title">Doanh thu theo khách × bữa</div><div class="panel-desc">Giá trị suất đã giao (đơn × đơn giá hợp đồng); khách chưa xác nhận bảng kê tháng ghi "chưa xác nhận"</div></div><b title="${esc(full(totRev))}">${trd(totRev)} trđ</b></div>
            ${rows.length ? `<div class="table-wrap"><table class="table dc-table rp-t"><thead><tr><th>Khách hàng</th>${meals.map(m => `<th class="num">${esc(MEAL[m] || m)}</th>`).join('')}<th class="num">Suất</th><th class="num">Tổng</th></tr></thead><tbody>
              ${rows.map(r => `<tr><td><b>${esc(r.customerName || '')}</b>${r.unconfirmed ? ' ' + UI.tag('chưa xác nhận', 'orange') : ''}</td>${meals.map(m => `<td class="num" title="${esc(full(r.byMeal[m] || 0))}">${r.byMeal[m] ? trd(r.byMeal[m]) : '–'}</td>`).join('')}<td class="num">${U.num(r.meals)}</td><td class="num" title="${esc(full(r.total))}"><b>${trd(r.total)}</b></td></tr>`).join('')}</tbody>
              <tfoot><tr><td>Cả tháng${unconf.length ? ` <span class="small muted">(${unconf.length} khách chưa xác nhận)</span>` : ''}</td>${meals.map(m => `<td class="num">${trd(d.revenueByMeal[m] || 0)}</td>`).join('')}<td class="num">${U.num(U.sum(rows, 'meals'))}</td><td class="num"><b>${trd(totRev)}</b></td></tr></tfoot></table></div>
              <div class="small muted mt">${esc(d.customerGroupNote || '')}</div>` : '<div class="muted small">Tháng này chưa có doanh thu suất (đơn chưa đối soát / chưa có giá vốn suất).</div>'}</div>
          <div class="card panel"><div class="panel-head"><div><div class="panel-title">Doanh thu theo bữa</div><div class="panel-desc">Tỷ trọng các bữa trong tháng</div></div></div>
            ${UI.chart({ type: 'donut', title: 'Doanh thu theo bữa', unit: 'đ', series: meals.map(m => ({ l: MEAL[m] || m, v: d.revenueByMeal[m] || 0 })), h: 220 })}</div>
        </div>
        <div class="rp-grid eq mt">
          <div class="card panel"><div class="panel-head"><div><div class="panel-title">Nguyên vật liệu theo nhóm</div><div class="panel-desc">Xuất kho × giá lô (không gồm bao bì) – tháng ${esc(monthName(d.month))} so tháng ${esc(monthName(d.prevMonth))}</div></div></div>
            ${groups.length ? UI.chart({ type: 'hbar', title: 'NVL theo nhóm', unit: 'đ', labels: groups.map(g => g.group), series: [{ name: 'Tháng ' + monthName(d.month), values: groups.map(g => g.value), role: 'actual' }, { name: 'Tháng ' + monthName(d.prevMonth), values: groups.map(g => g.prev), role: 'plan' }], h: Math.max(200, groups.length * 30) }) : '<div class="muted small">Chưa có phiếu xuất kho trong 2 tháng.</div>'}</div>
          <div class="card panel"><div class="panel-head"><div><div class="panel-title">Pareto nhóm NVL</div><div class="panel-desc">Nhóm nào chiếm phần lớn chi phí tháng ${esc(monthName(d.month))}</div></div></div>
            ${groups.length ? `<div class="table-wrap"><table class="table dc-table rp-t"><thead><tr><th>Nhóm</th><th class="num">Tháng này</th><th class="num">Tháng trước</th><th class="num">± %</th><th class="num">% lũy kế</th></tr></thead><tbody>
              ${groups.map(g => { const ch = g.prev ? (g.value - g.prev) / g.prev * 100 : null; return `<tr class="${g.cumPct <= 80 && !g.other ? '' : 'muted'}"><td>${esc(g.group)}</td><td class="num" title="${esc(full(g.value))}">${trd(g.value)}</td><td class="num" title="${esc(full(g.prev))}">${trd(g.prev)}</td><td class="num ${nil(ch) ? '' : ch > 0 ? 'warn' : 'up'}">${nil(ch) ? '–' : (ch > 0 ? '+' : '') + U.num(ch, 1)}</td><td class="num">${U.num(g.cumPct, 1)}%</td></tr>`; }).join('')}</tbody></table></div><div class="small muted mt">Dòng đậm = nhóm nằm trong 80% chi phí.</div>` : '<div class="muted small">–</div>'}</div>
        </div>
        <div class="card panel mt"><div class="panel-head"><div><div class="panel-title">Nguyên vật liệu theo nhóm – 12 tháng</div><div class="panel-desc">Triệu đồng · xuất kho × giá lô</div></div></div>
          ${gm.length ? `<div class="table-wrap"><table class="table dc-table rp-t rp-12"><thead><tr><th>Nhóm</th>${d.months.map(m => `<th class="num">${esc(m.slice(5))}/${esc(m.slice(2, 4))}</th>`).join('')}</tr></thead><tbody>
            ${gm.map(g => `<tr><td>${esc(g.group)}</td>${g.values.map(v => `<td class="num" title="${esc(full(v))}">${v ? trd(v) : '–'}</td>`).join('')}</tr>`).join('')}</tbody>
            <tfoot><tr><td>Tổng</td>${d.months.map((m, i) => `<td class="num">${trd(U.sum(allM.map(g => ({ v: g.values[i] })), 'v'))}</td>`).join('')}</tr></tfoot></table></div>` : '<div class="muted small">Chưa có dữ liệu 12 tháng.</div>'}</div>
        <div class="notice info mt">${I('help')}<div><b>Phần AMIS</b> (nhân công, điện / nước / gas, khấu hao, chi phí bán hàng & quản lý, thu nhập / chi phí khác, lợi nhuận trước thuế, ROA / ROE) có khi Kế toán nhập sổ AMIS của tháng; trước đó các thẻ ghi "Chờ số AMIS".</div></div>`;
      report(el.querySelector('#rpRep'), d.month);
      el.querySelector('#rpAmis').onclick = () => amisView(d.month);
      el.querySelector('#rpRules').onclick = () => rulesEdit();
      const ib = el.querySelector('#rpImp'); if (ib) ib.onclick = () => importAmis(d.month, () => render(el, p));
    });
  }

  // ---------- RPT-1.3 (D-140 f, i): báo cáo KQKD chi tiết Tháng | Lũy kế năm, theo quý, 12 biểu đồ xu hướng, cơ cấu giá vốn ----------
  const rs = { ytd: false, layer: 'AMIS' };
  const pctOf = (a, b) => (nil(a) || nil(b) || !b ? null : a / b * 100);
  function report(host, month) {
    host.innerHTML = '<div class="card panel"><div class="muted small">Đang tải báo cáo kết quả kinh doanh…</div></div>';
    Store.query('Dashboard/GetPlReport', { month }).then(x => {
      if (!host.isConnected) return;
      if (!x.ok || !x.data) { host.innerHTML = `<div class="notice warn">${esc(x.message || 'Không tải được báo cáo')}</div>`; return; }
      const d = x.data, ser = d.series || {}, mlab = d.months.map(m => m.slice(5) + '/' + m.slice(2, 4));
      const S = k => ser[k] || d.months.map(() => null);
      const div = (a, b) => a.map((v, i) => pctOf(v, b[i]));
      const amis = rs.layer === 'AMIS';
      const rev = S(amis ? 'AMIS_REVENUE' : 'EST_PL_EST_REVENUE');
      const charts = amis ? [
        ['Doanh thu', S('AMIS_REVENUE'), 'đ', 'deliver'], ['Giá vốn hàng bán', S('AMIS_COGS'), 'đ', 'actual'], ['Lợi nhuận gộp', S('AMIS_GROSS_PROFIT'), 'đ', 'ok'],
        ['Chi phí bán hàng & quản lý', S('AMIS_SELLING_ADMIN'), 'đ', 'actual'], ['Lợi nhuận thuần', S('AMIS_NET_OPERATING'), 'đ', 'ok'], ['Lợi nhuận trước thuế', S('AMIS_PROFIT_BEFORE_TAX'), 'đ', 'ok'],
        ['% GVHB / doanh thu', div(S('AMIS_COGS'), rev), '%', 'over'], ['Biên lợi nhuận gộp', div(S('AMIS_GROSS_PROFIT'), rev), '%', 'ok'], ['% CP bán hàng & quản lý / DT', div(S('AMIS_SELLING_ADMIN'), rev), '%', 'actual'],
        ['Biên lợi nhuận trước thuế', div(S('AMIS_PROFIT_BEFORE_TAX'), rev), '%', 'ok'], ['ROA (theo tháng)', S('AMIS_ROA'), '%', 'deliver'], ['ROE (theo tháng)', S('AMIS_ROE'), '%', 'deliver'],
      ] : [
        ['Doanh thu (tạm tính)', S('EST_PL_EST_REVENUE'), 'đ', 'deliver'], ['Nguyên vật liệu (tạm tính)', S('EST_PL_EST_FOOD_COST'), 'đ', 'actual'], ['Lãi gộp theo NVL', S('EST_PL_EST_FOOD_MARGIN'), 'đ', 'ok'],
        ['% NVL / doanh thu', S('EST_PL_EST_FOOD_PCT'), '%', 'over'], ['Bao bì', S('EST_PL_EST_PACKAGING'), 'đ', 'actual'], ['Số suất', S('EST_PL_EST_MEALS'), 'suất', 'deliver'],
      ];
      const has = arr => arr.some(v => !nil(v));
      const fmtPct = v => (nil(v) ? '–' : U.num(v, 1) + '%');
      const st = d.statement || [], col = rs.ytd ? 'ytd' : 'month', revRow = st.find(l => l.code === 'REVENUE');
      const qs = d.quarters || [];
      host.innerHTML = `<div class="card panel"><div class="panel-head" style="flex-wrap:wrap;gap:8px"><div><div class="panel-title">Báo cáo kết quả kinh doanh</div><div class="panel-desc">Tháng ${esc(monthName(d.month))} · lũy kế năm ${d.year} · triệu đồng</div></div>
          <div class="row-flex" style="gap:8px;flex-wrap:wrap"><div class="seg"><button class="seg-b ${rs.ytd ? '' : 'on'}" data-ytd="0">Tháng</button><button class="seg-b ${rs.ytd ? 'on' : ''}" data-ytd="1">Lũy kế năm</button></div>
            <div class="seg"><button class="seg-b ${amis ? 'on' : ''}" data-ly="AMIS">Chính thức · AMIS</button><button class="seg-b ${amis ? '' : 'on'}" data-ly="EST">Tạm tính</button></div></div></div>
        ${amis ? (d.hasAmis || st.some(l => !nil(l.ytd)) ? `<div class="table-wrap"><table class="table dc-table rp-t rp-st"><thead><tr><th>Chỉ tiêu</th><th class="num">${rs.ytd ? 'Lũy kế ' + d.year : 'Tháng ' + esc(monthName(d.month))}</th><th class="num">% doanh thu</th>${rs.ytd ? '' : '<th class="num">Tháng trước</th>'}</tr></thead><tbody>
          ${st.filter(l => l.level < 2 || !nil(l[col])).map(l => { const v = l[col], ratio = l.code === 'ROA' || l.code === 'ROE'; return `<tr class="lv${l.level}"><td>${esc(l.name)}</td><td class="num" title="${esc(ratio ? '' : full(v))}">${ratio ? (rs.ytd ? '–' : fmtPct(v)) : trd(v)}</td><td class="num">${ratio || nil(v) ? '' : fmtPct(pctOf(v, revRow && revRow[col]))}</td>${rs.ytd ? '' : `<td class="num">${ratio ? fmtPct(l.prevMonth) : trd(l.prevMonth)}</td>`}</tr>`; }).join('')}</tbody></table></div>`
          : '<div class="notice info">Tháng này chưa nhập sổ AMIS – bấm <b>Nhập sổ AMIS</b> (Kế toán).</div>') : '<div class="notice info small">Lớp Tạm tính: số Delicare tự tính (doanh thu theo bảng kê / đơn, nguyên vật liệu, bao bì), chụp hằng đêm – xem biểu đồ bên dưới; báo cáo KQKD đầy đủ chỉ có ở lớp Chính thức.</div>'}
        ${amis ? `<div class="panel-title mt">Theo quý – năm ${d.year}</div><div class="table-wrap"><table class="table dc-table rp-t"><thead><tr><th>Chỉ tiêu</th>${qs.map(q => `<th class="num">${q.quarter}${q.monthsWithData && q.monthsWithData < 3 ? ` <span class="muted small">(${q.monthsWithData} th)</span>` : ''}</th>`).join('')}</tr></thead><tbody>
          ${[['revenue', 'Doanh thu'], ['cogs', 'Giá vốn hàng bán'], ['grossProfit', 'Lợi nhuận gộp'], ['sellingAdmin', 'Chi phí bán hàng & quản lý'], ['netOperating', 'Lợi nhuận thuần'], ['profitBeforeTax', 'Lợi nhuận trước thuế']]
            .map(([k, n]) => `<tr class="${k === 'revenue' || k === 'profitBeforeTax' ? 'lv0' : ''}"><td>${n}</td>${qs.map(q => `<td class="num" title="${esc(full(q[k]))}">${trd(q[k])}</td>`).join('')}</tr>`).join('')}</tbody></table></div>` : ''}
        <div class="panel-title mt">Xu hướng 12 tháng ${amis ? '(Chính thức · AMIS)' : '(Tạm tính)'}</div>
        <div class="rp-charts">${charts.map(([t, vals, unit, role]) => `<div class="rp-ch"><div class="small"><b>${esc(t)}</b></div>${has(vals) ? UI.chart({ type: 'line', title: t, labels: mlab, unit, series: [{ name: t, values: unit === 'đ' ? vals.map(v => nil(v) ? null : Math.round(v / 1e5) / 10) : vals.map(v => nil(v) ? null : Math.round(v * 10) / 10), role }], h: 150, legend: false }) : '<div class="muted small rp-empty">Chưa có số</div>'}</div>`).join('')}</div>
        <div class="small muted">Đơn vị biểu đồ tiền: triệu đồng.</div>
        ${amis && (d.cogsMix || []).length ? `<div class="rp-grid eq mt"><div><div class="panel-title">Cơ cấu giá vốn tháng ${esc(monthName(d.month))}</div>${UI.chart({ type: 'donut', title: 'Cơ cấu giá vốn', unit: 'đ', series: d.cogsMix.slice(0, 10).map(i => ({ l: i.item, v: i.value })).concat(d.cogsMix.length > 10 ? [{ l: (d.cogsMix.length - 10) + ' khoản khác', v: U.sum(d.cogsMix.slice(10), 'value') }] : []), h: 240 })}</div>
          <div class="table-wrap"><table class="table dc-table rp-t"><thead><tr><th>Khoản mục</th><th class="num">trđ</th><th class="num">% GVHB</th></tr></thead><tbody>${d.cogsMix.slice(0, 15).map(i => `<tr><td>${esc(i.item)} <span class="muted small">${esc(i.account)}</span></td><td class="num" title="${esc(full(i.value))}">${trd(i.value)}</td><td class="num">${fmtPct(pctOf(i.value, U.sum(d.cogsMix, 'value')))}</td></tr>`).join('')}</tbody></table></div></div>` : ''}</div>`;
      host.querySelectorAll('[data-ytd]').forEach(b => b.onclick = () => { rs.ytd = b.dataset.ytd === '1'; report(host, month); });
      host.querySelectorAll('[data-ly]').forEach(b => b.onclick = () => { rs.layer = b.dataset.ly; report(host, month); });
    });
  }

  // ---------- RPT-1.2 (D-140 d): nhập sổ AMIS · xem số AMIS tháng · bảng quy tắc phân loại ----------
  const ACC = { 511: 'Doanh thu', 621: 'NVL trực tiếp', 622: 'Nhân công trực tiếp', 627: 'Sản xuất chung', 641: 'Bán hàng', 642: 'Quản lý doanh nghiệp', 711: 'Thu nhập khác', 811: 'Chi phí khác' };
  const MAIN = [['REVENUE', 'Doanh thu'], ['COGS', 'Giá vốn hàng bán'], ['GROSS_PROFIT', 'Lợi nhuận gộp'], ['SELLING_ADMIN', 'Chi phí bán hàng & quản lý'], ['NET_OPERATING', 'Lợi nhuận thuần'],
    ['OTHER_INCOME', 'Thu nhập khác'], ['OTHER_EXPENSE', 'Chi phí khác'], ['PROFIT_BEFORE_TAX', 'Lợi nhuận trước thuế'], ['ROA', 'ROA (theo tháng)'], ['ROE', 'ROE (theo tháng)']];
  function monthBody(d) {
    const mt = d.metrics || {}, items = d.listItem || [], un = d.listUnclassified || [];
    if (!Object.keys(mt).length && !items.length) return '<div class="muted">Tháng này chưa nhập sổ AMIS.</div>';
    return `<div class="small muted mb">${d.importedAt ? 'Nhập lúc ' + esc(U.dt(d.importedAt)) + (d.importedBy ? ' · ' + esc(d.importedBy) : '') : ''}${d.accounts && d.accounts.length ? ' · sổ: ' + esc(d.accounts.join(', ')) : ''}${d.periodLocked ? ' · kỳ đã khóa' : ''}</div>
      <div class="table-wrap"><table class="table dc-table"><tbody>${MAIN.filter(([k]) => mt[k] !== undefined).map(([k, n]) => `<tr><td>${esc(n)}</td><td class="num"><b>${k === 'ROA' || k === 'ROE' ? U.num(mt[k], 2) + '%' : U.num(Math.round(mt[k])) + ' đ'}</b></td></tr>`).join('')}</tbody></table></div>
      ${Object.keys(ACC).filter(a => items.some(i => i.account === a)).map(a => `<details class="mt"><summary><b>${a} ${esc(ACC[a])}</b> · ${U.num(Math.round(U.sum(items.filter(i => i.account === a), 'value')))} đ</summary><div class="table-wrap"><table class="table dc-table"><tbody>${items.filter(i => i.account === a).map(i => `<tr><td>${esc(i.item)}</td><td class="num">${U.num(Math.round(i.value))}</td></tr>`).join('')}</tbody></table></div></details>`).join('')}
      ${un.length ? `<div class="notice warn mt">${I('alert')}<div><b>${un.length} nhóm dòng chưa phân loại</b> (đang tính vào "khác") – thêm quy tắc ở <b>Quy tắc phân loại</b> rồi nhập lại sổ.</div></div><div class="table-wrap"><table class="table dc-table"><thead><tr><th>TK</th><th>Diễn giải</th><th>TK đối ứng</th><th class="num">Số dòng</th><th class="num">Số tiền</th></tr></thead><tbody>${un.slice(0, 200).map(u => `<tr><td>${esc(u.account)}</td><td>${esc(u.description || '')}</td><td>${esc(u.counterAccount || '')}</td><td class="num">${u.count}</td><td class="num">${U.num(Math.round(u.amount))}</td></tr>`).join('')}</tbody></table></div>` : '<div class="small muted mt">Mọi dòng đã phân loại.</div>'}`;
  }
  function amisView(month) {
    UI.modal({ title: 'Số AMIS tháng ' + esc(monthName(month)), size: 'lg', body: '<div id="rpAm" class="muted small">Đang tải…</div>', foot: '<button class="btn" data-close>Đóng</button>',
      onMount: m => Store.query('Dashboard/GetAmisMonth', { month }).then(x => { const b = m.querySelector('#rpAm'); if (b) b.innerHTML = x.ok && x.data ? monthBody(x.data) : esc(x.message || 'Không tải được'); }) });
  }
  function importAmis(month, done) {
    UI.modal({ title: 'Nhập sổ AMIS', size: 'lg',
      body: `<p class="small">Chọn cùng lúc các tệp Excel xuất từ AMIS: <b>Sổ chi tiết các tài khoản</b> 511, 621, 622, 627, 641, 642, 711, 811 (mỗi tài khoản 1 tệp, đúng mẫu AMIS – có dòng "Tài khoản: 627, Tháng 8 năm 2026") và <b>Báo cáo tài chính</b> (sheet B01-DN) để tính ROA / ROE. Tháng lấy từ sổ; nhập lại = ghi đè số của các tài khoản có trong lần nhập (không nhân đôi).</p>
        <input type="file" class="input" id="rpFiles" multiple accept=".xlsx"><div id="rpRes" class="mt"></div>`,
      foot: '<button class="btn" data-close>Đóng</button><button class="btn primary" id="rpGo">Nhập</button>',
      onMount: m => {
        m.querySelector('#rpGo').onclick = () => {
          const files = [...m.querySelector('#rpFiles').files]; if (!files.length) { UI.toast('Chọn tệp sổ AMIS', 'warn'); return; }
          const fd = new FormData(); files.forEach(f => fd.append('files', f));
          const btn = m.querySelector('#rpGo'); btn.disabled = true; m.querySelector('#rpRes').innerHTML = '<div class="muted small">Đang nhập…</div>';
          Store.act('Dashboard/ImportAmis', { json: fd }).then(x => {
            btn.disabled = false;
            if (!x.ok) { m.querySelector('#rpRes').innerHTML = `<div class="notice warn">${esc(x.message || 'Không nhập được')}</div>`; return; }
            UI.toast(esc((x.res && x.res.responseMessage) || 'Đã nhập'), 'ok');
            m.querySelector('#rpRes').innerHTML = monthBody(x.data || {}); done();
          });
        };
      } });
  }
  function rulesEdit() {
    const canEdit = Store.perm('dash-pl').edit === true;
    Store.query('Dashboard/GetPlRules').then(x => {
      if (!x.ok) return;
      const rows = (x.list || []).map(r => Object.assign({}, r));
      const tbl = () => `<div class="table-wrap" style="max-height:60vh"><table class="table dc-table"><thead><tr><th>TK</th><th>Từ khóa trong diễn giải (không dấu, cách nhau |)</th><th>hoặc TK đối ứng</th><th>Khoản mục</th>${canEdit ? '<th></th>' : ''}</tr></thead><tbody>${rows.map((r, i) => canEdit
        ? `<tr><td><select class="select" data-f="account" data-i="${i}">${Object.keys(ACC).map(a => `<option ${a === r.account ? 'selected' : ''}>${a}</option>`).join('')}</select></td><td><input class="input" data-f="keyword" data-i="${i}" value="${esc(r.keyword || '')}"></td><td><input class="input" style="width:90px" data-f="counter" data-i="${i}" value="${esc(r.counter || '')}"></td><td><input class="input" data-f="item" data-i="${i}" value="${esc(r.item || '')}"></td><td><button class="btn sm" data-del="${i}" aria-label="Xóa dòng">✕</button></td></tr>`
        : `<tr><td>${esc(r.account)}</td><td>${esc(r.keyword || '')}</td><td>${esc(r.counter || '')}</td><td>${esc(r.item)}</td></tr>`).join('')}</tbody></table></div>`;
      UI.modal({ title: 'Quy tắc phân loại khoản mục (sổ AMIS)', size: 'lg',
        body: `<p class="small muted">Mỗi dòng sổ lấy quy tắc <b>đầu tiên</b> khớp: cùng tài khoản và (TK đối ứng bắt đầu bằng … hoặc diễn giải chứa nguyên từ / cụm từ). Không khớp → "khác / chưa phân loại". Sửa xong nhập lại sổ để phân loại lại.</p><div id="rpRt">${tbl()}</div>`,
        foot: '<button class="btn" data-close>Đóng</button>' + (canEdit ? '<button class="btn" id="rpAdd">+ Thêm dòng</button><button class="btn primary" id="rpSave">Lưu quy tắc</button>' : ''),
        onMount: (m, close) => {
          const bind = () => {
            m.querySelectorAll('[data-f]').forEach(i => { i.oninput = i.onchange = () => { rows[+i.dataset.i][i.dataset.f] = i.value; }; });
            m.querySelectorAll('[data-del]').forEach(b => { b.onclick = () => { rows.splice(+b.dataset.del, 1); m.querySelector('#rpRt').innerHTML = tbl(); bind(); }; });
          };
          bind(); if (!canEdit) return;
          m.querySelector('#rpAdd').onclick = () => { rows.unshift({ account: '621', keyword: '', counter: '', item: '' }); m.querySelector('#rpRt').innerHTML = tbl(); bind(); };
          m.querySelector('#rpSave').onclick = () => Store.act('Dashboard/SavePlRules', { json: rows }).then(r => { if (r.ok) { UI.toast(esc((r.res && r.res.responseMessage) || 'Đã lưu'), 'ok'); close(); } });
        } });
    });
  }

  if (APP.dashTabs) APP.dashTabs.push(['pl', 'Kết quả kinh doanh', 'dash-pl', false, render]);
  APP.plView = render;
})();
