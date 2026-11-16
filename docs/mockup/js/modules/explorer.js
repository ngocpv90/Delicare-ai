/* DATABASE ARCHITECTURE EXPLORER + DATABASE GAP / BA CONFIRMATION */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc, S = Store.schema;
  const CLS = [['Master Data', 'Master Data'], ['Business', 'Transaction'], ['Configuration', 'Policy'], ['Workflow', 'Workflow'], ['System', 'System'], ['Integration', 'Integration'], ['Analytics', 'Analytics'], ['Legacy', 'Legacy']];
  const clsOf = t => { const g = S.tables[t].grp || ''; const c = CLS.find(x => g.startsWith(x[0])); return c ? c[1] : 'Khác'; };
  const ex = { q: '', ph: '', cls: '', mod: '', t: null, view: 'cols' };
  APP.register('dbx-explorer', (el, f, params) => {
    if (params.t && S.tables[params.t]) ex.t = params.t;
    const names = Object.keys(S.tables).sort();
    if (!ex.t) ex.t = 'Order';
    const mods = [...new Set(names.map(t => S.tables[t].mod))].sort();
    const list = names.filter(t => (!ex.q || t.toLowerCase().includes(ex.q.toLowerCase()) || (S.tables[t].desc || '').toLowerCase().includes(ex.q.toLowerCase()) || S.tables[t].cols.some(c => c[0].toLowerCase() === ex.q.toLowerCase())) && (!ex.ph || S.tables[t].ph === ex.ph) && (!ex.cls || clsOf(t) === ex.cls) && (!ex.mod || S.tables[t].mod === ex.mod));
    const nCols = names.reduce((a, t) => a + S.tables[t].cols.length, 0), nDef = names.reduce((a, t) => a + S.tables[t].ndef, 0);
    const T = S.tables[ex.t];
    el.innerHTML = APP.header(f, `<a class="btn" href="docs/DELICARE_Module_Function_Mapping.xlsx" download>${I('export')} Mapping.xlsx</a><a class="btn" href="#/dbx-gap">${I('alert')} GAP Register</a>`, { sub: 'Sinh tự động từ 20261113_structure_db_appdelicare.sql (cấu trúc vật lý) + Dictionary (phân nhóm, quan hệ) + BRD (mô tả). Không có bảng/cột nào ngoài SQL nguồn.' }) + `
    <div class="kpis">${UI.kpi('Bảng (schema dbo)', names.length, 'CREATE TABLE trong SQL', 'database', 'green')}${UI.kpi('Cột', U.num(nCols), `${nDef} DEFAULT · ${S.dep.length} deprecated`, 'layers', 'blue')}${UI.kpi('FK vật lý', 0, 'Quan hệ kiểm soát bằng .NET', 'link', 'gray')}${UI.kpi('Quan hệ logic', S.rels.length, `${S.rels.filter(r => r.level.startsWith('PO')).length} PO xác nhận · ${S.rels.filter(r => !r.level.startsWith('PO')).length} đề xuất`, 'route', 'purple')}${UI.kpi('Tham chiếu chưa rõ', S.unres.length, 'Không tự gán bảng đích', 'alert', 'orange', 'dbx-gap')}</div>
    <div class="explorer"><div class="card panel"><input class="input" style="width:100%" id="xQ" placeholder="Tìm bảng, mô tả hoặc tên cột chính xác…" value="${esc(ex.q)}">
      <div class="grid mt" style="grid-template-columns:1fr 1fr;gap:6px"><select class="select" id="xPh"><option value="">Phase: tất cả</option>${['P0', 'P1', 'P2', 'P3', 'P4', 'P5', 'P6', 'P7', 'P8', 'LEGACY'].map(p => `<option ${p === ex.ph ? 'selected' : ''}>${p}</option>`).join('')}</select><select class="select" id="xCls"><option value="">Nhóm: tất cả</option>${CLS.map(c => c[1]).map(c => `<option ${c === ex.cls ? 'selected' : ''}>${c}</option>`).join('')}</select><select class="select" id="xMod" style="grid-column:1/-1"><option value="">Module: tất cả</option>${mods.map(m => `<option ${m === ex.mod ? 'selected' : ''}>${esc(m)}</option>`).join('')}</select></div>
      <div class="small muted mt">${list.length} / ${names.length} bảng</div>
      <div class="tlist mt">${list.map(t => `<div class="titem ${t === ex.t ? 'active' : ''}" data-t="${t}"><div style="min-width:0"><b>${t}</b><small>${S.tables[t].ph} · ${esc(clsOf(t))} · ${esc(S.tables[t].mod)}</small></div><span class="n">${S.tables[t].cols.length}</span></div>`).join('')}</div></div>
      <div class="card panel"><div class="spread"><div><div class="eyebrow">dbo · ${T.ph} · ${esc(clsOf(ex.t))}</div><h2 class="mono" style="font-size:18px">${ex.t}</h2></div><div class="row-flex">${UI.tag(T.grp)}${T.dd ? UI.tag('Có DeleteddBy', 'orange') : ''}<span class="tag blue">${Store.rows(ex.t).length} dòng DEMO</span></div></div>
        <div class="tabs mt">${[['cols', 'Cấu trúc & truy vết'], ['graph', 'Sơ đồ quan hệ'], ['data', 'Dữ liệu demo']].map(([k, l]) => `<button class="tab ${ex.view === k ? 'active' : ''}" data-v="${k}">${l}</button>`).join('')}</div><div id="xBody"></div></div></div>`;
    const body = el.querySelector('#xBody');
    if (ex.view === 'cols') body.innerHTML = UI.traceHtml(ex.t);
    if (ex.view === 'graph') body.innerHTML = graph(ex.t);
    if (ex.view === 'data') { const cols = APP.pickCols(ex.t, 9); body.innerHTML = '<div id="xD"></div>'; UI.table(body.querySelector('#xD'), { table: ex.t, cols, rows: () => Store.rows(ex.t), onRow: id => APP.openDetail(ex.t, id), pageSize: 10 }); }
    const debounce = (fn, ms) => { let h; return v => { clearTimeout(h); h = setTimeout(() => fn(v), ms); }; };
    const q = el.querySelector('#xQ'); const deb = debounce(v => { ex.q = v; APP.refresh(); setTimeout(() => { const n = document.getElementById('xQ'); if (n) { n.focus(); n.setSelectionRange(v.length, v.length); } }, 0); }, 250);
    q.oninput = e => deb(e.target.value);
    el.querySelector('#xPh').onchange = e => { ex.ph = e.target.value; APP.refresh(); };
    el.querySelector('#xCls').onchange = e => { ex.cls = e.target.value; APP.refresh(); };
    el.querySelector('#xMod').onchange = e => { ex.mod = e.target.value; APP.refresh(); };
    el.querySelectorAll('[data-t]').forEach(x => x.onclick = () => { ex.t = x.dataset.t; history.replaceState(null, '', '#/dbx-explorer?t=' + ex.t); APP.refresh(); });
    el.querySelectorAll('[data-v]').forEach(x => x.onclick = () => { ex.view = x.dataset.v; APP.refresh(); });
    body.querySelectorAll('a[href^="#/dbx-explorer?t="]').forEach(a => a.onclick = e => { e.preventDefault(); ex.t = a.getAttribute('href').split('=')[1]; history.replaceState(null, '', a.getAttribute('href')); APP.refresh(); });
  });
  function graph(t) {
    const out = S.rels.filter(r => r.src === t), inn = (Store.relIn[t] || []), un = S.unres.filter(u => u.src === t);
    const W = 760, H = Math.max(260, Math.max(out.length + un.length, inn.length) * 34 + 60), cx = W / 2, cy = H / 2;
    let g = `<rect x="${cx - 95}" y="${cy - 22}" width="190" height="44" rx="10" fill="#91AF42"/><text x="${cx}" y="${cy + 5}" text-anchor="middle" style="fill:#fff;font-weight:800;font-size:13px">${t}</text>`;
    inn.forEach((r, i) => { const y = 30 + i * ((H - 60) / Math.max(1, inn.length - 1 || 1)); g += `<path d="M175 ${y} C ${cx - 160} ${y}, ${cx - 160} ${cy}, ${cx - 95} ${cy}" fill="none" stroke="#79A8CC" stroke-width="1.4" marker-end="url(#ar)"/><a href="#/dbx-explorer?t=${r.src}"><rect x="10" y="${y - 13}" width="165" height="26" rx="7" fill="#EDF4FA" stroke="#C9DDEE"/><text x="18" y="${y + 4}" style="fill:#33597C;font-size:10.5px">${r.src}.${r.col}</text></a>`; });
    out.concat(un.map(u => ({ src: t, col: u.col, dst: '?', un: 1 }))).forEach((r, i, arr) => { const y = 30 + i * ((H - 60) / Math.max(1, arr.length - 1 || 1)); g += `<path d="M${cx + 95} ${cy} C ${cx + 160} ${cy}, ${cx + 160} ${y}, ${W - 175} ${y}" fill="none" stroke="${r.un ? '#F07825' : r.level && r.level.startsWith('PO') ? '#6F9131' : '#B5BCB4'}" stroke-width="1.4" ${r.un ? 'stroke-dasharray="4 3"' : ''} marker-end="url(#ar)"/>${r.un ? '' : `<a href="#/dbx-explorer?t=${r.dst}">`}<rect x="${W - 175}" y="${y - 13}" width="165" height="26" rx="7" fill="${r.un ? '#FFF3E8' : '#F3F6EF'}" stroke="${r.un ? '#F5B17E' : '#DCE3D5'}"/><text x="${W - 168}" y="${y + 4}" style="fill:#4E5C51;font-size:10.5px">${r.col} → ${r.dst}</text>${r.un ? '' : '</a>'}`; });
    return `<div class="chart-legend mb"><span><i class="legend-dot" style="background:#79A8CC"></i>Bảng tham chiếu đến (${inn.length})</span><span><i class="legend-dot" style="background:#B5BCB4"></i>Đề xuất (${out.filter(r => !r.level.startsWith('PO')).length})</span><span><i class="legend-dot" style="background:#6F9131"></i>PO xác nhận (${out.filter(r => r.level.startsWith('PO')).length})</span><span><i class="legend-dot" style="background:#F07825"></i>Chưa xác định (${un.length})</span></div><svg class="chart" viewBox="0 0 ${W} ${H}"><defs><marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M0 0L10 5L0 10z" fill="#9AA39B"/></marker></defs>${g}</svg><div class="small muted">Không có FOREIGN KEY vật lý – đây là quan hệ logic kiểm soát ở tầng ứng dụng .NET.</div>`;
  }
  // ---------- GAP / BA CONFIRMATION ----------
  const gs = { tab: 'func' };
  APP.register('dbx-gap', (el, f) => {
    const G = Store.cat.gaps;
    const missingDesc = Object.entries(S.tables).filter(([t, v]) => v.dsrc.startsWith('CHƯA'));
    const tabs = [['func', 'Chức năng chưa có bảng', G.func.length], ['col', 'Thiếu cột', G.col.length], ['ref', 'Tham chiếu chưa rõ', S.unres.length], ['desc', 'Bảng chưa mô tả', missingDesc.length], ['dq', 'Chất lượng cấu trúc', G.dq.length], ['st', 'Trạng thái chờ chốt', Object.keys(Store.cat.status).length], ['rd', 'Danh mục tham chiếu', G.ref.length], ['fn', 'Chức năng PARTIAL/GAP', Store.cat.funcs.filter(x => x.st !== 'MAPPED').length]];
    el.innerHTML = APP.header(f, `<a class="btn" href="docs/DELICARE_Database_Gap_Register.xlsx" download>${I('export')} Gap_Register.xlsx</a>`, { sub: 'Mọi chức năng cần dữ liệu nhưng Database chưa hỗ trợ được ghi nhận tại đây – mockup không giả vờ Database đã có. Cần BA/PO xác nhận trước khi đưa vào SRS.' }) + `
    <div class="kpis">${tabs.slice(0, 5).map(([k, l, n], i) => UI.kpi(l, n, 'BA/PO CONFIRM', ['alert', 'layers', 'link', 'clipboard', 'database'][i], ['red', 'orange', 'orange', 'purple', 'blue'][i])).join('')}</div>
    <div class="card panel"><div class="row-flex mb">${tabs.map(([k, l, n]) => `<button class="chip ${gs.tab === k ? 'active' : ''}" data-g="${k}">${l} · ${n}</button>`).join('')}</div><div id="gT"></div></div>`;
    el.querySelectorAll('[data-g]').forEach(b => b.onclick = () => { gs.tab = b.dataset.g; APP.refresh(); });
    const box = el.querySelector('#gT');
    const arr = (rows, cols) => UI.table(box, { rows: rows.map((r, i) => Object.assign({ Id: 'g' + i }, r)), cols, pageSize: 15 });
    const gapCols = [{ k: 0, label: 'Mã' }, { k: 2, label: 'Hạng mục' }, { k: 3, label: 'Mô tả GAP', render: r => `<div style="white-space:normal;min-width:280px">${esc(r[3])}</div>` }, { k: 4, label: 'Bảng/cột SQL', render: r => `<span class="mono small" style="white-space:normal">${esc(r[4])}</span>` }, { k: 6, label: 'Đề xuất xử lý', render: r => `<div style="white-space:normal;min-width:200px">${esc(r[6])}</div>` }, { k: 7, label: 'Ưu tiên', render: r => UI.badge(r[7] === 'Cao' ? 'High' : r[7] === 'Trung bình' ? 'Medium' : 'Low') }, { k: 8, label: 'Cần xác nhận' }, { k: 9, label: 'Trạng thái', render: r => UI.badge(r[9]) }];
    if (gs.tab === 'func') arr(G.func, gapCols);
    if (gs.tab === 'col') arr(G.col, gapCols);
    if (gs.tab === 'ref') arr(S.unres, [{ k: 'src', label: 'Bảng nguồn', render: r => `<a class="mono" href="#/dbx-explorer?t=${r.src}">${r.src}</a>` }, { k: 'col', label: 'Cột', render: r => `<span class="mono">${r.col}</span>` }, { k: 'reason', label: 'Lý do' }, { k: 'proposal', label: 'Bảng đích đề xuất (CHƯA xác nhận)' }]);
    if (gs.tab === 'desc') arr(missingDesc.map(([t, v]) => ({ t, ph: v.ph, grp: v.grp, desc: v.desc })), [{ k: 't', label: 'Bảng', render: r => `<a class="mono" href="#/dbx-explorer?t=${r.t}">${r.t}</a>` }, { k: 'ph', label: 'Phase' }, { k: 'grp', label: 'Nhóm' }, { k: 'desc', label: 'Mô tả hiện có', render: r => `<div style="white-space:normal">${esc(r.desc)}</div>` }]);
    if (gs.tab === 'dq') arr(G.dq, [{ k: 0, label: 'Mã' }, { k: 1, label: 'Vấn đề' }, { k: 2, label: 'Chi tiết', render: r => `<div style="white-space:normal;min-width:300px">${esc(r[2])}</div>` }, { k: 3, label: 'Số lượng', num: true }, { k: 5, label: 'Đề xuất', render: r => `<div style="white-space:normal;min-width:220px">${esc(r[5])}</div>` }]);
    if (gs.tab === 'st') arr(Object.entries(Store.cat.status).map(([k, v]) => ({ k, flow: v.flow, vi: v.vi.join(' / '), src: v.src, conf: v.conf })), [{ k: 'k', label: 'Bảng.Cột', render: r => `<span class="mono">${r.k}</span>` }, { k: 'flow', label: 'Giá trị (luồng)', render: r => `<div style="white-space:normal;min-width:260px">${esc(r.flow)}</div>` }, { k: 'vi', label: 'Hiển thị', render: r => `<div style="white-space:normal;min-width:220px">${esc(r.vi)}</div>` }, { k: 'src', label: 'Nguồn', render: r => UI.tag(r.src, r.src === 'PROPOSED' ? 'orange' : 'green') }, { k: 'conf', label: 'Xác nhận' }]);
    if (gs.tab === 'rd') arr(G.ref, [{ k: 0, label: 'Mã' }, { k: 1, label: 'Danh mục BRD' }, { k: 2, label: 'Bảng SQL' }, { k: 3, label: 'Cột đang lưu', render: r => `<span class="mono small" style="white-space:normal">${esc(r[3])}</span>` }, { k: 4, label: 'Giá trị đề xuất', render: r => `<div style="white-space:normal;min-width:220px">${esc(r[4])}</div>` }, { k: 6, label: 'Trạng thái' }]);
    if (gs.tab === 'fn') arr(Store.cat.funcs.filter(x => x.st !== 'MAPPED'), [{ k: 'ph', label: 'Phase' }, { k: 'fn', label: 'Chức năng', render: r => `<a href="#/${r.id}">${esc(r.fn)}</a>` }, { k: 'table', label: 'Bảng chính', render: r => r.table ? `<span class="mono">${r.table}</span>` : '<span class="bad">(chưa có)</span>' }, { k: 'st', label: 'Mapping', render: r => UI.badge(r.st === 'GAP' ? 'Failed' : 'PARTIAL') + ' ' + r.st }, { k: 'note', label: 'Ghi chú BA', render: r => `<div style="white-space:normal;min-width:320px">${esc(r.note)}</div>` }]);
  });
})();
