/* UI-6.22 (D-103): Đơn vị tính gộp quy đổi – đơn vị gốc (kg, g, lít…) / đơn vị đóng gói có kích cỡ ("thùng 10 kg"…), lưu cùng quy đổi chung
 * (UnitMeasure/SaveWithConversion – 1 giao dịch, trùng kích cỡ → dùng lại). Màn danh sách: loại, "= bao nhiêu đơn vị gốc", "Đang dùng ở", lọc Gốc / Đóng gói.
 * Mục menu "Quy đổi đơn vị" đã ẩn. Thành phần dùng chung: APP.packUnitForm (tạo đơn vị đóng gói tại chỗ), UI.unitSelect (ô chọn đơn vị quy đổi được về
 * đơn vị kho của nguyên liệu, "= N kg", + Tạo đơn vị đóng gói). Chế độ DEMO giữ màn chung. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const PACKS = ['thùng', 'bao', 'khay', 'hộp', 'chai', 'can', 'túi', 'kiện', 'bình', 'lốc'];
  const us = { kind: '' };
  const can = a => { const p = Store.perm('scm-uom'); return p.access !== false && p[a] !== false; };
  const fmt = n => U.num(n, 4);
  // Hộp đơn vị đóng gói (dùng ở màn Đơn vị tính và ngay trong form nguyên liệu / giá NCC / đề nghị mua).
  APP.packUnitForm = (o = {}) => Store.ensure('UnitMeasure').then(() => {
    const bases = Store.rows('UnitMeasure').filter(u => u.Status !== 'Inactive' && u.UomType !== 'PACK');
    const r = o.row || {};
    UI.modal({ title: r.id ? 'Sửa đơn vị đóng gói' : 'Tạo đơn vị đóng gói', sub: 'Mỗi kích cỡ là một đơn vị riêng (vd "thùng 10 kg", "bao 25 kg") – quy đổi đúng cho mọi nguyên liệu', size: 'md',
      body: `<div class="form-grid"><div class="field"><label>Loại bao bì <span class="req">*</span></label><select class="select" id="pkT">${PACKS.map(p => `<option ${p === (r.packType || 'thùng') ? 'selected' : ''}>${p}</option>`).join('')}</select></div>
        <div class="field"><label>1 đơn vị = <span class="req">*</span></label><div class="row-flex"><input type="number" min="0" step="any" class="input" id="pkQ" style="max-width:120px" value="${r.factor || ''}"><select class="select" id="pkB">${bases.map(u => `<option value="${esc(u.Id)}" ${u.Id === (r.toUnitId || o.baseUnitId) ? 'selected' : ''}>${esc(u.Code || u.Name || '')}</option>`).join('')}</select></div></div></div>
        <div class="notice info mt">${I('help')}<div>Tên đơn vị: <b id="pkName">–</b>. Đã có đơn vị cùng kích cỡ thì hệ thống chọn lại đơn vị đó, không tạo trùng.${r.id ? ' Đơn vị đã dùng thì không đổi được kích cỡ – tạo đơn vị mới.' : ''}</div></div>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="pkOk">Lưu</button>',
      onMount: (m, close) => {
        const g = s => m.querySelector(s);
        const nm = () => { const b = Store.get('UnitMeasure', g('#pkB').value) || {}; const q = g('#pkQ').value; g('#pkName').textContent = q > 0 ? `${g('#pkT').value} ${String(+q).replace('.', ',')} ${b.Symbol || b.Code || ''}` : '–'; };
        ['#pkT', '#pkQ', '#pkB'].forEach(s => { g(s).oninput = nm; g(s).onchange = nm; }); nm();
        g('#pkOk').onclick = () => {
          const q = +g('#pkQ').value; if (!(q > 0)) return UI.toast('Nhập số lượng của 1 đơn vị (lớn hơn 0)', 'err');
          Store.act('UnitMeasure/SaveWithConversion', { json: { id: r.id || null, kind: 'PACK', packType: g('#pkT').value, quantity: q, baseUnitId: g('#pkB').value, status: 'Active' }, tables: ['UnitMeasure', 'UomConversion'] })
            .then(x => { if (!x.ok || !x.data) return; close(); UI.toast(esc((x.res && x.res.responseMessage) || 'Đã lưu'), x.data.reused ? 'warn' : 'ok'); if (o.onSave) o.onSave(x.data); });
        };
      } });
  });
  // Ô chọn đơn vị quy đổi được về đơn vị kho của nguyên liệu: UI.unitSelect(selectEl, { ingredientId | baseUnitId, value, createBtn }).
  UI.unitSelect = (sel, o = {}) => {
    const load = () => Store.query('UnitMeasure/GetForIngredient', o.ingredientId ? { ingredientId: o.ingredientId } : { baseUnitId: o.baseUnitId }).then(x => {
      const d = x.ok ? x.data : null; const list = d ? d.listUnit || [] : []; const base = list.find(u => u.isBase);
      sel.innerHTML = list.length ? list.map(u => `<option value="${esc(u.id)}" ${u.id === (o.value || (base && base.id)) ? 'selected' : ''}>${esc(u.code || u.name || '')}${u.isBase ? '' : ' = ' + fmt(u.factor) + ' ' + esc((base && base.code) || '')}</option>`).join('') : '<option value="">— Chưa có đơn vị kho —</option>';
      return list;
    });
    if (o.createBtn && can('add')) o.createBtn.onclick = () => APP.packUnitForm({ baseUnitId: o.baseUnitId, onSave: u => { o.value = u.id; load(); } });
    return load();
  };
  // Hộp đơn vị gốc (mã, tên, ký hiệu, trạng thái) + "Quy đổi khác" (hiếm).
  function baseForm(r) {
    const locked = r && r.locked;
    UI.modal({ title: r ? 'Sửa đơn vị gốc' : 'Thêm đơn vị gốc', sub: 'kg, g, lít, ml, quả, cái, bó… – không cần quy đổi', size: 'sm',
      body: `<div class="form-grid"><div class="field"><label>Mã <span class="req">*</span></label><input class="input" id="ubC" value="${esc((r && r.code) || '')}" ${r ? 'disabled' : ''}></div>
        <div class="field"><label>Tên <span class="req">*</span></label><input class="input" id="ubN" value="${esc((r && r.name) || '')}" ${locked ? 'disabled' : ''}></div>
        <div class="field"><label>Ký hiệu</label><input class="input" id="ubS" value="${esc((r && r.symbol) || '')}" ${locked ? 'disabled' : ''}></div>
        <div class="field"><label>Trạng thái</label><select class="select" id="ubSt" ${locked ? 'disabled' : ''}><option value="Active">Đang dùng</option><option value="Inactive" ${r && r.status === 'Inactive' ? 'selected' : ''}>Ngừng dùng</option></select></div></div>
        ${locked ? '<div class="small muted">Đơn vị chuẩn (kg, g, lít, ml) – khóa sửa.</div>' : ''}${r && !locked && can('edit') ? `<div class="mt"><a href="javascript:void 0" id="ubOther" class="small">Quy đổi khác (giữa 2 đơn vị gốc khác loại – hiếm)</a></div>` : ''}`,
      foot: `<button class="btn" data-close>Đóng</button>${locked ? '' : '<button class="btn primary" id="ubOk">Lưu</button>'}`,
      onMount: (m, close) => {
        const g = s => m.querySelector(s);
        const ot = g('#ubOther'); if (ot) ot.onclick = () => { close(); UI.form('UomConversion', null, { title: 'Quy đổi khác', defaults: { FromUnitId: r.id, FromUomId: r.id }, onSave: () => APP.refresh() }); };
        const ok = g('#ubOk'); if (ok) ok.onclick = () => {
          const code = g('#ubC').value.trim(), name = g('#ubN').value.trim(); if (!code || !name) return UI.toast('Nhập mã và tên đơn vị', 'err');
          Store.act('UnitMeasure/SaveWithConversion', { json: { id: r ? r.id : null, kind: 'BASE', code, name, symbol: g('#ubS').value.trim() || null, status: g('#ubSt').value }, tables: ['UnitMeasure'] })
            .then(x => { if (!x.ok) return; close(); UI.toast('Đã lưu đơn vị', 'ok'); APP.refresh(); });
        };
      } });
  }
  const base0 = APP.SCREENS['scm-uom'];
  // FE-2 (thử quy đổi – UomConversion/Convert): nhập số lượng + đơn vị nguồn / đích → kết quả theo quy đổi đang hiệu lực (kể cả quy đổi qua đơn vị gốc).
  function tryConvert() {
    Store.query('UnitMeasure/GetUsage').then(x => {
      const units = (x.list || []).filter(u => u.id).sort((a, b) => String(a.name || '').localeCompare(String(b.name || ''), 'vi'));
      const opts = sel => units.map(u => `<option value="${esc(u.id)}" ${u.id === sel ? 'selected' : ''}>${esc(u.name || u.symbol || u.code || '')}${u.kind === 'PACK' ? ' (đóng gói)' : ''}</option>`).join('');
      UI.modal({ title: 'Thử quy đổi đơn vị', size: 'sm',
        body: `<div class="field"><label>Số lượng</label><input type="number" step="any" class="input" id="cvQ" value="1"></div>
          <div class="field"><label>Từ đơn vị</label><select class="select" id="cvF">${opts(units[0] && units[0].id)}</select></div>
          <div class="field"><label>Sang đơn vị</label><select class="select" id="cvT">${opts(units[1] && units[1].id)}</select></div>
          <div id="cvR" class="mt"></div>`,
        foot: '<button class="btn" data-close>Đóng</button><button class="btn primary" id="cvGo">Quy đổi</button>',
        onMount: m => {
          m.querySelector('#cvGo').onclick = () => {
            const q = +m.querySelector('#cvQ').value, fu = m.querySelector('#cvF'), tu = m.querySelector('#cvT');
            const name = sl => sl.options[sl.selectedIndex] ? sl.options[sl.selectedIndex].text : '';
            Store.query('UomConversion/Convert', { Quantity: q, FromUnitId: fu.value, ToUnitId: tu.value }).then(r => {
              m.querySelector('#cvR').innerHTML = r.ok
                ? `<div class="notice info"><div><b>${U.num(q, 4)} ${esc(name(fu))} = ${U.num(r.data, 4)} ${esc(name(tu))}</b></div></div>`
                : `<div class="notice warn">${esc(r.message || 'Không quy đổi được – chưa có quy đổi giữa 2 đơn vị này')}</div>`;
            });
          };
        } });
    });
  }

  APP.register('scm-uom', (el, f, p) => {
    if (!Store.isLive) return base0 ? base0(el, f, p) : APP.genericList(el, f);
    el.innerHTML = APP.header(f, `<button class="btn" id="umTry" data-help="uom-try">${I('calc')} Thử quy đổi</button>${can('add') ? `<button class="btn" id="umBase">${I('plus')} Đơn vị gốc</button><button class="btn primary" id="umPack">${I('plus')} Đơn vị đóng gói</button>` : ''}`, { noNote: true }) + `
      <div class="notice info mb">${I('help')}<div>Đơn vị gốc (kg, g, lít, quả…) và đơn vị đóng gói có kích cỡ (thùng 10 kg, bao 25 kg…). Quy đổi lưu cùng đơn vị – không còn mục menu "Quy đổi đơn vị" riêng. Đơn vị đã dùng thì không đổi kích cỡ.</div></div>
      <div id="umKpi"></div><div class="card panel"><div class="row-flex mb" id="umChips"></div><div id="umBody" data-help="uom-list"><div class="muted small">Đang tải…</div></div></div>`;
    const g = s => el.querySelector(s);
    if (g('#umBase')) g('#umBase').onclick = () => baseForm(null);
    g('#umTry').onclick = () => tryConvert();
    if (g('#umPack')) g('#umPack').onclick = () => APP.packUnitForm({ onSave: () => APP.refresh() });
    Store.query('UnitMeasure/GetUsage').then(x => {
      if (!g('#umBody')) return;
      if (!x.ok) { g('#umBody').innerHTML = `<div class="notice warn">${I('alert')}<div>${esc(x.message || 'Không tải được')}</div></div>`; return; }
      const all = x.list || [];
      const used = u => u.ingredientCount + u.supplierPriceCount + u.recipeLineCount + u.documentLineCount;
      g('#umKpi').innerHTML = UI.kpis([UI.kpi('Đơn vị gốc', U.num(all.filter(u => u.kind !== 'PACK').length), '', 'layers', 'green'), UI.kpi('Đơn vị đóng gói', U.num(all.filter(u => u.kind === 'PACK').length), 'có kích cỡ', 'package', 'blue'),
        UI.kpi('Chưa dùng', U.num(all.filter(u => !used(u)).length), '', 'clipboard', 'gray')]);
      const draw = () => {
        g('#umChips').innerHTML = [['', 'Tất cả'], ['BASE', 'Gốc'], ['PACK', 'Đóng gói']].map(([k, l]) => `<button class="chip ${us.kind === k ? 'active' : ''}" data-uk="${k}">${l} · ${U.num(all.filter(u => !k || (k === 'PACK' ? u.kind === 'PACK' : u.kind !== 'PACK')).length)}</button>`).join('');
        el.querySelectorAll('[data-uk]').forEach(b => b.onclick = () => { us.kind = b.dataset.uk; draw(); });
        const rows = all.filter(u => !us.kind || (us.kind === 'PACK' ? u.kind === 'PACK' : u.kind !== 'PACK'));
        g('#umBody').innerHTML = `<div class="table-wrap"><table class="table dc-table"><thead><tr><th>Mã</th><th>Đơn vị</th><th>Loại</th><th class="num">= bao nhiêu đơn vị gốc</th><th>Đang dùng ở</th><th>Trạng thái</th></tr></thead><tbody>${rows.map(u => {
          const where = [u.ingredientCount && u.ingredientCount + ' nguyên liệu', u.supplierPriceCount && u.supplierPriceCount + ' giá NCC', u.recipeLineCount && u.recipeLineCount + ' dòng công thức', u.documentLineCount && u.documentLineCount + ' dòng chứng từ'].filter(Boolean).join(' · ');
          return `<tr class="click" data-u="${esc(u.id)}"><td>${esc(u.code || '')}</td><td><b>${esc(u.name || '')}</b>${u.locked ? ' ' + UI.tag('chuẩn', 'gray') : ''}</td><td>${u.kind === 'PACK' ? UI.tag('Đóng gói', 'blue') : UI.tag('Gốc', 'green')}</td><td class="num">${u.factor ? fmt(u.factor) + ' ' + esc(u.toUnitCode || '') : '–'}</td><td class="small">${where || '<span class="muted">Chưa dùng</span>'}</td><td>${u.status === 'Inactive' ? UI.tag('Ngừng dùng', 'gray') : UI.tag('Đang dùng', 'green')}</td></tr>`;
        }).join('') || '<tr><td colspan="6" class="muted">Không có đơn vị</td></tr>'}</tbody></table></div>`;
        el.querySelectorAll('[data-u]').forEach(tr => tr.onclick = () => { if (!can('edit')) return; const u = all.find(z => z.id === tr.dataset.u); if (u.kind === 'PACK') APP.packUnitForm({ row: u, onSave: () => APP.refresh() }); else baseForm(u); });
      };
      draw();
    });
  });
})();
