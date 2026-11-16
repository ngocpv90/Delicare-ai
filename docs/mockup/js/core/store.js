/* DELICARE – Store dữ liệu DEMO trong bộ nhớ (mất khi tải lại trang). Chỉ dùng tên bảng/cột có trong SQL nguồn. */
(function () {
  const S = window.DC_SCHEMA, CAT = window.DC_CATALOG, RAW = window.DC_DEMO;
  const AUDIT = new Set(['CreatedDate', 'CreatedBy', 'ModifiedDate', 'ModifiedBy', 'DeletedDate', 'DeletedBy', 'DeleteddBy', 'IsDeleted']);
  const COLMETA = {};               // table -> {col: meta}
  Object.entries(S.tables).forEach(([t, v]) => {
    COLMETA[t] = {};
    v.cols.forEach(([name, type, nullable, def, usage, note, desc, basis], i) => {
      COLMETA[t][name] = { name, type, nullable: !!nullable, def, usage, note, desc, basis, ord: i + 1, table: t };
    });
  });
  const REL = {}, REL_IN = {};
  S.rels.forEach(r => { REL[r.src + '.' + r.col] = r; (REL_IN[r.dst] = REL_IN[r.dst] || []).push(r); });
  const DEP = {}; S.dep.forEach(d => { DEP[d.table + '.' + d.col] = d.use; });
  const UNRES = {}; S.unres.forEach(u => { UNRES[u.src + '.' + u.col] = u; });
  const data = {}, idx = {};
  function load() {
    Object.keys(S.tables).forEach(t => { data[t] = []; idx[t] = {}; });
    Object.entries(RAW).forEach(([t, pack]) => {
      pack.r.forEach(arr => {
        const row = {};
        pack.c.forEach((c, i) => { if (arr[i] !== null && arr[i] !== undefined) row[c] = arr[i]; });
        if (COLMETA[t].CreatedDate && !row.CreatedDate) row.CreatedDate = '2026-11-01T08:00';
        if (COLMETA[t].IsDeleted && row.IsDeleted === undefined) row.IsDeleted = 0;
        data[t].push(row); idx[t][row.Id] = row;
      });
    });
  }
  load();
  const NAME_FIELDS = ['DisplayName', 'Name', 'FullName', 'MetricName', 'ChannelName', 'BotName', 'ItemName', 'NameVN', 'MealName', 'FileName', 'UserName'];
  const NO_FIELDS = ['OrderNumber', 'ContractNumber', 'RequestNumber', 'PurchaseOrderNumber', 'ReceiptNumber', 'LotNumber', 'IssueNumber', 'CountNumber', 'PlanNumber', 'ProductionOrderNumber', 'BatchNumber', 'PackingOrderNumber', 'DeliveryNumber', 'RouteNo', 'InspectionNumber', 'CleaningBatchNumber', 'TransactionNumber', 'RequirementNo', 'RequestNo', 'ComplaintNo', 'LicensePlate', 'VersionCode', 'MetricCode', 'BotCode', 'ChannelCode', 'Code', 'ItemCode', 'IdempotencyKey', 'JobCode'];
  const Store = {
    schema: S, cat: CAT, data, idx, colmeta: COLMETA, rel: REL, relIn: REL_IN, dep: DEP, unres: UNRES, AUDIT,
    user: { id: null, group: 'Quản trị hệ thống' },
    ctx: { branch: null, date: '2026-11-14' },
    rows(t, withDeleted) { return (data[t] || []).filter(r => withDeleted || !r.IsDeleted); },
    get(t, id) { return idx[t] && idx[t][id]; },
    where(t, fn) { return Store.rows(t).filter(fn); },
    by(t, col, val) { return Store.rows(t).filter(r => r[col] === val); },
    one(t, col, val) { return Store.rows(t).find(r => r[col] === val); },
    target(t, col) { const r = REL[t + '.' + col]; return r ? r.dst : null; },
    isDeprecated(t, col) { return !!DEP[t + '.' + col]; },
    noOf(t, row) { if (!row) return ''; for (const f of NO_FIELDS) if (row[f] && COLMETA[t][f]) return row[f]; return ''; },
    nameOf(t, row) { if (!row) return ''; for (const f of NAME_FIELDS) if (row[f] && COLMETA[t][f]) return row[f]; return ''; },
    label(t, rowOrId) {
      const row = typeof rowOrId === 'string' || typeof rowOrId === 'number' ? Store.get(t, rowOrId) : rowOrId;
      if (!row) return rowOrId ? '(không tìm thấy)' : '';
      const nm = Store.nameOf(t, row), no = Store.noOf(t, row);
      if (t === 'Employee') return row.FullName + (row.Code ? ' · ' + row.Code : '');
      if (t === 'Person') return row.FullName || row.Id.slice(0, 8);
      if (t === 'User') { const p = Store.get('Person', row.PersonId); return (p ? p.FullName : row.UserName) + ' (' + row.UserName + ')'; }
      if (t === 'Recipe') { const d = Store.get('Dishe', row.DishId); return (d ? d.Name : '') + ' · V' + row.VersionNo; }
      if (t === 'MaterialLot') { const i = Store.get('Ingredient', row.IngredientId); return row.LotNumber + (i ? ' · ' + i.Name : ''); }
      if (t === 'ShiftAssignment') { const e = Store.get('Employee', row.EmployeeId), s = Store.get('Shift', row.ShiftId); return (e ? e.FullName : '') + ' · ' + (s ? s.Code : '') + ' · ' + UTIL.date(row.WorkDate); }
      if (nm && no && nm !== no) return nm.length > 46 ? no : nm;
      return nm || no || ('#' + String(row.Id).slice(0, 8));
    },
    statusCol(t) { const m = COLMETA[t] || {}; for (const c of ['Status', 'RequestStatus', 'ComplaintStatus', 'IssueStatus', 'StopStatus', 'AssignmentStatus', 'DecisionStatus', 'ProcessingStatus', 'MessageStatus', 'ChannelStatus', 'BotStatus', 'IdentityStatus', 'RunStatus', 'LogStatus', 'QcStatus', 'Result', 'InspectionResult', 'ResultCode']) if (m[c]) return c; return null; },
    statusDef(t, col) { return CAT.status[t + '.' + col] || null; },
    log: [],
    audit(t, id, action, changes) {
      const now = new Date();
      const row = { Id: 'audit-' + Date.now() + '-' + Math.random().toString(16).slice(2, 6), IdChange: id, Action: action, EntityName: t, Changes: JSON.stringify({ summary: changes, by: Store.user.group, demo: true }), CreatedDate: UTIL.iso(now), CreatedBy: Store.user.id, IsDeleted: 0 };
      data.AuditChange.unshift(row); idx.AuditChange[row.Id] = row;
    },
    add(t, row) {
      row.Id = row.Id || UTIL.uuid();
      if (COLMETA[t].CreatedDate) row.CreatedDate = UTIL.iso(new Date());
      if (COLMETA[t].CreatedBy) row.CreatedBy = Store.user.id;
      if (COLMETA[t].IsDeleted) row.IsDeleted = 0;
      Object.entries(COLMETA[t]).forEach(([c, m]) => { if (row[c] === undefined && m.def) { const v = UTIL.defVal(m.def); if (v !== null) row[c] = v; } });
      data[t].unshift(row); idx[t][row.Id] = row;
      Store.audit(t, row.Id, 'INSERT', 'Tạo mới ' + (Store.label(t, row) || ''));
      return row;
    },
    update(t, id, patch, note) {
      const row = idx[t][id]; if (!row) return null;
      const ch = [];
      Object.entries(patch).forEach(([k, v]) => { if (row[k] !== v) { ch.push(k + ': ' + (row[k] ?? '∅') + ' → ' + (v ?? '∅')); row[k] = v; } });
      if (COLMETA[t].ModifiedDate) row.ModifiedDate = UTIL.iso(new Date());
      if (COLMETA[t].ModifiedBy) row.ModifiedBy = Store.user.id;
      if (ch.length) Store.audit(t, id, 'UPDATE', (note ? note + ' · ' : '') + ch.join('; '));
      return row;
    },
    remove(t, id) {
      const row = idx[t][id]; if (!row) return;
      if (COLMETA[t].IsDeleted) {
        row.IsDeleted = 1; if (COLMETA[t].DeletedDate) row.DeletedDate = UTIL.iso(new Date());
        if (COLMETA[t].DeletedBy) row.DeletedBy = Store.user.id; else if (COLMETA[t].DeleteddBy) row.DeleteddBy = Store.user.id;
        Store.audit(t, id, 'SOFT_DELETE', 'IsDeleted = 1');
      } else { data[t] = data[t].filter(r => r !== row); delete idx[t][id]; Store.audit(t, id, 'DELETE', 'Xóa (bảng không có IsDeleted)'); }
    },
    reset() { load(); Store.audit('System', 'reset', 'RESET', 'Khôi phục dữ liệu demo'); },
    // Phân quyền theo UserRole × ObjectMenu (MetaLink = '#/screen-id')
    perm(screen) {
      const g = Store.one('UserGroup', 'Name', Store.user.group);
      const om = Store.rows('ObjectMenu').find(o => o.MetaLink === '#/' + screen);
      if (!g) return { access: true, add: true, edit: true, del: true, exp: true };
      if (!om) return { access: true, add: Store.user.group === 'Quản trị hệ thống', edit: Store.user.group === 'Quản trị hệ thống', del: false, exp: true };
      const ur = Store.rows('UserRole').find(u => u.UserGroupId === g.Id && u.ObjectMenuId === om.Id);
      if (!ur) return { access: false };
      return { access: !!ur.AllowAccess, add: !!ur.AllowAdd, edit: !!ur.AllowEdit, del: !!ur.AllowDelete, exp: !!ur.AllowExport, print: !!ur.AllowPrint, imp: !!ur.AllowImport, ur };
    },
    branchOk(row, col) { return !Store.ctx.branch || !row[col] || row[col] === Store.ctx.branch; }
  };
  window.Store = Store;
})();
