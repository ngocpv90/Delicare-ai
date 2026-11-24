/* UI-6.18 (D-99): mua hàng liền mạch.
 * A. Form "Đề nghị mua" (thay UI.form('PurchaseRequest') chế độ API + nút "Sửa dòng" ở trang chi tiết bản Nháp): số / ngày lập / người đề nghị máy chủ tự điền (chỉ đọc),
 *    không có ô trạng thái; Ngày cần hàng*, Ghi chú, bảng nguyên liệu (gõ tên để thêm; số lượng; ĐVT quy đổi được; ngày cần từng dòng; tồn hiện tại; NCC gợi ý · giá – quy tắc C;
 *    cảnh báo dưới tối thiểu / chưa có NCC) + Tổng ước tính; Lưu nháp · Gửi duyệt luôn (API PurchaseRequest/Create, UpdateLines, GetSupplierSuggestion).
 * D. Màn Đơn mua hàng: khối trên cùng "Chọn nhà cung cấp" (dòng đề nghị đã duyệt chưa có PO, nhóm theo nhóm nguyên liệu, ≤ 3 thẻ NCC – bấm thẻ = chọn, "Theo gợi ý cho tất cả",
 *    cột "Đơn mua sẽ lập" gom theo NCC) + "Chờ gửi NCC" (PO Đã duyệt – Gửi / Gửi tất cả) + 4 chỉ số. Danh sách đơn mua cũ giữ bên dưới. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const nil = v => v === null || v === undefined || v === '';
  const day = v => (v ? String(v).slice(0, 10) : '');
  const ingBox = (inp, onPick) => {
    const dl = document.createElement('datalist'); dl.id = 'pr' + Math.random().toString(36).slice(2); inp.setAttribute('list', dl.id); inp.after(dl);
    let map = {}, t;
    inp.oninput = () => { clearTimeout(t); const q = inp.value.trim(); if (map[q]) { onPick(map[q]); return; } if (q.length < 2) return;
      t = setTimeout(() => Store.query('Ingredient/GetAll', { SearchText: q, Page: 1, PageSize: 20 }).then(x => { map = {}; (x.list || []).forEach(r => { map[(r.name || '') + ' · ' + (r.code || '')] = r; }); dl.innerHTML = Object.keys(map).map(k => `<option value="${esc(k)}">`).join(''); }), 250); };
  };
  // ---------- A. Form đề nghị mua ----------
  function prForm(row, opt = {}) {
    const isNew = !row || !row.Id;
    const st = { lines: [], sug: {} };
    const load = isNew ? Promise.resolve(null) : Store.query('PurchaseRequest/GetDetail', { id: row.Id });
    load.then(x => {
      const d = x && x.ok ? x.data : null, pr = d ? d.purchaseRequest : {};
      if (d) st.lines = (d.listLine || []).map(l => ({ key: l.id, ingredientId: l.ingredientId, name: l.ingredientName || '', quantity: l.quantity, unitId: l.unitId, requiredDate: day(l.requiredDate), note: l.note || '' }));
      UI.modal({ title: isNew ? 'Đề nghị mua hàng' : 'Sửa đề nghị mua ' + esc(pr.requestNumber || ''), sub: isNew ? 'Số, ngày lập, người đề nghị hệ thống tự điền khi lưu · trạng thái: Nháp' : 'Nháp – sửa dòng trước khi gửi duyệt', size: 'xl',
        body: `<div class="form-grid"><div class="field"><label>Ngày cần hàng <span class="req">*</span></label><input type="date" class="input" id="prNeed" value="${day(pr.requiredDate) || U.addDays(Store.ctx.date, 2)}"></div>
          <div class="field"><label>Ghi chú / lý do</label><input class="input" id="prNote" value="${esc(pr.note || '')}"></div>
          <div class="field"><label>Số đề nghị</label><input class="input" value="${esc(pr.requestNumber || 'Tự sinh khi lưu')}" disabled></div><div class="field"><label>Người đề nghị · ngày lập</label><input class="input" value="${esc(d ? (d.requesterName || '') + ' · ' + U.date(pr.requestDate) : 'Tài khoản đang đăng nhập · hôm nay')}" disabled></div></div>
          <div class="table-wrap mt"><table class="table dc-table pr-lines"><thead><tr><th>Nguyên liệu</th><th class="num">Số lượng</th><th>ĐVT</th><th>Ngày cần</th><th class="num">Tồn hiện tại</th><th>NCC gợi ý · giá</th><th class="num">Ước tính</th><th></th></tr></thead><tbody id="prBody"></tbody></table></div>
          <div class="row-flex mt"><input class="input" id="prAdd" placeholder="+ Gõ tên nguyên liệu để thêm dòng" style="max-width:360px"><span class="spacer"></span><b>Tổng giá trị ước tính: <span id="prTotal">0 ₫</span></b></div>`,
        foot: `<button class="btn" data-close>Hủy</button><button class="btn" id="prSave">Lưu nháp</button><button class="btn primary" id="prSub">Gửi duyệt luôn</button>`,
        onMount: (m, close) => {
          const g = s => m.querySelector(s);
          let tq;
          const refresh = () => { clearTimeout(tq); tq = setTimeout(() => {
            if (!st.lines.length) { draw(); return; }
            Store.act('PurchaseRequest/GetSupplierSuggestion', { json: { requiredDate: g('#prNeed').value, listLine: st.lines.map(l => ({ key: l.key, ingredientId: l.ingredientId, quantity: +l.quantity || 0, unitId: l.unitId || null, requiredDate: l.requiredDate || null })) }, quiet: true, tables: [] })
              .then(x => { st.sug = {}; ((x.res && x.res.listData) || []).forEach(s => { st.sug[s.key] = s; }); draw(); });
          }, 300); };
          const draw = () => {
            let total = 0;
            g('#prBody').innerHTML = st.lines.map((l, i) => {
              const s = st.sug[l.key] || {}, card = (s.listCard || []).find(c => c.suggested), amt = card && card.amount ? card.amount : 0; total += amt;
              return `<tr data-i="${i}"><td><b>${esc(l.name)}</b>${s.belowMinimum ? '<div class="small bad">Dưới tồn tối thiểu</div>' : ''}</td><td class="num"><input type="number" min="0" step="any" class="input pr-q" data-q value="${l.quantity || ''}"></td>
                <td><select class="select pr-u" data-u></select></td><td><input type="date" class="input" data-d value="${l.requiredDate || ''}"></td><td class="num">${nil(s.stockQuantity) ? '–' : U.num(s.stockQuantity, 2)}</td>
                <td class="small">${s.supplierName ? `<b>${esc(s.supplierName)}</b> · ${U.money(s.unitPrice)}${card && card.note ? `<div class="muted">${esc(card.note)}</div>` : ''}` : `<span class="bad">Cần chọn NCC${s.reason ? ': ' + esc(s.reason) : ''}</span>`}${s.reason && s.supplierName ? `<div class="muted">${esc(s.reason)}</div>` : ''}</td>
                <td class="num">${amt ? U.money(amt) : '–'}</td><td><button class="btn xs danger" data-x>✕</button></td></tr>`;
            }).join('') || '<tr><td colspan="8" class="muted">Chưa có dòng – gõ tên nguyên liệu bên dưới để thêm</td></tr>';
            g('#prTotal').textContent = U.money(total);
            m.querySelectorAll('#prBody [data-i]').forEach(tr => {
              const l = st.lines[+tr.dataset.i];
              UI.unitSelect(tr.querySelector('[data-u]'), { ingredientId: l.ingredientId, value: l.unitId }).then(list => { if (!l.unitId && list.length) l.unitId = (list.find(u => u.isBase) || list[0]).id; });
              tr.querySelector('[data-q]').onchange = e => { l.quantity = +e.target.value; refresh(); };
              tr.querySelector('[data-u]').onchange = e => { l.unitId = e.target.value; refresh(); };
              tr.querySelector('[data-d]').onchange = e => { l.requiredDate = e.target.value; refresh(); };
              tr.querySelector('[data-x]').onclick = () => { st.lines.splice(+tr.dataset.i, 1); refresh(); };
            });
          };
          ingBox(g('#prAdd'), r => {
            g('#prAdd').value = '';
            const ex = st.lines.find(l => l.ingredientId === r.id);
            if (ex) { UI.toast('Nguyên liệu đã có trong đề nghị – sửa số lượng ở dòng đó', 'warn'); return; }
            st.lines.push({ key: U.uuid(), ingredientId: r.id, name: r.name || '', quantity: 1, unitId: null, requiredDate: '' }); refresh();
            setTimeout(() => { const q = [...m.querySelectorAll('[data-q]')].pop(); if (q) { q.focus(); q.select(); } }, 400);
          });
          g('#prNeed').onchange = refresh;
          draw(); refresh();
          const save = submit => {
            if (!g('#prNeed').value) return UI.toast('Chọn ngày cần hàng', 'err');
            if (!st.lines.length) return UI.toast('Đề nghị phải có ít nhất 1 dòng nguyên liệu', 'err');
            if (st.lines.some(l => !(+l.quantity > 0))) return UI.toast('Số lượng mỗi dòng phải lớn hơn 0', 'err');
            const listLine = st.lines.map(l => ({ ingredientId: l.ingredientId, quantity: +l.quantity, unitId: l.unitId || null, requiredDate: l.requiredDate || null, note: l.note || null }));
            const call = isNew ? Store.act('PurchaseRequest/Create', { json: { requiredDate: g('#prNeed').value, note: g('#prNote').value.trim() || null, submit, listLine }, tables: ['PurchaseRequest', 'PurchaseRequestLine'] })
              : Store.act('PurchaseRequest/UpdateLines', { json: { id: row.Id, requiredDate: g('#prNeed').value, note: g('#prNote').value, listLine }, tables: ['PurchaseRequest', 'PurchaseRequestLine'] })
                .then(x => (x.ok && submit ? Store.act('PurchaseRequest/Submit', { form: { Id: row.Id }, tables: ['PurchaseRequest'] }) : x));
            call.then(x => { if (!x.ok) return; close(); UI.toast(esc((x.res && x.res.responseMessage) || (submit ? 'Đã gửi duyệt' : 'Đã lưu nháp')), 'ok'); if (opt.onSave) opt.onSave(x); else APP.refresh(); });
          };
          g('#prSave').onclick = () => save(false); g('#prSub').onclick = () => save(true);
        } });
    });
  }
  APP.prForm = prForm;
  const baseForm = UI.form;
  UI.form = (t, row, opt) => (t === 'PurchaseRequest' && Store.isLive ? prForm(row, opt || {}) : baseForm(t, row, opt));

  // ---------- D. Màn Đơn mua hàng: Chọn nhà cung cấp + Chờ gửi NCC ----------
  const pk = { tab: 'pick', chosen: {}, data: null };
  const base = APP.SCREENS['scm-po'];
  APP.register('scm-po', (el, f, p) => {
    base(el, f, p);
    if (!Store.isLive) return;
    const anchor = el.querySelector('.page-heading'); if (!anchor) return;
    const host = document.createElement('div'); host.className = 'card panel mb'; host.innerHTML = '<div class="muted small">Đang tải việc mua hàng…</div>';
    anchor.insertAdjacentElement('afterend', host);
    const canAdd = Store.perm('scm-po').add !== false, canEdit = Store.perm('scm-po').edit !== false;
    const draw = () => {
      const d = pk.data; if (!d || !host.isConnected) return;
      const lines = d.listLine || [], unsent = (d.listUnsent || []).filter(u => u.status === 'Approved');
      const chosenAmt = lines.reduce((s, l) => { const c = (l.listCard || []).find(x => x.supplierId === pk.chosen[l.purchaseRequestLineId]); return s + (c && c.amount ? c.amount : 0); }, 0);
      host.innerHTML = `<div class="small muted mb">Đề nghị đã duyệt → <b>Chọn nhà cung cấp</b> (dòng hệ thống chưa tự lập được đơn) → Duyệt đơn mua → <b>Gửi nhà cung cấp</b>.</div>
        ${UI.kpis([UI.kpi('Dòng cần chọn NCC', U.num(d.needPickCount), '', 'alert', d.needPickCount ? 'orange' : 'green'), UI.kpi('Đơn chờ gửi', U.num(d.toSendCount), '', 'package', d.toSendCount ? 'orange' : 'green'),
          UI.kpi('Đơn chờ duyệt', U.num(d.pendingApprovalCount), '', 'approve', 'blue'), UI.kpi('Giá trị đang chọn', U.moneyShort(chosenAmt), '', 'calc', 'blue')], 'compact')}
        <div class="tabs mt"><button class="tab ${pk.tab === 'pick' ? 'active' : ''}" data-pt="pick">Chọn nhà cung cấp <span class="od-cnt ${d.needPickCount ? 'warn' : ''}">${d.needPickCount}</span></button><button class="tab ${pk.tab === 'send' ? 'active' : ''}" data-pt="send">Chờ gửi NCC <span class="od-cnt">${unsent.length}</span></button></div>
        <div id="pkBody" class="mt"></div>`;
      host.querySelectorAll('[data-pt]').forEach(b => b.onclick = () => { pk.tab = b.dataset.pt; draw(); });
      const body = host.querySelector('#pkBody');
      if (pk.tab === 'send') {
        body.innerHTML = unsent.length ? `<div class="row-flex mb">${canEdit ? `<button class="btn sm primary" id="pkSendAll">${I('up')} Gửi tất cả (${unsent.length})</button>` : ''}</div><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Số PO</th><th>Nhà cung cấp</th><th class="num">Giá trị</th><th>Nguồn</th><th>Duyệt</th><th></th></tr></thead><tbody>${unsent.map(u => `<tr><td><a href="${APP.detailHref ? APP.detailHref('PurchaseOrder', u.id) : '#'}">${esc(u.purchaseOrderNumber || '')}</a></td><td>${esc(u.supplierName || '')}</td><td class="num">${U.money(u.totalAmount)}</td><td>${esc(u.source || '')}</td><td>${u.autoApproved ? UI.tag('Hệ thống tự duyệt', 'green') : 'Đã duyệt'}</td><td>${canEdit ? `<button class="btn xs" data-send="${esc(u.id)}">Gửi</button> ` : ''}<button class="btn xs" data-exp="${esc(u.id)}">${I('down')} Phiếu đặt hàng</button></td></tr>`).join('')}</tbody></table></div>` : '<div class="muted small">Không có đơn mua chờ gửi</div>';
        const send = ids => Store.act('PurchaseOrder/SendMany', { json: { listId: ids }, tables: ['PurchaseOrder'] }).then(x => { if (x.ok) { UI.toast(esc((x.res && x.res.responseMessage) || 'Đã gửi'), 'ok'); load(); APP.refresh(); } });
        body.querySelectorAll('[data-send]').forEach(b => b.onclick = () => send([b.dataset.send]));
        body.querySelectorAll('[data-exp]').forEach(b => b.onclick = () => Store.download('PurchaseOrder/ExportOrder', { id: b.dataset.exp }));
        const all = body.querySelector('#pkSendAll'); if (all) all.onclick = () => UI.confirm('Gửi ' + unsent.length + ' đơn mua cho nhà cung cấp?', () => send(unsent.map(u => u.id)), { ok: 'Gửi tất cả' });
        return;
      }
      if (!lines.length) { body.innerHTML = '<div class="muted small">Không có dòng nào cần chọn nhà cung cấp – hệ thống đã tự lập đơn mua cho mọi đề nghị đã duyệt.</div>'; return; }
      const groups = {}; lines.forEach(l => { (groups[l.categoryName || 'Chưa phân nhóm'] = groups[l.categoryName || 'Chưa phân nhóm'] || []).push(l); });
      const bySup = {}; lines.forEach(l => { const sid = pk.chosen[l.purchaseRequestLineId]; if (!sid) return; const c = (l.listCard || []).find(x => x.supplierId === sid); (bySup[sid] = bySup[sid] || { name: c ? c.supplierName : '', amt: 0, n: 0, pref: true }); bySup[sid].amt += c && c.amount ? c.amount : 0; bySup[sid].n++; bySup[sid].pref = bySup[sid].pref && !!(c && c.isPreferred && c.eligible); });
      const ready = lines.length && lines.every(l => pk.chosen[l.purchaseRequestLineId]);
      body.innerHTML = `<div class="pk-two"><div><div class="row-flex mb"><button class="btn sm" id="pkSuggest">Theo gợi ý cho tất cả</button></div>${Object.entries(groups).map(([g, ls]) => `<div class="pk-grp"><b>${esc(g)}</b>${ls.map(l => `<div class="pk-line"><div class="pk-info"><b>${esc(l.ingredientName || '')}</b><div class="small">${U.num(l.quantity, 2)} ${esc(l.unitName || '')} · cần ${U.date(l.needDate)} · ${esc(l.purchaseRequestNumber || '')}</div>${l.reason ? `<span class="tag orange">${esc(l.reason)}</span>` : ''}</div>
          <div class="pk-cards">${(l.listCard || []).map(c => `<button class="pk-card ${pk.chosen[l.purchaseRequestLineId] === c.supplierId ? 'on' : ''} ${c.eligible ? '' : 'off'}" data-l="${esc(l.purchaseRequestLineId)}" data-s="${esc(c.supplierId)}" ${c.eligible ? '' : 'disabled'} title="${esc(c.reason || '')}">
            <div class="spread"><b>${esc(c.supplierName || '')}</b><span>${c.suggested ? UI.tag('Gợi ý', 'green') : ''}${c.isPreferred ? UI.tag('Ưu tiên', 'blue') : ''}</span></div>
            <div class="small">${nil(c.pricePerLineUnit) ? '–' : U.money(c.pricePerLineUnit) + ' / ' + esc(l.unitName || '')} · giao ${nil(c.leadTimeDays) ? '–' : c.leadTimeDays + ' ngày'} · ${nil(c.onTimePct) ? 'chưa có lịch sử' : U.num(c.onTimePct, 0) + '% đúng hẹn'} · ${c.issueCount90} sự cố</div>
            ${c.note ? `<div class="small muted">${esc(c.note)}</div>` : ''}${!nil(c.changePct) ? `<div class="small ${c.changePct >= 10 ? 'bad' : ''}">${c.changePct > 0 ? '+' : ''}${U.num(c.changePct, 1)}% lần trước</div>` : ''}${c.eligible ? `<div class="num"><b>${c.amount ? U.money(c.amount) : '–'}</b></div>` : `<div class="small bad">${esc(c.reason || '')}</div>`}</button>`).join('') || '<span class="muted small">Chưa NCC nào có giá</span>'}</div></div>`).join('')}</div>`).join('')}</div>
        <div class="pk-side"><h3>Đơn mua sẽ lập</h3>${Object.keys(bySup).length ? Object.entries(bySup).map(([sid, s]) => `<div class="pk-po"><div class="spread"><b>${esc(s.name || '')}</b><span>${U.money(s.amt)}</span></div><div class="small">${s.n} dòng · ${s.amt >= ((APP.rules && APP.rules.purchaseOrderDirectorApprovalAmount) || 50000000) ? '<span class="bad">Chờ Giám đốc duyệt (≥ 50 tr)</span>' : s.pref ? '<span class="up">Tự duyệt → chờ gửi</span>' : '<span>Gửi Trưởng Mua duyệt (có NCC không ưu tiên)</span>'}</div></div>`).join('') : '<div class="muted small">Bấm thẻ NCC để chọn</div>'}
          ${canAdd ? `<button class="btn primary mt" id="pkCreate" ${Object.keys(pk.chosen).length ? '' : 'disabled'}>Lập đơn mua${ready ? '' : ' (dòng đã chọn)'}</button>` : ''}</div></div>`;
      body.querySelectorAll('[data-l]').forEach(b => b.onclick = () => { pk.chosen[b.dataset.l] = b.dataset.s; draw(); });
      body.querySelector('#pkSuggest').onclick = () => { lines.forEach(l => { const c = (l.listCard || []).find(x => x.suggested); if (c) pk.chosen[l.purchaseRequestLineId] = c.supplierId; }); draw(); };
      const cb = body.querySelector('#pkCreate'); if (cb) cb.onclick = () => {
        const listLine = Object.entries(pk.chosen).filter(([lid]) => lines.some(l => l.purchaseRequestLineId === lid)).map(([lid, sid]) => ({ purchaseRequestLineId: lid, supplierId: sid }));
        Store.act('PurchaseOrder/CreateFromPick', { json: { listLine }, tables: ['PurchaseOrder', 'PurchaseOrderLine', 'PurchaseRequest'] }).then(x => { if (!x.ok) return; UI.toast(esc((x.res && x.res.responseMessage) || 'Đã lập đơn mua'), 'ok'); pk.chosen = {}; load(); APP.refresh(); });
      };
    };
    const load = () => Store.query('PurchaseOrder/GetSupplierPickBoard', {}).then(x => { if (!x.ok || !x.data) { host.innerHTML = `<div class="small muted">${esc(x.message || 'Không tải được việc mua hàng')}</div>`; return; } pk.data = x.data; draw(); });
    load();
  });
})();
