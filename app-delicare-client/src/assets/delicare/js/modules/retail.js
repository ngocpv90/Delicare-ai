/* RETAIL-1 (anh chốt 2026-11-26): nhóm menu "Bán lẻ" – tách hẳn đơn doanh nghiệp (Order / hợp đồng).
 * A. Đơn khách lẻ (#/sal-retail-order): đơn khách đặt trên trang khách lẻ (kênh Web) + đơn nhân viên lập khi khách gọi / nhắn Zalo.
 *    Chip trạng thái có số đếm (mặc định "Đang xử lý" = Mới + Đã xác nhận + Đang giao), lọc ngày nhận / kênh / tìm, 4 chỉ số,
 *    nút bước tiếp trên dòng + làm hàng loạt (§4.5.6), ngăn chi tiết (thông tin, món, lịch sử), form Lập đơn có dòng món.
 * B. Sản phẩm bán lẻ (#/sal-retail-product): danh mục món bán trên trang khách lẻ – giá, nhóm, nhãn, Đang bán / Ngừng bán.
 * API: docs/frontend-handoff/RETAIL-1.md (RetailOrder/*, RetailProduct/*). Máy chủ chưa có API (404) → báo "Tính năng chưa được nâng cấp".
 * Khai báo màn với cờ extra: trước khi có seed ObjectMenu chỉ admin thấy trên sidebar. Chỉ chạy chế độ dữ liệu thật. Tải CUỐI danh sách. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, FN = APP.FN || {};
  const def = (id, name) => { if (!FN[id]) FN[id] = { extra: true, ph: 'RETAIL', grp: 'Bán lẻ', fn: name, id, name, types: ['LS'], table: null, rel: [], st: 'MAPPED', note: '' }; };
  def('sal-retail-order', 'Đơn khách lẻ');
  def('sal-retail-product', 'Sản phẩm bán lẻ');

  // Từ điển hiển thị (§4.5.1) – mã mới phải có tiếng Việt trước khi lên giao diện.
  if (UI.STATUS_VI && !UI.STATUS_VI.Delivering) UI.STATUS_VI.Delivering = 'Đang giao';
  const STATUS = [['New', 'Mới', 'blue'], ['Confirmed', 'Đã xác nhận', 'purple'], ['Delivering', 'Đang giao', 'orange'], ['Completed', 'Hoàn thành', 'green'], ['Cancelled', 'Đã hủy', '']];
  const NEXT = { New: ['Confirmed', 'Cancelled'], Confirmed: ['Delivering', 'Cancelled'], Delivering: ['Completed', 'Cancelled'] };
  const ACT = { Confirmed: 'Xác nhận', Delivering: 'Bắt đầu giao', Completed: 'Hoàn thành', Cancelled: 'Hủy' };
  const CHANNEL = { WEB: 'Trang web', PHONE: 'Điện thoại', ZALO: 'Zalo' };
  const PAY = { COD: 'Tiền mặt khi nhận', BANK: 'Chuyển khoản' };
  const PAY_ST = { UNPAID: ['Chưa thanh toán', 'orange'], PAID: ['Đã thanh toán', 'green'] };
  const MEALS = () => { const m = (Store.mealTypes ? Store.mealTypes() : []).map(x => x.name).filter(Boolean); return m.length ? m : ['Sáng', 'Trưa', 'Tối']; };
  const CATS = () => { const r = Store.refList ? Store.refList('RetailCategory') : []; return r && r.length ? r : [
    { code: 'com-van-phong', name: 'Cơm văn phòng' }, { code: 'an-sang', name: 'Món ăn sáng' }, { code: 'combo', name: 'Combo nhóm' },
    { code: 'dinh-duong', name: 'Suất dinh dưỡng' }, { code: 'trang-mieng', name: 'Tráng miệng & đồ uống' }]; };
  const ICONS = ['lunch_dining', 'set_meal', 'dinner_dining', 'bakery_dining', 'ramen_dining', 'groups', 'family_restroom', 'spa', 'eco', 'icecream', 'local_drink', 'restaurant'];
  const stTag = s => { const x = STATUS.find(v => v[0] === s); return x ? UI.tag(x[1], x[2]) : esc(s || ''); };
  const vnd = v => U.money(v || 0);
  /** Giờ chốt đơn ngày hôm sau (BUSINESS_RULES Retail:OrderCutoffHour – máy chủ kiểm lại; giao diện chỉ để gợi ý ngày). */
  const CUTOFF_HOUR = 20;
  const earliest = () => U.addDays(today(), new Date().getHours() < CUTOFF_HOUR ? 1 : 2);
  const isMissing = msg => /\(404\)/.test(msg || '');
  /**
   * §4.5.1: thông báo lỗi không lộ mã kỹ thuật – bỏ đuôi "(NNN)", đổi các câu kỹ thuật thường gặp sang tiếng Việt dễ hiểu.
   * (Thông báo nghiệp vụ tiếng Việt của máy chủ giữ nguyên.)
   */
  const clean = msg => {
    const s = String(msg || '');
    if (/\(401\)/.test(s)) return 'Phiên đăng nhập đã hết – vui lòng đăng nhập lại.';
    if (/\(403\)/.test(s)) return 'Tài khoản chưa được cấp quyền thao tác này.';
    if (/\(5\d\d\)/.test(s) || /Internal Server Error|Bad Gateway|Service Unavailable/i.test(s)) return 'Máy chủ đang bận hoặc gặp lỗi – vui lòng thử lại sau ít phút.';
    if (/Không kết nối được server/i.test(s)) return 'Không kết nối được máy chủ – kiểm tra mạng rồi thử lại.';
    return s.replace(/\s*\(\d{3}\)$/, '') || 'Không thực hiện được – vui lòng thử lại.';
  };
  /** §4.5.8: API chưa có → không để màn lỗi kỹ thuật. */
  const notReady = (body, msg) => {
    body.innerHTML = isMissing(msg)
      ? `<div class="card panel empty">${I('clock')}<h2 class="mt">Tính năng chưa được nâng cấp. Vui lòng liên hệ quản trị viên.</h2><p class="muted small">Đang chờ máy chủ bổ sung chức năng Bán lẻ (RETAIL-1).</p></div>`
      : `<div class="card panel muted">${esc(clean(msg))}</div>`;
  };
  const notReadyToast = () => UI.toast('Tính năng chưa được nâng cấp. Vui lòng liên hệ quản trị viên.', 'warn');
  const errToast = msg => isMissing(msg) ? notReadyToast() : UI.toast(esc(clean(msg)), 'err');
  const today = () => U.day(U.iso(new Date()));
  /** Ghi qua Store.act nhưng tự hiện thông báo: API chưa có → câu chuẩn §4.5.8 (không in đường dẫn API / mã lỗi kỹ thuật). */
  const act = (path, opt = {}) => Store.act(path, Object.assign({}, opt, { quiet: true })).then(r => {
    if (!r.ok && !opt.quiet) errToast(r.message);
    return r;
  });
  /** Vẽ lại danh sách mà không làm mất con trỏ trong ô tìm (§4.5 – gõ liên tục không bị ngắt). */
  const keepFocus = (body, id, render) => {
    const f = document.activeElement, had = f && f.id === id, pos = had ? f.selectionStart : 0;
    render();
    if (had) { const i = body.querySelector('#' + id); if (i) { i.focus(); try { i.setSelectionRange(pos, pos); } catch (e) { /* type=search */ } } }
  };

  // ================= A. Đơn khách lẻ =================
  const st = { status: 'open', q: '', channel: '', period: 'all', sel: new Set() };
  APP.register('sal-retail-order', (el, f, p) => {
    f = f || FN['sal-retail-order'];
    if (!Store.isLive) { el.innerHTML = APP.header(f, '', { noNote: true }) + '<div class="card panel empty"><h2>Màn này chỉ chạy với dữ liệu thật.</h2></div>'; return; }
    const perm = Store.perm('sal-retail-order');
    el.innerHTML = APP.header(f, `<button class="btn" id="roPrep">${I('file')} Tổng hợp cho bếp</button>${perm.exp !== false ? `<button class="btn" id="roExp">${I('export')} Xuất Excel</button>` : ''}${perm.add !== false ? `<button class="btn primary" id="roNew">${I('plus')} Lập đơn</button>` : ''}`, { noNote: true, sub: 'Đơn khách lẻ đặt trên trang web hoặc qua điện thoại / Zalo – tách riêng đơn suất ăn doanh nghiệp.' })
      + '<div id="roBody"><div class="card panel small muted">Đang tải…</div></div>';
    st.sel.clear();
    const re = () => draw(el);
    const nb = el.querySelector('#roNew'); if (nb) nb.onclick = () => orderForm(re);
    el.querySelector('#roPrep').onclick = () => prepSummary();
    const eb = el.querySelector('#roExp'); if (eb) eb.onclick = () => st.apiMissing ? notReadyToast() : Store.download('RetailOrder/Export', params());
    (Store.loadRefLists ? Store.loadRefLists() : Promise.resolve()).then(re, re);
    if (p && p.id) openDetail(p.id, re);
  });
  function params() {
    const t = today();
    const range = { today: [t, t], tomorrow: [U.addDays(t, 1), U.addDays(t, 1)], week: [t, U.addDays(t, 6)] }[st.period] || [null, null];
    // "Đang xử lý" lọc ở MÁY CHỦ (OrderStatus nhận danh sách, cách nhau dấu phẩy) – không lọc phía giao diện sau khi cắt trang.
    const status = st.status === 'open' ? 'New,Confirmed,Delivering' : st.status === 'all' ? null : st.status;
    return { SearchText: st.q || null, OrderStatus: status, Channel: st.channel || null, FromDate: range[0], ToDate: range[1], Page: 1, PageSize: 500 };
  }
  let drawSeq = 0;
  function draw(el) {
    const body = el.querySelector('#roBody'); if (!body) return;
    const re = () => draw(el), perm = Store.perm('sal-retail-order'), seq = ++drawSeq;
    Store.query('RetailOrder/GetList', params()).then(x => {
      if (!body.isConnected || seq !== drawSeq) return;   // bấm lọc nhanh: bỏ kết quả cũ về sau
      st.apiMissing = !x.ok && isMissing(x.message);
      if (!x.ok) return notReady(body, x.message);
      const d = x.data || {}, rows = d.listItem || x.list || [], counts = d.statusCounts || {};
      const open = ['New', 'Confirmed', 'Delivering'].reduce((a, k) => a + (counts[k] || 0), 0);
      const more = (d.totalRecords || 0) > rows.length;
      keepFocus(body, 'roQ', () => render(body, rows, counts, open, d, more, perm, re));
    });
  }
  function render(body, rows, counts, open, d, more, perm, re) {
    {
      [...st.sel].forEach(id => { if (!rows.some(o => o.id === id)) st.sel.delete(id); });
      const canEdit = perm.edit !== false;
      body.innerHTML = UI.kpis([
        UI.kpi('Đơn mới chờ xác nhận', U.num(counts.New || 0), counts.New ? '<span class="bad">gọi khách xác nhận</span>' : 'Không có', 'bell', 'blue'),
        UI.kpi('Đang giao', U.num(counts.Delivering || 0), '', 'truck', 'orange'),
        UI.kpi('Đơn giao hôm nay', U.num(d.todayCount || 0), '', 'calendar', 'purple'),
        UI.kpi('Doanh thu hôm nay (đồng)', U.num(d.todayAmount || 0), 'đơn hoàn thành', 'chart', 'green'),
      ]) + `<div class="card panel mb"><div class="row-flex" style="flex-wrap:wrap;gap:8px">
          ${[['open', 'Đang xử lý', open], ...STATUS.map(s => [s[0], s[1], counts[s[0]] || 0]), ['all', 'Tất cả', Object.values(counts).reduce((a, b) => a + (+b || 0), 0)]]
            .map(([k, l, n]) => `<button class="chip ${st.status === k ? 'active' : ''}" data-st="${k}">${l} · ${U.num(n)}</button>`).join('')}</div>
        <div class="row-flex mt" style="flex-wrap:wrap;gap:8px">
          <input class="input" type="search" id="roQ" placeholder="Tìm số đơn, tên, số điện thoại" value="${esc(st.q)}" style="width:260px">
          <select class="select" id="roCh" style="width:auto"><option value="">Mọi kênh</option>${Object.entries(CHANNEL).map(([k, v]) => `<option value="${k}" ${st.channel === k ? 'selected' : ''}>${v}</option>`).join('')}</select>
          ${[['all', 'Mọi ngày nhận'], ['today', 'Hôm nay'], ['tomorrow', 'Ngày mai'], ['week', '7 ngày tới']].map(([k, l]) => `<button class="chip ${st.period === k ? 'active' : ''}" data-pd="${k}">${l}</button>`).join('')}</div></div>
        ${canEdit ? '<div id="roBulk"></div>' : ''}
        <div class="card panel">${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr>${canEdit ? '<th style="width:36px"><input type="checkbox" id="roAll" title="Chọn tất cả"></th>' : ''}<th>Số đơn</th><th>Khách hàng</th><th>Giao đến</th><th>Ngày nhận</th><th>Món</th><th class="num">Tổng tiền</th><th>Kênh</th><th>Trạng thái</th><th></th></tr></thead><tbody>${rows.map((o, n) => `<tr data-i="${n}" style="cursor:pointer">
            ${canEdit ? `<td>${NEXT[o.orderStatus] ? `<input type="checkbox" data-sel="${esc(o.id)}" ${st.sel.has(o.id) ? 'checked' : ''}>` : ''}</td>` : ''}
            <td><b>${esc(o.orderNumber || '')}</b><br><span class="small muted">${U.dt(o.createdDate)}</span></td>
            <td><b>${esc(o.customerName || '')}</b><br><a class="small" href="tel:${esc(o.phoneNumber || '')}">${esc(o.phoneNumber || '')}</a></td>
            <td class="small" title="${esc(o.address || '')}"><span style="display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;max-width:240px">${esc(o.address || '')}</span></td>
            <td>${U.date(o.deliveryDate)}<br><span class="small muted">Bữa ${esc((o.mealType || '').toLowerCase())}</span></td>
            <td class="small"><span style="display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;max-width:260px">${esc(o.lineSummary || '')}</span></td>
            <td class="num"><b>${vnd(o.totalAmount)}</b>${o.paymentStatus === 'PAID' ? '<br>' + UI.tag('Đã thanh toán', 'green') : ''}</td>
            <td class="small">${esc(CHANNEL[o.channel] || o.channel || '')}</td>
            <td>${stTag(o.orderStatus)}${o.cancelReason ? `<br><span class="small muted">${esc(o.cancelReason)}</span>` : ''}</td>
            <td class="actions">${canEdit ? (NEXT[o.orderStatus] || []).filter(to => to !== 'Cancelled').map(to => `<button class="btn xs primary" data-to="${to}" data-n="${n}" style="white-space:nowrap">${ACT[to]}</button>`).join(' ') : ''}</td></tr>`).join('')}</tbody></table></div>`
          : `<div class="empty">${st.status === 'open' ? 'Không có đơn đang chờ xử lý.' : 'Không có đơn nào khớp bộ lọc.'}</div>`}
          ${more ? `<div class="small muted mt">Đang hiện ${U.num(rows.length)} / ${U.num(d.totalRecords)} đơn – thu hẹp bằng ngày nhận hoặc ô tìm để xem đơn cũ hơn.</div>` : ''}</div>`;
      body.querySelectorAll('[data-st]').forEach(b => b.onclick = () => { st.status = b.dataset.st; re(); });
      body.querySelectorAll('[data-pd]').forEach(b => b.onclick = () => { st.period = b.dataset.pd; re(); });
      let tq; body.querySelector('#roQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { st.q = e.target.value.trim(); re(); }, 400); };
      body.querySelector('#roCh').onchange = e => { st.channel = e.target.value; re(); };
      body.querySelectorAll('[data-to]').forEach(b => b.onclick = e => { e.stopPropagation(); transit([rows[+b.dataset.n]], b.dataset.to, re); });
      body.querySelectorAll('tr[data-i]').forEach(tr => tr.onclick = e => { if (e.target.closest('a,button,input')) return; openDetail(rows[+tr.dataset.i].id, re); });
      if (!canEdit) return;
      const bulk = () => {
        const box = body.querySelector('#roBulk'), sel = rows.filter(o => st.sel.has(o.id)); if (!box) return;
        // Làm hàng loạt chỉ khi các đơn chọn cùng trạng thái (cùng bước tiếp theo).
        const same = sel.length && sel.every(o => o.orderStatus === sel[0].orderStatus) ? sel[0].orderStatus : null;
        box.innerHTML = sel.length ? `<div class="notice info mb">${I('check')}<div style="flex:1">Đã chọn <b>${sel.length}</b> đơn${same ? '' : ' – chọn các đơn cùng trạng thái để làm hàng loạt'}.</div>
          ${same ? (NEXT[same] || []).filter(t => t !== 'Cancelled').map(t => `<button class="btn sm primary" data-bulk="${t}">${ACT[t]} ${sel.length} đơn</button>`).join(' ') : ''}
          <button class="btn sm" data-bclr>Bỏ chọn</button></div>` : '';
        box.querySelectorAll('[data-bulk]').forEach(b => b.onclick = () => transit(sel, b.dataset.bulk, () => { st.sel.clear(); re(); }));
        const c = box.querySelector('[data-bclr]'); if (c) c.onclick = () => { st.sel.clear(); re(); };
      };
      body.querySelectorAll('[data-sel]').forEach(cb => cb.onchange = () => { cb.checked ? st.sel.add(cb.dataset.sel) : st.sel.delete(cb.dataset.sel); bulk(); });
      const allCb = body.querySelector('#roAll'); if (allCb) allCb.onchange = () => { rows.filter(o => NEXT[o.orderStatus]).forEach(o => allCb.checked ? st.sel.add(o.id) : st.sel.delete(o.id)); re(); };
      bulk();
    }
  }
  /** Chuyển trạng thái 1 hoặc nhiều đơn (Transition tuần tự – máy chủ kiểm máy trạng thái từng đơn). */
  function transit(list, to, done) {
    const one = list.length === 1 ? list[0] : null, who = one ? `đơn <b>${esc(one.orderNumber || '')}</b> – ${esc(one.customerName || '')}` : `<b>${list.length}</b> đơn`;
    const run = extra => {
      let ok = 0, fail = 0, missing = false;
      // 1 đơn: act tự hiện lỗi; nhiều đơn: gom kết quả rồi báo 1 lần. API chưa có → dừng ngay, báo câu chuẩn.
      const step = i => i >= list.length ? Promise.resolve()
        : act('RetailOrder/Transition', { form: Object.assign({ Id: list[i].id, To: to }, extra || {}), quiet: list.length > 1 }).then(r => {
          if (r.ok) ok++; else if (isMissing(r.message)) { missing = true; return; } else fail++;
          return step(i + 1);
        });
      return step(0).then(() => {
        if (missing) { if (list.length > 1) notReadyToast(); return; }
        if (ok) UI.toast(`${ACT[to]}: ${ok} đơn${fail ? ` · ${fail} đơn không làm được (đã đổi trạng thái hoặc không hợp lệ)` : ''}`, fail ? 'warn' : 'ok');
        else if (list.length > 1) UI.toast('Không làm được – kiểm tra lại trạng thái các đơn đã chọn', 'err');
        done();
      });
    };
    if (to === 'Cancelled') return UI.modal({
      title: 'Hủy ' + (one ? 'đơn ' + esc(one.orderNumber || '') : list.length + ' đơn'), size: 'sm',
      body: `<div class="field"><label>Lý do hủy <span class="req">*</span></label><div class="row-flex" style="flex-wrap:wrap;gap:6px;margin-bottom:8px">${['Khách hủy', 'Không liên lạc được khách', 'Ngoài khu vực giao', 'Hết món'].map(r => `<button type="button" class="chip" data-rs="${esc(r)}">${esc(r)}</button>`).join('')}</div><textarea class="input" rows="2" id="cxR"></textarea></div>`,
      foot: '<button class="btn" data-close>Đóng</button><button class="btn danger" id="cxGo">Hủy đơn</button>',
      onMount: (m, close) => {
        m.querySelectorAll('[data-rs]').forEach(b => b.onclick = () => { m.querySelector('#cxR').value = b.dataset.rs; });
        m.querySelector('#cxGo').onclick = () => { const r = m.querySelector('#cxR').value.trim(); if (r.length < 3) return UI.toast('Nhập lý do hủy', 'err'); close(); run({ Reason: r }); };
      }
    });
    if (to === 'Confirmed') return UI.modal({
      title: 'Xác nhận ' + (one ? 'đơn ' + esc(one.orderNumber || '') : list.length + ' đơn'), sub: 'Đã gọi khách xác nhận món, địa chỉ và giờ giao.', size: 'sm',
      body: `<div class="field"><label>Phí giao (đ)</label><input type="number" class="input" id="cfFee" min="0" step="1000" value="${one ? +one.shippingFee || 0 : 0}"><div class="small muted">Để 0 nếu miễn phí giao.${one ? '' : ' Áp cùng phí cho các đơn đã chọn.'}</div></div>
        <div class="field mt"><label>Thanh toán</label><div class="row-flex" style="gap:6px">${Object.entries(PAY).map(([k, v], i) => `<button type="button" class="chip ${(one ? (one.paymentMethod || 'COD') : 'COD') === k ? 'active' : ''}" data-pm="${k}">${v}</button>`).join('')}</div></div>`,
      foot: '<button class="btn" data-close>Đóng</button><button class="btn primary" id="cfGo">Xác nhận</button>',
      onMount: (m, close) => {
        let pm = one ? (one.paymentMethod || 'COD') : 'COD';
        m.querySelectorAll('[data-pm]').forEach(b => b.onclick = () => { pm = b.dataset.pm; m.querySelectorAll('[data-pm]').forEach(x => x.classList.toggle('active', x === b)); });
        m.querySelector('#cfGo').onclick = () => { const fee = Math.max(0, +m.querySelector('#cfFee').value || 0); close(); run({ ShippingFee: fee, PaymentMethod: pm }); };
      }
    });
    UI.confirm(to === 'Completed' ? `Hoàn thành ${who}? Đơn chuyển sang <b>Đã thanh toán</b>.` : `${ACT[to]} ${who}?`, () => run(), { ok: ACT[to] });
  }
  /** RETAIL-2: khối Giao hàng trong chi tiết đơn – chuyến đang gán + chọn chuyến cùng ngày nhận (đơn chưa Hoàn thành / Hủy). */
  function routeBlock(d, o, canEdit) {
    const r = d.route, opts = d.listRouteOption || [], open = !['Completed', 'Cancelled'].includes(o.orderStatus);
    const cur = r ? `<div><b>${esc(r.routeNumber || '')}</b> · ${esc(r.statusLabel || '')}${r.driverName ? ' · tài xế ' + esc(r.driverName) : ''}${r.licensePlate ? ' · xe ' + esc(r.licensePlate) : ''}
        <div class="small muted">Đã giao ${U.num(r.stopDoneCount || 0)}/${U.num(r.stopCount || 0)} điểm${r.actualStart ? ' · xuất phát ' + U.dt(r.actualStart) : r.plannedStart ? ' · dự kiến ' + U.dt(r.plannedStart) : ''}</div></div>`
      : '<div class="muted">Chưa gán chuyến giao.</div>';
    const pick = canEdit && open ? (opts.length
      ? `<div class="row-flex mt" style="gap:8px;align-items:center"><select class="select" id="roRoute" style="flex:1"><option value="">Không gán chuyến</option>${opts.map(x => `<option value="${esc(x.id)}" ${x.id === d.deliveryRouteId ? 'selected' : ''}>${esc(x.label)}</option>`).join('')}</select>
          <button type="button" class="btn primary" id="roRouteSave">Lưu chuyến</button></div>`
      : `<div class="small muted mt">Chưa có chuyến giao ngày ${U.date(o.deliveryDate)} – tạo chuyến ở màn Giao hàng rồi gán tại đây.</div>`) : '';
    return `<div class="mt"><div class="muted small" style="margin-bottom:4px">Chuyến giao${d.hasAccount ? ' · khách có tài khoản, theo dõi được trên trang khách' : ''}</div>${cur}${pick}</div>`;
  }
  /** RETAIL-2: các bước tiến độ đơn (cùng dữ liệu khách thấy ở "Đơn của tôi"). */
  function progressHtml(d) {
    const steps = d.listStep || [];
    if (!steps.length) return '<div class="empty">Chưa có dữ liệu tiến độ.</div>';
    const color = { done: 'green', current: 'orange', cancelled: 'red', pending: 'gray' };
    return `<div class="muted small" style="margin-bottom:8px">Bước hiện tại: <b>${esc(d.stageLabel || '')}</b></div>
      <div class="table-wrap"><table class="table dc-table"><thead><tr><th>Bước</th><th>Trạng thái</th><th>Thời điểm</th></tr></thead><tbody>
      ${steps.map(s => `<tr><td>${esc(s.label || '')}${s.note ? `<div class="small muted">${esc(s.note)}</div>` : ''}</td><td>${UI.tag(s.state === 'done' ? 'Đã xong' : s.state === 'current' ? 'Đang thực hiện' : s.state === 'cancelled' ? 'Đã hủy' : 'Chưa tới', color[s.state] || 'gray')}</td><td>${s.at ? U.dt(s.at) : '–'}</td></tr>`).join('')}
      </tbody></table></div>`;
  }
  function openDetail(id, done) {
    Store.query('RetailOrder/GetDetail', { id }).then(x => {
      if (!x.ok) return errToast(x.message);
      const d = x.data || {}, o = d.order || {}, lines = d.lines || [], next = d.allowedTransitions || NEXT[o.orderStatus] || [];
      const canEdit = Store.perm('sal-retail-order').edit !== false;
      UI.drawer({
        title: esc(o.orderNumber || ''), sub: esc(CHANNEL[o.channel] || o.channel || ''), badge: stTag(o.orderStatus), width: 600,
        tabs: [
          { label: 'Đơn hàng', html: `<div style="display:grid;grid-template-columns:130px 1fr;gap:6px 12px">${[
              ['Khách hàng', `<b>${esc(o.customerName || '')}</b>`], ['Điện thoại', `<a href="tel:${esc(o.phoneNumber || '')}">${esc(o.phoneNumber || '')}</a>`], ['Giao đến', esc(o.address || '')],
              ['Ngày nhận', U.date(o.deliveryDate) + ' · bữa ' + esc((o.mealType || '').toLowerCase())], ['Ghi chú', esc(o.note || '–')],
              ['Thanh toán', esc(PAY[o.paymentMethod] || '–') + ' · ' + UI.tag(...(PAY_ST[o.paymentStatus] || ['Chưa thanh toán', 'orange']))],
              ['Đặt lúc', U.dt(o.createdDate)], ['Xác nhận lúc', o.confirmedAt ? U.dt(o.confirmedAt) : '–'], ...(o.cancelReason ? [['Lý do hủy', esc(o.cancelReason)]] : [])]
              .map(([k, v]) => `<div class="muted small">${k}</div><div>${v}</div>`).join('')}</div>
            <div class="table-wrap mt"><table class="table dc-table"><thead><tr><th>Món</th><th class="num">Số lượng</th><th class="num">Đơn giá</th><th class="num">Thành tiền</th></tr></thead><tbody>
              ${lines.map(l => `<tr><td>${esc(l.productName || '')}</td><td class="num">${U.num(l.quantity)} ${esc(l.unit || '')}</td><td class="num">${vnd(l.unitPrice)}</td><td class="num">${vnd(l.lineAmount)}</td></tr>`).join('')}
              <tr><td colspan="3" class="num muted">Tạm tính</td><td class="num">${vnd(o.subTotal)}</td></tr>
              <tr><td colspan="3" class="num muted">Phí giao</td><td class="num">${vnd(o.shippingFee)}</td></tr>
              <tr><td colspan="3" class="num"><b>Tổng tiền</b></td><td class="num"><b>${vnd(o.totalAmount)}</b></td></tr></tbody></table></div>
            ${routeBlock(d, o, canEdit)}` },
          { label: 'Tiến độ', html: progressHtml(d) },
          { label: 'Lịch sử', html: pane => { UI.history('RetailOrder', o.id, pane); return '<div class="small muted">Đang tải…</div>'; } },
        ],
        foot: canEdit ? (['New', 'Confirmed'].includes(o.orderStatus) ? '<button class="btn" data-ed>Sửa đơn</button>' : '') + next.map(t => `<button class="btn ${t === 'Cancelled' ? '' : 'primary'}" data-go="${t}">${ACT[t] || esc(t)}</button>`).join('') : '',
        onMount: (dw, close) => {
          const foot = dw.parentNode;
          foot.querySelectorAll('[data-go]').forEach(b => b.onclick = () => { close(); transit([Object.assign({}, o)], b.dataset.go, done); });
          const ed = foot.querySelector('[data-ed]'); if (ed) ed.onclick = () => { close(); orderForm(done, { order: o, lines }); };
          // RETAIL-2: gán / bỏ chuyến giao – khách đã đăng nhập theo dõi chuyến ở "Đơn của tôi" (RetailOrder/AssignRoute).
          const rs = dw.querySelector('#roRouteSave');
          if (rs) rs.onclick = () => {
            const rid = dw.querySelector('#roRoute').value;
            if (rid === (d.deliveryRouteId || '')) return UI.toast('Chuyến giao không đổi.', 'warn');
            rs.disabled = true;
            act('RetailOrder/AssignRoute', { form: { Id: o.id, DeliveryRouteId: rid } }).then(r => {
              rs.disabled = false;
              if (!r.ok) return;
              UI.toast(rid ? 'Đã gán chuyến giao cho đơn.' : 'Đã bỏ gán chuyến giao.', 'ok');
              close(); openDetail(id, done);
            });
          };
        }
      });
    });
  }
  /** Lập đơn (khách gọi / nhắn Zalo) hoặc sửa đơn Mới / Đã xác nhận. Số đơn, giá do máy chủ; giá trên form chỉ để xem. */
  function orderForm(done, cur) {
    Store.query('RetailProduct/GetAll', { Status: 'Active', Page: 1, PageSize: 500 }).then(px => {
      if (!px.ok) return errToast(px.message);
      const prods = (px.list || []).filter(p => !p.isDeleted), byId = new Map(prods.map(p => [p.id, p]));
      const o = (cur && cur.order) || {}, editing = !!o.id;
      let lines = cur ? (cur.lines || []).map(l => ({ productId: l.retailProductId || l.productId, quantity: +l.quantity || 1 })) : [{ productId: '', quantity: 1 }];
      let channel = o.channel && o.channel !== 'WEB' ? o.channel : 'PHONE', pay = o.paymentMethod || 'COD';
      const min = earliest(), oldDate = U.day(o.deliveryDate);
      const lineRows = () => lines.map((l, i) => { const p = byId.get(l.productId); return `<tr>
          <td><select class="select" data-lp="${i}"><option value="">Chọn món</option>${prods.map(x => `<option value="${esc(x.id)}" ${x.id === l.productId ? 'selected' : ''}>${esc(x.name)} – ${vnd(x.price)}/${esc(x.unit || '')}</option>`).join('')}</select></td>
          <td style="width:110px"><input type="number" class="input" min="1" max="999" data-lq="${i}" value="${l.quantity}"></td>
          <td class="num" style="width:120px">${p ? vnd(p.price * l.quantity) : ''}</td>
          <td style="width:40px">${lines.length > 1 ? `<button type="button" class="btn xs" data-lx="${i}" title="Bỏ dòng">${I('x')}</button>` : ''}</td></tr>`; }).join('');
      const total = () => lines.reduce((a, l) => a + ((byId.get(l.productId) || {}).price || 0) * l.quantity, 0);
      UI.modal({
        title: editing ? 'Sửa đơn ' + esc(o.orderNumber || '') : 'Lập đơn khách lẻ', sub: editing ? '' : 'Đơn nhận qua điện thoại / Zalo – lưu ở trạng thái Đã xác nhận. Số đơn máy chủ tự sinh.', size: 'lg',
        body: `${editing ? '' : `<div class="field"><label>Kênh</label><div class="row-flex" style="gap:6px">${['PHONE', 'ZALO'].map(k => `<button type="button" class="chip ${channel === k ? 'active' : ''}" data-ch="${k}">${CHANNEL[k]}</button>`).join('')}</div></div>`}
          <div class="grid-2 mt" style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
            <div class="field"><label>Tên khách <span class="req">*</span></label><input class="input" id="ofName" maxlength="100" value="${esc(o.customerName || '')}" ${editing ? 'disabled' : ''}></div>
            <div class="field"><label>Số điện thoại <span class="req">*</span></label><input class="input" id="ofPhone" maxlength="20" value="${esc(o.phoneNumber || '')}" ${editing ? 'disabled' : ''}></div></div>
          <div class="field mt"><label>Địa chỉ giao <span class="req">*</span></label><input class="input" id="ofAddr" maxlength="250" value="${esc(o.address || '')}"></div>
          <div class="mt" style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
            <div class="field"><label>Ngày nhận <span class="req">*</span></label><input type="date" class="input" id="ofDate" min="${editing && oldDate && oldDate < min ? oldDate : min}" value="${esc(U.day(o.deliveryDate) || min)}"></div>
            <div class="field"><label>Bữa <span class="req">*</span></label><select class="select" id="ofMeal">${MEALS().map(m => `<option ${(o.mealType || 'Trưa') === m ? 'selected' : ''}>${esc(m)}</option>`).join('')}</select></div></div>
          <div class="field mt"><label>Món <span class="req">*</span></label><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Món</th><th>Số lượng</th><th class="num">Thành tiền</th><th></th></tr></thead><tbody id="ofLines">${lineRows()}</tbody></table></div>
            <button type="button" class="btn sm mt" id="ofAdd">${I('plus')} Thêm món</button></div>
          <div class="mt" style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
            <div class="field"><label>Phí giao (đ)</label><input type="number" class="input" id="ofFee" min="0" step="1000" value="${+o.shippingFee || 0}"></div>
            <div class="field"><label>Thanh toán</label><div class="row-flex" style="gap:6px">${Object.entries(PAY).map(([k, v]) => `<button type="button" class="chip ${pay === k ? 'active' : ''}" data-pm="${k}">${v}</button>`).join('')}</div></div></div>
          <div class="field mt"><label>Ghi chú</label><textarea class="input" rows="2" id="ofNote" maxlength="500">${esc(o.note || '')}</textarea></div>
          <div class="mt" style="text-align:right">Tạm tính: <b id="ofTotal">${vnd(total())}</b> <span class="small muted">(máy chủ tính lại theo giá hiện hành)</span></div>`,
        foot: `<button class="btn" data-close>Hủy</button><button class="btn primary" id="ofGo">${editing ? 'Lưu' : 'Lập đơn'}</button>`,
        onMount: (m, close) => {
          const redraw = () => { m.querySelector('#ofLines').innerHTML = lineRows(); m.querySelector('#ofTotal').innerHTML = vnd(total()); bindLines(); };
          const bindLines = () => {
            m.querySelectorAll('[data-lp]').forEach(s => s.onchange = () => { lines[+s.dataset.lp].productId = s.value; redraw(); });
            m.querySelectorAll('[data-lq]').forEach(s => s.onchange = () => { lines[+s.dataset.lq].quantity = Math.min(999, Math.max(1, Math.floor(+s.value || 1))); redraw(); });
            m.querySelectorAll('[data-lx]').forEach(b => b.onclick = () => { lines.splice(+b.dataset.lx, 1); redraw(); });
          };
          bindLines();
          m.querySelector('#ofAdd').onclick = () => { lines.push({ productId: '', quantity: 1 }); redraw(); };
          m.querySelectorAll('[data-ch]').forEach(b => b.onclick = () => { channel = b.dataset.ch; m.querySelectorAll('[data-ch]').forEach(x => x.classList.toggle('active', x === b)); });
          m.querySelectorAll('[data-pm]').forEach(b => b.onclick = () => { pay = b.dataset.pm; m.querySelectorAll('[data-pm]').forEach(x => x.classList.toggle('active', x === b)); });
          m.querySelector('#ofGo').onclick = () => {
            const v = id => m.querySelector(id).value.trim(), phone = v('#ofPhone').replace(/[\s.\-]/g, ''), used = lines.filter(l => l.productId);
            if (!editing && v('#ofName').length < 2) return UI.toast('Nhập tên khách', 'err');
            if (!editing && !/^(0|\+84)(3|5|7|8|9)\d{8}$/.test(phone)) return UI.toast('Số điện thoại chưa đúng', 'err');
            if (v('#ofAddr').length < 5) return UI.toast('Nhập địa chỉ giao', 'err');
            // Sửa đơn: chỉ kiểm giờ chốt khi ĐỔI ngày nhận (sửa địa chỉ / phí của đơn ngày mai sau 20:00 vẫn được).
            if (!v('#ofDate')) return UI.toast('Chọn ngày nhận', 'err');
            if ((!editing || v('#ofDate') !== oldDate) && v('#ofDate') < min) return UI.toast('Ngày nhận đã qua giờ chốt – chọn ngày sau', 'err');
            if (!used.length) return UI.toast('Chọn ít nhất 1 món', 'err');
            const wrongMeal = used.map(l => byId.get(l.productId)).filter(p => p && mealsOf(p).length && !mealsOf(p).includes(v('#ofMeal')));
            if (wrongMeal.length) return UI.toast(esc(`Bữa ${v('#ofMeal').toLowerCase()} không bán: ${wrongMeal.map(p => p.name).join(', ')}`), 'err');
            const json = { address: v('#ofAddr'), deliveryDate: v('#ofDate'), mealType: v('#ofMeal'), note: v('#ofNote'), shippingFee: Math.max(0, +v('#ofFee') || 0), paymentMethod: pay,
              lines: used.map(l => ({ productId: l.productId, productCode: (byId.get(l.productId) || {}).code || '', quantity: l.quantity })) };
            const req = editing ? act('RetailOrder/UpdateLines', { method: 'PUT', json: Object.assign({ id: o.id }, json) })
              : act('RetailOrder/Create', { json: Object.assign({ channel, customerName: v('#ofName'), phoneNumber: phone }, json) });
            req.then(r => { if (r.ok) { UI.toast(editing ? 'Đã lưu đơn' : `Đã lập đơn ${esc((r.data && r.data.orderNumber) || '')}`, 'ok'); close(); done(); } });
          };
        }
      });
    });
  }

  /**
   * Tổng hợp cho bếp: tổng số lượng từng món của đơn Đã xác nhận / Đang giao theo ngày nhận + bữa (RetailOrder/GetPrepSummary).
   * Đơn lẻ chưa vào kế hoạch sản xuất / MRP (RETAIL-1 mục 8) → bếp nấu theo bảng này. Có nút In (mở cửa sổ in của trình duyệt).
   */
  function prepSummary() {
    let date = U.addDays(today(), 1);   // mặc định ngày mai – bếp chuẩn bị từ chiều hôm trước
    const load = m => Store.query('RetailOrder/GetPrepSummary', { date }).then(x => {
      const box = m.querySelector('#psBody'); if (!box) return;
      if (!x.ok) { box.innerHTML = isMissing(x.message) ? '<div class="empty">Tính năng chưa được nâng cấp. Vui lòng liên hệ quản trị viên.</div>' : `<div class="muted">${esc(clean(x.message))}</div>`; return; }
      const meals = ((x.data || {}).meals || []).filter(g => (g.items || []).length);
      box.innerHTML = meals.length ? meals.map(g => `<h4 class="mt">Bữa ${esc((g.mealType || '').toLowerCase())} <span class="small muted">· ${U.num(g.orderCount || 0)} đơn</span></h4>
          <div class="table-wrap"><table class="table dc-table"><thead><tr><th>Món</th><th class="num">Số lượng</th></tr></thead><tbody>
          ${g.items.map(i => `<tr><td>${esc(i.productName || '')}</td><td class="num"><b>${U.num(i.quantity)}</b> ${esc(i.unit || '')}</td></tr>`).join('')}</tbody></table></div>`).join('')
        : '<div class="empty">Không có đơn đã xác nhận cho ngày này.</div>';
    });
    UI.modal({
      title: 'Tổng hợp cho bếp – đơn khách lẻ', sub: 'Tổng số lượng từng món của đơn Đã xác nhận và Đang giao, theo ngày nhận và bữa.', size: 'md',
      body: `<div class="field"><label>Ngày nhận</label><input type="date" class="input" id="psDate" value="${date}" style="max-width:200px"></div><div id="psBody" class="mt"><div class="small muted">Đang tải…</div></div>`,
      foot: '<button class="btn" data-close>Đóng</button><button class="btn primary" id="psPrint">In</button>',
      onMount: (m) => {
        m.querySelector('#psDate').onchange = e => { date = e.target.value || date; m.querySelector('#psBody').innerHTML = '<div class="small muted">Đang tải…</div>'; load(m); };
        m.querySelector('#psPrint').onclick = () => {
          const w = window.open('', '_blank', 'width=800,height=900'); if (!w) return UI.toast('Trình duyệt chặn cửa sổ in – cho phép cửa sổ bật lên rồi thử lại', 'warn');
          w.document.write(`<!doctype html><html lang="vi"><head><meta charset="utf-8"><title>Tổng hợp cho bếp ${U.date(date)}</title><style>body{font-family:Arial,sans-serif;padding:16px}table{border-collapse:collapse;width:100%;margin-bottom:12px}td,th{border:1px solid #999;padding:6px 8px;text-align:left}.num{text-align:right}.small,.muted{color:#555;font-size:12px}</style></head><body><h2>Tổng hợp cho bếp – đơn khách lẻ ngày ${U.date(date)}</h2>${m.querySelector('#psBody').innerHTML}</body></html>`);
          w.document.close(); w.focus(); w.print();
        };
        load(m);
      }
    });
  }

  // ================= B. Sản phẩm bán lẻ =================
  const sp = { q: '', cat: '', status: 'Active' };
  APP.register('sal-retail-product', (el, f) => {
    f = f || FN['sal-retail-product'];
    if (!Store.isLive) { el.innerHTML = APP.header(f, '', { noNote: true }) + '<div class="card panel empty"><h2>Màn này chỉ chạy với dữ liệu thật.</h2></div>'; return; }
    const perm = Store.perm('sal-retail-product');
    el.innerHTML = APP.header(f, perm.add !== false ? `<button class="btn primary" id="rpNew">${I('plus')} Thêm sản phẩm</button>` : '', { noNote: true, sub: 'Món bán trên trang khách lẻ – giá bán lẻ đã gồm VAT. Ngừng bán thì món ẩn khỏi trang khách.' })
      + '<div id="rpBody"><div class="card panel small muted">Đang tải…</div></div>';
    const re = () => drawProducts(el);
    const nb = el.querySelector('#rpNew'); if (nb) nb.onclick = () => productForm(null, re);
    (Store.loadRefLists ? Store.loadRefLists() : Promise.resolve()).then(re, re);
  });
  let drawPSeq = 0;
  function drawProducts(el) {
    const body = el.querySelector('#rpBody'); if (!body) return;
    const seq = ++drawPSeq;
    const re = () => drawProducts(el), perm = Store.perm('sal-retail-product'), catName = c => (CATS().find(x => x.code === c) || {}).name || c || '';
    Store.query('RetailProduct/GetAll', { SearchText: sp.q || null, CategoryCode: sp.cat || null, Status: sp.status || null, Page: 1, PageSize: 500 }).then(x => {
      if (!body.isConnected || seq !== drawPSeq) return;
      if (!x.ok) return notReady(body, x.message);
      const rows = x.list || [];
      keepFocus(body, 'rpQ', () => { body.innerHTML = `<div class="card panel mb"><div class="row-flex" style="flex-wrap:wrap;gap:8px">
          ${[['Active', 'Đang bán'], ['Inactive', 'Ngừng bán'], ['', 'Tất cả']].map(([k, l]) => `<button class="chip ${sp.status === k ? 'active' : ''}" data-ss="${k}">${l}</button>`).join('')}
          <input class="input" type="search" id="rpQ" placeholder="Tìm mã, tên món" value="${esc(sp.q)}" style="width:220px">
          <select class="select" id="rpCat" style="width:auto"><option value="">Mọi nhóm</option>${CATS().map(c => `<option value="${esc(c.code)}" ${sp.cat === c.code ? 'selected' : ''}>${esc(c.name)}</option>`).join('')}</select></div></div>
        <div class="card panel">${rows.length ? `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Mã</th><th>Tên món</th><th>Nhóm</th><th>Đơn vị</th><th class="num">Giá bán</th><th>Bữa bán</th><th>Nhãn</th><th class="num">Thứ tự</th><th>Trạng thái</th><th></th></tr></thead><tbody>${rows.map((r, n) => `<tr>
            <td>${esc(r.code || '')}</td><td><b>${esc(r.name || '')}</b>${r.description ? `<br><span class="small muted">${esc(r.description)}</span>` : ''}</td><td>${esc(catName(r.categoryCode))}</td><td>${esc(r.unit || '')}</td>
            <td class="num"><b>${vnd(r.price)}</b></td><td class="small">${esc(mealsOf(r).join(', ') || 'Mọi bữa')}</td><td>${String(r.tags || '').split(';').filter(Boolean).map(t => UI.tag(t.trim(), 'orange')).join(' ')}</td><td class="num">${U.num(r.sortOrder || 0)}</td>
            <td>${r.status === 'Active' ? UI.tag('Đang bán', 'green') : UI.tag('Ngừng bán', '')}</td>
            <td class="actions">${perm.edit !== false ? `<button class="btn xs" data-ed="${n}">Sửa</button> <button class="btn xs" data-tg="${n}">${r.status === 'Active' ? 'Ngừng bán' : 'Bán lại'}</button>` : ''}</td></tr>`).join('')}</tbody></table></div>`
          : '<div class="empty">Chưa có sản phẩm nào khớp bộ lọc.</div>'}</div>`; });
      body.querySelectorAll('[data-ss]').forEach(b => b.onclick = () => { sp.status = b.dataset.ss; re(); });
      let tq; body.querySelector('#rpQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { sp.q = e.target.value.trim(); re(); }, 400); };
      body.querySelector('#rpCat').onchange = e => { sp.cat = e.target.value; re(); };
      body.querySelectorAll('[data-ed]').forEach(b => b.onclick = () => productForm(rows[+b.dataset.ed], re));
      body.querySelectorAll('[data-tg]').forEach(b => b.onclick = () => { const r = rows[+b.dataset.tg], to = r.status === 'Active' ? 'Inactive' : 'Active';
        UI.confirm(to === 'Inactive' ? `Ngừng bán <b>${esc(r.name)}</b>? Món ẩn khỏi trang khách lẻ, đơn đã đặt không đổi.` : `Bán lại <b>${esc(r.name)}</b>?`,
          () => act('RetailProduct/Update', { method: 'PUT', form: Object.assign(productFormData(r), { Status: to }) }).then(x => { if (x.ok) { UI.toast(to === 'Inactive' ? 'Đã ngừng bán' : 'Đã mở bán lại', 'ok'); re(); } }), { ok: to === 'Inactive' ? 'Ngừng bán' : 'Bán lại' }); });
    });
  }
  const mealsOf = r => (Array.isArray(r.mealTypes) ? r.mealTypes : String(r.mealTypes || '').split(';')).map(m => m.trim()).filter(Boolean);
  const productFormData = r => ({ Id: r.id, Code: r.code || '', Name: r.name, CategoryCode: r.categoryCode, MealTypes: mealsOf(r).join(';'), Unit: r.unit, Price: r.price, Description: r.description || '', Tags: r.tags || '', Icon: r.icon || '', SortOrder: r.sortOrder || 0, Status: r.status || 'Active' });
  /** Thêm / sửa sản phẩm: mã do máy chủ sinh (SPL-NNNNN, chỉ đọc); nhóm, biểu tượng chọn trong danh sách (§4.5.10). */
  function productForm(r, done) {
    const editing = !!(r && r.id), x = r || {};
    let icon = x.icon || 'lunch_dining';
    UI.modal({
      title: editing ? 'Sửa sản phẩm' : 'Thêm sản phẩm bán lẻ', sub: editing ? 'Mã ' + esc(x.code || '') : 'Mã sản phẩm máy chủ tự sinh.', size: 'md',
      body: `<div class="field"><label>Tên món <span class="req">*</span></label><input class="input" id="pfName" maxlength="255" value="${esc(x.name || '')}"></div>
        <div class="mt" style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
          <div class="field"><label>Nhóm <span class="req">*</span></label><select class="select" id="pfCat"><option value="">Chọn nhóm</option>${CATS().map(c => `<option value="${esc(c.code)}" ${x.categoryCode === c.code ? 'selected' : ''}>${esc(c.name)}</option>`).join('')}</select></div>
          <div class="field"><label>Đơn vị <span class="req">*</span></label><select class="select" id="pfUnit">${['suất', 'combo', 'bát', 'phần', 'chai', 'hộp'].map(u => `<option ${(x.unit || 'suất') === u ? 'selected' : ''}>${u}</option>`).join('')}</select></div></div>
        <div class="mt" style="display:grid;grid-template-columns:1fr 1fr;gap:12px">
          <div class="field"><label>Giá bán (đ, đã gồm VAT) <span class="req">*</span></label><input type="number" class="input" id="pfPrice" min="0" step="1000" value="${x.price != null ? +x.price : ''}"></div>
          <div class="field"><label>Thứ tự hiển thị</label><input type="number" class="input" id="pfSort" min="0" value="${+x.sortOrder || 0}"></div></div>
        <div class="field mt"><label>Bữa bán <span class="req">*</span></label><div class="row-flex" style="flex-wrap:wrap;gap:6px">${MEALS().map(ml => `<button type="button" class="chip ${(editing ? mealsOf(x) : MEALS()).includes(ml) ? 'active' : ''}" data-ml="${esc(ml)}">${esc(ml)}</button>`).join('')}</div><div class="small muted">Khách chỉ đặt được món ở bữa đã chọn (vd món sáng không giao bữa tối).</div></div>
        <div class="field mt"><label>Mô tả</label><textarea class="input" rows="2" id="pfDesc" maxlength="1000">${esc(x.description || '')}</textarea></div>
        <div class="field mt"><label>Nhãn</label><div class="row-flex" style="flex-wrap:wrap;gap:6px">${['Bán chạy', 'Mới', 'Tiết kiệm', 'Lành mạnh'].map(t => `<button type="button" class="chip ${String(x.tags || '').split(';').map(s => s.trim()).includes(t) ? 'active' : ''}" data-tag="${t}">${t}</button>`).join('')}</div></div>
        <div class="field mt"><label>Biểu tượng (tạm thay ảnh)</label><div class="row-flex" style="flex-wrap:wrap;gap:6px">${ICONS.map(ic => `<button type="button" class="chip ${icon === ic ? 'active' : ''}" data-ic="${ic}" title="${ic}"><span class="material-icons" style="font-size:20px">${ic}</span></button>`).join('')}</div></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="pfGo">Lưu</button>',
      onMount: (m, close) => {
        m.querySelectorAll('[data-tag]').forEach(b => b.onclick = () => b.classList.toggle('active'));
        m.querySelectorAll('[data-ml]').forEach(b => b.onclick = () => b.classList.toggle('active'));
        m.querySelectorAll('[data-ic]').forEach(b => b.onclick = () => { icon = b.dataset.ic; m.querySelectorAll('[data-ic]').forEach(y => y.classList.toggle('active', y === b)); });
        m.querySelector('#pfGo').onclick = () => {
          const v = id => m.querySelector(id).value.trim(), price = +v('#pfPrice');
          if (v('#pfName').length < 2) return UI.toast('Nhập tên món', 'err');
          if (!v('#pfCat')) return UI.toast('Chọn nhóm', 'err');
          if (!(price > 0)) return UI.toast('Nhập giá bán', 'err');
          const meals = [...m.querySelectorAll('[data-ml].active')].map(b => b.dataset.ml);
          if (!meals.length) return UI.toast('Chọn ít nhất 1 bữa bán', 'err');
          const form = Object.assign(editing ? productFormData(x) : { Code: '', Status: 'Active' }, {
            Name: v('#pfName'), CategoryCode: v('#pfCat'), Unit: v('#pfUnit'), Price: price, SortOrder: Math.max(0, Math.floor(+v('#pfSort') || 0)), Description: v('#pfDesc'),
            Tags: [...m.querySelectorAll('[data-tag].active')].map(b => b.dataset.tag).join(';'), MealTypes: meals.join(';'), Icon: icon });
          act(editing ? 'RetailProduct/Update' : 'RetailProduct/Insert', { method: editing ? 'PUT' : 'POST', form }).then(y => {
            if (y.ok) { UI.toast(editing ? 'Đã lưu sản phẩm' : 'Đã thêm sản phẩm', 'ok'); close(); done(); }
          });
        };
      }
    });
  }
})();
