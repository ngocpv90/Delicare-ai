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
          <div class="row-flex" style="gap:6px;flex-wrap:wrap">${d.periodLocked ? UI.tag('Kỳ đã khóa', 'green') : UI.tag('Kỳ đang mở – số tạm tính cập nhật hằng ngày', 'orange')}${d.hasAmis ? UI.tag('Đã có số AMIS', 'green') : UI.tag('Chưa nhập sổ AMIS', '')}</div></div>
        <div class="rp-cards">${(d.listCard || []).map(card).join('')}</div>
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
    });
  }

  if (APP.dashTabs) APP.dashTabs.push(['pl', 'Kết quả kinh doanh', 'dash-pl', false, render]);
  APP.plView = render;
})();
