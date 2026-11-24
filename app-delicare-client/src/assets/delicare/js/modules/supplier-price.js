/* UI-6.19 (D-100): bảng giá nhà cung cấp.
 * A. Form "Giá nguyên liệu của nhà cung cấp" (thay UI.form('SupplierMaterial') chế độ API – danh sách, trang NCC, trang nguyên liệu): NCC* / Nguyên liệu* (ô tìm theo
 *    từ khóa), Đơn giá* + Giá tính theo* (chỉ đơn vị quy đổi được về ĐVT kho – UI.unitSelect, + Tạo đơn vị đóng gói), "= X đ / ĐVT kho", so với giá trước ±%
 *    (≥ ngưỡng → đỏ "đơn mua sẽ phải duyệt"), Áp dụng từ* / Đến ngày (chỉ ngày), Đặt tối thiểu, Số ngày giao, Ngày giao trong tuần (T2…CN / Hằng ngày), Ưu tiên,
 *    Mã hàng NCC, Yêu cầu chất lượng. Cột phải: Lịch sử giá + So với NCC khác. Giá đã áp dụng → "Lưu giá mới từ ngày" (SaveNewPrice), không ghi đè.
 * C. Màn "Bảng giá nhà cung cấp" (scm-supplier-material): bảng nguyên liệu × NCC tại 1 ngày, 4 chỉ số, chip việc cần làm, ô bấm sửa / + Thêm giá, Đặt ưu tiên,
 *    chọn nhiều → Gia hạn, Nhập báo giá Excel theo NCC (xem trước → áp dụng), Xuất Excel. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const nil = v => v === null || v === undefined || v === '';
  const DAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
  const pct = () => (APP.rules && APP.rules.approvalFlagPricePct) || 10;
  const day = v => (v ? String(v).slice(0, 10) : '');
  const can = a => { const p = Store.perm('scm-supplier-material'); return p.access !== false && p[a] !== false; };
  // Ô tìm theo từ khóa (không giới hạn 400 dòng): input + datalist, tra API GetAll.
  const searchBox = (inp, api, label, onPick) => {
    const dl = document.createElement('datalist'); dl.id = 'dl' + Math.random().toString(36).slice(2); inp.setAttribute('list', dl.id); inp.after(dl);
    let map = {}, t;
    inp.oninput = () => {
      clearTimeout(t); const q = inp.value.trim();
      if (map[q]) return onPick(map[q]);
      if (q.length < 2) return;
      t = setTimeout(() => Store.query(api, { SearchText: q, Page: 1, PageSize: 20 }).then(x => { map = {}; (x.list || []).forEach(r => { map[label(r)] = r; }); dl.innerHTML = Object.keys(map).map(k => `<option value="${esc(k)}">`).join(''); }), 250);
    };
  };
  function priceForm(row, opt = {}) {
    const r = row || Object.assign({}, opt.defaults || {}), isNew = !r.Id;
    const st = { supplierId: r.SupplierId || '', ingredientId: r.IngredientId || '', base: null, factor: 1, units: [] };
    const fixedSup = !!(opt.defaults && opt.defaults.SupplierId) || !isNew, fixedIng = !!(opt.defaults && opt.defaults.IngredientId) || !isNew;
    const freq = String(r.DeliveryFrequency || ''), daysOn = freq === 'HANG_NGAY' ? DAYS : freq.split(',').filter(d => DAYS.includes(d));
    const oldFreq = freq && freq !== 'HANG_NGAY' && !daysOn.length ? freq : '';
    const applied = !isNew && r.EffectiveFrom && day(r.EffectiveFrom) <= Store.ctx.date;
    UI.modal({ title: 'Giá nguyên liệu của nhà cung cấp', sub: isNew ? 'Giá mới – giá đang áp dụng của cùng NCC × nguyên liệu tự kết thúc hôm trước' : (applied ? 'Giá đã áp dụng – đổi đơn giá / đơn vị sẽ lưu thành giá mới' : 'Giá chưa áp dụng – sửa trực tiếp'), size: 'xl',
      body: `<div class="sp-two"><div>
        <h3>Nhà cung cấp &amp; nguyên liệu</h3><div class="form-grid">
          <div class="field"><label>Nhà cung cấp <span class="req">*</span></label><input class="input" id="spSup" value="${esc(fixedSup ? Store.label('Supplier', r.SupplierId) : '')}" ${fixedSup ? 'disabled' : 'placeholder="Gõ tên / mã NCC"'}></div>
          <div class="field"><label>Nguyên liệu <span class="req">*</span></label><input class="input" id="spIng" value="${esc(fixedIng ? Store.label('Ingredient', r.IngredientId) : '')}" ${fixedIng ? 'disabled' : 'placeholder="Gõ tên / mã nguyên liệu"'}></div></div>
        <h3 class="mt">Giá</h3><div class="form-grid">
          <div class="field"><label>Đơn giá (đ) <span class="req">*</span></label><input type="number" min="0" step="any" class="input" id="spPrice" value="${nil(r.UnitPrice) ? '' : r.UnitPrice}"><div class="small" id="spPer"></div></div>
          <div class="field"><label>Giá tính theo <span class="req">*</span></label><div class="row-flex"><select class="select" id="spUnit"><option value="">— Chọn nguyên liệu trước —</option></select><button class="btn sm" id="spPack" title="Tạo đơn vị đóng gói">${I('plus')}</button></div></div></div>
        <h3 class="mt">Thời gian áp dụng</h3><div class="form-grid">
          <div class="field"><label>Áp dụng từ ngày <span class="req">*</span></label><input type="date" class="input" id="spFrom" value="${day(r.EffectiveFrom) || Store.ctx.date}"></div>
          <div class="field"><label>Đến ngày</label><input type="date" class="input" id="spTo" value="${day(r.EffectiveTo)}"><div class="small muted">Trống = chưa hết hạn</div></div></div>
        <h3 class="mt">Điều kiện giao</h3><div class="form-grid">
          <div class="field"><label>Đặt tối thiểu <span class="small muted" id="spMoqU"></span></label><input type="number" min="0" step="any" class="input" id="spMoq" value="${nil(r.MinimumOrderQuantity) ? '' : r.MinimumOrderQuantity}"></div>
          <div class="field"><label>Số ngày giao (kể từ lúc đặt)</label><input type="number" min="0" class="input" id="spLead" value="${nil(r.LeadTimeDays) ? '' : r.LeadTimeDays}"></div>
          <div class="field" style="grid-column:1/-1"><label>Ngày giao trong tuần</label><div class="row-flex sp-days">${DAYS.map(d => `<label class="chip-radio"><input type="checkbox" data-day="${d}" ${daysOn.includes(d) ? 'checked' : ''}> ${d}</label>`).join('')}<label class="chip-radio"><input type="checkbox" id="spDaily" ${freq === 'HANG_NGAY' ? 'checked' : ''}> Hằng ngày</label>${oldFreq ? `<span class="small muted">${esc(oldFreq)} (giá trị cũ)</span>` : ''}</div></div></div>
        <h3 class="mt">Ưu tiên &amp; thông tin thêm</h3><div class="form-grid">
          <div class="field"><label class="row-flex"><input type="checkbox" id="spPref" ${r.IsPreferred ? 'checked' : ''}> Nhà cung cấp ưu tiên cho nguyên liệu này</label><div class="small muted">Mỗi nguyên liệu 1 NCC ưu tiên – bật sẽ bỏ ưu tiên NCC khác.</div></div>
          <div class="field"><label>Mã hàng của NCC</label><input class="input" id="spCode" value="${esc(r.SupplierMaterialCode || '')}"></div>
          <div class="field" style="grid-column:1/-1"><label>Yêu cầu chất lượng</label><input class="input" id="spQa" value="${esc(r.QualityRequirement || '')}"></div></div>
        </div><div class="sp-side"><h3>Lịch sử giá</h3><div id="spHist" class="small muted">Chọn NCC và nguyên liệu</div><h3 class="mt">So với NCC khác</h3><div id="spCmp" class="small muted">Chọn nguyên liệu</div></div></div>`,
      foot: `<button class="btn" data-close>Hủy</button>${!isNew && applied ? '<button class="btn" id="spSaveInfo" title="Chỉ lưu tối thiểu / ngày giao / mã hàng / yêu cầu chất lượng / ngày kết thúc">Lưu thông tin phụ</button>' : ''}<button class="btn primary" id="spOk">${isNew ? 'Lưu giá' : applied ? 'Lưu giá mới từ ngày' : 'Lưu'}</button>`,
      onMount: (m, close) => {
        const g = s => m.querySelector(s);
        let hist = [];
        const per = () => {
          const p = +g('#spPrice').value, u = st.units.find(x => x.id === g('#spUnit').value), base = st.units.find(x => x.isBase);
          g('#spMoqU').textContent = u ? '(' + (u.code || '') + ')' : '';
          if (!(p > 0) || !u) { g('#spPer').innerHTML = ''; return; }
          const pb = p / (u.factor || 1), prev = hist.find(h => h.id !== r.Id && day(h.effectiveFrom) <= g('#spFrom').value && h.pricePerBase);
          const ch = prev ? (pb - prev.pricePerBase) / prev.pricePerBase * 100 : null;
          g('#spPer').innerHTML = `= ${U.money(Math.round(pb))} / ${esc((base && base.code) || '')}${ch === null ? '' : ` · so với giá trước: <span class="${ch >= pct() ? 'bad' : ch < 0 ? 'up' : ''}">${ch > 0 ? '+' : ''}${U.num(ch, 1)}%</span>${ch >= pct() ? ' – <b class="bad">đơn mua sẽ phải duyệt</b>' : ''}`}`;
        };
        const loadSide = () => {
          if (st.supplierId && st.ingredientId) Store.query('SupplierMaterial/GetPriceHistory', { supplierId: st.supplierId, ingredientId: st.ingredientId }).then(x => {
            hist = x.list || [];
            g('#spHist').innerHTML = hist.length ? `<table class="table dc-table"><thead><tr><th>Áp dụng</th><th class="num">Giá / ĐVT kho</th><th class="num">%</th></tr></thead><tbody>${hist.map(h => `<tr class="${h.id === r.Id ? 'sp-cur' : ''}"><td>${U.date(h.effectiveFrom)} – ${h.effectiveTo ? U.date(h.effectiveTo) : '…'}</td><td class="num">${nil(h.pricePerBase) ? '–' : U.money(h.pricePerBase)}<div class="small muted">${U.money(h.unitPrice)} / ${esc(h.unitName || '')}</div></td><td class="num">${nil(h.changePct) ? '' : (h.changePct > 0 ? '+' : '') + U.num(h.changePct, 1) + '%'}</td></tr>`).join('')}</tbody></table>` : 'Chưa có giá trước đây';
            per();
          });
          if (st.ingredientId) Store.query('SupplierMaterial/GetCompare', { ingredientId: st.ingredientId, date: g('#spFrom').value }).then(x => {
            const l = x.list || [];
            g('#spCmp').innerHTML = l.length ? l.map(c => `<div class="spread sp-cmp"><span>${esc(c.supplierName || '')} ${c.isPreferred ? UI.tag('Ưu tiên', 'green') : ''}${c.isCheapest ? ' ' + UI.tag('Rẻ nhất', 'blue') : ''}</span><span class="num">${nil(c.pricePerBase) ? '–' : U.money(c.pricePerBase)}<span class="small muted"> · ${nil(c.onTimePct) ? 'chưa có lịch sử' : U.num(c.onTimePct, 0) + '% đúng hẹn'}</span></span></div>`).join('') : 'Chưa NCC nào có giá đang áp dụng';
          });
        };
        const loadUnits = () => {
          if (!st.ingredientId) return;
          UI.unitSelect(g('#spUnit'), { ingredientId: st.ingredientId, value: r.PurchaseUomId }).then(list => { st.units = list; per(); });
        };
        g('#spPack').onclick = () => { const base = st.units.find(x => x.isBase); APP.packUnitForm({ baseUnitId: base && base.id, onSave: u => { r.PurchaseUomId = u.id; loadUnits(); } }); };
        if (!fixedSup) searchBox(g('#spSup'), 'Supplier/GetAll', s => (s.name || '') + ' · ' + (s.code || ''), s => { st.supplierId = s.id; loadSide(); });
        if (!fixedIng) searchBox(g('#spIng'), 'Ingredient/GetAll', s => (s.name || '') + ' · ' + (s.code || ''), s => { st.ingredientId = s.id; r.PurchaseUomId = null; loadUnits(); loadSide(); });
        ['#spPrice', '#spUnit', '#spFrom'].forEach(s => { g(s).addEventListener('input', per); g(s).addEventListener('change', per); });
        g('#spDaily').onchange = () => m.querySelectorAll('[data-day]').forEach(c => { c.checked = g('#spDaily').checked; });
        // Tên NCC / nguyên liệu khi mở từ bảng (dữ liệu máy khách có thể chưa nạp) → đọc GetById.
        if (fixedSup && st.supplierId) Store.query('Supplier/GetById', { id: st.supplierId }).then(x => { if (x.ok && x.data) g('#spSup').value = (x.data.name || '') + ' · ' + (x.data.code || ''); });
        if (fixedIng && st.ingredientId) Store.query('Ingredient/GetById', { id: st.ingredientId }).then(x => { if (x.ok && x.data) g('#spIng').value = (x.data.name || '') + ' · ' + (x.data.code || ''); });
        loadUnits(); loadSide();
        const form = () => {
          const ds = [...m.querySelectorAll('[data-day]:checked')].map(c => c.dataset.day);
          return { SupplierId: st.supplierId, IngredientId: st.ingredientId, UnitPrice: +g('#spPrice').value || null, PurchaseUomId: g('#spUnit').value || null, EffectiveFrom: g('#spFrom').value, EffectiveTo: g('#spTo').value || null,
            MinimumOrderQuantity: g('#spMoq').value === '' ? null : +g('#spMoq').value, LeadTimeDays: g('#spLead').value === '' ? null : +g('#spLead').value,
            DeliveryFrequency: g('#spDaily').checked || ds.length === 7 ? 'HANG_NGAY' : ds.length ? ds.join(',') : (oldFreq || null), IsPreferred: g('#spPref').checked,
            SupplierMaterialCode: g('#spCode').value.trim() || null, QualityRequirement: g('#spQa').value.trim() || null, Status: 'Active' };
        };
        const done = x => { if (!x.ok) return; close(); const msg = (x.res && x.res.responseMessage) || 'Đã lưu giá'; UI.toast(esc(msg), /Bỏ ưu tiên|tự kết thúc/.test(msg) ? 'warn' : 'ok'); if (opt.onSave) opt.onSave(x); else APP.refresh(); };
        const check = f => { if (!f.SupplierId || !f.IngredientId) return 'Chọn nhà cung cấp và nguyên liệu'; if (!(f.UnitPrice > 0)) return 'Nhập đơn giá lớn hơn 0'; if (!f.PurchaseUomId) return 'Chọn "Giá tính theo"'; if (!f.EffectiveFrom) return 'Chọn ngày áp dụng'; return ''; };
        g('#spOk').onclick = () => {
          const f = form(), e = check(f); if (e) return UI.toast(e, 'err');
          if (isNew) return Store.act('SupplierMaterial/Insert', { form: f, tables: ['SupplierMaterial'] }).then(done);
          const priceChanged = +f.UnitPrice !== +r.UnitPrice || f.PurchaseUomId !== r.PurchaseUomId || f.EffectiveFrom !== day(r.EffectiveFrom);
          if (applied && priceChanged) return Store.act('SupplierMaterial/SaveNewPrice', { form: f.EffectiveFrom === day(r.EffectiveFrom) ? Object.assign(f, { EffectiveFrom: Store.ctx.date }) : f, tables: ['SupplierMaterial'] }).then(done);
          if (applied && !priceChanged) return Store.act('SupplierMaterial/Update', { method: 'PUT', form: Object.assign({ Id: r.Id }, f), tables: ['SupplierMaterial'] }).then(done);
          return Store.act('SupplierMaterial/Update', { method: 'PUT', form: Object.assign({ Id: r.Id }, f), tables: ['SupplierMaterial'] }).then(done);
        };
        const si = g('#spSaveInfo'); if (si) si.onclick = () => { const f = form(); Object.assign(f, { UnitPrice: r.UnitPrice, PurchaseUomId: r.PurchaseUomId, EffectiveFrom: day(r.EffectiveFrom) }); Store.act('SupplierMaterial/Update', { method: 'PUT', form: Object.assign({ Id: r.Id }, f), tables: ['SupplierMaterial'] }).then(done); };
      } });
  }
  APP.priceForm = priceForm;
  const baseForm = UI.form;
  UI.form = (t, row, opt) => (t === 'SupplierMaterial' && Store.isLive ? priceForm(row, opt || {}) : baseForm(t, row, opt));

  // ---------- C. Màn Bảng giá nhà cung cấp: nguyên liệu × NCC ----------
  const FLAGS = [['NO_PRICE', 'Chưa có giá'], ['NO_PREFERRED', 'Chưa có NCC ưu tiên'], ['EXPIRING', 'Sắp hết hạn (≤ 7 ngày)'], ['PRICE_UP', 'Tăng giá ≥ 10%'], ['PREFERRED_NOT_CHEAPEST', 'NCC ưu tiên không còn rẻ nhất'], ['NO_CONVERSION', 'Chưa quy đổi đơn vị']];
  const sm = { date: '', flag: '', q: '', cat: '', closed: new Set(), sel: new Set(), data: null, key: '' };
  const base0 = APP.SCREENS['scm-supplier-material'];
  APP.register('scm-supplier-material', (el, f, p) => {
    if (!Store.isLive) return base0 ? base0(el, f, p) : APP.genericList(el, f);
    if (!sm.date) sm.date = Store.ctx.date;
    el.innerHTML = APP.header(f, `${can('imp') || can('edit') ? `<button class="btn" id="smImp">${I('file')} Nhập báo giá Excel</button>` : ''}<button class="btn" id="smExp">${I('export')} Xuất Excel</button>${can('add') ? `<button class="btn primary" id="smNew">${I('plus')} Thêm giá</button>` : ''}`, { noNote: true }) + `
      <div class="notice info mb">${I('help')}<div>So sánh giá của các nhà cung cấp cho từng nguyên liệu tại một ngày – mọi giá quy về đơn vị kho. Ô viền xanh = NCC ưu tiên, chấm xanh = rẻ nhất. Bấm ô để sửa / lưu giá mới; ô trống để thêm giá.</div></div>
      <div class="card panel mb"><div class="row-flex"><button class="btn sm" id="smPrev">◀</button><b id="smDay"></b><button class="btn sm" id="smNext">▶</button><button class="chip" id="smToday">Hôm nay</button><input class="input" id="smQ" placeholder="Tìm nguyên liệu" value="${esc(sm.q)}" style="max-width:240px"></div><div id="smKpi" class="mt"></div></div>
      <div class="card panel"><div class="row-flex mb" id="smChips"></div><div class="row-flex mb hide" id="smBulk"><span class="small" id="smSelN"></span><button class="btn sm" id="smExt">Gia hạn hàng loạt</button><button class="btn sm" id="smClr">Bỏ chọn</button></div><div id="smBody"><div class="muted small">Đang tải…</div></div></div>`;
    const g = s => el.querySelector(s);
    g('#smDay').textContent = U.date(sm.date);
    const go = d => { sm.date = d; sm.data = null; APP.refresh(); };
    g('#smPrev').onclick = () => go(U.addDays(sm.date, -1)); g('#smNext').onclick = () => go(U.addDays(sm.date, 1)); g('#smToday').onclick = () => go(Store.ctx.date);
    let tq; g('#smQ').oninput = e => { clearTimeout(tq); tq = setTimeout(() => { sm.q = e.target.value.trim(); draw(); }, 200); };
    if (g('#smNew')) g('#smNew').onclick = () => priceForm(null, { onSave: () => { sm.data = null; APP.refresh(); } });
    g('#smExp').onclick = () => { const d = sm.data; if (!d) return; const rows = []; d.listRow.forEach(r => { const o = { nl: r.ingredientName, nhom: r.categoryName || '', dv: r.baseUnitName || '' }; d.listSupplier.forEach(s => { const c = r.listCell.find(x => x.supplierId === s.supplierId); o[s.supplierId] = c && !nil(c.pricePerBase) ? c.pricePerBase : ''; }); rows.push(o); }); U.csv(rows, [{ k: 'nl', label: 'Nguyên liệu' }, { k: 'nhom', label: 'Nhóm' }, { k: 'dv', label: 'ĐVT kho' }].concat(d.listSupplier.map(s => ({ k: s.supplierId, label: s.supplierName }))).map(c => Object.assign(c, { col: c.k, text: r => r[c.k] })), 'BangGiaNCC_' + sm.date.replace(/-/g, '') + '.csv'); };
    if (g('#smImp')) g('#smImp').onclick = () => quoteImport();
    const draw = () => {
      const d = sm.data; if (!d) return;
      g('#smKpi').innerHTML = UI.kpis([UI.kpi('Nguyên liệu có giá', U.num(d.pricedCount) + ' / ' + U.num(d.ingredientCount), '', 'calc', 'green'), UI.kpi('Chưa có NCC ưu tiên', U.num(d.noPreferredCount), '', 'star', d.noPreferredCount ? 'orange' : 'green'),
        UI.kpi('Sắp hết hạn 7 ngày', U.num(d.expiringCount), '', 'calendar', d.expiringCount ? 'orange' : 'green'), UI.kpi('Tăng giá ≥ ' + pct() + '%', U.num(d.priceUpCount), '', 'up', d.priceUpCount ? 'red' : 'green')], 'compact');
      g('#smChips').innerHTML = `<button class="chip ${!sm.flag ? 'active' : ''}" data-fl="">Tất cả · ${U.num(d.ingredientCount)}</button>` + FLAGS.map(([k, l]) => `<button class="chip ${sm.flag === k ? 'active' : ''}" data-fl="${k}">${esc(l)} · ${U.num(d.flagCount[k] || 0)}</button>`).join('');
      el.querySelectorAll('[data-fl]').forEach(b => b.onclick = () => { sm.flag = b.dataset.fl; draw(); });
      const q = sm.q.toLowerCase();
      const rows = d.listRow.filter(r => (!sm.flag || r.listFlag.includes(sm.flag)) && (!q || String(r.ingredientName || '').toLowerCase().includes(q) || String(r.ingredientCode || '').toLowerCase().includes(q)));
      const sups = d.listSupplier;
      const groups = {}; rows.forEach(r => { const k = r.categoryName || 'Chưa phân nhóm'; (groups[k] = groups[k] || []).push(r); });
      let h = `<div class="table-wrap sp-matrix"><table class="table dc-table"><thead><tr><th></th><th>Nguyên liệu</th><th>ĐVT kho</th>${sups.map(s => `<th class="num" title="${nil(s.onTimePct) ? 'Chưa có lịch sử giao' : U.num(s.onTimePct, 0) + '% đúng hẹn 90 ngày'}">${esc(s.supplierName || '')}</th>`).join('')}</tr></thead><tbody>`;
      Object.entries(groups).forEach(([k, list]) => {
        const closed = sm.closed.has(k);
        h += `<tr class="od-grp" data-smg="${esc(k)}"><td colspan="${3 + sups.length}"><span class="od-caret">${closed ? '▸' : '▾'}</span> <b>${esc(k)}</b> <span class="small muted">· ${list.length} nguyên liệu</span></td></tr>`;
        if (closed) return;
        list.forEach(r => {
          h += `<tr><td><input type="checkbox" data-smsel="${esc(r.ingredientId)}" ${sm.sel.has(r.ingredientId) ? 'checked' : ''}></td><td><b>${esc(r.ingredientName || '')}</b><div class="small muted">${r.listFlag.map(fl => esc((FLAGS.find(x => x[0] === fl) || [])[1] || '')).join(' · ')}</div></td><td>${esc(r.baseUnitName || '')}</td>${sups.map(s => {
            const c = r.listCell.find(x => x.supplierId === s.supplierId);
            if (!c) return `<td class="num">${can('add') ? `<button class="btn xs" data-smadd="${esc(r.ingredientId)}|${esc(s.supplierId)}">+ Thêm giá</button>` : ''}</td>`;
            const tip = `${U.money(c.unitPrice)} / ${c.unitName || ''} · tối thiểu ${nil(c.minimumOrderQty) ? '–' : U.num(c.minimumOrderQty)} · giao ${nil(c.leadTimeDays) ? '–' : c.leadTimeDays + ' ngày'} · ${c.deliveryDays || ''} · ${U.date(c.effectiveFrom)} – ${c.effectiveTo ? U.date(c.effectiveTo) : '…'}${nil(c.changePct) ? '' : ' · ' + (c.changePct > 0 ? '+' : '') + U.num(c.changePct, 1) + '% lần trước'}`;
            return `<td class="num sp-cell ${c.isPreferred ? 'pref' : ''}" title="${esc(tip)}" data-smc="${esc(c.priceId)}">${c.isCheapest ? '<i class="sp-dot"></i>' : ''}${c.noConversion ? '<span class="bad small">chưa quy đổi</span>' : U.money(c.pricePerBase)}${!nil(c.changePct) && c.changePct >= pct() ? ' <span class="bad small">↑' + U.num(c.changePct, 0) + '%</span>' : ''}${!c.isPreferred && can('edit') ? ` <button class="btn xs sp-pref" data-smp="${esc(c.priceId)}" title="Đặt ưu tiên">★</button>` : ''}</td>`;
          }).join('')}</tr>`;
        });
      });
      if (!rows.length) h += `<tr><td colspan="${3 + sups.length}" class="muted">Không có nguyên liệu phù hợp</td></tr>`;
      g('#smBody').innerHTML = h + '</tbody></table></div>';
      el.querySelectorAll('[data-smg]').forEach(r => r.onclick = () => { const k = r.dataset.smg; if (sm.closed.has(k)) sm.closed.delete(k); else sm.closed.add(k); draw(); });
      const open = id => Store.query('SupplierMaterial/GetById', { id }).then(x => { if (!x.ok || !x.data) return; const row = {}; Object.keys(x.data).forEach(k2 => { row[k2.charAt(0).toUpperCase() + k2.slice(1)] = x.data[k2]; }); priceForm(row, { onSave: () => { sm.data = null; APP.refresh(); } }); });
      el.querySelectorAll('[data-smc]').forEach(td => td.onclick = e => { if (e.target.closest('button')) return; open(td.dataset.smc); });
      el.querySelectorAll('[data-smadd]').forEach(b => b.onclick = () => { const [ing, sup] = b.dataset.smadd.split('|'); priceForm(null, { defaults: { IngredientId: ing, SupplierId: sup }, onSave: () => { sm.data = null; APP.refresh(); } }); });
      el.querySelectorAll('[data-smp]').forEach(b => b.onclick = e => { e.stopPropagation(); Store.query('SupplierMaterial/GetById', { id: b.dataset.smp }).then(x => { const o = x.data || {}; UI.confirm('Đặt NCC này là ưu tiên cho nguyên liệu? NCC ưu tiên hiện tại (nếu có) sẽ bị bỏ ưu tiên.', () => Store.act('SupplierMaterial/Update', { method: 'PUT', form: { Id: o.id, SupplierId: o.supplierId, IngredientId: o.ingredientId, UnitPrice: o.unitPrice, PurchaseUomId: o.purchaseUomId, EffectiveFrom: day(o.effectiveFrom), EffectiveTo: day(o.effectiveTo) || null, MinimumOrderQuantity: o.minimumOrderQuantity, LeadTimeDays: o.leadTimeDays, DeliveryFrequency: o.deliveryFrequency, SupplierMaterialCode: o.supplierMaterialCode, QualityRequirement: o.qualityRequirement, IsPreferred: true, Status: o.status || 'Active' }, tables: ['SupplierMaterial'] }).then(y => { if (y.ok) { UI.toast(esc((y.res && y.res.responseMessage) || 'Đã đặt ưu tiên'), 'ok'); sm.data = null; APP.refresh(); } }), { ok: 'Đặt ưu tiên' }); }); });
      el.querySelectorAll('[data-smsel]').forEach(c => c.onchange = () => { if (c.checked) sm.sel.add(c.dataset.smsel); else sm.sel.delete(c.dataset.smsel); bulk(); });
      bulk();
    };
    const bulk = () => { const n = sm.sel.size; g('#smBulk').classList.toggle('hide', !n || !can('edit')); g('#smSelN').textContent = n + ' nguyên liệu đã chọn'; };
    g('#smClr').onclick = () => { sm.sel.clear(); draw(); };
    g('#smExt').onclick = () => {
      const ids = sm.data.listRow.filter(r => sm.sel.has(r.ingredientId)).flatMap(r => r.listCell.map(c => c.priceId));
      UI.modal({ title: 'Gia hạn hàng loạt', sub: ids.length + ' dòng giá đang áp dụng của ' + sm.sel.size + ' nguyên liệu', size: 'sm', body: `<div class="field"><label>Ngày kết thúc mới</label><input type="date" class="input" id="exTo"><div class="small muted">Để trống = bỏ ngày kết thúc (áp dụng đến khi có giá mới).</div></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="exOk">Cập nhật</button>',
        onMount: (m, close) => m.querySelector('#exOk').onclick = () => Store.act('SupplierMaterial/ExtendMany', { json: { listId: ids, effectiveTo: m.querySelector('#exTo').value || null }, tables: ['SupplierMaterial'] }).then(x => { if (!x.ok) return; close(); UI.toast(esc((x.res && x.res.responseMessage) || 'Đã cập nhật'), 'ok'); sm.sel.clear(); sm.data = null; APP.refresh(); }) });
    };
    const quoteImport = () => {
      UI.modal({ title: 'Nhập báo giá Excel theo nhà cung cấp', size: 'lg', body: `<div class="field"><label>Nhà cung cấp <span class="req">*</span></label><input class="input" id="qiSup" placeholder="Gõ tên / mã NCC"></div><div class="row-flex mt"><button class="btn sm" id="qiTpl" disabled>${I('down')} Tải mẫu của NCC</button><button class="btn sm" id="qiUp" disabled>${I('file')} Tải báo giá lên</button><input type="file" id="qiFile" accept=".xlsx" hidden></div><div id="qiOut" class="mt"></div>`,
        foot: '<button class="btn" data-close>Đóng</button><button class="btn primary hide" id="qiApply">Áp dụng</button>',
        onMount: (m, close) => {
          let sup = null, file = null; const g2 = s => m.querySelector(s);
          searchBox(g2('#qiSup'), 'Supplier/GetAll', s => (s.name || '') + ' · ' + (s.code || ''), s => { sup = s.id; g2('#qiTpl').disabled = false; g2('#qiUp').disabled = false; });
          g2('#qiTpl').onclick = () => Store.download('SupplierMaterial/GetQuoteTemplate', { supplierId: sup });
          g2('#qiUp').onclick = () => g2('#qiFile').click();
          const send = apply => { const fd = new FormData(); fd.append('supplierId', sup); fd.append('apply', apply ? 'true' : 'false'); fd.append('file', file); return Store.act('SupplierMaterial/ImportQuote', { json: fd, tables: apply ? ['SupplierMaterial'] : [], quiet: !apply }); };
          g2('#qiFile').onchange = () => { file = g2('#qiFile').files[0]; g2('#qiFile').value = ''; if (!file) return; send(false).then(x => {
            if (!x.ok || !x.data) return UI.toast(esc(x.message || 'Không đọc được tệp'), 'err');
            const d = x.data, T = { UP: ['Tăng', 'red'], DOWN: ['Giảm', 'green'], NEW: ['Mới', 'blue'], SAME: ['Không đổi', 'gray'], ERROR: ['Lỗi', 'red'] };
            g2('#qiOut').innerHTML = `<div class="mb">Tăng ${d.up} · Giảm ${d.down} · Mới ${d.new} · Lỗi ${d.error}</div><div class="table-wrap"><table class="table dc-table"><thead><tr><th>Nguyên liệu</th><th class="num">Giá hiện hành</th><th class="num">Giá mới</th><th>Đơn vị</th><th>Từ ngày</th><th></th></tr></thead><tbody>${d.listLine.map(l => `<tr><td>${esc(l.ingredientName || l.ingredientCode || '')}</td><td class="num">${nil(l.currentPrice) ? '–' : U.money(l.currentPrice)}</td><td class="num">${nil(l.newPrice) ? esc(l.text || '') : U.money(l.newPrice)}</td><td>${esc(l.unitName || '')}</td><td>${l.effectiveFrom ? U.date(l.effectiveFrom) : ''}</td><td>${UI.tag(T[l.status][0], T[l.status][1])} <span class="small bad">${esc(l.message || '')}</span></td></tr>`).join('')}</tbody></table></div>`;
            g2('#qiApply').classList.toggle('hide', !(d.up + d.down + d.new));
          }); };
          g2('#qiApply').onclick = () => send(true).then(x => { if (!x.ok) return; close(); UI.toast(esc((x.res && x.res.responseMessage) || 'Đã áp dụng'), 'ok'); sm.data = null; APP.refresh(); });
        } });
    };
    const key = sm.date;
    if (sm.data && sm.key === key) return draw();
    Store.query('SupplierMaterial/GetMatrix', { Date: sm.date }).then(x => { if (!g('#smBody') || !g('#smBody').isConnected) return; if (!x.ok || !x.data) { g('#smBody').innerHTML = `<div class="notice warn">${I('alert')}<div>${esc(x.message || 'Không tải được')}</div></div>`; return; } sm.data = x.data; sm.key = key; draw(); });
  });
})();
