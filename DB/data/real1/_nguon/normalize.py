# Chuẩn hóa dữ liệu thật Delicare-Share → file nhập đúng mẫu MasterDataImportService (sheet "DuLieu", tiêu đề dòng 1).
import pandas as pd, re, unicodedata, difflib, warnings, os, datetime as dt, json
from collections import defaultdict, Counter
warnings.filterwarnings('ignore')
import sys
R = (sys.argv[1] if len(sys.argv) > 1 else '/mnt/user-data/uploads/Delicare-Share').rstrip('/\\') + '/'
OUT = (sys.argv[2] if len(sys.argv) > 2 else R + 'Chuan-hoa-REAL1').rstrip('/\\') + '/'
os.makedirs(OUT, exist_ok=True)
TAG = '[DEMO] REAL-1'
issues = []          # (loại, đối tượng, chi tiết, xử lý)
assume = []          # (đối tượng, giả định)

def strip_acc(s):
    s = unicodedata.normalize('NFD', str(s)).replace('đ', 'd').replace('Đ', 'D')
    return ''.join(c for c in s if unicodedata.category(c) != 'Mn')
def key(s):
    return re.sub(r'\s+', ' ', strip_acc(s).lower().strip())
def clean(s):
    return re.sub(r'\s+', ' ', str(s).strip()) if pd.notna(s) else None
def pct(x):
    """tỷ lệ 0.05 hoặc % 5 → chuỗi '5%' (TryPercent đọc có dấu %)."""
    if x is None or pd.isna(x): return None
    v = float(x)
    if 0 < v <= 1: v *= 100
    v = round(v, 2)
    return f'{v:g}%'

# ---------------------------------------------------------------- ĐVT
UOM = {  # tên gốc (không dấu, thường) → (Mã, Tên, Ký hiệu, số lẻ)
    'kg': ('KG', 'kg', 'kg', 3), 'cai': ('CAI', 'Cái', 'cái', 0), 'chiec': ('CAI', 'Cái', 'cái', 0), 'qua': ('QUA', 'Quả', 'quả', 0),
    'hop': ('HOP', 'Hộp', 'hộp', 0), 'chai': ('CHAI', 'Chai', 'chai', 0), 'can': ('CAN', 'Can', 'can', 0), 'goi': ('GOI', 'Gói', 'gói', 0),
    'suat': ('SUAT', 'Suất', 'suất', 0), 'tui': ('TUI', 'Túi', 'túi', 0), 'lit': ('LIT', 'Lít', 'lít', 3), 'thung': ('THUNG', 'Thùng', 'thùng', 0),
    'hu': ('HU', 'Hũ', 'hũ', 0), 'bich': ('BICH', 'Bịch', 'bịch', 0), 'lo': ('LO', 'Lọ', 'lọ', 0), 'con': ('CON', 'Con', 'con', 0),
    'binh': ('BINH', 'Bình', 'bình', 0), 'bia': ('BIA', 'Bìa', 'bìa', 0), 'doi': ('DOI', 'Đôi', 'đôi', 0), 'cuon': ('CUON', 'Cuộn', 'cuộn', 0),
    'bo': ('BO', 'Bộ', 'bộ', 0), 'vi': ('VI', 'Vỉ', 'vỉ', 0), 'bao': ('BAO', 'Bao', 'bao', 0), 'g': ('G', 'gam', 'g', 0), 'coc': ('COC', 'Cốc', 'cốc', 0),
}
COUNT_UNITS = {c for c, *_ in UOM.values()} - {'KG', 'LIT', 'G'}
def uom(raw, ctx=''):
    if raw is None or pd.isna(raw) or str(raw).strip() in ('', '0'): return None
    k = key(raw)
    if k not in UOM:
        issues.append(('ĐVT lạ', ctx, str(raw), 'Bỏ qua dòng – cần bổ sung ĐVT'))
        return None
    return UOM[k][0]

# ---------------------------------------------------------------- Nguồn nguyên liệu (3 nguồn)
c = pd.read_excel(R + 'Menu/Rule Menu.xlsx', 'Chi tiết món và hao hụt, giá', header=None)
rule = c.iloc[2:, [11, 12, 13, 14, 15]]; rule.columns = ['Ma', 'Ten', 'DVT', 'Gia', 'hh']
rule = rule.dropna(subset=['Ma']); rule['Ma'] = rule.Ma.astype(str).str.strip()
bg = pd.read_excel(R + 'Purchase/Tool_Tong hop bao gia_1011.xlsm', 'Summary')
bg.columns = ['STT', 'Ma', 'Ten', 'DVT', 'Gia', 'VAT', 'GiaVAT', 'GhiChu', 'NCC', 'MaNCC']
bg = bg.dropna(subset=['Ma']); bg['Ma'] = bg.Ma.astype(str).str.strip()
tk = pd.read_excel(R + 'DB/202608/BCTC T8/Tong_hop_ton_kho NVL.xlsx', header=None, skiprows=4)
tk = tk[tk[1].astype(str).str.match(r'^[A-Z]{2}\d+')]
tk = tk.rename(columns={1: 'Ma', 2: 'Ten', 3: 'DVT', 10: 'SLCuoi', 11: 'GTCuoi'}); tk['Ma'] = tk.Ma.astype(str).str.strip()

ING = {}  # mã → dict
def add_ing(ma, ten, dvt, src, gia=None, hh=None):
    ten = clean(ten)
    if not ten: return
    if ma not in ING:
        ING[ma] = dict(Ma=ma, Ten=ten, DVT=uom(dvt, f'{ma} {ten}'), src=[src], Gia=gia, hh=hh)
    else:
        e = ING[ma]; e['src'].append(src)
        if key(e['Ten']) != key(ten): issues.append(('Cùng mã khác tên', ma, f"{e['Ten']} ({e['src'][0]}) ≠ {ten} ({src})", f"Giữ tên {e['src'][0]}"))
        u = uom(dvt, f'{ma} {ten}')
        if e['DVT'] is None: e['DVT'] = u
        elif u and u != e['DVT']: issues.append(('Cùng mã khác ĐVT', ma, f"{e['DVT']} ≠ {u} ({src})", f"Giữ {e['DVT']}"))
        if e['Gia'] is None and gia is not None: e['Gia'] = gia
        if e['hh'] is None and hh is not None: e['hh'] = hh
for _, r in rule.iterrows(): add_ing(r.Ma, r.Ten, r.DVT, 'Rule Menu', None if pd.isna(r.Gia) else float(r.Gia), None if pd.isna(r.hh) else float(r.hh))
for _, r in bg.drop_duplicates('Ma').iterrows(): add_ing(r.Ma, r.Ten, r.DVT, 'Báo giá')
for _, r in tk.iterrows(): add_ing(r.Ma, r.Ten, r.DVT, 'Tồn kho kế toán T8')
# trùng tên khác mã
byname = defaultdict(list)
for m, e in ING.items(): byname[key(e['Ten'])].append(m)
for n, ms in byname.items():
    if len(ms) > 1: issues.append(('Trùng tên khác mã', ', '.join(ms), ING[ms[0]]['Ten'], 'Giữ cả hai mã (kế toán dùng mã riêng) – Mua hàng gộp sau'))

# Nhóm + bảo quản theo tiền tố mã (giả định cho demo – Bếp trưởng / QA xác nhận)
CAT = {'AD': 'Bún, phở, mì & đồ ăn sẵn', 'AS': 'Đồ ăn sáng & sữa', 'GA': 'Gạo', 'GC': 'Giò chả', 'GV': 'Gia vị & đồ khô', 'HS': 'Thủy hải sản',
       'NV': 'Vật tư tiêu hao & nhiên liệu', 'RC': 'Rau củ quả', 'TG': 'Thịt gia cầm', 'TH': 'Thịt lợn', 'TM': 'Tráng miệng & đồ uống',
       'TR': 'Trứng', 'TT': 'Thịt trâu bò', 'CC': 'Dụng cụ dùng một lần', 'NL': 'Nguyên liệu khác'}
def storage(ma, ten):
    p = ma[:2]; k = key(ten)
    frozen = 'dong lanh' in k or re.search(r'\bcn\b', k) is not None
    if p in ('GV', 'GA', 'NV', 'CC'): return ('DRY', p not in ('NV', 'CC'), 180)
    if p == 'AD': return ('CHILLED', True, 1) if 'tuoi' in k else ('DRY', True, 180)
    if p == 'AS':
        if 'sua' in k: return ('DRY', True, 90)
        if any(w in k for w in ('xuc xich', 'ruoc')): return ('CHILLED', True, 7)
        return ('CHILLED', True, 1)
    if p == 'GC': return ('CHILLED', True, 5)
    if p in ('HS', 'TG', 'TH', 'TT'): return ('FROZEN', True, 90) if (frozen or p == 'HS' and 'tuoi' not in k) else ('CHILLED', True, 2)
    if p == 'RC': return ('CHILLED', True, 3)
    if p == 'TM':
        if 'sua chua' in k: return ('CHILLED', True, 14)
        if any(w in k for w in ('coca', 'thach', 'nuoc')): return ('DRY', True, 180)
        return ('CHILLED', True, 5)
    if p == 'TR': return ('DRY', True, 14)
    return ('DRY', True, 30)
assume.append(('Nguyên liệu – nhóm', 'Theo 2 chữ đầu của mã: ' + '; '.join(f'{k} = {v}' for k, v in CAT.items())))
assume.append(('Nguyên liệu – kiểu bảo quản / hạn dùng', 'Rau củ Mát 3 ngày; thịt tươi Mát 2 ngày, thịt "đông lạnh"/"CN" Đông lạnh 90 ngày; hải sản Đông lạnh 90 ngày (trừ "tươi"); giò chả Mát 5 ngày; bánh/xôi sáng Mát 1 ngày; sữa Khô 90 ngày; gia vị, gạo Khô 180 ngày; trứng Khô 14 ngày; vật tư không quản lý hạn. Mát / Đông lạnh tự bật kiểm tra khi nhận hàng.'))

# tra tên → mã (dùng cho thực đơn)
name_idx = {}
for m, e in sorted(ING.items(), key=lambda x: (0 if 'Rule Menu' in x[1]['src'] else 1, x[0])):
    name_idx.setdefault(key(e['Ten']), m)
new_seq = [0]
def ing_code(name, dvt, ctx):
    k = key(name)
    if k in name_idx: return name_idx[k], 'khớp tên'
    cand = [x for x in difflib.get_close_matches(k, list(name_idx), n=3, cutoff=0.92) if x.split()[0] == k.split()[0]][:1]
    if cand:
        issues.append(('Tên NL gần giống', ctx, f'"{name}" → "{ING[name_idx[cand[0]]]["Ten"]}"', 'Khớp tự động (≥ 92 %)'))
        name_idx[k] = name_idx[cand[0]]
        return name_idx[k], 'gần giống'
    new_seq[0] += 1
    m = f'NLT-{new_seq[0]:03d}'
    ING[m] = dict(Ma=m, Ten=clean(name), DVT=uom(dvt, ctx), src=['Thực đơn tuần (mới)'], Gia=None, hh=None)
    name_idx[k] = m
    issues.append(('NL chưa có trong danh mục', ctx, clean(name), f'Tạo mới mã {m}, nhóm "Nguyên liệu khác" – Mua hàng bổ sung giá / NCC'))
    return m, 'mới'

# ---------------------------------------------------------------- Món + nhóm món
GROUP = {'0': 'Món ăn sáng', '1': 'Món chính', '2': 'Món phụ', '3': 'Món rau', '4': 'Món canh', '5': 'Món cơm', '6': 'Món tráng miệng', '7': 'Món ăn đêm'}
BANKCOL = {'Món ăn sáng': 'Món ăn sáng', 'Món chính': 'Món chính', 'Món phụ': 'Món phụ', 'Cơm': 'Món cơm', 'Rau': 'Món rau', 'Canh': 'Món canh',
           'Tráng miệng': 'Món tráng miệng', 'Món ăn đêm': 'Món ăn đêm'}
def grp(label):
    m = re.match(r'\s*(\d)\.', str(label))
    return GROUP.get(m.group(1)) if m else None
DISH = {}  # key → dict(Ten, Nhom, src)
def add_dish(name, nhom, src):
    name = clean(name)
    if not name or name in ('0', 'nan'): return None
    k = key(name)
    if k not in DISH: DISH[k] = dict(Ten=name, Nhom=nhom, src=src)
    return k
bank = pd.read_excel(R + 'Menu/Rule Menu.xlsx', 'Ngân hàng món')
for col in bank.columns:
    for v in bank[col].dropna(): add_dish(v, BANKCOL.get(col.strip(), col.strip()), 'Ngân hàng món')
det = c.iloc[1:, 0:8]; det.columns = ['Group', 'Nhom', 'NLchinh', 'Mon', 'TP', 'PP', 'h1', 'h2']
for _, r in det.dropna(subset=['Mon']).iterrows(): add_dish(r.Mon, grp(r.Group) or 'Món chính', 'Chi tiết món')
# phương pháp chế biến theo món (để ghi mô tả)
method = det.dropna(subset=['Mon']).groupby(det.Mon.map(lambda x: key(x) if pd.notna(x) else None)).PP.agg(lambda s: ', '.join(sorted(set(s.dropna().astype(str)))))

# ---------------------------------------------------------------- Thực đơn thật (W1, W3) + định mức
w3 = pd.read_excel(R + 'Menu/New_Menu_Delicare_202611_W3.xlsx', 'Data_menu_RAW')
w1 = pd.read_excel(R + 'Menu/Phieu_Truoc sx.xlsx', 'Raw')
raw = pd.concat([w3.assign(src='W3'), w1.assign(src='W1')])
raw = raw[raw.Mon.notna() & (raw.Mon.astype(str).str.strip() != '0')]
MEAL = {'SÁNG': 'SANG', 'TRƯA': 'TRUA', 'TỐI': 'TOI', 'ĐÊM': 'DEM'}
DAY = {'Thứ 2': 1, 'Thứ 3': 2, 'Thứ 4': 3, 'Thứ 5': 4, 'Thứ 6': 5, 'Thứ 7': 6, 'CN': 7}
menu_dish_map = {}
for name in raw.Mon.astype(str).str.strip().unique():
    k = key(name)
    if k in DISH: menu_dish_map[name] = k; continue
    cand = [x for x in difflib.get_close_matches(k, list(DISH), n=3, cutoff=0.9) if x.split()[0] == k.split()[0]][:1]
    if cand:
        issues.append(('Tên món gần giống', name, f'→ "{DISH[cand[0]]["Ten"]}"', 'Khớp tự động (≥ 90 %)'))
        menu_dish_map[name] = cand[0]
    else:
        g = raw[raw.Mon.astype(str).str.strip() == name].Group.dropna()
        add_dish(name, grp(g.iloc[0]) if len(g) else 'Món chính', 'Thực đơn tuần')
        issues.append(('Món chưa có trong ngân hàng món', name, '', 'Tạo món mới'))
        menu_dish_map[name] = key(name)
# mã món ổn định: theo nhóm rồi tên
GORDER = list(GROUP.values())
for i, (k, d) in enumerate(sorted(DISH.items(), key=lambda x: (GORDER.index(x[1]['Nhom']) if x[1]['Nhom'] in GORDER else 9, x[0])), 1):
    d['Ma'] = f'MON-{i:04d}'

# định mức: ưu tiên W3 (mới hơn) → W1
lines = raw[raw.Nguyen_lieu.notna() & ~raw.Nguyen_lieu.astype(str).str.strip().isin(['0', 'Gia vị', 'Nhiên liệu']) & (pd.to_numeric(raw.DL_truoc_so_che, errors='coerce') > 0)].copy()
lines['dk'] = lines.Mon.astype(str).str.strip().map(menu_dish_map)
# Kiểm theo thành phần chuẩn của món (Rule Menu › Chi tiết món): file thực đơn có dòng chép nhầm (vd "Đậu tẩm hành" mang dòng Trứng gà).
_dd = det.dropna(subset=['Mon']).copy(); _dd['k'] = _dd.Mon.map(key)
COMP = {k: {key(v) for v in pd.concat([g.NLchinh, g.TP]).dropna() if str(v).strip() not in ('', '0')} for k, g in _dd.groupby('k')}
def comp_ok(dk, nl):
    cs = COMP.get(dk)
    if not cs: return None
    k = key(nl)
    return any(c in k or k in c or difflib.SequenceMatcher(None, c, k).ratio() >= 0.6 or (set(c.split()) & set(k.split())) - {'thit', 'ca', 'rau', 'cu', 'la', 'qua'} for c in cs)
lines['occ'] = lines.src + '|' + lines.Thu.astype(str) + '|' + lines.Buoi.astype(str)
keep = []
for dk, g in lines.groupby('dk'):
    occs = []
    for occ, gg in g.groupby('occ'):
        oks = [comp_ok(dk, n) for n in gg.Nguyen_lieu.astype(str)]
        score = sum(1 for o in oks if o) / len(oks) if any(o is not None for o in oks) else 1.0
        occs.append((score, occ.startswith('W3'), -len(gg), occ))
    best = max(occs)[3]
    gg = g[g.occ == best]
    for idx, r in gg.iterrows():
        ok = comp_ok(dk, str(r.Nguyen_lieu))
        if ok is False and max(occs)[0] > 0:
            issues.append(('Dòng định mức nghi chép nhầm', DISH[dk]['Ten'], f'{r.Nguyen_lieu} không có trong thành phần chuẩn của món', 'Bỏ dòng'))
            continue
        keep.append(idx)
    if len(occs) > 1:
        issues.append(('Món xuất hiện nhiều ngày', DISH[dk]['Ten'], f'{len(occs)} lần; lấy {best.replace("|", " ")}', 'Lấy lần khớp thành phần chuẩn nhất (ưu tiên tuần 3)'))
lines = lines.loc[keep]
rec = {}
conflict = 0
for (dk, nl), g in lines.groupby(['dk', lines.Nguyen_lieu.astype(str).str.strip()]):
    g = g.sort_values('src', ascending=False)  # W3 trước
    r = g.iloc[0]
    if g.DL_truoc_so_che.round(3).nunique() > 1:
        conflict += 1
        issues.append(('Định lượng khác nhau giữa 2 tuần', f"{DISH[dk]['Ten']} / {nl}", ' ; '.join(f"{s}: {v:.3f}" for s, v in zip(g.src, g.DL_truoc_so_che)), 'Lấy tuần 3 (mới hơn)'))
    code, how = ing_code(nl, r.DVT, f"{DISH[dk]['Ten']}")
    div = pd.to_numeric(r.Dinh_luong_tren_kg, errors='coerce')
    line_unit = uom(r.DVT, f"{DISH[dk]['Ten']} / {nl}")
    ing_unit = ING[code]['DVT'] or line_unit
    if ING[code]['DVT'] is None:
        ING[code]['DVT'] = line_unit or ('HOP' if 'ml' in key(nl) else 'CAI')
        if not line_unit: issues.append(('NL không có ĐVT', code, nl, f"Đặt {ING[code]['DVT']} (giả định)"))
    ing_unit = ING[code]['DVT']
    q = float(r.DL_truoc_so_che)
    # REAL-1 (D-129, Claude 2026-11-26): hệ thống coi "Định lượng" của định mức là lượng TINH và tự tính lượng mua = tinh ÷ (1−%CB) ÷ (1−%SC)
    # (BUSINESS_RULES BIZ-1.1) → đổi "DL trước sơ chế" (đã là lượng mua) về lượng tinh để giá vốn / MRP không tính hao hụt 2 lần.
    _h = lambda x: float(pct(x)[:-1]) / 100 if pct(x) else 0.0
    q = q * (1 - _h(r.Ty_le_hao_hut_so_che)) * (1 - _h(r.Ty_le_hao_hut_che_bien))
    if line_unit == 'KG' or (pd.notna(div) and div == 1000): qty_base, unit = q / 1000 * 100, 'KG'   # g/suất → kg/100 suất
    else: qty_base, unit = q * 100, line_unit or ing_unit
    if unit != ing_unit:
        if unit in COUNT_UNITS and ing_unit in COUNT_UNITS:
            issues.append(('ĐVT định mức ≠ ĐVT nguyên liệu', f"{DISH[dk]['Ten']} / {nl}", f'{unit} vs {ing_unit}', f'Dùng {ing_unit} (cùng là đơn vị đếm)')); unit = ing_unit
        else:
            issues.append(('ĐVT định mức ≠ ĐVT nguyên liệu', f"{DISH[dk]['Ten']} / {nl}", f'{unit} vs {ing_unit}', 'BỎ dòng định mức – cần quy đổi')); continue
    rec[(dk, code)] = dict(MaMon=DISH[dk]['Ma'], Mon=DISH[dk]['Ten'], MaNL=code, NL=ING[code]['Ten'], qty=round(qty_base, 4), unit=unit,
                           hh1=pct(r.Ty_le_hao_hut_so_che), hh2=pct(r.Ty_le_hao_hut_che_bien), gsau=float(r.DL_sau_che_bien) if unit == 'KG' and pd.notna(pd.to_numeric(r.DL_sau_che_bien, errors='coerce')) else 0)
assume.append(('Định mức công thức', 'Định lượng lấy từ thực đơn thật tuần 1 + tuần 3 (DL trước sơ chế/suất), quy về 100 suất chuẩn: kg = g × 100 / 1000; đơn vị đếm = số × 100. Gia vị / nhiên liệu chung không đưa vào định mức món. Công thức nạp ở trạng thái Nháp.'))

# ---------------------------------------------------------------- NCC
nccl = pd.read_excel(R + 'Purchase/Tool_Tong hop bao gia_1011.xlsm', 'DS NCC')
SUP = {}
def scode(en): return 'NCC-' + re.sub(r'[^A-Z0-9]', '', strip_acc(en).upper())[:16]
for _, r in nccl.iterrows():
    en = clean(r['Name_EN']) or clean(r['Name_VN'])
    if not en: continue
    SUP[key(en)] = dict(Ma=scode(en), Ten=clean(r['Nhà cung cấp']), Ngan=clean(r['Name_VN']), DiaChi=clean(r['Địa chỉ']), DT=clean(r['Phone_number']))
for n in bg.NCC.dropna().unique():
    if key(n) not in SUP:
        SUP[key(n)] = dict(Ma=scode(n), Ten=clean(n), Ngan=clean(n), DiaChi=None, DT=None)
        issues.append(('NCC chỉ có trong báo giá', clean(n), 'Không có tên đầy đủ / địa chỉ trong DS NCC', 'Tạo NCC theo tên ngắn'))
assume.append(('Nhà cung cấp', 'Mã NCC-<tên ngắn không dấu>. File không có mã số thuế, SĐT → để trống.'))

# ---------------------------------------------------------------- Bảng giá NCC
sm = []
for (ma, n), g in bg.dropna(subset=['NCC']).groupby(['Ma', 'NCC']):
    p = pd.to_numeric(g.Gia, errors='coerce').dropna()
    p = p[p > 0]
    if ma not in ING or p.empty: continue
    sm.append(dict(MaNCC=SUP[key(n)]['Ma'], MaNL=ma, NL=ING[ma]['Ten'], Gia=float(p.iloc[0]), DVT=ING[ma]['DVT'], VAT=float(pd.to_numeric(g.VAT, errors='coerce').iloc[0] or 0)))
smd = pd.DataFrame(sm)
smd['UuTien'] = smd.groupby('MaNL').Gia.transform('min').eq(smd.Gia)
smd['UuTien'] = smd.UuTien & ~smd.duplicated(['MaNL', 'UuTien'])  # 1 NCC ưu tiên / NL
assume.append(('Bảng giá NCC', 'Giá chưa VAT từ Tool_Tong hop bao gia (10/11/2026), hiệu lực từ 2026-11-10; NCC rẻ nhất = Ưu tiên. % VAT chưa có cột trên hệ thống → ghi ở sheet đối chiếu (GAP-REAL-01).'))
for ma, e in ING.items():
    if e['Gia'] is None and ma not in set(smd.MaNL):
        pass
no_price = [m for m in ING if m not in set(smd.MaNL)]
issues.append(('NL chưa có giá NCC', f'{len(no_price)} mã', ', '.join(no_price[:40]) + (' …' if len(no_price) > 40 else ''), 'Mua hàng bổ sung báo giá (MRP vẫn chạy, đề nghị mua thiếu NCC gợi ý)'))

# ---------------------------------------------------------------- Khách hàng, điểm giao, hợp đồng
d = pd.read_excel(R + 'DB/Report_202608_sent.xlsx', 'DTKH'); d = d[d.MONTH >= 202601]
CUST_NAME = {  # mã kế toán → (tên pháp lý, tên hiển thị, ngành)
    'KH00001': ('Vincons – Vinhomes Ocean Park 1', 'Vin Ocean Park 1', 'CONSTRUCTION'),
    'KH00002': ('Vincons – Vinhomes Cổ Loa', 'Vin Cổ Loa', 'CONSTRUCTION'),
    'KH00015': ('CÔNG TY CỔ PHẦN ĐẦU TƯ VÀ XÂY DỰNG SGC', 'SGC Cổ Loa', 'CONSTRUCTION'),
    'KH00016': ('CÔNG TY CỔ PHẦN ĐẦU TƯ VÀ THI CÔNG HẠ TẦNG VINALPHA', 'Vinalpha Cổ Loa', 'CONSTRUCTION'),
    'KH00023': ('CÔNG TY CỔ PHẦN ĐẦU TƯ VÀ THI CÔNG HẠ TẦNG VINALPHA – Bãi đúc 1A', 'Vinalpha Bãi đúc 1A', 'CONSTRUCTION'),
    'KH00024': ('CÔNG TY VINALPHA – Xưởng sản xuất', 'Vinalpha SX', 'CONSTRUCTION'),
    'KH00025': ('CÔNG TY SGC – Km17', 'SGC Km17', 'CONSTRUCTION'),
    'KH00026': ('CÔNG TY SGC – Cơ điện', 'SGC Cơ điện', 'CONSTRUCTION'),
}
POINTS = {  # mã kế toán → [(mã điểm, tên điểm, địa chỉ)]
    'KH00001': [('DG-VINC-GL-KTX', 'Vincons Gia Lâm – KTX', 'Vinhomes Ocean Park, Gia Lâm, Hà Nội')],
    'KH00002': [('DG-VINC-CL-A5', 'Vincons Cổ Loa – A5', 'Vinhomes Cổ Loa, Đông Anh, Hà Nội'), ('DG-VINC-CL-A7', 'Vincons Cổ Loa – A7', 'Vinhomes Cổ Loa, Đông Anh, Hà Nội')],
    'KH00015': [(f'DG-SGC-CL-{p}', f'SGC Cổ Loa – {n}', 'Cổ Loa, Đông Anh, Hà Nội') for p, n in [('KTX', 'KTX'), ('CT', 'Công trường'), ('KM12', 'KM12'), ('KM13', 'KM13'), ('KM14', 'KM14'), ('KM15', 'KM15')]],
    'KH00025': [('DG-SGC-TT-KM17', 'SGC Thuận Thành – KM17', 'Thuận Thành, Bắc Ninh'), ('DG-SGC-TT-KM23', 'SGC Thuận Thành – KM23', 'Thuận Thành, Bắc Ninh'), ('DG-SGC-TT-BD1A', 'SGC Thuận Thành – Bãi Đúc 1A', 'Thuận Thành, Bắc Ninh')],
    'KH00026': [('DG-SGC-GB-KM36', 'SGC Gia Bình – KM36', 'Gia Bình, Bắc Ninh'), ('DG-SGC-GB-BD2', 'SGC Gia Bình – Bãi Đúc 2', 'Gia Bình, Bắc Ninh')],
    'KH00016': [('DG-VINA-CL-KTX', 'Vinalpha Cổ Loa – KTX', 'Cổ Loa, Đông Anh, Hà Nội')],
    'KH00023': [('DG-VINA-TQ-BD1A', 'Vinalpha Trí Quả – Bãi Đúc 1A', 'Trí Quả, Thuận Thành, Bắc Ninh')],
    'KH00024': [('DG-VINA-SX', 'Vinalpha – Xưởng sản xuất', 'Bắc Ninh')],
    'KH00008': [('DG-PHENIKAA', 'Phenikaa – điểm giao chính', 'Hà Nội')],
    'KH00020': [('DG-NEOIMPLANT', 'Neoimplant Vina – điểm giao chính', 'Hà Nội')],
    'KH00011': [('DG-VIN-NGOAI', 'Vin ngoài – điểm giao chính', 'Hà Nội')],
}
assume.append(('Khách hàng', 'Mỗi mã khách kế toán (KH000xx) = 1 khách để bảng kê khớp sổ bán hàng. 11 khách có doanh thu tháng 8 = Đang hoạt động; 9 khách cũ = Ngừng hoạt động. Mã số thuế / SĐT không có trong file.'))
assume.append(('Điểm giao', '16 điểm từ Tool_Tong hop so suat gắn vào khách kế toán theo tên công trường: Gia Lâm → Ocean Park 1; Cổ Loa A5/A7 → Vin Cổ Loa; SGC Cổ Loa 6 điểm → KH00015; Thuận Thành 3 điểm → SGC Km17; Gia Bình 2 điểm → SGC Cơ điện (GIẢ ĐỊNH – KD xác nhận); Vinalpha SX, Phenikaa, Neoimplant, Vin ngoài mỗi khách 1 điểm. Khung nhận 10:30–11:30.'))
cust = []
last = d.groupby('Mã khách hàng')['Ngày hạch toán'].max()
first = d.groupby('Mã khách hàng')['Ngày hạch toán'].min()
names = d.groupby('Mã khách hàng')['Tên khách hàng'].first()
for code in sorted(names.index):
    legal, disp, ind = CUST_NAME.get(code, (clean(names[code]), clean(names[code]), None))
    active = pd.to_datetime(last[code]) >= pd.Timestamp('2026-08-01')
    cust.append(dict(Ma=code, Ten=legal, Hien=disp, Loai='BUSINESS' if code != 'KH00012' else 'INDIVIDUAL', Active=active,
                     DiaChi=(POINTS.get(code) or [(None, None, None)])[0][2]))
# đơn giá theo buổi (tháng 8, giá phổ biến nhất)
PROD = {'TP0001': 'SANG', 'TP0002': 'TRUA', 'TP0003': 'TOI', 'TP0007': 'DEM', 'TP0004': 'TRUA', 'TP0005': 'TRUA', 'TP0006': 'TRUA'}
a = d[d.MONTH == 202608]
contracts = []
for code, g in a.groupby('Mã khách hàng'):
    meals = {}
    for p, gg in g.groupby('Mã hàng'):
        mt = PROD.get(p)
        if not mt: continue
        price = Counter(gg['Đơn giá'].round(0)).most_common(1)[0][0]
        meals.setdefault(mt, price)
    days = 31
    qty = {PROD[p]: g[g['Mã hàng'] == p]['Tổng số lượng bán'].sum() / days for p in g['Mã hàng'].unique() if p in PROD}
    contracts.append(dict(MaKH=code, KH=CUST_NAME.get(code, (names[code],))[0], Tu=str(pd.to_datetime(first[code]).date()), Den='2027-06-30', meals=meals, qty=qty))
assume.append(('Hợp đồng', 'Mỗi khách đang hoạt động 1 hợp đồng: hiệu lực từ ngày bán đầu tiên tới 30/06/2027 (giả định), đơn giá chưa VAT theo buổi = giá phổ biến tháng 8 (Sáng 20.000, Trưa / Tối 40.000, Đêm 30.000; Phenikaa / Neoimplant cơm suất 80.000).'))

# số suất mẫu / ngày / điểm (trung bình tháng 8 chia đều điểm)
sosuat = []
for ct in contracts:
    pts = POINTS.get(ct['MaKH'], [])
    for pc, pn, _ in pts:
        row = dict(MaKH=ct['MaKH'], MaDiem=pc, Diem=pn)
        for mt in ('SANG', 'TRUA', 'TOI', 'DEM'):
            row[mt] = int(round(ct['qty'].get(mt, 0) / len(pts)))
        sosuat.append(row)
assume.append(('Số suất mẫu', 'Trung bình / ngày tháng 8/2026 theo khách × buổi, chia đều cho các điểm giao của khách. Sáng chia 3 lựa chọn 45 % / 33 % / 22 % (theo tỷ lệ 2.000 / 1.500 / 1.000 của thực đơn thật).'))

# ---------------------------------------------------------------- Thực đơn tuần (mẫu sheet "1. Menu")
SLOTCAT = {'Món chính 1': 'Món chính', 'Món chính 2': 'Món chính', 'Món phụ': 'Món phụ', 'Món rau': 'Món rau', 'Món canh': 'Món canh', 'Cơm': 'Món cơm', 'Tráng miệng': 'Món tráng miệng'}
DAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']
def menu_rows(m):
    m = m.copy()
    m['dk'] = m.Mon.astype(str).str.strip().map(menu_dish_map)
    m['meal'] = m.Buoi.map(MEAL); m['day'] = m.Thu.map(DAY)
    m['opt'] = m.Phan_loai.astype(str).str.extract(r'Lựa chọn (\d)')[0]
    m = m[m.dk.notna() & m.meal.notna()]
    rows = []
    fixed = m[m.opt.isna() & m.Phan_loai.isin(list(SLOTCAT))]
    for meal, g in fixed.groupby('meal', sort=False):
        for slot in SLOTCAT:
            sub = g[g.Phan_loai == slot]
            if sub.empty: continue
            per_day = {dd: list(dict.fromkeys(gg.dk)) for dd, gg in sub.groupby('day')}
            for i in range(max(len(v) for v in per_day.values())):
                row = {'Buổi': meal, 'Lựa chọn': 1, 'Nhóm món': SLOTCAT[slot]}
                for dd, lab in enumerate(DAYS, 1):
                    v = per_day.get(dd, []); row[lab] = DISH[v[i]]['Ten'] if i < len(v) else ''
                rows.append(row)
    opt = m[m.opt.notna()]
    for (meal, o), g in opt.groupby(['meal', 'opt'], sort=False):
        per_day = {dd: list(dict.fromkeys(gg.dk)) for dd, gg in g.groupby('day')}
        for i in range(max(len(v) for v in per_day.values())):
            first = next((v[i] for dd, v in sorted(per_day.items()) if i < len(v)), None)
            row = {'Buổi': meal, 'Lựa chọn': int(o), 'Nhóm món': DISH[first]['Nhom'] if first else ''}
            for dd, lab in enumerate(DAYS, 1):
                v = per_day.get(dd, []); row[lab] = DISH[v[i]]['Ten'] if i < len(v) else ''
            rows.append(row)
    return rows
def menu_sheet(src, fill_from=None):
    rows = menu_rows(raw[raw.src == src])
    if fill_from:
        have = {(r['Buổi'], r['Lựa chọn']) for r in rows}
        extra = [r for r in menu_rows(raw[raw.src == fill_from]) if (r['Buổi'], r['Lựa chọn']) not in have]
        if extra:
            assume.append(('Thực đơn tuần 30/11 (nguồn tuần 1)', 'Tuần 1 thật không có ' + ', '.join(sorted({f"{r['Buổi']} lựa chọn {r['Lựa chọn']}" for r in extra})) + f' → lấy từ tuần 3 để đủ buổi cho khách có suất đêm / 3 lựa chọn sáng.'))
        rows += extra
    order = {'SANG': 0, 'TRUA': 1, 'TOI': 2, 'DEM': 3}
    return pd.DataFrame(sorted(rows, key=lambda r: (order[r['Buổi']], r['Lựa chọn'])), columns=['Buổi', 'Lựa chọn', 'Nhóm món'] + DAYS)

# ---------------------------------------------------------------- Tồn đầu kỳ
ton = tk[pd.to_numeric(tk.SLCuoi, errors='coerce') > 0].copy()
ton['DonGia'] = (pd.to_numeric(ton.GTCuoi) / pd.to_numeric(ton.SLCuoi)).round(0)
assume.append(('Tồn đầu kỳ', 'Số cuối kỳ 31/08/2026 của kế toán (32 mặt hàng, 468 triệu) dùng làm tồn đầu kỳ GIẢ ĐỊNH cho demo; lô nhận ngày nạp, hạn = ngày nạp + số ngày hạn dùng. Không phải số kiểm kê thật.'))

# ================================================================ GHI FILE
def write(name, df, sheet='DuLieu', note=None):
    p = OUT + name
    with pd.ExcelWriter(p, engine='openpyxl') as w:
        df.to_excel(w, sheet_name=sheet, index=False)
        ws = w.sheets[sheet]
        for col in ws.columns:
            ws.column_dimensions[col[0].column_letter].width = min(48, max(10, max(len(str(c.value or '')) for c in col[:200]) + 2))
        ws.freeze_panes = 'A2'
    return len(df)

counts = {}
uoms = sorted({v for v in UOM.values()}, key=lambda x: x[0])
used_u = {e['DVT'] for e in ING.values()} | {r['unit'] for r in rec.values()}
counts['01_DonViTinh'] = write('01_DonViTinh.xlsx', pd.DataFrame([{'Mã ĐVT': u[0], 'Tên ĐVT': u[1], 'Ký hiệu': u[2], 'Số chữ số thập phân': u[3]} for u in uoms if u[0] in used_u]))
counts['02_NhaCungCap'] = write('02_NhaCungCap.xlsx', pd.DataFrame([{'Mã NCC': s['Ma'], 'Tên NCC': s['Ten'], 'Loại NCC': '', 'Mã số thuế': '', 'Điện thoại': s['DT'] or '', 'Email': '', 'Địa chỉ': s['DiaChi'] or '', 'Người liên hệ': '', 'SĐT liên hệ': ''} for s in sorted(SUP.values(), key=lambda x: x['Ma'])]))
ing_rows = []
for ma, e in sorted(ING.items()):
    st, exp, days = storage(ma, e['Ten'])
    hh = e['hh'] if e['hh'] is not None and 0 <= e['hh'] < 1 else 0
    if e['DVT'] is None:
        e['DVT'] = 'HOP' if 'ml' in key(e['Ten']) else 'KG'
        issues.append(('NL không có ĐVT', ma, e['Ten'], f"Đặt {e['DVT']} (giả định)"))
    ing_rows.append({'Mã nguyên liệu': ma, 'Tên nguyên liệu': e['Ten'], 'Nhóm nguyên liệu': CAT.get(ma[:2], 'Nguyên liệu khác') if not ma.startswith('NLT') else 'Nguyên liệu khác',
                     'ĐVT': e['DVT'], 'Kiểu bảo quản': st, 'Tỷ lệ thu hồi (%)': f'{100 - hh * 100:g}%', 'Hao hụt chuẩn (%)': f'{hh * 100:g}%',
                     'Quản lý hạn dùng': 'Có' if exp else 'Không', 'Số ngày hạn dùng': days if exp else '', 'Tồn tối thiểu': ''})
counts['03_NguyenLieu'] = write('03_NguyenLieu.xlsx', pd.DataFrame(ing_rows))
portion = defaultdict(float)
for r in rec.values(): portion[r['MaMon']] += r['gsau']
dish_rows = [{'Mã món': dd['Ma'], 'Tên món': dd['Ten'], 'Nhóm món': dd['Nhom'], 'Định lượng chuẩn (g/suất)': round(portion[dd['Ma']]) if portion.get(dd['Ma']) else '',
              'Mô tả': ('Chế biến: ' + method.get(k, '')) if method.get(k, '') else ''} for k, dd in sorted(DISH.items(), key=lambda x: x[1]['Ma'])]
counts['04_MonAn'] = write('04_MonAn.xlsx', pd.DataFrame(dish_rows))
counts['05_DinhMucCongThuc'] = write('05_DinhMucCongThuc.xlsx', pd.DataFrame([{'Mã món': r['MaMon'], 'Mã nguyên liệu': r['MaNL'], 'Định lượng': r['qty'], 'ĐVT': r['unit'],
    '% hao hụt sơ chế': r['hh1'] or '0%', '% hao hụt chế biến': r['hh2'] or '0%', 'Ghi chú sơ chế': f"{r['Mon']} – {r['NL']} (định lượng cho 100 suất)"} for r in sorted(rec.values(), key=lambda x: (x['MaMon'], x['MaNL']))]))
counts['06_BangGiaNCC'] = write('06_BangGiaNCC.xlsx', pd.DataFrame([{'Mã NCC': r.MaNCC, 'Mã nguyên liệu': r.MaNL, 'Đơn giá (chưa VAT)': r.Gia, 'ĐVT mua': r.DVT,
    'Hiệu lực từ': '2026-11-10', 'Hiệu lực đến': '', 'Ưu tiên': 'Có' if r.UuTien else 'Không', 'Số lượng đặt tối thiểu': ''} for r in smd.sort_values(['MaNL', 'Gia']).itertuples()]))
counts['07_KhachHang'] = write('07_KhachHang.xlsx', pd.DataFrame([{'Mã khách hàng': x['Ma'], 'Tên khách hàng': x['Ten'], 'Loại khách': x['Loai'], 'Tên hiển thị': x['Hien'],
    'CIF': '', 'Mã số thuế': '', 'Điện thoại': '', 'Email': '', 'Địa chỉ': x['DiaChi'] or '', 'Trạng thái': 'Active' if x['Active'] else 'Inactive'} for x in cust]))
counts['08_DiemGiao'] = write('08_DiemGiao.xlsx', pd.DataFrame([{'Mã khách hàng': k, 'Mã điểm giao': pc, 'Tên điểm giao': pn, 'Địa chỉ': ad, 'Người nhận': '', 'SĐT người nhận': '',
    'Giờ nhận từ': '10:30', 'Giờ nhận đến': '11:30', 'Vĩ độ': '', 'Kinh độ': '', 'Hướng dẫn giao': ''} for k, pts in POINTS.items() for pc, pn, ad in pts]))
hd = []
for ct in contracts:
    for mt in ('SANG', 'TRUA', 'TOI', 'DEM'):
        if mt in ct['meals']:
            hd.append({'Mã khách hàng': ct['MaKH'], 'Khách hàng': ct['KH'], 'Hiệu lực từ': ct['Tu'], 'Hiệu lực đến': ct['Den'], 'Loại suất (MealType)': mt,
                       'Đơn giá (chưa VAT)': ct['meals'][mt], 'Suất TB / ngày tháng 8': round(ct['qty'].get(mt, 0))})
counts['09_HopDong_DonGia'] = write('09_HopDong_DonGia.xlsx', pd.DataFrame(hd), sheet='HopDong')
for src, wk, label in [('W3', '2026-11-23', 'tuan-23-11'), ('W1', '2026-11-30', 'tuan-30-11'), ('W3', '2026-12-07', 'tuan-07-12')]:
    counts[f'10_ThucDon_{label}'] = write(f'10_ThucDon_{label}.xlsx', menu_sheet(src, 'W3' if src == 'W1' else None), sheet='1. Menu')
ss = pd.DataFrame(sosuat)
ss = ss.rename(columns={'MaKH': 'Mã khách hàng', 'MaDiem': 'Mã điểm giao', 'Diem': 'Điểm giao', 'SANG': 'Sáng', 'TRUA': 'Trưa', 'TOI': 'Tối', 'DEM': 'Đêm'})
ss['Sáng – LC1'] = (ss['Sáng'] * 0.45).round().astype(int); ss['Sáng – LC2'] = (ss['Sáng'] * 0.33).round().astype(int); ss['Sáng – LC3'] = ss['Sáng'] - ss['Sáng – LC1'] - ss['Sáng – LC2']
counts['11_SoSuatMau'] = write('11_SoSuatMau.xlsx', ss, sheet='SoSuat')
counts['12_TonDauKy'] = write('12_TonDauKy.xlsx', pd.DataFrame([{'Mã nguyên liệu': r.Ma, 'Tên nguyên liệu': clean(r.Ten), 'ĐVT': uom(r.DVT, r.Ma) or '', 'Số lượng': float(r.SLCuoi), 'Đơn giá': float(r.DonGia),
    'Thành tiền': float(r.GTCuoi)} for r in ton.itertuples()]), sheet='TonDauKy')

# đối chiếu mã 3 nguồn
kh = pd.DataFrame([{'Mã': m, 'Tên chuẩn': e['Ten'], 'ĐVT': e['DVT'], 'Nguồn': ', '.join(dict.fromkeys(e['src'])), 'Có giá NCC': 'Có' if m in set(smd.MaNL) else 'Không',
                    'Có trong định mức': 'Có' if any(k[1] == m for k in rec) else ''} for m, e in sorted(ING.items())])
vat = smd[['MaNCC', 'MaNL', 'NL', 'Gia', 'VAT']].rename(columns={'MaNCC': 'Mã NCC', 'MaNL': 'Mã NL', 'NL': 'Nguyên liệu', 'Gia': 'Giá chưa VAT', 'VAT': '% VAT'})
with pd.ExcelWriter(OUT + '00_TongQuan_DoiChieu.xlsx', engine='openpyxl') as w:
    pd.DataFrame([{'Tệp': k + ('.xlsx'), 'Số dòng': v} for k, v in counts.items()]).to_excel(w, 'Tong quan', index=False)
    pd.DataFrame(assume, columns=['Đối tượng', 'Giả định cho demo (cần xác nhận khi chạy thật)']).to_excel(w, 'Gia dinh', index=False)
    pd.DataFrame(issues, columns=['Loại', 'Đối tượng', 'Chi tiết', 'Xử lý']).to_excel(w, 'Loi va xu ly', index=False)
    kh.to_excel(w, 'Doi chieu ma NL', index=False)
    pd.DataFrame([{'Mã món': dd['Ma'], 'Tên món': dd['Ten'], 'Nhóm': dd['Nhom'], 'Nguồn': dd['src'], 'Có định mức': 'Có' if any(r['MaMon'] == dd['Ma'] for r in rec.values()) else 'Không'} for dd in sorted(DISH.values(), key=lambda x: x['Ma'])]).to_excel(w, 'Doi chieu mon', index=False)
    vat.to_excel(w, 'VAT theo gia NCC', index=False)
    for sh in w.sheets.values():
        for col in sh.columns:
            sh.column_dimensions[col[0].column_letter].width = min(70, max(10, max(len(str(cc.value or '')) for cc in col[:300]) + 2))
json.dump(dict(counts=counts, issues=Counter(i[0] for i in issues), ing=len(ING), dish=len(DISH), rec_dish=len({r['MaMon'] for r in rec.values()}), rec=len(rec),
               sup=len(SUP), sm=len(smd), cust=len(cust), active=sum(c['Active'] for c in cust), pts=sum(len(v) for v in POINTS.values()), conflict=conflict),
          open(OUT + '_summary.json', 'w'), ensure_ascii=False, indent=1, default=str)
print(open(OUT + '_summary.json').read())
