/* UI-6 (D-80, anh chốt 2026-11-23) – TRANG CHI TIẾT TOÀN MÀN thay ngăn trượt cho 14 hồ sơ / chứng từ.
 * Khung chung (UI-6.1): APP.D360.page(def) dựng 2 khuôn – "profile" (hồ sơ, như 3 màn 360) và "doc" (chứng từ: số + loại + trạng thái + thanh tiến trình
 * trạng thái + nút theo trạng thái / quyền) – phần đầu gọn UI-6.0 (1 khung bọc dòng tên + hàng chỉ số .kpis.compact) → hàng tab (tải khi mở lần đầu).
 * Điều hướng: #/<screen>-detail?id=<Id>. APP.openDetail(bảng, id) của 14 loại mở trang (Ctrl / Cmd / chuột giữa → tab mới); "← Danh sách" quay lại giữ
 * bộ lọc, trang, vị trí cuộn của danh sách. Danh mục nhỏ (đơn vị tính, ca, chức vụ, kho, xe…) vẫn ngăn trượt. Quyền = quyền xem màn gốc (D-79).
 * Chế độ DEMO giữ ngăn trượt (trang đọc API GetOverview của máy chủ).
 *
 * Trang đăng ký bằng APP.D360.page({
 *   id: 'scm-stockcount-detail', screen: 'scm-stockcount', table: 'StockCount', kind: 'doc' | 'profile', api: 'StockCount/GetOverview',
 *   resource: 'StockCount',                       // DocumentFile / Common/GetHistory / GetRelated
 *   head: d => ({ title, avatar, avatarCls, tags: [html], meta: [html…], steps: [{ label, state: 'done'|'cur'|'bad'|'' }], buttons: [{ act, icon, label, ok, primary }], more: [html] }),
 *   kpis: d => html (UI.kpi …, tiền dùng U.moneyShort),
 *   overview: (box, d, id) => void,               // tab Tổng quan (D.split(trái 1/3, phải 2/3) + UI.infoPanel)
 *   tabs: [{ label, screen?, render: (box, d, id) => void }],   // tab riêng; tự thêm: Duyệt (d.approvalRequestId), Liên quan, Tài liệu, Lịch sử
 *   approval: true, docs: true, related: true,   // mặc định true – false để bỏ tab chuẩn
 *   onAct: (act, d, id, reload) => void,          // nút phần đầu (gọi đúng action nghiệp vụ đang có)
 * }); */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const D = APP.D360;
  // Bảng (tên FE) → trang chi tiết. Trang chưa đăng ký → vẫn ngăn trượt.
  const PAGE_OF = {
    Order: 'crm-order-detail', Contract: 'crm-contract-detail', Dishe: 'menu-dish-detail', Ingredient: 'scm-ingredient-detail',
    ProductionBatche: 'prod-batch-detail', ProductionOrder: 'prod-order-detail', PurchaseOrder: 'scm-po-detail', DeliveryRoute: 'log-route-detail',
    CustomerComplaint: 'log-complaint-detail', MaterialLot: 'scm-lot-detail', ProductionPlan: 'prod-plan-detail', GoodsReceipt: 'scm-gr-detail',
    PurchaseRequest: 'scm-pr-detail', StockCount: 'scm-stockcount-detail', CustomerDeliveryLocation: 'crm-location-detail',
  };
  APP.DETAIL_PAGE_OF = PAGE_OF;
  // AI-17 (D-133): bảng có tab Tri thức (loại đối tượng của Knowledge/GetByEntity).
  const KB_OF = { Dishe: 'dish', MaterialLot: 'lot', ProductionBatche: 'batch', DeliveryRoute: 'route' };
  APP.detailPageOf = t => (Store.isLive && PAGE_OF[t] && APP.SCREENS[PAGE_OF[t]] ? PAGE_OF[t] : null);
  APP.detailHref = (t, id) => { const p = APP.detailPageOf(t); return p ? APP.href(p + '?id=' + encodeURIComponent(id)) : null; };

  // Quay lại danh sách giữ bộ lọc / trang / cuộn: UI.table lưu trạng thái theo màn (core/ui.js), app.js khôi phục khi APP._restoreList = màn.
  const remember = () => { APP._back = { screen: APP.current, hash: location.hash, scroll: window.scrollY }; };
  const base = APP.openDetail;
  APP.openDetail = (t, id, f, opt) => {
    const page = (!opt || !opt.drawer) && APP.detailPageOf(t);
    if (!page || !id) return base(t, id, f, opt);
    const ev = window.event;
    const url = APP.href(page + '?id=' + encodeURIComponent(id));
    if (ev && (ev.ctrlKey || ev.metaKey || ev.button === 1)) { window.open(url, '_blank'); return; }
    remember();
    location.hash = url;
  };
  // Chuột giữa trên dòng danh sách → tab mới.
  document.addEventListener('auxclick', e => {
    if (e.button !== 1) return;
    const tr = e.target.closest('tr[data-row]'); if (!tr) return;
    const f = APP.FN[APP.current], href = f && f.table && APP.detailHref(f.table, tr.dataset.row);
    if (href) { e.preventDefault(); window.open(href, '_blank'); }
  });
  function goBack(screen) {
    const b = APP._back;
    if (b && b.screen === screen) { APP._restoreList = screen; APP._restoreScroll = b.scroll; location.hash = b.hash; return; }
    APP.go(screen);
  }

  // ---------- thành phần ----------
  // Thanh tiến trình trạng thái của chứng từ: bước xong (xanh), hiện tại (tô đậm), lỗi / hủy (đỏ).
  D.steps = steps => `<ol class="d360-steps" aria-label="Tiến trình trạng thái">${(steps || []).map((s, i) => `<li class="${s.state || ''}" ${s.state === 'cur' ? 'aria-current="step"' : ''}><span class="n">${s.state === 'done' ? '✓' : s.state === 'bad' ? '✕' : i + 1}</span><span class="l">${esc(s.label)}</span>${s.sub ? `<span class="s">${esc(s.sub)}</span>` : ''}</li>`).join('')}</ol>`;
  // Bước theo danh sách trạng thái + trạng thái hiện tại (bad = trạng thái lỗi / hủy → bước hiện tại đỏ).
  D.stepsOf = (flow, cur, bad, labelOf) => {
    const i = flow.indexOf(cur), isBad = (bad || []).includes(cur);
    return flow.map((s, k) => ({ label: labelOf ? labelOf(s) : s, state: isBad && k === Math.max(i, 0) ? 'bad' : i < 0 ? (isBad ? '' : '') : k < i ? 'done' : k === i ? 'cur' : '' }))
      .concat(isBad && i < 0 ? [{ label: labelOf ? labelOf(cur) : cur, state: 'bad' }] : []);
  };
  // Tổng quan 1/3 – 2/3.
  D.split = (left, right) => `<div class="dc-two">${left}<div class="dc-col">${right}</div></div>`;
  D.block = (title, body, tools) => `<div class="card panel"><div class="spread mb"><h3 style="margin:0">${esc(title)}</h3>${tools || ''}</div>${body}</div>`;
  D.kv = rows => `<div class="c360-rows">${rows.filter(r => r && r[1] !== null && r[1] !== undefined && r[1] !== '').map(([l, v]) => `<div class="r"><label>${esc(l)}</label><span>${v}</span></div>`).join('') || D.none}</div>`;
  // Tab Duyệt: tiến trình + khuôn tóm tắt APR-UI-1 của yêu cầu duyệt mới nhất (d.approvalRequestId).
  D.approvalTab = (box, d) => {
    const rid = d.approvalRequestId || (d.pendingApproval && d.pendingApproval.id);
    if (!rid) { box.innerHTML = '<div class="muted small">Chứng từ chưa gửi duyệt.</div>'; return; }
    D.run(box, Store.query('ApprovalRequest/GetDetail', { id: rid }), x => {
      const a = x.data || {}, s = a.summary || {}, steps = a.listStepInfo || [];
      box.innerHTML = `${D.kv([['Số yêu cầu', esc((a.request || {}).requestNo || '')], ['Trạng thái', esc(a.statusName || '')], ['Người gửi', esc(a.requestedByName || '')], ['Hạn', a.dueAt ? esc(U.dt(a.dueAt)) + (a.isOverdue ? ' <span class="tag red">Quá hạn</span>' : '') : '']])}
        ${(s.flags || []).length ? `<div class="apr-box warn mt"><div class="apr-cap">Cảnh báo</div>${s.flags.map(f => `<div>• ${esc(f.text)}</div>`).join('')}</div>` : ''}
        <h3 class="mt">Các cấp duyệt</h3><div class="timeline">${steps.map(st => `<div class="tl ${['Rejected', 'Cancelled'].includes(st.decisionStatus) ? 'red' : st.decisionStatus === 'Approved' ? '' : 'orange'}"><b>${st.stepNo}. ${esc(st.stepName || '')} – ${esc(st.decisionStatusName || '')}${st.isCurrent ? ' (đang chờ)' : ''}</b><small>${[st.approverName && esc(st.approverName), st.decidedAt && esc(U.dt(st.decidedAt)), st.decisionNote && esc(st.decisionNote)].filter(Boolean).join(' · ')}</small></div>`).join('') || D.none}</div>
        ${a.canDecide ? `<a class="btn primary mt" href="${APP.href('wf-inbox')}">${I('approve')} Mở màn Phê duyệt</a>` : ''}`;
    });
  };
  D.relatedTab = resource => (box, d, id) => {
    if (!Store.isLive) { box.innerHTML = D.demoNote; return; }
    D.run(box, Store.query('Common/GetRelated', { resource, id }), x => {
      const groups = (x.list || []).filter(g => g.count);
      box.innerHTML = groups.length ? groups.map(g => `<div class="mb"><div class="spread"><b>${esc(g.title)} · ${U.num(g.count)}</b>${g.route && D.can(g.route) ? `<a href="javascript:void 0" data-go="${esc(g.route)}" class="small">Mở màn</a>` : ''}</div>
        <div class="small">${(g.items || []).slice(0, 12).map(i => `${esc(i.label || '')}${i.statusName ? ' (' + esc(i.statusName) + ')' : ''}`).join(' · ')}</div></div>`).join('') : D.none;
      D.bindGo(box);
    });
  };

  // ---------- trang ----------
  D.page = def => {
    const parent = APP.FN[def.screen] || { fn: def.screen, name: def.screen, grp: '' };
    APP.FN[def.id] = { ph: parent.ph, grp: parent.grp, fn: parent.fn, id: def.id, name: parent.name, types: ['DT'], table: null, rel: [], st: 'MAPPED', note: '', detailOf: def.screen };
    if (APP.keepId) APP.keepId(def.id);
    Store.permAlias(def.id, def.screen);
    APP.register(def.id, (el, f, params) => {
      const id = params && params.id;
      const back = `<a href="javascript:void 0" data-back>${I('down')} ${esc(parent.fn || parent.name || 'Danh sách')}</a>`;
      if (!id) { el.innerHTML = `<div class="page-head"><div class="breadcrumb small">${back}</div></div>` + D.failBox('Chưa chọn bản ghi – mở từ danh sách.'); el.querySelector('[data-back]').onclick = () => goBack(def.screen); return; }
      if (!Store.isLive) { el.innerHTML = `<div class="page-head"><div class="breadcrumb small">${back}</div></div>` + D.demoNote; el.querySelector('[data-back]').onclick = () => goBack(def.screen); return; }
      el.innerHTML = `<div class="page-head"><div><div class="breadcrumb small">${back} / <span data-dpno></span></div></div></div>
        <div class="card d360-top"><div data-dphead>${D.loading}</div><div data-dpsteps></div><div class="kpis compact" data-dpkpi></div></div>
        <div class="tabs c360-tabs" data-dptabs></div><div data-dppane></div>`;
      el.querySelector('[data-back]').onclick = () => goBack(def.screen);
      const tok = el.dataset.d360 = String(Date.now() + Math.random());
      const load = () => Store.query(def.api, { id }).then(x => {
        if (!el.isConnected || el.dataset.d360 !== tok) return;
        if (!x.ok || !x.data) { el.querySelector('[data-dphead]').innerHTML = D.failBox(x.message || 'Không tìm thấy bản ghi'); el.querySelector('[data-dpkpi]').remove(); return; }
        const d = x.data, h = def.head(d) || {};
        document.title = (h.title ? String(h.title).replace(/<[^>]*>/g, '') + ' · ' : '') + (parent.name || '') + ' | DELI-CARE';
        el.querySelector('[data-dpno]').textContent = h.no || String(h.title || '').replace(/<[^>]*>/g, '');
        el.querySelector('[data-dphead]').innerHTML = D.headRow({ avatar: h.avatar || D.initials(String(h.title || '').replace(/<[^>]*>/g, '')), avatarCls: h.avatarCls || (def.kind === 'doc' ? 'biz' : 'ind'),
          title: `${h.titleHtml || esc(h.title || '')} ${(h.tags || []).join(' ')}`, meta: h.meta || [], buttons: h.buttons || [], more: h.more || [] });
        el.querySelector('[data-dpsteps]').innerHTML = def.kind === 'doc' && h.steps && h.steps.length ? D.steps(h.steps) : '';
        const kpi = el.querySelector('[data-dpkpi]'); kpi.innerHTML = def.kpis ? def.kpis(d) : ''; if (!kpi.innerHTML) kpi.remove();
        D.bindGo(kpi);
        D.bindMore(el.querySelector('[data-dphead]'), act => def.onAct && def.onAct(act, d, id, () => { delete el.dataset.d360; APP.refresh(); }));
        const res = def.resource;
        const tabs = [{ label: 'Tổng quan', render: (b, dd) => def.overview ? def.overview(b, dd, id) : (b.innerHTML = D.none) }].concat(def.tabs || [])
          .concat(def.approval !== false && (d.approvalRequestId || d.pendingApproval) ? [{ label: 'Duyệt', render: D.approvalTab }] : [])
          .concat(res && def.related !== false ? [{ label: 'Chứng từ liên quan', render: D.relatedTab(res) }] : [])
          .concat(res && def.docs !== false ? [{ label: 'Tài liệu', render: D.documentsTab(res, D.can(def.screen, 'edit')) }] : [])
          .concat(KB_OF[def.table] && def.knowledge !== false ? [{ label: 'Tri thức', render: D.knowledgeTab(KB_OF[def.table]) }] : [])
          .concat(res ? [{ label: 'Lịch sử', render: D.historyTab(res, true) }] : []);
        D.buildTabs(el.querySelector('[data-dptabs]'), el.querySelector('[data-dppane]'), tabs, d, id);
      });
      load();
    });
  };
})();
