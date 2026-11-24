/* UI-6.27 (D-108): "Giá vốn & lãi gộp" (#/cost-meal, nhóm Điều hành) – gộp Giá thành chuẩn · Giá thành thực tế · Giá vốn suất thành 1 báo cáo tháng
 * (mô phỏng docs/frontend-handoff/assets/cost-margin-mockup.html). Kỳ tháng ◀ ▶ + Xuất Excel; 4 thẻ chỉ số; 2 dòng báo (chỉ nguyên liệu · đơn tạm tính);
 * tab Theo khách hàng / hợp đồng (bấm dòng → theo ngày → đơn) · Theo món (chuẩn vs thực tế, nguyên nhân) · Xu hướng theo ngày (Chart.js – UI.combo).
 * API: MealCost/GetMargin (Month, GroupBy, CustomerId, ContractId), MealCost/GetList (chi tiết đơn). Chế độ DEMO giữ màn cũ (production.js). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const nil = v => v === null || v === undefined;
  const st = { month: null, tab: 'customer', data: {} };
  const ym = d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0');
  const shift = (m, n) => { const [y, mo] = m.split('-').map(Number); return ym(new Date(y, mo - 1 + n, 1)); };
  const monthName = m => { const [y, mo] = m.split('-'); return `Tháng ${+mo}/${y}`; };
  const color = (pct, lim) => (nil(pct) ? '' : pct > lim ? 'red' : pct > lim - 5 ? 'orange' : 'green');
  const pctTag = (pct, lim) => (nil(pct) ? '<span class="muted">–</span>' : UI.tag(U.pct(pct), color(pct, lim)));
  const bar = (pct, lim) => (nil(pct) ? '' : `<span class="cm-bar"><i class="${color(pct, lim)}" style="width:${Math.min(100, pct)}%"></i><b style="left:${Math.min(100, lim)}%"></b></span>`);

  function load(group, extra) {
    const key = st.month + '|' + group + '|' + JSON.stringify(extra || {});
    if (st.data[key]) return Promise.resolve(st.data[key]);
    return Store.query('MealCost/GetMargin', Object.assign({ Month: st.month, GroupBy: group }, extra || {})).then(x => { if (x.ok) st.data[key] = x; return x; });
  }

  function head(m) {
    const s = m.summary || {}, lim = m.threshold || m.thresholdPct || 55, lab = m.laborIncluded ? 'Lãi gộp' : 'Lãi sau nguyên liệu';
    const delta = !nil(s.costPerMeal) && !nil(s.prevCostPerMeal) && s.prevCostPerMeal ? (s.costPerMeal - s.prevCostPerMeal) / s.prevCostPerMeal * 100 : null;
    return UI.kpis([
      UI.kpi('Giá vốn nguyên liệu / suất', nil(s.costPerMeal) ? '–' : U.money(s.costPerMeal), nil(delta) ? 'chưa có tháng trước' : `${delta > 0 ? '▲' : '▼'} ${U.pct(Math.abs(delta))} so tháng trước`, 'calc', nil(delta) ? 'blue' : delta > 0 ? 'orange' : 'green'),
      UI.kpi('Tỷ lệ nguyên liệu / doanh thu', nil(s.ratioPct) ? '–' : U.pct(s.ratioPct), 'ngưỡng ≤ ' + U.pct(lim), 'chart', color(s.ratioPct, lim) || 'blue'),
      UI.kpi(lab, U.moneyShort(s.margin || 0), `${U.num(s.meals || 0)} suất · doanh thu ${U.moneyShort(s.revenue || 0)}`, 'chart', (s.margin || 0) >= 0 ? 'green' : 'red'),
      UI.kpi('Chi phí lỗi', U.moneyShort(s.faultCost || 0), `${U.num(s.faultMeals || 0)} suất bù / loại`, 'alert', s.faultCost ? 'orange' : 'green'),
    ]) + (m.laborIncluded ? '' : `<div class="notice info mb">${I('help')}<div>Giá vốn hiện chỉ gồm nguyên liệu – chưa có định mức nhân công / chi phí chung (Kế toán khai cấu hình). Chỉ tiêu "${lab}" = doanh thu − giá vốn nguyên liệu.</div></div>`)
      + (s.provisionalOrders ? `<div class="notice warn mb">${I('alert')}<div>${U.num(s.provisionalOrders)} đơn (${U.pct(s.provisionalMealsPct || 0)} suất) đang tạm tính theo giá thành chuẩn – lệnh sản xuất chưa đóng; hệ thống tự tính lại theo giá thành thực tế khi lệnh đóng.</div></div>` : '');
  }

  function customerTab(box, m) {
    const lim = m.thresholdPct || 55, rows = m.listRow || [], s = m.summary || {};
    box.innerHTML = rows.length ? `<div class="table-wrap cm-wrap"><table class="table dc-table cm-table"><thead><tr><th class="l">Khách hàng · hợp đồng</th><th class="num">Suất nhận</th><th class="num">Giá bán BQ / suất</th><th class="num">Nguyên liệu / suất</th><th class="num">Doanh thu</th><th class="num">Giá vốn NL</th><th class="num">${m.laborIncluded ? 'Lãi gộp' : 'Lãi sau NL'}</th><th>Tỷ lệ NL / DT</th><th class="num">Chi phí lỗi</th></tr></thead><tbody>
      ${rows.map((r, i) => `<tr data-r="${i}" style="cursor:pointer"><td class="l"><b>${esc(r.name || '')}</b><br><span class="small muted">${esc(r.sub || '')}${r.provisionalOrders ? ` · ${r.provisionalOrders} đơn tạm tính` : ''}</span></td><td class="num">${U.num(r.meals)}</td><td class="num">${nil(r.pricePerMeal) ? '–' : U.money(r.pricePerMeal)}</td><td class="num">${nil(r.costPerMeal) ? '–' : U.money(r.costPerMeal)}</td>
        <td class="num">${U.money(r.revenue)}</td><td class="num">${U.money(r.cost)}</td><td class="num">${U.money(r.margin)}</td><td>${pctTag(r.ratioPct, lim)} ${bar(r.ratioPct, lim)}</td><td class="num">${r.faultCost ? U.money(r.faultCost) : '–'}</td></tr>`).join('')}</tbody>
      <tfoot><tr><td class="l">Cả tháng</td><td class="num">${U.num(s.meals || 0)}</td><td class="num">${s.meals ? U.money(Math.round(s.revenue / s.meals)) : '–'}</td><td class="num">${nil(s.costPerMeal) ? '–' : U.money(s.costPerMeal)}</td><td class="num">${U.money(s.revenue || 0)}</td><td class="num">${U.money(s.cost || 0)}</td><td class="num">${U.money(s.margin || 0)}</td><td>${pctTag(s.ratioPct, lim)}</td><td class="num">${s.faultCost ? U.money(s.faultCost) : '–'}</td></tr></tfoot></table></div>`
      : '<div class="muted small">Tháng này chưa có giá vốn suất (đơn chưa đối soát).</div>';
    box.querySelectorAll('[data-r]').forEach(tr => tr.onclick = () => dayBox(rows[+tr.dataset.r], lim));
  }

  function dayBox(r, lim) {
    UI.modal({ title: esc(r.name || '') + ' · ' + esc(r.sub || ''), size: 'lg', body: '<div id="cmDays"><div class="muted small">Đang tải…</div></div>',
      foot: `<button class="btn" data-close>Đóng</button>${r.contractId && !nil(r.ratioPct) && r.ratioPct > lim ? `<button class="btn orange" id="cmAdj">Lập phụ lục điều chỉnh giá</button>` : ''}`,
      onMount: (m, close) => {
        const adj = m.querySelector('#cmAdj'); if (adj) adj.onclick = () => { close(); APP.openDetail ? APP.openDetail('Contract', r.contractId) : (location.hash = '#/crm-contract?id=' + r.contractId); };
        load('day', { CustomerId: r.customerId, ContractId: r.contractId || null }).then(x => {
          const host = m.querySelector('#cmDays'); if (!host) return;
          const days = (x.ok && x.data && x.data.listDay) || [];
          host.innerHTML = days.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Ngày ăn</th><th class="num">Suất</th><th class="num">NL / suất</th><th>Tỷ lệ NL / DT</th><th>Nguồn giá</th></tr></thead><tbody>${days.map((d, i) => `<tr data-d="${i}" style="cursor:pointer"><td>${esc(U.date(d.date))}</td><td class="num">${U.num(d.meals)}</td><td class="num">${d.meals ? U.money(Math.round(d.cost / d.meals)) : '–'}</td><td>${pctTag(d.ratioPct, lim)}</td><td>${d.provisionalOrders ? UI.tag('Tạm tính giá chuẩn', 'orange') : UI.tag('Thực tế', 'green')}</td></tr>`).join('')}</tbody></table></div><div id="cmOrders" class="mt"></div>` : '<div class="muted small">Không có ngày nào</div>';
          host.querySelectorAll('[data-d]').forEach(tr => tr.onclick = () => {
            const d = U.day(days[+tr.dataset.d].date), out = host.querySelector('#cmOrders'); out.innerHTML = '<div class="muted small">Đang tải đơn…</div>';
            Store.query('MealCost/GetList', { From: d, To: d }).then(y => {
              const list = ((y.ok && y.list) || []).filter(o => !r.customerId || o.customerId === r.customerId);
              out.innerHTML = list.length ? list.map(o => `<div class="card panel mb"><div class="spread"><b>${esc(o.orderNumber || '')}</b><span class="small">${U.num(o.acceptedMeals)} suất · giá vốn ${U.money(o.totalActualCost)} · ${o.isFaultCost ? UI.tag('Chi phí lỗi', 'orange') : UI.tag('Suất bán', 'green')}</span></div>
                <div class="table-wrap"><table class="table dc-table"><thead><tr><th>Món</th><th class="num">Phần</th><th class="num">Giá vốn / phần</th><th class="num">Giá vốn</th></tr></thead><tbody>${(o.listLine || []).flatMap(l => l.listDish || []).map(dd => `<tr><td>${esc(dd.dishName || '')}</td><td class="num">${U.num(dd.portions)}</td><td class="num">${nil(dd.costPerPortion) ? '–' : U.money(dd.costPerPortion)}</td><td class="num">${U.money(dd.totalActualCost)}</td></tr>`).join('')}</tbody></table></div></div>`).join('') : '<div class="muted small">Không có đơn</div>';
            });
          });
        });
      } });
  }

  function dishTab(box, m) {
    const rows = m.listDish || [], vc = v => (nil(v) ? '' : Math.abs(v) > 5 ? 'red' : Math.abs(v) > 2 ? 'orange' : 'green');
    box.innerHTML = rows.length ? `<div class="table-wrap cm-wrap"><table class="table dc-table cm-table"><thead><tr><th class="l">Món</th><th class="num">Số phần</th><th class="num">Giá chuẩn / phần</th><th class="num">Thực tế / phần</th><th>Chênh lệch</th><th class="num">Tiền chênh</th><th>Nguyên nhân chính</th></tr></thead><tbody>
      ${rows.map((d, i) => `<tr data-dish="${i}" ${d.lastPlanId ? 'style="cursor:pointer"' : ''}><td class="l"><b>${esc(d.dishName || '')}</b></td><td class="num">${U.num(d.portions)}</td><td class="num">${nil(d.standardPerPortion) ? '<span class="muted">thiếu giá</span>' : U.money(d.standardPerPortion)}</td><td class="num">${nil(d.actualPerPortion) ? '<span class="muted">chưa đóng lệnh</span>' : U.money(d.actualPerPortion)}</td>
        <td>${nil(d.variancePct) ? '–' : UI.tag((d.variancePct > 0 ? '+' : '') + U.pct(d.variancePct), vc(d.variancePct))}</td><td class="num">${nil(d.varianceAmount) ? '–' : U.money(d.varianceAmount)}</td><td class="small">${esc(d.mainCause || '')}</td></tr>`).join('')}</tbody></table></div>`
      : '<div class="muted small">Tháng này chưa có lệnh sản xuất có giá thành.</div>';
    box.querySelectorAll('[data-dish]').forEach(tr => tr.onclick = () => { const d = rows[+tr.dataset.dish]; if (d.lastPlanId) location.hash = '#/prod-plan-detail?id=' + encodeURIComponent(d.lastPlanId); });
  }

  function trendTab(box, m) {
    const days = (m.listDay || []).filter(d => d.revenue > 0), lim = m.thresholdPct || 55;
    if (!days.length) { box.innerHTML = '<div class="muted small">Chưa có dữ liệu theo ngày</div>'; return; }
    const lbl = days.map(d => U.date(d.date).slice(0, 5)), v = days.map(d => d.ratioPct || 0);
    box.innerHTML = UI.combo(lbl, [{ name: 'Trong ngưỡng', type: 'bar', role: 'ok', values: v.map(x => (x <= lim ? x : 0)) }, { name: 'Vượt ngưỡng', type: 'bar', role: 'bad', values: v.map(x => (x > lim ? x : 0)) },
      { name: 'Ngưỡng ' + U.pct(lim), type: 'line', role: 'plan', values: v.map(() => lim) }], { title: 'Tỷ lệ nguyên liệu / doanh thu theo ngày (%)', unit: '%', stacked: true, labelHead: 'Ngày' })
      + `<div class="small muted mt">${days.filter(d => d.ratioPct > lim).length} / ${days.length} ngày vượt ngưỡng ${U.pct(lim)}.</div>`;
  }

  function exportCsv(m) {
    const rows = m.listRow || [];
    U.csv(rows, [{ label: 'Khách hàng', k: 'name' }, { label: 'Hợp đồng', k: 'sub' }, { label: 'Suất nhận', k: 'meals' }, { label: 'Giá bán BQ / suất', k: 'pricePerMeal' }, { label: 'Nguyên liệu / suất', k: 'costPerMeal' },
      { label: 'Doanh thu', k: 'revenue' }, { label: 'Giá vốn NL', k: 'cost' }, { label: m.laborIncluded ? 'Lãi gộp' : 'Lãi sau NL', k: 'margin' }, { label: 'Tỷ lệ NL / DT (%)', k: 'ratioPct' }, { label: 'Chi phí lỗi', k: 'faultCost' }],
      'Gia-von-lai-gop-' + st.month + '.csv');
  }

  const base = APP.SCREENS['cost-meal'];
  APP.register('cost-meal', (el, f, p) => {
    if (!Store.isLive) return base && base(el, f, p);
    if (!st.month) st.month = ym(new Date());
    if (p && ['customer', 'dish', 'trend'].includes(p.tab)) st.tab = p.tab;
    if (p && /^\d{4}-\d{2}$/.test(p.month || '')) st.month = p.month;
    el.innerHTML = APP.header(f, `<button class="btn" id="cmXls">${I('export')} Xuất Excel</button>`, { noNote: true })
      + `<div class="small muted mb">Biết suất ăn của từng khách / món đang lãi bao nhiêu sau nguyên liệu – để điều chỉnh giá hợp đồng hoặc thực đơn kịp thời.</div>
      <div class="card panel mb"><div class="fw-week"><button class="btn sm" id="cmPrev" aria-label="Tháng trước">◀</button><span>${monthName(st.month)}${st.month === ym(new Date()) ? ' <span class="small muted" style="font-weight:400">(tính đến hôm nay)</span>' : ''}</span><button class="btn sm" id="cmNext" aria-label="Tháng sau">▶</button></div></div>
      <div id="cmHead"><div class="muted small">Đang tính…</div></div>
      <div class="card panel"><div class="tabs mb">${[['customer', 'Theo khách hàng / hợp đồng'], ['dish', 'Theo món'], ['trend', 'Xu hướng theo ngày']].map(([k, l]) => `<button class="tab ${st.tab === k ? 'active' : ''}" data-cmt="${k}">${l}</button>`).join('')}</div><div id="cmBody"><div class="muted small">Đang tải…</div></div></div>`;
    const go = n => { st.month = shift(st.month, n); APP.SCREENS['cost-meal'](el, f, {}); };
    el.querySelector('#cmPrev').onclick = () => go(-1);
    el.querySelector('#cmNext').onclick = () => go(1);
    el.querySelectorAll('[data-cmt]').forEach(b => b.onclick = () => { st.tab = b.dataset.cmt; APP.SCREENS['cost-meal'](el, f, {}); });
    const group = st.tab === 'dish' ? 'dish' : st.tab === 'trend' ? 'day' : 'customer';
    load(group).then(x => {
      if (!el.isConnected) return;
      const body = el.querySelector('#cmBody'), hd = el.querySelector('#cmHead');
      if (!x.ok || !x.data) { hd.innerHTML = ''; body.innerHTML = `<div class="muted small">${esc((x.res && x.res.responseMessage) || 'Không tải được báo cáo')}</div>`; return; }
      const m = x.data; hd.innerHTML = head(m);
      (st.tab === 'dish' ? dishTab : st.tab === 'trend' ? trendTab : customerTab)(body, m);
      el.querySelector('#cmXls').onclick = () => (st.tab === 'customer' ? exportCsv(m) : load('customer').then(y => y.ok && exportCsv(y.data)));
    });
  });
})();
