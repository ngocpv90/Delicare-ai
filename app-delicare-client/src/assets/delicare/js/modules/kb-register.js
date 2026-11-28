/* AI-22.4 (D-141 i): Kho tài liệu – Sổ công văn đến / đi (vùng 05), Biên bản họp giao ban (vùng 15), Việc tôi được giao.
 * Số vào sổ máy chủ tự cấp theo năm (VD / VI-YYYY-NNNN, BH-YYYY-NNN). Giao người xử lý + hạn / việc giao trong họp → việc theo dõi (98): người được giao bấm Xong,
 * 07:00 nhắc qua bot khi sắp / quá hạn. API: Knowledge/GetRegister · UploadFile · CreateMeeting · GetAssignees · GetMyTasks · SetTask. */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const TODAY = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10);
  const st = { year: new Date().getFullYear() };
  const chip = (t, c) => `<span class="tag ${c || ''}">${esc(t)}</span>`;
  const taskChip = r => !r.taskCount ? '<span class="muted small">–</span>' : r.taskStatus === 'xong' ? chip('Xong', 'green') : r.overdue ? chip('Quá hạn', 'red') : chip(`Đang xử lý ${r.taskDone}/${r.taskCount}`, 'orange');
  // Ô chọn người (gõ tìm) – Knowledge/GetAssignees.
  const userPicker = (host, onPick) => {
    host.innerHTML = '<input class="input" placeholder="Gõ tên / tài khoản…" data-up-q><div class="small" data-up-l style="max-height:150px;overflow:auto"></div><input type="hidden" data-up-v>';
    const q = host.querySelector('[data-up-q]'), l = host.querySelector('[data-up-l]'), v = host.querySelector('[data-up-v]'); let t;
    const load = () => Store.query('Knowledge/GetAssignees', { keyword: q.value.trim() || null }).then(x => {
      l.innerHTML = (x.list || []).map(u => `<a href="javascript:void 0" data-u="${esc(u.id)}" data-n="${esc(u.userName)}" style="display:block;padding:3px 0">${esc(u.fullName || u.userName)} <span class="muted">${esc(u.userName)}</span></a>`).join('') || '<span class="muted">Không tìm thấy</span>';
      l.querySelectorAll('[data-u]').forEach(a => a.onclick = () => { v.value = a.dataset.u; q.value = a.dataset.n; l.innerHTML = ''; onPick && onPick(a.dataset.u); });
    });
    q.oninput = () => { v.value = ''; clearTimeout(t); t = setTimeout(load, 250); };
    return () => v.value || null;
  };
  APP.kbRegister = (main, fd) => {
    const isMeet = fd.code === '15';
    main.innerHTML = '<div class="muted small">Đang tải…</div>';
    Store.query('Knowledge/GetRegister', { folder: fd.code, year: st.year }).then(x => {
      if (!main.isConnected) return;
      const list = x.list || [];
      main.innerHTML = `<div class="spread" style="flex-wrap:wrap;gap:8px"><div><h2 style="margin:0">${isMeet ? 'Biên bản họp giao ban' : 'Sổ công văn đến / đi'}</h2><div class="small muted">${U.num(list.length)} ${isMeet ? 'biên bản' : 'văn bản'} năm ${st.year} · số vào sổ do hệ thống cấp theo năm</div></div>
          <div class="row-flex" style="gap:6px"><select class="select" id="rgY" style="width:auto">${[0, 1, 2].map(i => new Date().getFullYear() - i).map(y => `<option ${y === st.year ? 'selected' : ''}>${y}</option>`).join('')}</select>
          ${fd.canEdit ? (isMeet ? `<button class="btn primary sm" id="rgNew">${I('plus')} Lập biên bản họp</button>` : `<button class="btn sm" id="rgOut">${I('up')} Vào sổ công văn đi</button><button class="btn primary sm" id="rgIn">${I('down')} Vào sổ công văn đến</button>`) : ''}</div></div>
        <div class="table-wrap mt"><table class="table dc-table"><thead><tr><th>Số vào sổ</th><th>Ngày</th>${isMeet ? '<th>Cuộc họp</th><th>Chủ trì</th>' : '<th>Loại</th><th>Số / ký hiệu</th><th>Nơi gửi / nhận</th><th>Trích yếu</th><th>Người xử lý</th><th>Hạn</th>'}<th>Việc giao</th></tr></thead><tbody>
        ${list.map(r => `<tr><td class="mono"><a href="#/knowledge?folder=${esc(fd.code)}&path=${encodeURIComponent(r.path)}">${esc(r.registerNo || '')}</a></td><td>${esc(U.date(r.date))}</td>${isMeet
          ? `<td>${esc(r.title)}</td><td>${esc(r.party || '')}</td>`
          : `<td>${r.type === 'cong-van-di' ? chip('Đi', 'blue') : chip('Đến', 'green')}</td><td>${esc(r.soHieu || '')}</td><td>${esc(r.party || '')}</td><td><a href="#/knowledge?folder=${esc(fd.code)}&path=${encodeURIComponent(r.path)}">${esc(r.title)}</a></td><td>${esc(r.handler || '')}</td><td>${esc(U.date(r.due))}</td>`}<td>${taskChip(r)}</td></tr>`).join('') || `<tr><td colspan="9" class="muted">Chưa có ${isMeet ? 'biên bản' : 'văn bản'} nào năm ${st.year}.</td></tr>`}</tbody></table></div>`;
      main.querySelector('#rgY').onchange = e => { st.year = +e.target.value; APP.kbRegister(main, fd); };
      const inB = main.querySelector('#rgIn'), outB = main.querySelector('#rgOut'), nb = main.querySelector('#rgNew');
      if (inB) inB.onclick = () => dispatchForm(fd, 'cong-van-den', () => APP.kbRegister(main, fd));
      if (outB) outB.onclick = () => dispatchForm(fd, 'cong-van-di', () => APP.kbRegister(main, fd));
      if (nb) nb.onclick = () => meetingForm(() => APP.kbRegister(main, fd));
    });
  };
  function dispatchForm(fd, type, done) {
    const isIn = type === 'cong-van-den';
    UI.modal({
      title: isIn ? 'Vào sổ công văn đến' : 'Vào sổ công văn đi', size: 'md', sub: 'Số vào sổ do hệ thống cấp (VD / VI-năm-số thứ tự). Giao người xử lý + hạn → thành việc theo dõi, nhắc qua bot khi sắp / quá hạn.',
      body: `<label class="field"><span>Tệp văn bản (PDF / Word / ảnh)</span><input type="file" class="input" id="dfFile" accept=".pdf,.docx,.xlsx,.jpg,.jpeg,.png"></label>
        <div class="grid2"><label class="field"><span>Số / ký hiệu văn bản</span><input class="input" id="dfNo" placeholder="vd 125/CV-SYT"></label><label class="field"><span>Ngày văn bản</span><input type="date" class="input" id="dfDay" value="${TODAY()}"></label></div>
        <label class="field"><span>${isIn ? 'Nơi gửi' : 'Nơi nhận'}</span><input class="input" id="dfParty"></label>
        <label class="field"><span>Trích yếu</span><input class="input" id="dfTitle"></label>
        <div class="grid2"><div class="field"><span>Người xử lý (không bắt buộc)</span><div id="dfUser"></div></div><label class="field"><span>Hạn xử lý</span><input type="date" class="input" id="dfDue"></label></div>
        <label class="field"><span>Ghi chú / tóm tắt</span><textarea class="input" id="dfSum" rows="2"></textarea></label>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="dfGo">Vào sổ</button>',
      onMount: (m, close) => {
        const pick = userPicker(m.querySelector('#dfUser'));
        m.querySelector('#dfGo').onclick = () => {
          const file = m.querySelector('#dfFile').files[0], v = id => m.querySelector(id).value.trim();
          if (!file) return UI.toast('Chọn tệp văn bản', 'warn');
          if (!v('#dfTitle')) return UI.toast('Nhập trích yếu', 'warn');
          if (pick() && !v('#dfDue')) return UI.toast('Giao người xử lý cần hạn xử lý', 'warn');
          const f = new FormData(); f.append('file', file); f.append('folder', '05'); f.append('type', type); f.append('title', v('#dfTitle')); f.append('soHieu', v('#dfNo'));
          f.append('ngay', v('#dfDay')); f.append('noiGuiNhan', v('#dfParty')); f.append('summary', v('#dfSum')); if (pick()) { f.append('handlerId', pick()); f.append('handlerDue', v('#dfDue')); }
          Store.act('Knowledge/UploadFile', { json: f }).then(r => { if (!r.ok) return; close(); UI.toast(esc((r.res && r.res.responseMessage) || 'Đã vào sổ'), 'ok'); done(); });
        };
      },
    });
  }
  function meetingForm(done) {
    UI.modal({
      title: 'Lập biên bản họp', size: 'lg', sub: 'Theo mẫu 99. Số BH-năm-số do hệ thống cấp; mỗi việc giao tự thành việc theo dõi (nút Xong, nhắc khi sắp / quá hạn).',
      body: `<div class="grid2"><label class="field"><span>Tên cuộc họp</span><input class="input" id="mtT" placeholder="vd Giao ban tuần 48"></label><label class="field"><span>Ngày họp</span><input type="date" class="input" id="mtD" value="${TODAY()}"></label></div>
        <div class="grid2"><label class="field"><span>Chủ trì</span><input class="input" id="mtC"></label><label class="field"><span>Thành phần</span><input class="input" id="mtA"></label></div>
        <label class="field"><span>Nội dung</span><textarea class="input" id="mtN" rows="4"></textarea></label><label class="field"><span>Kết luận</span><textarea class="input" id="mtK" rows="2"></textarea></label>
        <div class="panel-title">Việc giao</div><div id="mtRows"></div><button class="btn sm mt" id="mtAdd">${I('plus')} Thêm việc</button>`,
      foot: '<button class="btn" data-close>Hủy</button><button class="btn primary" id="mtGo">Lưu biên bản</button>',
      onMount: (m, close) => {
        const rows = [];
        const add = () => {
          const div = document.createElement('div'); div.className = 'grid3 mb'; div.style.cssText = 'display:grid;grid-template-columns:2fr 1.4fr 1fr;gap:8px;align-items:start';
          div.innerHTML = '<input class="input" placeholder="Việc" data-t><div data-u></div><input type="date" class="input" data-d>';
          m.querySelector('#mtRows').appendChild(div); rows.push({ div, pick: userPicker(div.querySelector('[data-u]')) });
        };
        add(); m.querySelector('#mtAdd').onclick = add;
        m.querySelector('#mtGo').onclick = () => {
          const v = id => m.querySelector(id).value.trim();
          if (!v('#mtT')) return UI.toast('Nhập tên cuộc họp', 'warn');
          const tasks = rows.map(r => ({ task: r.div.querySelector('[data-t]').value.trim(), assigneeId: r.pick(), due: r.div.querySelector('[data-d]').value })).filter(t => t.task);
          if (tasks.some(t => !t.assigneeId || !t.due)) return UI.toast('Mỗi việc giao cần người được giao và hạn', 'warn');
          Store.act('Knowledge/CreateMeeting', { json: { title: v('#mtT'), date: v('#mtD'), chair: v('#mtC'), attendees: v('#mtA'), content: v('#mtN'), conclusion: v('#mtK'), tasks } }).then(r => { if (!r.ok) return; close(); UI.toast(esc((r.res && r.res.responseMessage) || 'Đã lưu'), 'ok'); done(); });
        };
      },
    });
  }
  // Việc tôi được giao (từ sổ công văn / biên bản họp) – không cần quyền thư mục 98.
  APP.kbMyTasks = main => {
    main.innerHTML = '<div class="muted small">Đang tải…</div>';
    Store.query('Knowledge/GetMyTasks', { includeDone: true }).then(x => {
      if (!main.isConnected) return;
      const list = x.list || [];
      main.innerHTML = `<h2 style="margin:0">Việc tôi được giao</h2><div class="small muted mb">Từ sổ công văn và biên bản họp. Bấm Xong khi hoàn thành – người giao thấy ngay trên sổ.</div>
        <div class="table-wrap"><table class="table dc-table"><thead><tr><th>Việc</th><th>Hạn</th><th>Trạng thái</th><th></th></tr></thead><tbody>
        ${list.map(t => `<tr><td><b>${esc(t.title)}</b><div class="small muted">${esc((t.content || '').split('\n')[0])}</div></td><td class="nowrap">${esc(U.date(t.due))}</td><td>${t.status === 'xong' ? chip('Xong', 'green') : t.overdue ? chip('Quá hạn', 'red') : chip('Đang làm', 'orange')}</td>
          <td>${t.status === 'xong' ? `<button class="btn xs" data-st="${esc(t.path)}" data-v="đang theo dõi">Mở lại</button>` : `<button class="btn xs primary" data-st="${esc(t.path)}" data-v="xong">Xong</button>`}</td></tr>`).join('') || '<tr><td colspan="4" class="muted">Chưa có việc nào được giao cho anh/chị.</td></tr>'}</tbody></table></div>`;
      main.querySelectorAll('[data-st]').forEach(b => b.onclick = () => Store.act('Knowledge/SetMyTask', { json: { path: b.dataset.st, status: b.dataset.v } }).then(r => { if (r.ok) { UI.toast('Đã cập nhật', 'ok'); APP.kbMyTasks(main); } }));
    });
  };
})();
