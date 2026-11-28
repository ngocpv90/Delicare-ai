/* AI-22.3 (D-141 l, §7.11 – mockup docs/frontend-handoff/assets/ai-agent/Help, Guides, HelpMobile): Hướng dẫn sử dụng trên giao diện.
 * - Nút "?" cạnh tiêu đề MỌI màn (APP._header) → ngăn bên phải cao bằng màn hình, nổi đè lên trang (không làm trang hẹp); điện thoại (≤ 768px): tấm trượt từ dưới.
 *   3 tab: Các bước (nút "Chỉ cho tôi" tô viền phần tử [data-help=…] của màn) · Câu hỏi thường gặp · Hỏi Vivi (Agent/Chat kèm screen-id; Hermes tắt → báo tiếng Việt).
 *   Chân: "Hướng dẫn có giúp được không? Có / Chưa" (Knowledge/HelpFeedback) + "Mở toàn bộ hướng dẫn". Màn chưa có HDSD → mở mục lục.
 * - Thư viện HDSD (Kho tài liệu › 80 › Hướng dẫn sử dụng): mục lục theo nhóm, "Màn tôi dùng | Tất cả", nhãn Mới, tìm không dấu, "Có gì mới";
 *   trang hướng dẫn: Mở màn / In – tải PDF / Hỏi Vivi + mục lục trong trang.
 * API: Knowledge/GetHelp · GetGuides · HelpFeedback · Agent/Chat. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const fold = s => String(s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
  const NEW_DAYS = 14;
  const isNew = d => d && (Date.now() - new Date(d).getTime()) / 86400000 <= NEW_DAYS;
  const css = `
  .hp-back{position:fixed;inset:0;background:rgba(20,30,10,.18);z-index:1200}
  .hp-pane{position:fixed;top:0;right:0;bottom:0;width:440px;max-width:100vw;background:#fff;z-index:1201;box-shadow:-8px 0 28px rgba(0,0,0,.16);display:flex;flex-direction:column}
  .hp-head{padding:14px 18px 8px;border-bottom:1px solid #EEF1EA}.hp-head h3{margin:2px 0;font-size:17px}
  .hp-tabs{display:flex;gap:4px;padding:8px 14px;border-bottom:1px solid #EEF1EA}.hp-tabs button{border:0;background:none;padding:7px 10px;border-radius:8px;font-weight:600;color:#667;cursor:pointer}
  .hp-tabs button.on{background:#EEF4E4;color:#3A5115}
  .hp-body{flex:1;overflow:auto;padding:14px 18px}.hp-foot{border-top:1px solid #EEF1EA;padding:10px 18px;display:flex;gap:8px;align-items:center;flex-wrap:wrap}
  .hp-step{display:flex;gap:10px;padding:10px 0;border-bottom:1px dashed #EEF1EA}.hp-no{flex:0 0 26px;height:26px;border-radius:50%;background:#EEF4E4;color:#3A5115;font-weight:700;display:flex;align-items:center;justify-content:center}
  .hp-faq{border:1px solid #EEF1EA;border-radius:10px;margin-bottom:8px}.hp-faq summary{padding:9px 12px;cursor:pointer;font-weight:600}.hp-faq div{padding:0 12px 10px}
  .hp-msg{padding:8px 11px;border-radius:10px;margin:6px 0;max-width:92%}.hp-msg.me{background:#EEF4E4;margin-left:auto}.hp-msg.ai{background:#F5F6F3}
  .hp-peek .hp-pane{transform:translateX(calc(100% - 36px));transition:transform .25s}.hp-peek .hp-back{display:none}.hp-pane{transition:transform .25s}
  .hp-hl{outline:3px solid #E8A33D!important;outline-offset:3px;border-radius:8px;transition:outline-color .3s;scroll-margin:120px}
  .help-q{display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border-radius:50%;border:1px solid #C9D6B5;background:#fff;color:#4F6B1F;font-weight:800;margin-left:8px;cursor:pointer;vertical-align:middle;font-size:14px}
  .help-q:hover{background:#EEF4E4}
  .hl-tog button{border:1px solid #D8DFCF;background:#fff;padding:5px 12px;cursor:pointer}.hl-tog button.on{background:#4F6B1F;color:#fff;border-color:#4F6B1F}
  .hl-tog button:first-child{border-radius:8px 0 0 8px}.hl-tog button:last-child{border-radius:0 8px 8px 0}
  .hl-card{border:1px solid #EEF1EA;border-radius:12px;padding:12px 14px;margin-bottom:10px}.hl-new{background:#FFF3DC;color:#8A5A00;border-radius:6px;padding:1px 6px;font-size:11px;font-weight:700;margin-left:6px}
  .hl-toc{position:sticky;top:8px}.hl-toc a{display:block;padding:3px 0;font-size:13px}
  @media (max-width:768px){.hp-pane{top:auto;left:0;width:100vw;height:82vh;border-radius:16px 16px 0 0;box-shadow:0 -8px 28px rgba(0,0,0,.18)}}
  @media print{body.hl-printing .sidebar,body.hl-printing header,body.hl-printing .kn-side,body.hl-printing .hl-bar{display:none!important}}`;
  if (!document.getElementById('hp-style')) { const st = document.createElement('style'); st.id = 'hp-style'; st.textContent = css; document.head.appendChild(st); }

  const fmt = t => esc(t).replace(/\*\*([^*]+)\*\*/g, '<b>$1</b>').replace(/`([^`]+)`/g, '<code>$1</code>');
  const curScreen = id => {
    const h = location.hash.replace(/^#\/?/, ''), base = h.split('?')[0], q = new URLSearchParams(h.split('?')[1] || '');
    const sid = id || base; return q.get('tab') && sid === base ? `${sid}?tab=${q.get('tab')}` : sid;
  };
  // "Chỉ cho tôi": tô viền phần tử [data-help=key] trên màn đang mở.
  const showMe = key => {
    const el = document.querySelector(`#content [data-help="${CSS.escape(key)}"]`);
    if (!el) { UI.toast('Phần này chưa hiện trên màn (có thể ở tab khác hoặc chưa có dữ liệu)', 'warn'); return; }
    document.querySelectorAll('.hp-hl').forEach(x => x.classList.remove('hp-hl'));
    el.scrollIntoView({ behavior: 'smooth', block: 'center' }); el.classList.add('hp-hl');
    setTimeout(() => el.classList.remove('hp-hl'), 4500);
    if (window.innerWidth <= 768) close();
    else if (pane) { pane.classList.add('hp-peek'); setTimeout(() => pane && pane.classList.remove('hp-peek'), 4500); } // ngăn trượt sang phải để thấy phần tử được tô
  };
  let pane = null;
  const close = () => { if (pane) { pane.remove(); pane = null; } };
  const chat = (box, screen, history) => {
    box.innerHTML = `<div class="small muted mb">Vivi trả lời theo Hướng dẫn sử dụng và SOP trong Kho tài liệu, có nêu nguồn. Số liệu hiện tại xem trên màn nghiệp vụ.</div><div id="hpLog"></div>
      <div class="row-flex mt" style="gap:6px"><input class="input" id="hpQ" placeholder="Câu hỏi về màn này…"><button class="btn primary" id="hpSend">Gửi</button></div>`;
    const log = box.querySelector('#hpLog'), q = box.querySelector('#hpQ');
    const draw = () => { log.innerHTML = history.map(m => `<div class="hp-msg ${m.role === 'user' ? 'me' : 'ai'}">${m.role === 'user' ? esc(m.content) : (APP.knowledgeMd ? APP.knowledgeMd(m.content, []) : esc(m.content))}</div>`).join(''); };
    const send = () => {
      const text = q.value.trim(); if (!text) return; q.value = ''; history.push({ role: 'user', content: text }); draw();
      log.insertAdjacentHTML('beforeend', '<div class="hp-msg ai muted" id="hpWait">Vivi đang trả lời…</div>');
      Store.act('Agent/Chat', { json: { agent: 'VIVI', message: text, screen, history: history.slice(-8, -1) }, quiet: true }).then(r => {
        const w = log.querySelector('#hpWait'); if (w) w.remove();
        history.push({ role: 'assistant', content: r.ok && r.data && r.data.reply ? r.data.reply : 'Trợ lý AI chưa trả lời được' + (r.message ? ` – ${r.message}` : '') + '. Hai tab Các bước / Câu hỏi thường gặp vẫn dùng được.' }); draw();
      });
    };
    box.querySelector('#hpSend').onclick = send; q.onkeydown = e => { if (e.key === 'Enter') send(); }; draw(); q.focus();
  };
  APP.help = (screenId, opt = {}) => {
    close();
    const screen = curScreen(screenId), f = (APP.FN || {})[screen.split('?')[0]] || {};
    const back = document.createElement('div'); back.className = 'hp-back';
    const box = document.createElement('aside'); box.className = 'hp-pane'; box.setAttribute('role', 'dialog'); box.setAttribute('aria-label', 'Hướng dẫn sử dụng');
    pane = document.createElement('div'); pane.append(back, box); document.body.appendChild(pane);
    back.onclick = close; document.addEventListener('keydown', function esc1(e) { if (e.key === 'Escape') { close(); document.removeEventListener('keydown', esc1); } });
    box.innerHTML = '<div class="hp-body muted">Đang tải hướng dẫn…</div>';
    const history = [];
    Store.query('Knowledge/GetHelp', { screen }).then(x => {
      if (!pane) return;
      const h = (x.ok && x.data) || { found: false, steps: [], faq: [], notes: [] };
      const tab = opt.tab || (h.found ? 'steps' : 'toc');
      box.innerHTML = `<div class="hp-head"><div class="spread"><div class="small muted">Hướng dẫn sử dụng${h.guide ? ' · ' + esc(h.guide.replace(/^\d+\s*[–-]\s*/, '')) : ''}</div><button class="btn sm" id="hpX" aria-label="Đóng">×</button></div>
          <h3>${esc(h.section || f.name || 'Hướng dẫn')}</h3><div class="small muted">Dành cho: ${esc(String(h.roles || '').replace(/[[\]]/g, '') || 'mọi người dùng')}${h.updated ? ' · Cập nhật ' + esc(U.date(h.updated)) : ''}</div></div>
        <div class="hp-tabs">${h.found ? `<button data-t="steps">Các bước</button><button data-t="faq">Câu hỏi thường gặp</button>` : '<button data-t="toc">Mục lục</button>'}<button data-t="ask">Hỏi Vivi</button></div>
        <div class="hp-body" id="hpBody"></div>
        <div class="hp-foot">${h.found ? `<span class="small">Hướng dẫn có giúp được không?</span><button class="btn sm" data-fb="1">Có</button><button class="btn sm" data-fb="0">Chưa</button>` : ''}<a href="#/knowledge?folder=80-HDSD${h.path ? '&path=' + encodeURIComponent(h.path) + '&anchor=' + esc(h.anchor || '') : ''}" class="small" id="hpAll" style="margin-left:auto">Mở toàn bộ hướng dẫn ›</a></div>`;
      const body = box.querySelector('#hpBody');
      const render = t => {
        box.querySelectorAll('.hp-tabs button').forEach(b => b.classList.toggle('on', b.dataset.t === t));
        if (t === 'steps') {
          body.innerHTML = `${h.intro ? `<p class="small">${esc(h.intro)}</p>` : ''}${h.steps.length ? h.steps.map(s => `<div class="hp-step"><span class="hp-no">${s.no}</span><div style="flex:1"><b>${fmt(s.title)}</b>${s.text ? `<div class="small muted">${fmt(s.text)}</div>` : ''}${s.target ? `<button class="btn xs mt" data-show="${esc(s.target)}">Chỉ cho tôi</button>` : ''}</div></div>`).join('') : '<p class="muted small">Mục này chưa chia bước – mở toàn bộ hướng dẫn.</p>'}
            ${(h.notes || []).map(n => `<div class="notice info mt small"><div>${esc(n)}</div></div>`).join('')}
            ${(h.related || []).length ? `<div class="small mt">Xem thêm: ${h.related.map(r => `<a href="#/knowledge?folder=80-HDSD&path=${encodeURIComponent(r.path)}">${esc(r.title)}</a>`).join(' · ')}</div>` : ''}`;
          body.querySelectorAll('[data-show]').forEach(b => b.onclick = () => showMe(b.dataset.show));
        } else if (t === 'faq') {
          body.innerHTML = (h.faq || []).length ? h.faq.map(q => `<details class="hp-faq"><summary>${fmt(q.q)}</summary><div class="small">${fmt(q.a)}</div></details>`).join('') : '<p class="muted small">Chưa có câu hỏi thường gặp cho hướng dẫn này – thử Hỏi Vivi.</p>';
        } else if (t === 'toc') {
          body.innerHTML = '<p class="small muted">Màn này chưa có hướng dẫn riêng. Mục lục Hướng dẫn sử dụng:</p><div id="hpToc" class="muted small">Đang tải…</div>';
          Store.query('Knowledge/GetGuides').then(g => { const el = body.querySelector('#hpToc'); if (el) el.innerHTML = (g.list || []).map(x => `<a class="kn-note" href="#/knowledge?folder=80-HDSD&path=${encodeURIComponent(x.path)}">${esc(x.title)}</a>`).join('') || 'Chưa có hướng dẫn nào.'; });
        } else chat(body, screen, history);
      };
      box.querySelectorAll('.hp-tabs button').forEach(b => b.onclick = () => render(b.dataset.t));
      box.querySelector('#hpX').onclick = close; box.querySelector('#hpAll').onclick = close;
      box.querySelectorAll('[data-fb]').forEach(b => b.onclick = () => {
        const ok = b.dataset.fb === '1';
        const go = comment => Store.act('Knowledge/HelpFeedback', { json: { screen, path: h.path, helpful: ok, comment }, quiet: true }).then(r => { box.querySelector('.hp-foot').insertAdjacentHTML('afterbegin', `<span class="small up">${esc((r.res && r.res.responseMessage) || 'Đã ghi nhận')}</span>`); box.querySelectorAll('[data-fb]').forEach(z => z.disabled = true); });
        if (ok) go(null); else UI.confirm('Hướng dẫn còn thiếu gì?', c => go(c), { input: 'Góp ý (không bắt buộc)', ok: 'Gửi' });
      });
      render(tab);
    });
  };
  document.addEventListener('click', e => { const b = e.target.closest && e.target.closest('[data-help-open]'); if (b) { e.preventDefault(); APP.help(b.dataset.helpOpen); } });

  // ---------- Thư viện HDSD (Kho tài liệu › 80 › Hướng dẫn sử dụng) ----------
  const GROUPS = [[/^0[01]_/, 'Chung & quản trị'], [/^0[2-3]_/, 'Kinh doanh & kế hoạch'], [/^0[4-5]_/, 'Mua hàng & kho'], [/^0[6-8]_/, 'Chất lượng, bếp, đóng gói'], [/^(09|10)_/, 'Giao vận & điểm phát'], [/^1[1-2]_/, 'Nhân sự & duyệt'], [/^1[3-6]_/, 'Điều hành, kế toán, AI, kho tài liệu'], [/./, 'Khác']];
  const groupOf = p => (GROUPS.find(([rx]) => rx.test(p.split('/').pop())) || [, 'Khác'])[1];
  const lib = { mine: true, q: '' };
  APP.helpLibrary = main => {
    main.innerHTML = '<div class="muted small">Đang tải thư viện hướng dẫn…</div>';
    Store.query('Knowledge/GetGuides').then(x => {
      if (!main.isConnected) return;
      const all = x.list || [];
      const mineOk = g => (g.screens || []).some(s => Store.perm(s.split('?')[0]).access !== false && (APP.FN || {})[s.split('?')[0]]);
      const draw = () => {
        const q = fold(lib.q), list = all.filter(g => (!lib.mine || mineOk(g)) && (!q || fold(g.title + ' ' + (g.sections || []).join(' ')).includes(q)));
        const news = all.filter(g => isNew(g.updated)).sort((a, b) => String(b.updated).localeCompare(String(a.updated))).slice(0, 5);
        const groups = [...new Set(list.map(g => groupOf(g.path)))];
        main.innerHTML = `<div class="spread" style="flex-wrap:wrap;gap:8px"><div><h2 style="margin:0">Hướng dẫn sử dụng</h2><div class="small muted">${U.num(all.length)} hướng dẫn · tự cập nhật theo bản phát hành</div></div>
            <div class="row-flex" style="gap:8px"><div class="hl-tog"><button data-m="1" class="${lib.mine ? 'on' : ''}">Màn tôi dùng</button><button data-m="0" class="${lib.mine ? '' : 'on'}">Tất cả</button></div><input class="input" id="hlQ" placeholder="Tìm (gõ không dấu được)…" value="${esc(lib.q)}" style="width:220px"></div></div>
          ${news.length ? `<div class="notice info mt"><div><b>Có gì mới:</b> ${news.map(g => `<a href="#/knowledge?folder=80-HDSD&path=${encodeURIComponent(g.path)}">${esc(g.title)}</a> <span class="small muted">(${esc(U.date(g.updated))})</span>`).join(' · ')}</div></div>` : ''}
          ${groups.map(gr => `<div class="panel-title mt">${esc(gr)}</div>${list.filter(g => groupOf(g.path) === gr).map(g => `<div class="hl-card"><a href="#/knowledge?folder=80-HDSD&path=${encodeURIComponent(g.path)}"><b>${esc(g.title)}</b></a>${isNew(g.updated) ? '<span class="hl-new">Mới</span>' : ''}
            <div class="small muted">${(g.sections || []).slice(0, 6).map(esc).join(' · ')}${(g.sections || []).length > 6 ? ' …' : ''}</div><div class="small muted">Cập nhật ${esc(U.date(g.updated))}${g.roles && g.roles !== '[]' ? ' · ' + esc(g.roles.replace(/[[\]]/g, '')) : ''}</div></div>`).join('')}`).join('') || `<p class="muted mt">${lib.mine ? 'Chưa có hướng dẫn cho các màn anh/chị dùng – bấm "Tất cả".' : 'Không có hướng dẫn phù hợp.'}</p>`}`;
        main.querySelectorAll('[data-m]').forEach(b => b.onclick = () => { lib.mine = b.dataset.m === '1'; draw(); });
        const qi = main.querySelector('#hlQ'); let t; qi.oninput = () => { clearTimeout(t); t = setTimeout(() => { lib.q = qi.value.trim(); draw(); const n = main.querySelector('#hlQ'); n.focus(); n.setSelectionRange(n.value.length, n.value.length); }, 250); };
      };
      draw();
    });
  };
  // Trang 1 hướng dẫn: thanh Mở màn / In / Hỏi Vivi + mục lục trong trang (gọi sau khi vẽ ghi chú type hdsd).
  APP.helpGuidePage = (main, n) => {
    const art = main.querySelector('.kn-body'); if (!art) return;
    const heads = [...art.querySelectorAll('h2,h3,h4')];
    heads.forEach((h, i) => { h.id = 'h-' + i; });
    const screens = String((n.meta || {}).screens || '').replace(/[[\]]/g, '').split(',').map(s => s.trim()).filter(Boolean);
    const open = screens.find(s => (APP.FN || {})[s.split('?')[0]] && Store.perm(s.split('?')[0]).access !== false);
    art.insertAdjacentHTML('beforebegin', `<div class="row-flex hl-bar mb" style="gap:6px;flex-wrap:wrap">${open ? `<a class="btn sm" href="#/${esc(open)}">${I('external')} Mở màn</a>` : ''}<button class="btn sm" id="hlPrint">${I('print')} In / tải PDF</button><button class="btn sm" id="hlAsk">Hỏi Vivi</button>
        <span class="small muted" style="margin-left:auto">Dành cho: ${esc(String((n.meta || {}).roles || '').replace(/[[\]]/g, '') || 'mọi người dùng')} · cập nhật ${esc(U.date((n.meta || {}).updated))}</span></div>
      ${heads.length > 3 ? `<details class="mb" open><summary class="small"><b>Mục lục</b></summary><div class="hl-toc">${heads.filter(h => h.tagName !== 'H4').map(h => `<a href="javascript:void 0" data-go="${h.id}" style="padding-left:${h.tagName === 'H3' ? 0 : 14}px">${esc(h.textContent)}</a>`).join('')}</div></details>` : ''}`);
    main.querySelectorAll('[data-go]').forEach(a => a.onclick = () => document.getElementById(a.dataset.go).scrollIntoView({ behavior: 'smooth' }));
    main.querySelector('#hlPrint').onclick = () => { document.body.classList.add('hl-printing'); setTimeout(() => { window.print(); document.body.classList.remove('hl-printing'); }, 50); };
    main.querySelector('#hlAsk').onclick = () => APP.help(open || 'knowledge', { tab: 'ask' });
    const anchor = new URLSearchParams(location.hash.split('?')[1] || '').get('anchor') || '';
    if (anchor) { const key = h => { const c = h.cloneNode(true); c.querySelectorAll('code').forEach(x => x.remove()); return 'h-' + fold(c.textContent).replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, ''); }; const t = heads.find(h => key(h) === anchor); if (t) setTimeout(() => t.scrollIntoView({ behavior: 'smooth' }), 200); }
  };
})();
