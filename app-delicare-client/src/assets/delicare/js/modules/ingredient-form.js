/* UI-6.20 (D-101): form Nguyên liệu đúng logic nghiệp vụ – thay form tự sinh UI.form('Ingredient') ở chế độ API (danh sách Nguyên liệu, nút Sửa trang chi tiết,
 * "+ Tạo nguyên liệu mới" từ tab Công thức / đề nghị mua – dùng chung). 5 nhóm: Thông tin chung · Đơn vị · Bảo quản & ATTP · Định mức · Ghi chú & trạng thái.
 * Máy chủ (IngredientService.CheckRules) kiểm lại mọi luật; API phụ Ingredient/GetFormOptions (ngưỡng nhận hàng, quy đổi đơn vị, định mức theo kho).
 * Đơn vị mua / xuất: chỉ liệt kê đơn vị quy đổi được về đơn vị kho (bảng quy đổi chung – D-103); thiếu → "+ Tạo đơn vị đóng gói" (APP.packUnitForm – UI-6.22). */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const nil = v => v === null || v === undefined || v === '';
  // Hệ số quy đổi 1 đơn vị u → đơn vị base (đi theo đồ thị quy đổi chung, 2 chiều); null = không quy đổi được.
  const factorTo = (edges, from, to) => {
    if (!from || !to) return null; if (from === to) return 1;
    const adj = {}; edges.forEach(e => { (adj[e.fromUnitId] = adj[e.fromUnitId] || []).push([e.toUnitId, e.factor]); (adj[e.toUnitId] = adj[e.toUnitId] || []).push([e.fromUnitId, 1 / e.factor]); });
    const best = { [from]: 1 }, q = [from];
    while (q.length) { const u = q.shift(); if (u === to) return best[u]; (adj[u] || []).forEach(([v, f]) => { if (best[v] === undefined) { best[v] = best[u] * f; q.push(v); } }); }
    return null;
  };
  function ingredientForm(row, opt) {
    const r = row || Object.assign({}, (opt && opt.defaults) || {}), isNew = !r.Id;
    const perm = s => Store.perm(s), can = (s, a) => perm(s).access !== false && perm(s)[a] !== false;
    const qa = can('qc-inspection', 'edit');
    return Promise.all([Store.ensure('IngredientCategorie'), Store.ensure('UnitMeasure'), Store.query('Ingredient/GetFormOptions', isNew ? {} : { id: r.Id })]).then(([, , fo]) => {
      const o = fo && fo.ok ? fo.data : { listStorageType: [], listConversion: [], listWarehousePolicy: [] };
      const units = Store.rows('UnitMeasure').filter(u => u.Status !== 'Inactive');
      const uname = id => { const u = Store.get('UnitMeasure', id); return u ? (u.Code || u.Name || '') : ''; };
      const cats = () => Store.rows('IngredientCategorie').filter(c => c.Status !== 'Inactive' || c.Id === (r.IngredientCategoryId || r.CategoryId));
      const storage = String(r.StorageType || '').toUpperCase() === 'AMBIENT' ? 'DRY' : String(r.StorageType || '').toUpperCase();
      const expiry = !!(r.IsExpiryManaged || r.ExpiryControl);
      const st = (o.listStorageType || []);
      UI.modal({ title: isNew ? 'Thêm nguyên liệu' : 'Sửa nguyên liệu', sub: isNew ? 'Mã nguyên liệu hệ thống tự sinh khi lưu' : esc(r.Code || ''), size: 'lg',
        body: `<div class="if-sec"><h3>1. Thông tin chung</h3><div class="form-grid">
            <div class="field"><label>Tên nguyên liệu <span class="req">*</span></label><input class="input" id="ifName" value="${esc(r.Name || '')}"></div>
            <div class="field"><label>Nhóm nguyên liệu <span class="req">*</span>${can('scm-ingredient-cat', 'add') ? ' <a href="javascript:void 0" class="small" id="ifCatNew">+ Thêm nhóm</a>' : ''}</label><select class="select" id="ifCat"></select></div>
            <div class="small muted" id="ifHint" style="grid-column:1/-1"></div>
            <div class="field"><label>Quy cách</label><input class="input" id="ifSpec" value="${esc(r.Specification || '')}" placeholder="vd. thăn bò, không xương"></div>
            <div class="field"><label>Mã nguyên liệu</label><input class="input" value="${esc(r.Code || 'Tự sinh khi lưu')}" disabled></div></div></div>
          <div class="if-sec"><h3>2. Đơn vị</h3><div class="form-grid">
            <div class="field"><label>Đơn vị kho <span class="req">*</span></label><select class="select" id="ifBase"><option value="">— Chọn —</option>${units.map(u => `<option value="${esc(u.Id)}" ${u.Id === (r.BaseUnitId || r.BaseUomId) ? 'selected' : ''}>${esc(u.Code || u.Name || '')}${u.Name && u.Code && u.Name !== u.Code ? ' – ' + esc(u.Name) : ''}</option>`).join('')}</select></div>
            <div class="field"><label>Đơn vị mua</label><select class="select" id="ifPur"></select></div>
            <div class="field"><label>Đơn vị xuất</label><select class="select" id="ifIss"></select></div>
            <div class="field"><label>&nbsp;</label>${typeof APP.packUnitForm === 'function' && can('scm-uom', 'add') ? `<button class="btn sm" id="ifPack">${I('plus')} Tạo đơn vị đóng gói</button>` : ''}<div class="small muted">Chỉ hiện đơn vị quy đổi được về đơn vị kho.</div></div></div></div>
          <div class="if-sec"><h3>3. Bảo quản &amp; an toàn thực phẩm</h3>
            <div class="field"><label>Loại bảo quản <span class="req">*</span></label><div class="if-st">${st.map(x => `<label class="if-st-opt"><input type="radio" name="ifSt" value="${x.code}" ${x.code === storage ? 'checked' : ''}> <b>${esc(x.label)}</b><span class="small muted">${esc(x.note || '')}</span></label>`).join('')}</div></div>
            <div class="form-grid mt">
              <div class="field"><label class="row-flex"><input type="checkbox" id="ifExp" ${expiry ? 'checked' : ''}> Quản lý hạn dùng</label><div id="ifDaysBox"><label>Hạn dùng (ngày) <span class="req">*</span></label><input type="number" min="1" class="input" id="ifDays" value="${nil(r.ShelfLifeDays) ? '' : r.ShelfLifeDays}"></div></div>
              <div class="field"><label class="row-flex"><input type="checkbox" id="ifLot" ${r.LotControl === undefined || r.LotControl === null ? (expiry ? 'checked' : '') : r.LotControl ? 'checked' : ''}> Quản lý theo lô</label><div class="small bad hide" id="ifLotWarn">Xuất theo hạn dùng cần quản lý lô.</div></div>
              <div class="field"><label class="row-flex"><input type="checkbox" id="ifQc" ${r.QcRequired === undefined || r.QcRequired === null ? (['CHILLED', 'FROZEN'].includes(storage) ? 'checked' : '') : r.QcRequired ? 'checked' : ''}> Kiểm tra khi nhận</label><div class="small bad hide" id="ifQcWarn">Hàng Mát / Đông lạnh không kiểm khi nhận – rủi ro ATTP${qa ? '' : ' (chỉ QA được tắt)'}.</div></div>
              <div class="field"><label>Điều kiện bảo quản (mô tả thêm)</label><input class="input" id="ifCond" value="${esc(r.StorageCondition || '')}"></div></div></div>
          <div class="if-sec"><h3>4. Định mức</h3><div class="form-grid">
            <div class="field"><label>Hao hụt sơ chế %</label><input type="number" min="0" max="99" step="any" class="input" id="ifWaste" value="${nil(r.StandardWastePercent) ? '' : r.StandardWastePercent}"><div class="small muted">Dùng khi công thức không ghi hao hụt riêng.</div></div>
            <div class="field"><label>Tồn tối thiểu <span class="small muted" data-bu></span></label><input type="number" min="0" step="any" class="input" id="ifMin" value="${nil(r.MinimumStock) ? '' : r.MinimumStock}"></div>
            <div class="field"><label>Tồn an toàn <span class="small muted" data-bu></span></label><input type="number" min="0" step="any" class="input" id="ifSafe" value="${nil(r.SafetyStock) ? '' : r.SafetyStock}"></div>
            <div class="field"><div class="small muted">Kho có định mức riêng thì dùng định mức kho.</div>${(o.listWarehousePolicy || []).length ? `<div class="small">${o.listWarehousePolicy.map(p => `${esc(p.warehouseName || '')}: tối thiểu ${U.num(p.minStock)} · an toàn ${U.num(p.safetyStock)}`).join('<br>')}</div>${can('scm-inv-policy', 'access') !== false && Store.perm('scm-inv-policy').access !== false ? '<a href="javascript:void 0" class="small" data-go="scm-inv-policy">Màn Định mức tồn kho</a>' : ''}` : '<div class="small muted">Chưa có kho nào có định mức riêng.</div>'}</div></div></div>
          <div class="if-sec"><h3>5. Ghi chú &amp; trạng thái</h3><div class="form-grid">
            <div class="field"><label>Ghi chú</label><textarea class="input" rows="2" id="ifNote">${esc(r.Note || '')}</textarea></div>
            <div class="field"><label>Trạng thái</label><select class="select" id="ifStatus"><option value="Active" ${r.Status !== 'Inactive' ? 'selected' : ''}>Đang dùng</option><option value="Inactive" ${r.Status === 'Inactive' ? 'selected' : ''}>Ngừng dùng</option></select></div></div></div>`,
        foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="ifOk">Lưu</button>',
        onMount: (m, close) => {
          const g = s => m.querySelector(s);
          const drawCats = sel => { g('#ifCat').innerHTML = '<option value="">— Chọn nhóm —</option>' + cats().map(c => `<option value="${esc(c.Id)}" ${c.Id === sel ? 'selected' : ''}>${esc(c.Name || c.Code || '')}</option>`).join(''); };
          drawCats(r.IngredientCategoryId || r.CategoryId);
          // UI-6.21: chọn nhóm → gợi ý loại bảo quản + đơn vị kho theo đa số nguyên liệu cùng nhóm (chỉ điền khi chưa chọn).
          const suggest = () => {
            const id = g('#ifCat').value; g('#ifHint').textContent = ''; if (!id) return;
            Store.query('Ingredient/GetCategoryDefaults', { categoryId: id }).then(x => {
              const d = x.ok && x.data; if (!d || !d.sampleCount || !m.isConnected) return;
              const parts = [];
              if (d.storageType && !m.querySelector('[name=ifSt]:checked')) { const rb = m.querySelector('[name=ifSt][value=' + d.storageType + ']'); if (rb) { rb.checked = true; rb.dispatchEvent(new Event('change')); } }
              if (d.storageType) { const o2 = (o.listStorageType || []).find(z => z.code === d.storageType); parts.push('bảo quản ' + (o2 ? o2.label : d.storageType)); }
              if (d.baseUnitId && !g('#ifBase').value) { g('#ifBase').value = d.baseUnitId; drawUnits(); }
              if (d.baseUnitId) parts.push('đơn vị kho ' + uname(d.baseUnitId));
              g('#ifHint').textContent = 'Gợi ý theo ' + d.sampleCount + ' nguyên liệu cùng nhóm: ' + parts.join(', ') + '.';
            });
          };
          g('#ifCat').onchange = suggest;
          const drawUnits = () => {
            const base = g('#ifBase').value;
            m.querySelectorAll('[data-bu]').forEach(x => { x.textContent = base ? '(' + uname(base) + ')' : ''; });
            [['#ifPur', r.PurchaseUomId], ['#ifIss', r.IssueUomId]].forEach(([id, cur]) => {
              const el = g(id), keep = el.value || cur;
              const ok = base ? units.filter(u => u.Id === base || factorTo(o.listConversion || [], u.Id, base) !== null) : [];
              el.innerHTML = `<option value="">= đơn vị kho</option>${ok.filter(u => u.Id !== base).map(u => `<option value="${esc(u.Id)}" ${u.Id === keep ? 'selected' : ''}>${esc(u.Code || u.Name || '')} = ${U.num(factorTo(o.listConversion || [], u.Id, base), 4)} ${esc(uname(base))}</option>`).join('')}`;
            });
          };
          g('#ifBase').onchange = drawUnits; drawUnits(); if (isNew && g('#ifCat').value) suggest();
          const pk = g('#ifPack'); if (pk) pk.onclick = () => APP.packUnitForm({ baseUnitId: g('#ifBase').value, onSave: u => Store.query('Ingredient/GetFormOptions', {}).then(x => { if (x.ok) o.listConversion = x.data.listConversion || []; return Store.reload(['UnitMeasure']); }).then(() => { units.splice(0, units.length, ...Store.rows('UnitMeasure').filter(z => z.Status !== 'Inactive')); drawUnits(); if (u && u.id) { g('#ifPur').value = u.id; } }) });
          const cn = g('#ifCatNew'); if (cn) cn.onclick = () => UI.form('IngredientCategorie', null, { title: 'Nhóm nguyên liệu', onSave: () => Store.reload(['IngredientCategorie']).then(() => drawCats((Store.rows('IngredientCategorie')[0] || {}).Id)) });
          const stOf = () => (m.querySelector('[name=ifSt]:checked') || {}).value || '';
          const sync = (auto) => {
            const ex = g('#ifExp').checked; g('#ifDaysBox').classList.toggle('hide', !ex);
            if (auto === 'exp' && ex) g('#ifLot').checked = true;
            if (auto === 'st') g('#ifQc').checked = ['CHILLED', 'FROZEN'].includes(stOf());
            g('#ifLotWarn').classList.toggle('hide', !(ex && !g('#ifLot').checked));
            const cold = ['CHILLED', 'FROZEN'].includes(stOf());
            g('#ifQcWarn').classList.toggle('hide', !(cold && !g('#ifQc').checked));
            g('#ifQc').disabled = cold && g('#ifQc').checked && !qa;
          };
          g('#ifExp').onchange = () => sync('exp'); g('#ifLot').onchange = () => sync(); g('#ifQc').onchange = () => sync();
          m.querySelectorAll('[name=ifSt]').forEach(x => x.onchange = () => sync('st')); sync();
          m.querySelectorAll('[data-go]').forEach(a => a.onclick = () => { close(); APP.go(a.dataset.go); });
          g('#ifOk').onclick = () => {
            const name = g('#ifName').value.trim(), cat = g('#ifCat').value, base = g('#ifBase').value, sto = stOf(), ex = g('#ifExp').checked, days = g('#ifDays').value;
            if (!name) return UI.toast('Nhập tên nguyên liệu', 'err');
            if (!cat) return UI.toast('Chọn nhóm nguyên liệu', 'err');
            if (!base) return UI.toast('Chọn đơn vị kho', 'err');
            if (!sto) return UI.toast('Chọn loại bảo quản', 'err');
            if (ex && !(+days > 0)) return UI.toast('Nhập hạn dùng (số ngày lớn hơn 0)', 'err');
            const w = g('#ifWaste').value; if (w !== '' && (+w < 0 || +w >= 100)) return UI.toast('Hao hụt sơ chế phải từ 0 đến dưới 100%', 'err');
            const form = { Name: name, IngredientCategoryId: cat, Specification: g('#ifSpec').value.trim() || null, BaseUnitId: base, PurchaseUomId: g('#ifPur').value || base, IssueUomId: g('#ifIss').value || base,
              StorageType: sto, IsExpiryManaged: ex, ExpiryControl: ex, ShelfLifeDays: ex ? +days : (nil(r.ShelfLifeDays) ? null : r.ShelfLifeDays), LotControl: g('#ifLot').checked, QcRequired: g('#ifQc').checked,
              StorageCondition: g('#ifCond').value.trim() || null, StandardWastePercent: w === '' ? null : +w, MinimumStock: g('#ifMin').value === '' ? null : +g('#ifMin').value, SafetyStock: g('#ifSafe').value === '' ? null : +g('#ifSafe').value,
              Note: g('#ifNote').value.trim() || null, Status: g('#ifStatus').value };
            const call = isNew ? Store.act('Ingredient/Insert', { form, tables: ['Ingredient'] }) : Store.act('Ingredient/Update', { method: 'PUT', form: Object.assign({ Id: r.Id, Code: r.Code }, form), tables: ['Ingredient'] });
            call.then(x => { if (!x.ok) return; close(); const msg = (x.res && x.res.responseMessage) || 'Đã lưu nguyên liệu'; UI.toast(esc(msg), /Lưu ý/.test(msg) ? 'warn' : 'ok'); if (opt && opt.onSave) opt.onSave(x); });
          };
        } });
    });
  }
  APP.ingredientForm = ingredientForm;
  const baseForm = UI.form;
  UI.form = (t, row, opt) => (t === 'Ingredient' && Store.isLive ? ingredientForm(row, opt || {}) : baseForm(t, row, opt));
})();
