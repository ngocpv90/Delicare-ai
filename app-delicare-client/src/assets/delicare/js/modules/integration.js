/* P8 – Tin nhắn Bot (Zalo/Telegram/Facebook), KPI ngày/tháng */
(function () {
  const U = UTIL, I = U.icon, esc = U.esc;
  const txt = p => { try { return JSON.parse(p).text; } catch (e) { return p; } };
  APP.register('int-inbound', (el, f) => {
    const ms = Store.rows('InboundMessage').slice().sort((a, b) => (b.ReceivedAt || '').localeCompare(a.ReceivedAt || ''));
    APP.genericList(el, f, { noAdd: true,
      top: `<div class="grid g2 mb"><div class="card panel"><div class="panel-title mb">Hộp thư Bot (InboundMessage + OutboundMessage)</div><div class="chat">${Store.rows('InboundMessage').map(m => ({ t: m.ReceivedAt, h: `<div class="msg"><b>${esc(m.ExternalUserId)}</b> · ${UI.tag(m.MessageType)}<br>${esc(txt(m.PayloadJson))}<small>${U.dt(m.ReceivedAt)} · ${esc(Store.label('IntegrationChannel', m.IntegrationChannelId))} · ${UI.badge(m.ProcessingStatus, 'InboundMessage', 'ProcessingStatus')}</small></div>` })).concat(Store.rows('OutboundMessage').map(m => ({ t: m.QueuedAt, h: `<div class="msg out">${esc(txt(m.PayloadJson))}<small>${U.dt(m.QueuedAt)} · ${esc(Store.label('IntegrationChannel', m.IntegrationChannelId))} · ${UI.badge(m.MessageStatus, 'OutboundMessage', 'MessageStatus')}</small></div>` }))).sort((a, b) => (a.t || '').localeCompare(b.t || '')).map(x => x.h).join('')}</div>
        <div class="row-flex mt"><input class="input" style="flex:1" id="bMsg" placeholder="Gửi tin nhắn ra qua Telegram vận hành…"><button class="btn primary" id="bSend">${I('msg')} Gửi (DEMO)</button></div></div>
        <div class="card panel"><div class="panel-title mb">Xử lý tin nhắn vào</div><p class="small muted">Bot nhận số lượng suất ăn từ khách hàng (Zalo OA) → ProcessingStatus Received → Processed: cập nhật Forecast/Order. Tin lỗi (Failed) cần xử lý lại.</p>${ms.filter(m => m.ProcessingStatus !== 'Processed').map(m => `<div class="alert"><div class="alert-ico ${m.ProcessingStatus === 'Failed' ? 'red' : 'orange'}">${I('bot')}</div><div class="alert-copy"><b>${esc(txt(m.PayloadJson))}</b><small>${esc(m.ErrorMessage || 'Chờ xử lý')} · ${U.dt(m.ReceivedAt)}</small></div><button class="btn xs primary" data-proc="${m.Id}">Đánh dấu đã xử lý</button></div>`).join('') || '<div class="empty">Không có tin chờ xử lý</div>'}</div></div>` });
    el.querySelectorAll('[data-proc]').forEach(b => b.onclick = () => { Store.update('InboundMessage', b.dataset.proc, { ProcessingStatus: 'Processed', ErrorMessage: null }); Store.add('IntegrationLog', { IntegrationChannelId: Store.get('InboundMessage', b.dataset.proc).IntegrationChannelId, DirectionCode: 'IN', OperationCode: 'REPROCESS', RequestAt: U.iso(new Date()), HttpStatus: 200, LogStatus: 'Success' }); UI.toast('Đã xử lý tin nhắn – ghi IntegrationLog', 'ok'); APP.refresh(); });
    el.querySelector('#bSend').onclick = () => { const v = el.querySelector('#bMsg').value.trim(); if (!v) return; const ch = Store.one('IntegrationChannel', 'ChannelCode', 'TELEGRAM_OPS'); Store.add('OutboundMessage', { IntegrationChannelId: ch.Id, ExternalConversationId: 'grp-ops', RecipientExternalId: 'grp-ops', MessageType: 'TEXT', PayloadJson: JSON.stringify({ text: v }), QueuedAt: U.iso(new Date()), SentAt: U.iso(new Date()), MessageStatus: 'Sent', RetryCount: 0 }); UI.toast('Đã xếp hàng gửi tin (OutboundMessage · không gửi thật)', 'ok'); APP.refresh(); };
  });
  APP.register('int-outbound', (el, f) => {
    APP.genericList(el, f, { noAdd: true, cols: [{ k: 'IntegrationChannelId', label: 'Kênh' }, { k: 'RecipientExternalId', label: 'Người nhận' }, { k: 'PayloadJson', label: 'Nội dung', render: r => esc(txt(r.PayloadJson)) }, { k: 'QueuedAt', label: 'Xếp hàng' }, { k: 'RetryCount', label: 'Thử lại', num: true }, { k: 'LastError', label: 'Lỗi' }, { k: 'MessageStatus', label: 'Trạng thái' }] });
    APP.rowExtra(el, tr => { const r = Store.get('OutboundMessage', tr.dataset.row); if (r.MessageStatus === 'Failed') { const td = tr.querySelector('.actions'); td.insertAdjacentHTML('afterbegin', `<button class="btn xs orange" data-rt>Gửi lại</button> `); td.querySelector('[data-rt]').onclick = () => { Store.update('OutboundMessage', r.Id, { MessageStatus: 'Queued', RetryCount: (r.RetryCount || 0) + 1, LastError: null }); UI.toast('Đã đưa lại vào hàng đợi', 'ok'); APP.refresh(); }; } });
  });
  // ---------- KPI ----------
  const ks = { dom: 'ALL' };
  const kpiScreen = (monthly) => (el, f) => {
    const T = monthly ? 'KpiMonthlySnapshot' : 'KpiDailySnapshot', D = monthly ? 'SnapshotMonth' : 'SnapshotDate';
    const defs = Store.rows('KpiMetricDefinition').filter(k => ks.dom === 'ALL' || k.DomainCode === ks.dom);
    const rows = Store.rows(T).filter(r => !Store.ctx.branch || r.BranchId === Store.ctx.branch);
    const dates = [...new Set(rows.map(r => U.day(r[D])))].sort();
    const last = dates[dates.length - 1];
    const avg = (code, d) => { const r = rows.filter(x => x.MetricCode === code && U.day(x[D]) === d); return r.length ? U.sum(r, 'MetricValue') / r.length : null; };
    const doms = ['ALL', ...new Set(Store.rows('KpiMetricDefinition').map(k => k.DomainCode))];
    const lowerBetter = c => /WASTE|REJECT|EXPIRY|COST/.test(c) && c !== 'FIN_MARGIN';
    APP.genericList(el, f, { noAdd: true, rows: () => rows,
      top: `<div class="row-flex mb">${doms.map(d => `<button class="chip ${d === ks.dom ? 'active' : ''}" data-dom="${d}">${d === 'ALL' ? 'Tất cả' : d}</button>`).join('')}<span class="small muted">· ${monthly ? 'Tháng' : 'Ngày'} gần nhất: ${U.date(last)} · Đơn vị: ${Store.ctx.branch ? esc(Store.label('OrganizationUnit', Store.ctx.branch)) : 'Tất cả (bình quân)'}</span></div>
        <div class="grid g3 mb">${defs.map(k => { const v = avg(k.MetricCode, last), tg = (rows.find(r => r.MetricCode === k.MetricCode) || {}).TargetValue; const good = v === null ? null : lowerBetter(k.MetricCode) ? v <= tg : v >= tg; const ser = dates.slice(-30).map(d => avg(k.MetricCode, d) || 0); return `<div class="card panel"><div class="spread"><div><b>${esc(k.MetricName)}</b></div><span class="badge ${good ? 'green' : 'red'}">${good ? 'Đạt' : 'Chưa đạt'}</span></div><div class="kpi-mini-value">${v === null ? '–' : (k.UnitCode === 'VND' ? U.money(v) : U.num(v, 2) + (k.UnitCode === '%' ? '%' : ''))}</div><div class="kpi-foot">Mục tiêu ${k.UnitCode === 'VND' ? U.money(tg) : U.num(tg) + (k.UnitCode === '%' ? '%' : '')} · ${esc(k.FormulaDescription)}</div>${UI.lines(dates.slice(-30).map(d => U.date(d).slice(0, 5)), [{ name: k.MetricName, values: ser, color: good ? '#91AF42' : '#F07825' }], { h: 110, refs: [{ v: tg, color: '#D95C4F', label: 'Mục tiêu' }] })}</div>`; }).join('')}</div>`,
      cols: [{ k: D, label: monthly ? 'Tháng' : 'Ngày' }, { k: 'BranchId', label: 'Chi nhánh' }, { k: 'MetricCode', label: 'Mã KPI' }, { k: 'MetricValue', label: 'Giá trị', num: true }, { k: 'TargetValue', label: 'Mục tiêu', num: true }, { k: 'VarianceValue', label: 'Chênh lệch', num: true }, { k: 'DataQualityStatus', label: 'Chất lượng DL' }] });
    el.querySelectorAll('[data-dom]').forEach(b => b.onclick = () => { ks.dom = b.dataset.dom; APP.refresh(); });
  };
  APP.register('kpi-daily', kpiScreen(false));
  APP.register('kpi-monthly', kpiScreen(true));
})();
