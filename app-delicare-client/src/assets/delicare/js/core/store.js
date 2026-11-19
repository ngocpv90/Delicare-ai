/* DELICARE – Store dữ liệu dùng chung cho mọi màn hình.
 * Chế độ "api" (mặc định khi chạy trong Angular): bảng nào có controller thì đọc / ghi qua API cũ
 *   api/<Controller>/GetAllData | GetAll | Insert | Update | Delete  (khung D-30).
 * Bảng chưa có API đọc → giữ dữ liệu DEMO của mockup (docs/mockup/js/data/demo-data.js).
 * Chế độ "demo": toàn bộ dữ liệu DEMO trong bộ nhớ (như mockup gốc). */
(function () {
  const S = window.DC_SCHEMA, CAT = window.DC_CATALOG, RAW = window.DC_DEMO;
  const HOST = window.DC_HOST || null;
  const AUDIT = new Set(['CreatedDate', 'CreatedBy', 'ModifiedDate', 'ModifiedBy', 'DeletedDate', 'DeletedBy', 'DeleteddBy', 'IsDeleted']);
  const COLMETA = {};               // table -> {col: meta}
  const LOWER = {};                 // table -> {lowercase col: col}  (API trả camelCase)
  Object.entries(S.tables).forEach(([t, v]) => {
    COLMETA[t] = {}; LOWER[t] = {};
    v.cols.forEach(([name, type, nullable, def, usage, note, desc, basis], i) => {
      COLMETA[t][name] = { name, type, nullable: !!nullable, def, usage, note, desc, basis, ord: i + 1, table: t };
      LOWER[t][name.toLowerCase()] = name;
    });
  });
  const REL = {}, REL_IN = {};
  S.rels.forEach(r => { REL[r.src + '.' + r.col] = r; (REL_IN[r.dst] = REL_IN[r.dst] || []).push(r); });
  const DEP = {}; S.dep.forEach(d => { DEP[d.table + '.' + d.col] = d.use; });
  const UNRES = {}; S.unres.forEach(u => { UNRES[u.src + '.' + u.col] = u; });

  // ---------- Ánh xạ bảng → controller API cũ ----------
  // Tên bảng legacy → tên class / controller (tools/AppDelicare.CodeGen/Metadata.cs ClassNames).
  const CLASS = { Dishe: 'Dish', DishCategorie: 'DishCategory', IngredientCategorie: 'IngredientCategory', InventoryPolicie: 'InventoryPolicy', PricePolicie: 'PricePolicy', ProductionBatche: 'ProductionBatch', CleaningBatche: 'CleaningBatch', ProofOfDeliverie: 'ProofOfDelivery', QCInspection: 'QcInspection', QCInspectionLine: 'QcInspectionLine' };
  // Bảng không có API đọc danh sách → dữ liệu DEMO.
  const NO_READ = new Set(['ActualCosts_Legacy_20261113', 'UnitsOfMeasure_Legacy_20261113']);
  // Bảng đọc qua action danh sách riêng (không có GetAllData).
  const LIST_PATH = { DocumentFile: 'DocumentFile/GetList?Page=1&PageSize=200' };
  // Bảng nhật ký lớn / không có GetAllData → GetAll phân trang (1 trang 1000 dòng).
  const PAGED = new Set(['AuditChange', 'LocationTracking', 'CoreDataMergeConflict']);
  const PAGE_SIZE = 1000;
  const ctrlOf = t => CLASS[t] || t;
  const MODE_KEY = 'dc-data-mode';
  let mode = 'demo';
  if (HOST) { try { mode = localStorage.getItem(MODE_KEY) === 'demo' ? 'demo' : 'api'; } catch (e) { mode = 'api'; } }
  const isApi = t => mode === 'api' && !!S.tables[t] && !NO_READ.has(t);

  const data = {}, idx = {};
  const src = {};                   // table -> 'demo' | 'idle' | 'pending' | 'api' | 'error'
  const errMsg = {};
  const missing = {}, probed = {};   // table -> true
  // Bảng không có GetById (đọc qua action riêng) → không dò, coi như có.
  const NO_PROBE = new Set(['UserRole', 'DocumentFile', 'AuditChange', 'LocationTracking']);
  const inflight = {};              // table -> Promise
  const listeners = [];
  const touched = new Set();        // bảng được màn hình hiện tại đọc
  function demoRows(t) {
    const pack = RAW[t]; const out = [];
    if (!pack) return out;
    pack.r.forEach(arr => {
      const row = {};
      pack.c.forEach((c, i) => { if (arr[i] !== null && arr[i] !== undefined) row[c] = arr[i]; });
      if (COLMETA[t].CreatedDate && !row.CreatedDate) row.CreatedDate = '2026-11-01T08:00';
      if (COLMETA[t].IsDeleted && row.IsDeleted === undefined) row.IsDeleted = 0;
      out.push(row);
    });
    return out;
  }
  function setRows(t, rows) { data[t] = rows; idx[t] = {}; rows.forEach(r => { if (r.Id !== undefined) idx[t][r.Id] = r; }); }
  function load() {
    Object.keys(S.tables).forEach(t => {
      if (isApi(t)) { setRows(t, []); src[t] = 'idle'; delete errMsg[t]; }
      else { setRows(t, demoRows(t)); src[t] = 'demo'; }
    });
  }
  load();

  // ---------- Chuẩn hóa dữ liệu API ----------
  const isBit = (t, c) => COLMETA[t][c] && COLMETA[t][c].type === 'bit';
  function fromApi(t, obj) {
    const row = {};
    Object.entries(obj || {}).forEach(([k, v]) => {
      if (v === null || v === undefined || (typeof v === 'object' && !Array.isArray(v))) return;
      const col = LOWER[t][k.toLowerCase()] || (k.charAt(0).toUpperCase() + k.slice(1));
      if (Array.isArray(v)) return;
      if (typeof v === 'boolean') v = v ? 1 : 0;
      else if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?$/.test(v)) v = v.replace(/\.\d+$/, '');
      row[col] = v;
    });
    if (COLMETA[t].IsDeleted && row.IsDeleted === undefined) row.IsDeleted = 0;
    return row;
  }
  const listOf = res => (res && (res.listData || res.ListData || (res.data && (res.data.listData || res.data.ListData)))) || (Array.isArray(res) ? res : []);
  const failOf = res => res && res.responseCode !== undefined && res.responseCode !== 200 ? (res.responseMessage || 'Lỗi API') : null;
  const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

  async function fetchTable(t) {
    const c = ctrlOf(t);
    if (t === 'UserRole') {
      // Không có GetAllData: ghép ma trận quyền của từng nhóm (UserRole/GetMatrix).
      await Store.ensure('UserGroup');
      const out = [];
      for (const g of data.UserGroup) {
        const res = await HOST.get(`UserRole/GetMatrix?userGroupId=${g.Id}`);
        const fail = failOf(res); if (fail) throw new Error(fail);
        const walk = list => (list || []).forEach(n => {
          if (n.id && n.id !== EMPTY_GUID) out.push(fromApi(t, Object.assign({}, n, { userGroupId: g.Id })));
          walk(n.children);
        });
        walk(res && res.data && (res.data.listMenu || res.data.ListMenu));
      }
      return out;
    }
    const res = await HOST.get(LIST_PATH[t] || (PAGED.has(t) ? `${c}/GetAll?Page=1&PageSize=${PAGE_SIZE}` : `${c}/GetAllData`));
    const fail = failOf(res); if (fail) throw new Error(fail);
    return listOf(res).map(r => fromApi(t, r));
  }
  function notify(t) { listeners.forEach(fn => { try { fn(t); } catch (e) { console.error(e); } }); }

  // Ghi tuần tự (đầu phiếu trước, dòng sau) – giữ đúng thứ tự thao tác trên màn hình.
  let writeQueue = Promise.resolve();
  const refetchTimers = {};
  function refetchSoon(t) {
    clearTimeout(refetchTimers[t]);
    refetchTimers[t] = setTimeout(() => { if (src[t] === 'api' || src[t] === 'error') { src[t] = 'idle'; Store.ensure(t); } }, 400);
  }
  function toForm(t, row) {
    const fd = new FormData();
    Object.entries(row).forEach(([k, v]) => {
      if (v === null || v === undefined || v === '' || AUDIT.has(k)) return;
      if (isBit(t, k) || typeof v === 'boolean') v = (v === true || v === 1 || v === '1' || v === 'true') ? 'true' : 'false';
      else if (typeof v === 'object') v = JSON.stringify(v);
      fd.append(k, String(v));
    });
    return fd;
  }
  function toast(msg, kind) { if (window.UI && UI.toast) UI.toast(msg, kind); }
  // Object thường → FormData cho action [FromForm] (bỏ null / rỗng).
  function objForm(obj) {
    const fd = new FormData();
    Object.entries(obj || {}).forEach(([k, v]) => {
      if (v === null || v === undefined || v === '') return;
      fd.append(k, typeof v === 'boolean' ? (v ? 'true' : 'false') : String(v));
    });
    return fd;
  }
  const qs = obj => Object.entries(obj || {}).filter(([, v]) => v !== null && v !== undefined && v !== '').map(([k, v]) => encodeURIComponent(k) + '=' + encodeURIComponent(v)).join('&');
  function write(t, action, live) {
    const c = ctrlOf(t);
    const row = Object.assign({}, live);
    writeQueue = writeQueue.then(async () => {
      try {
        let res;
        if (t === 'UserRole') {
          const r = {}; Object.entries(row).forEach(([k, v]) => { r[k] = isBit(t, k) ? !!v : v; });
          const fd = new FormData(); fd.append('ListPermission', JSON.stringify([r]));
          res = await HOST.send('PUT', 'UserRole/Update', fd);
        } else if (action === 'Delete') res = await HOST.send('DELETE', `${c}/Delete?id=${encodeURIComponent(row.Id)}`);
        else res = await HOST.send(action === 'Insert' ? 'POST' : 'PUT', `${c}/${action}`, toForm(t, row));
        const fail = failOf(res);
        if (fail) toast(`<b>API ${c}/${action}</b> từ chối: ${UTIL.esc(fail)}`, 'err');
      } catch (e) {
        toast(`<b>Lỗi API ${c}/${action}</b> ${UTIL.esc(HOST.errorText(e))}`, 'err');
      }
      refetchSoon(t);
    });
    return writeQueue;
  }

  // ---------- MA-1 (D-63): quy tắc mã + loại khách hàng ----------
  // Danh mục loại khách hàng (D-63 f) – chỉ 2 giá trị; giá trị cũ (HOSPITAL…) hiện nguyên văn tới khi chạy script MA-1.4.
  CAT.status = CAT.status || {};
  CAT.status['Customer.CustomerType'] = { codes: ['BUSINESS', 'INDIVIDUAL'], vi: ['Doanh nghiệp', 'Cá nhân'], src: 'D-63', conf: 'D-63 f' };
  // Quy tắc cột mã: api/Common/GetCodeRules → { 'Table.Column': { group: G1|G2|G3, prefix, pattern } }.
  // Chế độ DEMO / chưa tải xong → dùng bảng tối thiểu của D-63 để form vẫn khóa đúng ô Mã danh mục G1.
  const CODE_RULES_FALLBACK = {
    'Customer.Code': 'KHB / KHC', 'Supplier.Code': 'NC', 'Ingredient.Code': 'NL', 'Dishe.Code': 'MA', 'Warehouse.Code': 'KO', 'Vehicle.Code': 'XE',
    'Employee.Code': 'NV', 'Employee.EmployeeCode': 'NV', 'CustomerDeliveryLocation.Code': 'DP', 'PricePolicie.Code': 'BG', 'Menu.Code': 'NU',
  };
  const codeRules = {};
  Object.entries(CODE_RULES_FALLBACK).forEach(([k, p]) => { codeRules[k] = { group: 'G1', prefix: p }; });
  let codeRulesLoaded = null;
  function loadCodeRules() {
    if (codeRulesLoaded || mode !== 'api' || !HOST) return codeRulesLoaded || Promise.resolve();
    codeRulesLoaded = HOST.get('Common/GetCodeRules').then(res => {
      if (typeof res === 'string' || failOf(res)) { codeRulesLoaded = null; return; }   // chưa đăng nhập / lỗi → lần sau thử lại
      listOf(res).forEach(r => { codeRules[r.table + '.' + r.column] = { group: r.group, prefix: r.prefix, pattern: r.pattern, catalog: r.catalog }; });
    }).catch(e => { codeRulesLoaded = null; console.warn('[DELICARE] Không tải được quy tắc mã', e); });
    return codeRulesLoaded;
  }
  // G2: chữ hoa không dấu, chỉ A–Z 0–9 - _ (giống CodeRules.NormalizeUserCode ở máy chủ).
  const userCode = v => String(v || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toUpperCase().replace(/[^A-Z0-9_-]/g, '');

  const PERM_ALIAS = { 'crm-customer-360': 'crm-customer' };
  const NAME_FIELDS = ['DisplayName', 'Name', 'FullName', 'MetricName', 'ChannelName', 'BotName', 'ItemName', 'NameVN', 'MealName', 'FileName', 'UserName'];
  const NO_FIELDS = ['OrderNumber', 'ContractNumber', 'RequestNumber', 'PurchaseOrderNumber', 'ReceiptNumber', 'LotNumber', 'IssueNumber', 'CountNumber', 'PlanNumber', 'ProductionOrderNumber', 'BatchNumber', 'PackingOrderNumber', 'DeliveryNumber', 'RouteNo', 'InspectionNumber', 'CleaningBatchNumber', 'TransactionNumber', 'RequirementNo', 'RequestNo', 'ComplaintNo', 'LicensePlate', 'VersionCode', 'MetricCode', 'BotCode', 'ChannelCode', 'Code', 'ItemCode', 'IdempotencyKey', 'JobCode'];
  const today = () => { const d = new Date(); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); };
  const Store = {
    schema: S, cat: CAT, data, idx, colmeta: COLMETA, rel: REL, relIn: REL_IN, dep: DEP, unres: UNRES, AUDIT,
    user: { id: null, group: 'Quản trị hệ thống' },
    ctx: { branch: null, date: mode === 'api' ? today() : '2026-11-14' },
    // ---- Nguồn dữ liệu ----
    get mode() { return mode; },
    setMode(m) { try { localStorage.setItem(MODE_KEY, m); } catch (e) { /* bỏ qua */ } location.reload(); },
    hasHost: !!HOST,
    srcOf(t) { return src[t] || 'demo'; },
    errorOf(t) { return errMsg[t] || ''; },
    controllerOf: ctrlOf,
    pendingCount() { return Object.values(src).filter(s => s === 'pending').length; },
    touched,
    onLoaded(fn) { listeners.push(fn); },
    // ---- Backend đang nối có API của bảng này không ----
    // missing: route không tồn tại (404) → coi như chưa có chức năng, giao diện ẩn menu. broken: gọi được nhưng lỗi (500…).
    isMissing(t) { return !!missing[t]; },
    isBroken(t) { return !!missing[t] || src[t] === 'error'; },
    // Dò nhanh lúc mở app: gọi <Controller>/GetById với Id rỗng (không tải dữ liệu). 404 = backend chưa có controller này.
    probe(tables) {
      if (mode !== 'api' || !HOST) return Promise.resolve();
      const list = [...new Set(tables)].filter(t => S.tables[t] && isApi(t) && !NO_PROBE.has(t) && !probed[t]);
      return Promise.all(list.map(t => {
        probed[t] = true;
        return HOST.get(`${ctrlOf(t)}/GetById?id=${EMPTY_GUID}`).then(() => { }, e => {
          if (e && e.status === 404) { missing[t] = true; setRows(t, []); src[t] = 'error'; errMsg[t] = HOST.errorText(e); }
        });
      }));
    },
    ensure(t) {
      if (!S.tables[t]) return Promise.resolve();
      touched.add(t);
      if (missing[t]) { src[t] = 'error'; return Promise.resolve(); }   // backend chưa có API → không gọi lại
      if (src[t] === 'idle') {
        src[t] = 'pending';
        inflight[t] = fetchTable(t).then(rows => { setRows(t, rows); src[t] = 'api'; delete errMsg[t]; })
          .catch(e => { setRows(t, []); src[t] = 'error'; errMsg[t] = HOST.errorText(e); if (e && e.status === 404) missing[t] = true; console.warn('[DELICARE] Không tải được', t, e); })
          .finally(() => { delete inflight[t]; notify(t); });
      }
      return inflight[t] || Promise.resolve();
    },
    // Tải lại các bảng từ API (sau action nghiệp vụ làm đổi nhiều bảng).
    reload(tables) {
      const list = (tables || []).filter(t => S.tables[t] && isApi(t));
      list.forEach(t => { if (src[t] !== 'pending') src[t] = 'idle'; });
      return Promise.all(list.map(t => Store.ensure(t)));
    },
    // ---- Action nghiệp vụ (api/<Controller>/<Action>) ----
    // opt: { method = 'POST', form | json, tables: bảng tải lại khi thành công, demo: thao tác thay thế ở chế độ DEMO, quiet }
    // Luôn resolve { ok, data, message } – lỗi đã hiện toast (trừ khi quiet). Backend tự kiểm tra quy tắc và ghi các bảng liên quan.
    act(path, opt = {}) {
      if (mode !== 'api' || !HOST) { if (opt.demo) opt.demo(); return Promise.resolve({ ok: true, demo: true }); }
      const body = opt.json !== undefined ? opt.json : (opt.form ? objForm(opt.form) : undefined);
      const bad = msg => { if (!opt.quiet) toast(`<b>${UTIL.esc(path)}</b>: ${UTIL.esc(msg)}`, 'err'); return { ok: false, message: msg }; };
      return HOST.send(opt.method || 'POST', path, body).then(res => {
        if (typeof res === 'string') return bad(res);           // JwtInterceptor nuốt 401 / 403
        const fail = failOf(res); if (fail) return bad(fail);
        return Store.reload(opt.tables).then(() => ({ ok: true, res, data: res && res.data !== undefined ? res.data : res }));
      }).catch(e => bad(HOST.errorText(e)));
    },
    // Đọc qua action riêng (GetDetail, GetBoard, GetHistory…). Resolve { ok, data, list, message }, không toast.
    query(path, params) {
      if (mode !== 'api' || !HOST) return Promise.resolve({ ok: false, demo: true });
      const q = qs(params);
      return HOST.get(path + (q ? '?' + q : '')).then(res => {
        if (typeof res === 'string') return { ok: false, message: res };
        const fail = failOf(res); if (fail) return { ok: false, message: fail };
        return { ok: true, res, data: res && res.data, list: listOf(res) };
      }).catch(e => ({ ok: false, message: HOST.errorText(e) }));
    },
    // Tải tệp: in phiếu (<Controller>/Print?id=…), xuất danh sách (Common/ExportList…), in hàng loạt (Common/PrintBatch + json).
    download(path, params, json) {
      if (mode !== 'api' || !HOST) { toast('Chế độ DEMO không có tệp in / xuất – chuyển sang dữ liệu API.', 'warn'); return Promise.resolve({ ok: false, demo: true }); }
      const q = qs(params);
      return HOST.download(path + (q ? '?' + q : ''), json).then(r => { if (!r.ok) toast(`<b>${UTIL.esc(path)}</b>: ${UTIL.esc(r.message || 'Lỗi API')}`, 'err'); return r; })
        .catch(e => { const m = HOST.errorText(e); toast(`<b>${UTIL.esc(path)}</b>: ${UTIL.esc(m)}`, 'err'); return { ok: false, message: m }; });
    },
    get isLive() { return mode === 'api' && !!HOST; },
    // ---- MA-1 (D-63): quy tắc mã ----
    loadCodeRules,
    codeRule(t, col) { return codeRules[t + '.' + col] || null; },
    userCode,
    // ---- MA-1.8: ghi CHỜ API (form chung) – chỉ báo thành công khi máy chủ xác nhận ----
    // Resolve { ok, message, row }: ok = API trả 200 (message = thông báo máy chủ, có mã mới); lỗi → giữ form, hiện message tại chỗ.
    // Cột mã nhóm G1 không gửi (máy chủ sinh). Chế độ DEMO: ghi bộ nhớ như cũ.
    save(t, action, row) {
      if (!isApi(t) || !HOST) {
        const saved = action === 'Update' ? Store.update(t, row.Id, row) : Store.add(t, row);
        return Promise.resolve({ ok: true, row: saved, demo: true });
      }
      const body = Object.assign({}, row);
      if (action === 'Insert') delete body.Id;
      Object.keys(body).forEach(k => { const r = codeRules[t + '.' + k]; if (r && r.group === 'G1') delete body[k]; });
      const c = ctrlOf(t);
      const run = () => HOST.send(action === 'Insert' ? 'POST' : 'PUT', `${c}/${action}`, toForm(t, body)).then(res => {
        if (typeof res === 'string') return { ok: false, message: res };   // JwtInterceptor nuốt 401 / 403
        const fail = failOf(res); if (fail) return { ok: false, message: fail };
        return Store.reload([t]).then(() => ({ ok: true, message: res && res.responseMessage, row: action === 'Update' ? idx[t][row.Id] : null }));
      }).catch(e => ({ ok: false, message: HOST.errorText(e) }));
      const p = writeQueue.then(run);
      writeQueue = p.then(() => { }, () => { });
      return p;
    },
    idle() { const p = Object.values(inflight); return p.length ? Promise.all(p).then(() => Store.idle()) : Promise.resolve(); },
    // ---- Đọc ----
    rows(t, withDeleted) { Store.ensure(t); return (data[t] || []).filter(r => withDeleted || !r.IsDeleted); },
    get(t, id) { Store.ensure(t); return idx[t] && idx[t][id]; },
    where(t, fn) { return Store.rows(t).filter(fn); },
    by(t, col, val) { return Store.rows(t).filter(r => r[col] === val); },
    one(t, col, val) { return Store.rows(t).find(r => r[col] === val); },
    target(t, col) { const r = REL[t + '.' + col]; return r ? r.dst : null; },
    isDeprecated(t, col) { return !!DEP[t + '.' + col]; },
    noOf(t, row) { if (!row) return ''; for (const f of NO_FIELDS) if (row[f] && COLMETA[t][f]) return row[f]; return ''; },
    nameOf(t, row) { if (!row) return ''; for (const f of NAME_FIELDS) if (row[f] && COLMETA[t][f]) return row[f]; return ''; },
    label(t, rowOrId) {
      const row = typeof rowOrId === 'string' || typeof rowOrId === 'number' ? Store.get(t, rowOrId) : rowOrId;
      if (!row) return rowOrId ? (src[t] === 'pending' || src[t] === 'idle' ? '…' : '(không tìm thấy)') : '';
      const nm = Store.nameOf(t, row), no = Store.noOf(t, row);
      if (t === 'Employee') return (row.FullName || '') + (row.Code ? ' · ' + row.Code : '');
      if (t === 'Person') return row.FullName || String(row.Id).slice(0, 8);
      if (t === 'User') { const p = row.PersonId && Store.get('Person', row.PersonId); return (p && p.FullName ? p.FullName : row.UserName) + ' (' + row.UserName + ')'; }
      if (t === 'Recipe') { const d = Store.get('Dishe', row.DishId); return (d ? d.Name : '') + ' · V' + row.VersionNo; }
      if (t === 'MaterialLot') { const i = Store.get('Ingredient', row.IngredientId); return row.LotNumber + (i ? ' · ' + i.Name : ''); }
      if (t === 'ShiftAssignment') { const e = Store.get('Employee', row.EmployeeId), s = Store.get('Shift', row.ShiftId); return (e ? e.FullName : '') + ' · ' + (s ? s.Code : '') + ' · ' + UTIL.date(row.WorkDate); }
      if (nm && no && nm !== no) return nm.length > 46 ? no : nm;
      return nm || no || ('#' + String(row.Id).slice(0, 8));
    },
    statusCol(t) { const m = COLMETA[t] || {}; for (const c of ['Status', 'RequestStatus', 'ComplaintStatus', 'IssueStatus', 'StopStatus', 'AssignmentStatus', 'DecisionStatus', 'ProcessingStatus', 'MessageStatus', 'ChannelStatus', 'BotStatus', 'IdentityStatus', 'RunStatus', 'LogStatus', 'QcStatus', 'Result', 'InspectionResult', 'ResultCode']) if (m[c]) return c; return null; },
    statusDef(t, col) { return CAT.status[t + '.' + col] || null; },
    log: [],
    // Nhật ký thao tác hiển thị ngay trên giao diện. Ở chế độ API, backend tự ghi AuditChange thật.
    audit(t, id, action, changes) {
      const now = new Date();
      const row = { Id: 'audit-' + Date.now() + '-' + Math.random().toString(16).slice(2, 6), IdChange: id, Action: action, EntityName: t, Changes: JSON.stringify({ summary: changes, by: Store.user.group, demo: mode !== 'api' }), CreatedDate: UTIL.iso(now), CreatedBy: Store.user.id, IsDeleted: 0 };
      data.AuditChange.unshift(row); idx.AuditChange[row.Id] = row;
    },
    // ---- Ghi: cập nhật ngay trên giao diện, gửi API nếu bảng có API, rồi tải lại bảng từ server ----
    add(t, row) {
      row.Id = row.Id || UTIL.uuid();
      if (COLMETA[t].CreatedDate) row.CreatedDate = UTIL.iso(new Date());
      if (COLMETA[t].CreatedBy) row.CreatedBy = Store.user.id;
      if (COLMETA[t].IsDeleted) row.IsDeleted = 0;
      Object.entries(COLMETA[t]).forEach(([c, m]) => { if (row[c] === undefined && m.def) { const v = UTIL.defVal(m.def); if (v !== null) row[c] = v; } });
      data[t].unshift(row); idx[t][row.Id] = row;
      Store.audit(t, row.Id, 'INSERT', 'Tạo mới ' + (Store.label(t, row) || ''));
      if (isApi(t)) write(t, 'Insert', row);
      return row;
    },
    update(t, id, patch, note) {
      const row = idx[t][id]; if (!row) return null;
      const ch = [];
      Object.entries(patch).forEach(([k, v]) => { if (row[k] !== v) { ch.push(k + ': ' + (row[k] ?? '∅') + ' → ' + (v ?? '∅')); row[k] = v; } });
      if (COLMETA[t].ModifiedDate) row.ModifiedDate = UTIL.iso(new Date());
      if (COLMETA[t].ModifiedBy) row.ModifiedBy = Store.user.id;
      if (ch.length) {
        Store.audit(t, id, 'UPDATE', (note ? note + ' · ' : '') + ch.join('; '));
        if (isApi(t)) write(t, 'Update', row);
      }
      return row;
    },
    remove(t, id) {
      const row = idx[t][id]; if (!row) return;
      if (COLMETA[t].IsDeleted) {
        row.IsDeleted = 1; if (COLMETA[t].DeletedDate) row.DeletedDate = UTIL.iso(new Date());
        if (COLMETA[t].DeletedBy) row.DeletedBy = Store.user.id; else if (COLMETA[t].DeleteddBy) row.DeleteddBy = Store.user.id;
        Store.audit(t, id, 'SOFT_DELETE', 'IsDeleted = 1');
      } else { data[t] = data[t].filter(r => r !== row); delete idx[t][id]; Store.audit(t, id, 'DELETE', 'Xóa (bảng không có IsDeleted)'); }
      if (isApi(t)) write(t, 'Delete', row);
    },
    reset() { load(); Store.audit('System', 'reset', 'RESET', mode === 'api' ? 'Tải lại dữ liệu từ API' : 'Khôi phục dữ liệu demo'); notify(null); },
    // Phân quyền theo UserRole × ObjectMenu (MetaLink = '#/screen-id')
    perm(screen) {
      // Màn con không có mục menu riêng → dùng quyền của màn cha (UI-2: crm-customer-360 = quyền crm-customer).
      screen = PERM_ALIAS[screen] || screen;
      if (HOST && mode === 'api') return HOST.perm(screen);
      const g = Store.one('UserGroup', 'Name', Store.user.group);
      const om = Store.rows('ObjectMenu').find(o => o.MetaLink === '#/' + screen);
      if (!g) return { access: true, add: true, edit: true, del: true, exp: true };
      if (!om) return { access: true, add: Store.user.group === 'Quản trị hệ thống', edit: Store.user.group === 'Quản trị hệ thống', del: false, exp: true };
      const ur = Store.rows('UserRole').find(u => u.UserGroupId === g.Id && u.ObjectMenuId === om.Id);
      if (!ur) return { access: false };
      return { access: !!ur.AllowAccess, add: !!ur.AllowAdd, edit: !!ur.AllowEdit, del: !!ur.AllowDelete, exp: !!ur.AllowExport, print: !!ur.AllowPrint, imp: !!ur.AllowImport, ur };
    },
    // UG-1 C-16: quyền Duyệt màn (giống máy chủ PermissionAction.Approve): mục menu wf-inbox?type=<màn> có Xem + Sửa; quản trị luôn có.
    canApprove(screen) {
      if (!(HOST && mode === 'api')) return true;
      if (HOST.user && HOST.user.isAdmin) return true;
      const p = HOST.perm('wf-inbox?type=' + screen);
      return p.access === true && p.edit === true;
    },
    branchOk(row, col) { return !Store.ctx.branch || !row[col] || row[col] === Store.ctx.branch; }
  };
  loadCodeRules();   // MA-1: DC_HOST gán sau khi đăng nhập (delicare-shell) → tải quy tắc mã ngay
  window.Store = Store;
})();
