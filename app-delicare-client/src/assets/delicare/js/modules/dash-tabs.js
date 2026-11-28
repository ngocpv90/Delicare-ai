/* UI-6.52 (D-139): "Bảng điều hành" (#/dash-exec?tab=overview|kpi|cost&date=yyyy-MM-dd&shift=…) – 3 tab dùng chung một bộ lọc:
 * ô ngày mở lịch + ◀ ▶ + "Hôm nay" (mọi ngày đã qua, tối đa hôm nay + 7) · Ca · Chi nhánh (ngữ cảnh chung ở thanh trên).
 * Mở từ menu (không có ?date) = HÔM NAY; chuyển tab giữ ngày / ca (link tab mang date + shift). Tab theo quyền Xem mục: Tổng quan = dash-exec,
 * Chỉ số kinh doanh = kpi-daily, Giá vốn & lãi gộp = cost-meal (2 mục ẩn khỏi sidebar, giữ dòng quyền). Thân tab = đúng màn cũ (p._embed) – không viết logic thứ hai.
 * Route cũ #/kpi-daily (?view=month), #/kpi-monthly, #/cost-meal (?month=) chuyển sang tab. Ô Ca mờ ở tab Chỉ số / Giá vốn (không chia theo ca – D-139 g).
 * Chế độ DEMO (không có máy chủ) giữ các màn cũ. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const FN = APP.FN || {};
  const S = APP.SCREENS;
  const baseOverview = S['dash-exec'], baseKpi = APP.kpiView || S['kpi-daily'], baseCost = APP.costMealView || S['cost-meal'];
  /** [khóa tab, tên, mục quyền, có chia ca, màn vẽ thân] – RPT-1 thêm tab "Kết quả kinh doanh" qua APP.dashTabs.push. */
  const TABS = APP.dashTabs = [
    ['overview', 'Tổng quan', 'dash-exec', true, (el, p) => baseOverview(el, FN['dash-exec'], p)],
    ['kpi', 'Chỉ số kinh doanh', 'kpi-daily', false, (el, p) => baseKpi(el, FN['kpi-daily'] || FN['dash-exec'], p)],
    ['cost', 'Giá vốn & lãi gộp', 'cost-meal', false, (el, p) => baseCost(el, FN['cost-meal'] || FN['dash-exec'], p)],
  ];
  const DX = { shift: '' };
  const TODAY = () => U.day(U.iso(new Date()));
  const MAXD = () => U.addDays(TODAY(), 7);
  const isDate = d => /^\d{4}-\d{2}-\d{2}$/.test(d || '');
  const DOW = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
  const can = id => Store.perm(id).access !== false && !(id === 'dash-exec' && Store.perm(id).tabsOnly);
  APP.dashTabsFor = () => TABS.filter(t => can(t[2]));
  const href = (tab, date, shift) => `#/dash-exec?tab=${tab}&date=${date}${shift ? '&shift=' + encodeURIComponent(shift) : ''}`;

  /** Bộ lọc ngày dùng lại được: ô ngày (lịch) + ◀ ▶ + Hôm nay; onPick(yyyy-MM-dd). */
  APP.dateBar = (date, opt = {}) => {
    const max = opt.max || MAXD(), dd = new Date(date + 'T00:00:00');
    return `<div class="dx-date" role="group" aria-label="Chọn ngày"><button class="btn sm" data-dxd="-1" aria-label="Ngày trước">◀</button>
      <label class="dx-cal" title="Chọn ngày (tối đa ${U.date(max)})"><span class="dx-dow">${DOW[dd.getDay()]}</span><input type="date" class="input" id="dxDate" value="${esc(date)}" max="${esc(max)}" aria-label="Ngày"><span class="dx-vn">${esc(U.date(date))}</span></label>
      <button class="btn sm" data-dxd="1" aria-label="Ngày sau" ${date >= max ? 'disabled' : ''}>▶</button>${date !== TODAY() ? '<button class="btn sm" data-dxd="0">Hôm nay</button>' : ''}</div>`;
  };
  APP.bindDateBar = (el, date, onPick) => {
    const pick = d => { if (!isDate(d)) return; if (d > MAXD()) { UI.toast('Chỉ chọn được tối đa đến ' + U.date(MAXD()), 'warn'); d = MAXD(); } onPick(d); };
    el.querySelectorAll('[data-dxd]').forEach(b => b.onclick = () => pick(b.dataset.dxd === '0' ? TODAY() : U.addDays(date, +b.dataset.dxd)));
    const inp = el.querySelector('#dxDate'); if (inp) inp.onchange = e => pick(e.target.value);
  };

  APP.register('dash-exec', (el, f, p) => {
    p = p || {};
    if (!Store.isLive) return baseOverview(el, f, p);
    const list = APP.dashTabsFor();
    if (!list.length) return baseOverview(el, f, p);
    let date = isDate(p.date) ? p.date : TODAY();
    if (date > MAXD()) date = MAXD();
    if (p.shift !== undefined) DX.shift = p.shift || '';
    const t = list.find(x => x[0] === p.tab) || list[0];
    if (p.tab && p.tab !== t[0] && TABS.some(x => x[0] === p.tab)) UI.toast('Anh / chị chưa có quyền xem tab này', 'warn');
    const shifts = Store.rows('Shift');
    const branch = Store.ctx.branch ? Store.label('OrganizationUnit', Store.ctx.branch) : 'Toàn công ty';
    el.innerHTML = APP.header(Object.assign({}, f, { name: 'Bảng điều hành', grp: 'Điều hành' }), `<a class="btn" href="#/flow-e2e">${I('flow')} 9 luồng nghiệp vụ</a>`, { noNote: true, title: 'Bảng điều hành', sub: 'Theo dõi toàn bộ hoạt động bếp trung tâm, kho, QC, giao nhận, chỉ số kinh doanh và giá vốn theo ngày / ca / đơn vị.' })
      + `<div class="card dx-bar">${APP.dateBar(date)}
          <label class="dx-f ${t[3] ? '' : 'off'}"><span>Ca</span><select class="select" id="dxShift" ${t[3] ? '' : 'disabled'}><option value="">Tất cả ca</option>${shifts.map(s => `<option value="${s.Id}" ${s.Id === DX.shift ? 'selected' : ''}>${esc(s.Name)}</option>`).join('')}</select>${t[3] ? '' : '<span class="small muted">Không chia theo ca</span>'}</label>
          <span class="dx-f"><span>Chi nhánh</span><b class="small" title="Đổi ở ô Đơn vị làm việc trên thanh trên cùng">${esc(branch)}</b></span></div>
        <nav class="ac-tabs" aria-label="Bảng điều hành">${list.map(x => `<a class="ac-tab ${x === t ? 'on' : ''}" href="${href(x[0], date, DX.shift)}" ${x === t ? 'aria-current="page"' : ''}>${esc(x[1])}</a>`).join('')}</nav><div id="dxBody"></div>`;
    APP.bindDateBar(el, date, d => { location.hash = href(t[0], d, DX.shift); });
    el.querySelector('#dxShift').onchange = e => { DX.shift = e.target.value; location.hash = href(t[0], date, DX.shift); };
    t[4](el.querySelector('#dxBody'), Object.assign({}, p, { _embed: true, _date: date, _shift: t[3] ? DX.shift : '' }));
  });

  // Route cũ → tab (chỉ chế độ có máy chủ; DEMO giữ màn cũ).
  const redirect = (id, tab, base) => APP.register(id, (el, f, p) => {
    p = p || {};
    if (!Store.isLive) return base && base(el, f, p);
    let date = isDate(p.date) ? p.date : '';
    if (!date && /^\d{4}-\d{2}$/.test(p.month || '')) date = p.month + '-01';
    const extra = id === 'kpi-monthly' || p.view === 'month' ? 'month' : p.view === 'day' ? 'day' : '';
    if (extra) try { localStorage.setItem('dc.kpiView', extra); } catch (e) { /* máy chặn lưu */ }
    location.replace('#/dash-exec?tab=' + tab + (date ? '&date=' + date : ''));
  });
  redirect('kpi-daily', 'kpi', S['kpi-daily']);
  redirect('kpi-monthly', 'kpi', S['kpi-monthly']);
  redirect('cost-meal', 'cost', S['cost-meal']);
})();
