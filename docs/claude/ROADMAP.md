# ROADMAP – Danh sách task

**Trạng thái:** `TODO` · `DOING` · `DONE` · `BLOCKED(<lý do / D-xx>)` · `DEFERRED(R2)`.

Mỗi task có: mục tiêu, phụ thuộc, việc cần làm, **tiêu chí nghiệm thu (AC)**. Chỉ đánh `DONE` khi mọi AC đạt, build xanh và test xanh. Task lớn được phép tách thành Txx.1, Txx.2… ngay trong file này.

## ▶ THỨ TỰ LÀM TIẾP (anh chốt 2026-11-19 – Claude Code đọc mục này trước)

1. **Làm xong task đang dở** (theo PROGRESS – trạng thái `DOING`), không bỏ ngang.
2. ~~UI-1R~~ ✅ · ~~UI-3~~ ✅ · ~~UI-4~~ ✅ (xong 2026-11-19).
3. ~~UI-5~~ ✅ hoàn thiện form khách hàng (xong 2026-11-21, đã gộp `main`).
4. ~~UI-2R~~ ✅ bố cục 1/3 – 2/3 + khối thông tin rút gọn (xong 2026-11-21).
5. ~~MENU-1~~ ✅ bỏ tiền tố "Duyệt – " ở 19 mục menu Phê duyệt (xong 2026-11-21; DB có sẵn: anh chạy `DB/scripts/MENU1_rename_approve_menu.sql` sau backup).
6. **FE-E** việc frontend tồn đọng (FE-E.1 Telegram → FE-E.2 QR → FE-E.3 màn tích hợp).
7. **UG-1** sửa chênh lệch tài liệu – code (`docs/user-guide/90_CHENH_LECH.md`), kèm phần giao diện (FE-E.4).
8. ĐỢT BOT còn lại (BOT-2.9 – cần server test / token thật) khi anh sẵn sàng.

Mỗi bước: build FE + BE xanh, test xanh, cập nhật PROGRESS và `docs/user-guide/*` liên quan; xong bước nào báo anh mở thử giao diện bước đó.

## ★ ƯU TIÊN CỦA ANH (2026-11-14): RELEASE 1 = CHỈ BACKEND (.NET + SQL) đủ API cho mockup

> **⚠ ĐỔI HƯỚNG 2026-11-15 (D-30):** **khung chuẩn là code cũ**, không sinh thêm API `/api/v1`. Mọi task dưới đây hiểu là: rà soát code cũ → bổ sung method vào Service cũ + action vào Controller cũ theo CLAUDE.md mục 4. Spec API chỉ là danh sách yêu cầu nghiệp vụ. Chỗ nào trong file này còn ghi `/api/v1/...` thì đọc là "chức năng đó, viết thành action kiểu cũ `api/<Entity>/<Action>`". (T45R đã DONE 2026-11-15.) **Từ 2026-11-16: làm theo bảng LÁT PILOT ngay dưới đây.**

## ★★★★ ĐỢT BE-3 – BIỂU ĐỒ, CẢNH BÁO, IOT, KPI, DASHBOARD (anh chốt 2026-11-17, D-57) · `DONE` 2026-11-17 (BE3.1–BE3.5, BE3.7; BE3.6 không làm – D-58)

> Anh đưa các mục sau vào làm tiếp (trước đây ở nhóm "Để sau"): KPI 221–225, dashboard + luồng E2E 231–232, biểu đồ 100 / 157 / 163, tìm kiếm toàn cục / tab Liên quan / chuông cảnh báo 16–18, vị trí xe 193, IoT nhiệt độ 166. Kèm dòng **192** (sự kiện giao – còn "Một phần" sau BE-2).
> **Luật chung:** ~~chỉ backend (D-49)~~ → **Claude Code làm cả backend và frontend (D-65)**, khung cũ (D-30: `api/<Controller>/<Action>`, Service, `Result<T>`, quyền ObjectMenu – D-31), **không tạo bảng / cột / FK / schema**, không job tự động (D-45 – mọi "job" trong spec làm thành **action bấm tay**, viết Service sao cho `AppDelicare.Job` sau này gọi lại – D-48), không gửi bot / Telegram / Zalo. Mỗi task: build 0 lỗi, test xanh, file `.http` mới chạy được trên DB dev, handoff `docs/frontend-handoff/`, cập nhật Catalog cột J / K / L / M (cột N của các dòng này đã đổi thành `BE-3`). Câu hỏi → mẫu D-49 (có đề xuất), làm theo đề xuất, ghi DECISIONS mục BE-3.
> **⛔ Anh chốt 2026-11-17 (D-58): KHÔNG làm tra cứu cấu trúc database** – bỏ DB Explorer + GAP (Catalog 226–230, 233, 234 = "Không làm"). Không API nào được trả tên bảng / cột / quan hệ / dữ liệu mẫu, không đọc `sys.*` / `INFORMATION_SCHEMA` để trả ra ngoài, không nhúng metadata cấu trúc DB vào WebApi.
> **Thứ tự:** BE3.1 → BE3.2 → BE3.3 → BE3.4 → BE3.5 → BE3.7 (BE3.5 cần cảnh báo của BE3.3 và snapshot của BE3.4).

| Task | Phạm vi (STT Catalog) | Nội dung + đề xuất của em (D-57) |
|---|---|---|
| **BE3.1** · `DONE` 2026-11-17 (nhánh `feat/BE3.1-charts`; PROPOSED D-57 j–n) | **Biểu đồ + sự kiện giao**: 100, 157, 163, 192, 193 | **100** `OrderReconciliation/GetList` thêm `chart: [{ date, ordered, delivered, accepted }]` theo ngày ăn (lọc from / to / khách / trạng thái). **157** `WasteRecord/GetSummary` (from, to, groupBy = wasteType / dish / shift) → `[{ key, label, quantityMeals, quantityKg, cost? }]`. **163** `TemperatureLog/GetSeries` (storageZoneId, from / to mặc định 48 giờ) → `{ zone: { min, max }, points: [{ measuredAt, temperatureC, isWithinRange }] }`, giới hạn số điểm (gộp theo phút nếu > 2 000). **192** `DeliveryOrder/GetEvents` – timeline `DeliveryEvent` theo `EventAt` (đọc), xem lại phần "Một phần" và làm nốt. **193** `DeliveryEvent/GetVehiclePositions` (deliveryDate, routeId?) → tọa độ **theo sự kiện** trong `DeliveryEvent` (GAP-F-05: không có GPS liên tục, không dùng `LocationTracking`); thêm `POST DeliveryEvent/RecordPosition` (EventType `POSITION`, lat / long, tài xế ghi từ điện thoại) – vẫn chỉ ghi vào bảng có sẵn. |
| **BE3.2** · `DONE` 2026-11-17 (nhánh `feat/BE3.2-iot`; PROPOSED D-57 f, o) | **IoT nhiệt độ**: 166 | `POST IotTemperature/Receive` (JSON mảng `[{ deviceCode, measuredAt, temperatureC }]`, ≤ 500 dòng / lần). Xác thực **API key riêng** qua header `X-Api-Key` (giá trị lấy từ biến môi trường / user-secrets – **không commit**; mỗi thiết bị / cổng một key được, R1 cho phép danh sách key), không dùng JWT người dùng, `[SkipPermission]` + filter kiểm key. `DeviceCode` → khu vực / vị trí theo cấu hình `Iot:Devices` (appsettings, không bảng). Ghi `TemperatureLog`: `SourceChannel = IOT`, ngưỡng từ `StorageZone`, `IsWithinRange` tính ở server, `RecordedBy` = tài khoản `system`. **Chống trùng**: cùng (DeviceCode, MeasuredAt) thì bỏ qua. Thiết bị lạ → trả trong `rejected`, ghi log. Giới hạn tần suất theo key. Ngoài ngưỡng → hiện ở chuông cảnh báo (BE3.3), không gửi tin. |
| **BE3.3** · `DONE` 2026-11-17 (nhánh `feat/BE3.3-alerts-search`; PROPOSED D-57 p–u) | **Cảnh báo, tìm kiếm, tab Liên quan**: 16, 17, 18 | **18** `Common/GetAlerts` (branchId) → `[{ code, severity, title, detail, route, count }]` theo spec 12.1: NL dưới tồn tối thiểu; duyệt Pending / InProgress **của người đang đăng nhập**; dòng QC đầu vào HOLD; `TemperatureLog.IsWithinRange = 0` trong 48 giờ; phản ánh đang mở / quá hạn; `OutboundMessage` Failed (nếu có dữ liệu); lô Available hết hạn trong 2 ngày. Chỉ trả cảnh báo thuộc màn anh / chị đó có quyền Xem; cache 60 giây theo (người, chi nhánh); FE gọi định kỳ (không push). **17** `Common/GlobalSearch` (q ≥ 2 ký tự) → **chỉ** màn hình (theo menu người dùng có quyền) và bản ghi của danh sách trắng (**không trả tên bảng** – D-58): Customer, Order, Ingredient, Dishe, Supplier, MaterialLot, Employee, PurchaseOrder – tìm theo mã / tên / số chứng từ, mỗi loại ≤ 5, chỉ loại có quyền Xem. **16** `Common/GetRelated` (resource, id) → chứng từ liên quan theo **danh sách quan hệ viết cứng trong code** (lấy từ `LOGICAL_RELATIONS.md`, chỉ các cặp nghiệp vụ cần cho tab Liên quan), mỗi nhóm ≤ 8 dòng + `count`; nhóm trả **tên màn hình + route + số / mã / trạng thái chứng từ**, **không trả tên bảng / cột** (D-58); bỏ nhóm không có quyền Xem; `resource` phải nằm trong danh sách trắng (chống SQL injection). |
| **BE3.4** · `DONE` 2026-11-17 (nhánh `feat/BE3.4-kpi`; PROPOSED D-57 w–ac, GAP-KPI-01) | **KPI**: 221–225 | **221** `KpiMetricDefinition`: CRUD cũ + **phiên bản**: đổi công thức / mục tiêu → tạo `VersionNo` mới, đóng `EffectiveTo` bản cũ, không sửa bản cũ; UNIQUE (MetricCode, VersionNo) kiểm trong Service. Seed định nghĩa KPI theo spec 11.7 (PROD_ACHIEVEMENT, DEL_ONTIME, INV_ACCURACY, FIN_COST_MEAL, FIN_FOOD_COST, WASTE_RATE, QC_REJECT_RATE…) – **add-only**, công thức + mục tiêu ghi `PROPOSED – BA/PO CONFIRM`. **224** `KpiSnapshot/Refresh` (jobCode = KPI_DAILY / KPI_MONTHLY, date / month) – **bấm tay** (chỉ admin / quyền Làm mới), `sp_getapplock` theo jobCode, ghi `AnalyticsRefreshLog` Running → Succeeded / Failed + RowsProcessed + SourceWatermark, **upsert** theo (SnapshotDate / Month, BranchId, MetricCode, MetricVersion) – chạy lại không nhân đôi; `DataQualityStatus` = INCOMPLETE khi mẫu số 0 / thiếu dữ liệu. Service `KpiCalculationService` để `AppDelicare.Job` gọi sau (D-48). **222 / 223** `KpiSnapshot/GetDaily`, `GetMonthly` (from / to, branchId, domainCode) → `{ metrics: [{ definition, latest: { value, target, variance, met }, series }] }`; `met` theo chiều chỉ tiêu (càng thấp càng tốt: WASTE, REJECT, EXPIRY, COST). **225** `AnalyticsRefreshLog/GetList` (jobCode, status, from / to). |
| **BE3.5** · `DONE` 2026-11-17 (nhánh `feat/BE3.5-dashboard`; PROPOSED D-57 ad–ai) | **Dashboard + hành trình đơn**: 231, 232 | **231** `Dashboard/GetExecutive` (date, shiftId?, branchId?) – đủ các khối spec 12.1 (`kpi.*`, `chart.meals7d`, `shiftProgress`, `kpiSnapshot` 4 chỉ tiêu, `orders` 6 đơn đầu, `alerts` = BE3.3), đọc thẳng bảng giao dịch, **cache 60 giây** theo (ngày, ca, chi nhánh) bằng `IMemoryCache`; số liệu phải khớp với các màn danh sách (test so sánh). **232** `Dashboard/GetOrderJourney` (orderId) → trạng thái từng chặng Order → ProductionPlan → ProductionOrder / Batch → PackingOrder → DeliveryOrder / Execution → ProofOfDelivery → OrderReconciliation, mỗi chặng `{ status, at, docNo, route }`. Quyền: mục menu `dash-exec`, `flow-e2e` (seed ObjectMenu add-only nếu chưa có). |
| ~~BE3.6~~ | ~~DB Explorer + GAP: 226–230, 233, 234~~ | **KHÔNG LÀM (D-58)** – anh không cho phép tra cứu cấu trúc database. |
| **BE3.7** · `DONE` 2026-11-17 (gộp `release/pilot` @ 4cbabee + test D-58; staging cập nhật; **chưa push – chờ anh duyệt**) | **Gộp, kiểm tra, deploy** | Chạy lại `full-flow.http` (+ dashboard / KPI kiểm khớp số liệu của luồng: số suất, tiến độ, giao, cảnh báo), mọi `.http` của BE-2 / BE-3, test xanh; test xác nhận **không API nào trả cấu trúc DB** (D-58); `scripts.order` / `DB/README.md` đủ seed mới; deploy staging + smoke; cập nhật Catalog; **dừng, báo anh** (không push / gộp `main` khi anh chưa duyệt). |

### T48.4 – Production Linux + Docker (D-60, anh chốt 2026-11-17) · làm **sau BE3.7**, trước T48.2 · `DONE` 2026-11-17 (nhánh `feat/T48.4-docker`; **Docker chưa chạy thật – máy dev không có Docker**, bước 7 dựng lại staging Docker trên Linux chờ server / máy có Docker; PROPOSED T48.4 a–e)
1. **`deploy/docker/docker-compose.production.yml`**: `nginx` (Angular build + HTTPS + `/api` → `api`), `api`, `job` (đúng 1 bản, `restart: unless-stopped`) – **không kèm SQL Server**; bí mật qua `.env` trên server (`deploy/docker/production.env.example` không có giá trị thật). Volume cho tệp đính kèm. Nginx: giới hạn kích thước upload khớp `DocumentStorage:MaxFileSizeMb`, header bảo mật, chuyển HTTP → HTTPS (chứng chỉ do anh cung cấp / Let's Encrypt – để trống cấu hình).
2. **Giờ SQL Server**: tài liệu + compose staging / production đặt `TZ=Asia/Ho_Chi_Minh` cho SQL Server; thêm kiểm tra khởi động (API `/health` và job) cảnh báo nếu `SYSDATETIME()` của DB lệch giờ VN của IClock > 5 phút.
3. **Tài khoản SQL riêng cho ứng dụng**: script `deploy/db/app-login.sql` (tham số tên / mật khẩu từ biến, không ghi cứng) tạo login + user `db_datareader`, `db_datawriter`, `GRANT EXECUTE` – **không** quyền DDL. Compose staging / production dùng tài khoản này cho `api` và `job`; `sa` chỉ dùng cho `db-init`. (Chỉ chạy trên DB dev / Docker dev để thử; DB khác do anh chạy.)
4. **Health check job**: `JobSchedulerService` ghi file nhịp (`/tmp/job-heartbeat`) mỗi vòng; Dockerfile / compose `HEALTHCHECK` kiểm file không cũ quá 2 phút. Test đơn vị cho file nhịp.
5. **Log**: Docker (`Staging` / `Production`) chỉ ghi console (Serilog Console), cấu hình `logging: driver json-file, max-size 20m, max-file 10` cho `api` / `job`; file log giữ cho chạy ngoài Docker (dev).
6. **Bỏ đường IIS / Windows Service cho production**: D-59 (g) bỏ, không thêm gói `WindowsServices`; `deploy/README.md` ghi rõ Linux + Docker là cách chuẩn, `deploy/iis/` chỉ chạy thử trên máy dev.
7. **Kiểm tra**: dựng lại staging Docker trên Linux theo compose production (+ SQL container), chạy `full-flow.http` + `--list` của job + kiểm health check; checklist go-live T48.2 cập nhật theo Docker. **Dừng, báo anh** (không push / gộp `main` khi anh chưa duyệt, không chạy lên server thật).

### T48.5 – Tương thích SQL Server 2022 (D-61, anh chốt 2026-11-17) · làm **ngay** (trước khi push đợt BE-3 / T48.4) · `DONE` 2026-11-17 bước 1–3 (nhánh `feat/T48.5-sql2022`; DB dev 160, test 316 / 316, full-flow 217 / 217) · bước 4 `BLOCKED(máy chưa có Docker)` · staging đã hạ 160 (anh đồng ý 2026-11-17, full-flow 217 / 217)
1. **Hạ mức tương thích DB dev**: `ALTER DATABASE [AppDelicare] SET COMPATIBILITY_LEVEL = 160;` – **chỉ** DB dev (và `AppDelicare_Staging` trên máy anh nếu anh đồng ý trong lúc làm). Đây là đổi cấu hình DB, không đụng bảng / cột / dữ liệu. Chạy lại test + `full-flow.http` trên DB dev sau khi hạ (kế hoạch truy vấn có thể khác – ghi lại API nào chậm rõ rệt).
2. **Script deploy giữ mức 160**: `Deploy-Database.ps1` và `deploy/docker/db-init.sh` kiểm `compatibility_level` của DB đích: > 160 → đặt về 160 (idempotent, ghi log); < 160 → cảnh báo, không tự nâng. Ghi vào `deploy/README.md`, `DB/README.md`.
3. **Test quét mã** (`AppDelicare.Tests/Schema/SqlServer2022CompatibilityTests`): quét `DB/**/*.sql`, `deploy/db/**/*.sql` và chuỗi SQL trong `AppDelicare.*/**/*.cs` (Dapper / SP), báo lỗi khi gặp từ khóa cấm (CLAUDE.md mục DB, quy tắc 0) – bỏ qua comment; có danh sách ngoại lệ rỗng mặc định.
4. **Kiểm tra trên SQL Server 2022 thật** (Docker `mcr.microsoft.com/mssql/server:2022-latest` – cổng riêng, không đụng DB dev): DB trống → `db-init` 2 lần (lần 2 không đổi số dòng seed) → `full-flow.http` đạt. Ghi lệnh vào `deploy/README.md`, thêm thành **bước bắt buộc trước mỗi lần gộp `main`** (CLAUDE.md quy trình) – kết quả ghi vào PROGRESS.
5. Báo anh; không push / gộp `main` khi anh chưa duyệt.

## ★ MA-1 – QUY TẮC MÃ TOÀN HỆ THỐNG + KHÁCH HÀNG THEO LOẠI (D-63, anh chốt 2026-11-19) · `DONE` 2026-11-19 (nhánh `feat/MA-1-code-rules`; .http cũ ghi chú – kiểm hồi quy bằng full-flow; script MA-1.4 chờ anh chạy thật sau backup)

> Nguồn: DECISIONS **D-63**, BUSINESS_RULES **§11**. Làm sau BOT-2.9 đang dở. Không thêm bảng / cột / SEQUENCE / FK; script dữ liệu chỉ UPDATE, chạy sau backup. **Anh giao làm cả backend và frontend** (D-63 l – ngoại lệ D-49 cho việc này).

- **MA-1.1 Sinh mã:** `DocumentNumberService` thêm dạng **danh mục** (không phần ngày, `TIỀNTỐ-NNNNN`, khóa `sp_getapplock` theo tiền tố, quá 99.999 → 6 chữ số) và `DocumentKind` mới: `CustomerBusiness` (KHB), `CustomerIndividual` (KHC), `Supplier` (NC), `Ingredient` (NL), `Dish` (MA), `Warehouse` (KO), `Vehicle` (XE), `Employee` (NV), `Menu` (NU). Tách `Voucher` (PH) thành các loại riêng theo §11: DH, DM, MH, PN, PX, KK, GD, LS, DG, GH, DC; đổi `MaterialLot` NL → **LO**, `ProductionPlan` KH → **KS**. MAX vẫn đọc cả cột cũ; số cũ (`PH-…`) không tính vào dãy mới.
- **MA-1.2 Khách hàng (BE):** `CustomerService` Insert: bỏ qua `Code` / `CIF` gửi lên → sinh `Code` theo `CustomerType`, `CIF = Code`, gán `Name` / `DisplayName` / `CompanyId` (D-63 h); `CheckRules` kiểm bắt buộc + định dạng theo loại (D-63 g: MST, CCCD, điện thoại, email, ngày), không trùng MST (KHB) / CCCD (KHC); xóa trống trường không thuộc loại. Update: `Code`, `CIF` không đổi; đổi loại theo D-63 i (chưa có hợp đồng / đơn → mã mới, ghi lịch sử; có rồi → báo lỗi). Bỏ kiểm trùng `Code` khi rỗng (lỗi null = IS NULL ở `CustomerService.cs:147`). Thông báo mới thêm vào `ConstantHelper.Message`. `CustomerType` nhận đúng 2 giá trị (hằng `CustomerTypes`).
- **MA-1.3 Danh mục khác (BE):** Supplier, Ingredient, Dish, Warehouse, Vehicle, Employee Insert: sinh mã khi `Code` trống hoặc luôn sinh (bỏ qua giá trị gửi lên) – chọn **luôn sinh**; Update không đổi `Code`. Employee mới: tên đăng nhập = mã khi tạo tài khoản từ nhân viên. Import dữ liệu gốc (`MasterDataImportService`): dòng mới không có mã → sinh mã; dòng có mã → khớp theo mã như cũ.
- **MA-1.4 Script dữ liệu (một lần):** `DB/scripts/` (không phải migration cấu trúc): (1) `Customer.CustomerType` cũ (HOSPITAL…) → `Industry`, loại = `BUSINESS`; (2) đánh lại mã khách hàng (`KHB`/`KHC`, theo ngày tạo) và NCC (`NC`), `CIF = Code`; xuất file đối chiếu mã cũ → mới; idempotent, có chế độ chạy thử (in trước, không ghi). Ghi rõ cần chạy lại import nào dùng mã cũ.
- **MA-1.5 Nhận dạng mã:** bot (mẫu tìm `XX-\d{6}` → `[A-Z]{2,3}-\d{5,6}…`, nhận cả mẫu cũ; gợi ý `DH-…` ở 🔍 Truy xuất), tìm kiếm toàn cục, mẫu in / QR, test (đổi kỳ vọng `PH-` / `KH-` / `NL-`), `full-flow.http`.
- **MA-1.6 Tài liệu:** STATUS / Catalog API, `docs/user-guide/91_PHU_LUC.md` §2, `02_KINH_DOANH.md` mục khách hàng, bot (`20_BOT_TELEGRAM.md` tìm nhanh), `docs/frontend-handoff/` ghi lại thay đổi giao diện MA-1.8 cho dev FE biết; `CLAUDE.md` §3.3 (đã có luật D-63 k).
- **MA-1.7 Phủ toàn hệ thống (D-63 k):** (1) bảng phân nhóm G1 / G2 / G3 trong code (một chỗ, cạnh `Specs`), theo BUSINESS_RULES §11.3; bổ sung tiền tố G1 còn thiếu (`DP` điểm giao, `BG` bảng giá, `QC` phiếu QC, `MR` lần tính MRP); (2) G2: kiểm chữ hoa không dấu / không trùng / khóa sửa khi đã dùng ở Service Insert / Update; (3) API `Common/GetCodeRules` (khung cũ, `Result<T>`, quyền đăng nhập) trả `{ bảng, cột, nhóm, tiền tố, mẫu }` cho giao diện; (4) **test chặn**: quét mọi entity có thuộc tính tên kết thúc `Code` / `No` / `Number` → phải có trong bảng phân nhóm, thiếu → đỏ; mọi bảng G1 → Insert bỏ qua giá trị gửi lên và sinh mã (test theo từng Service).
- **MA-1.8 Frontend:** (1) form chung (`core/ui.js` UI.form + `core/store.js`): đọc `Common/GetCodeRules`; cột G1 → ô chỉ đọc "(tự sinh khi lưu)", không gửi; G2 → tự chuyển chữ hoa không dấu, khóa ở chế độ sửa nếu máy chủ báo đã dùng; (2) **ghi chờ API**: chỉ đóng form và báo "Đã tạo … <mã>" khi API trả thành công, lỗi thì giữ form và hiện lỗi tại chỗ (bỏ báo thành công trước); (3) form riêng `crm-customer` theo D-63 g / j (chọn loại → ô theo loại, ẩn CIF ở form / danh sách / hồ sơ; danh mục `Customer.CustomerType` trong `data/catalog.js`); (4) danh sách / tìm kiếm / in hiện mã mới, nhận cả mẫu cũ; (5) build FE xanh.

**AC:** tạo 2 khách đồng thời cùng loại → 2 mã liên tiếp không trùng; KHB thiếu MST / KHC thiếu Họ-Tên → lỗi tiếng Việt rõ trường; gửi `Code` / `CIF` bất kỳ → bị bỏ qua; CIF = Code; đổi loại khi đã có đơn → lỗi; chứng từ mới ra tiền tố §11, chứng từ cũ vẫn tìm / in / mở bằng bot được; script chạy thử in đúng bảng đối chiếu, chạy thật lần 2 không đổi gì; thêm entity mới có cột `Code` chưa xếp nhóm → test đỏ; form chung không cho gõ mã bảng G1; test xanh, `full-flow.http` xanh, build FE xanh.

## ★ UI-1 – THẺ CHỈ SỐ KIỂU MỚI + DẢI THẺ KHÁCH HÀNG (D-64, anh chốt 2026-11-19) · `DONE` 2026-11-19 (UI-1R xong – ảnh so chuẩn ở `docs/frontend-handoff/ui1r/`)

> **UI-1R – SỬA THẺ CHỈ SỐ CHO ĐÚNG D-64 c (anh báo 2026-11-19; làm ngay sau task đang làm, trước các task TODO khác).** Phạm vi: mọi dải thẻ chỉ số, kể cả màn khách hàng 360 (UI-2) và các màn 360 làm sau (UI-3, UI-4) – sửa ở thành phần chung `UI.kpi` / `app.css` rồi rà từng chỗ gọi. Rà soát của chat tài liệu trên nhánh `feat/MA-1-code-rules`:
> 1. **Biểu tượng đang nằm giữa, phải nằm bên trái (lỗi anh thấy).** Nguyên nhân: thẻ dùng class `card` của template – Bootstrap đặt `.card{display:flex;flex-direction:column}`; `app.css` chỉ thêm `display:flex;align-items:center` cho `.card.kpi` nên thẻ vẫn xếp **dọc** và `align-items:center` đẩy biểu tượng + chữ ra **giữa**. Sửa: `.card.kpi{flex-direction:row;justify-content:flex-start;align-items:center;text-align:left}` (hoặc bỏ class `card`, dùng class riêng `dc-kpi` không kế thừa template). `.kpi-body` căn trái.
> 2. **Số phụ đang chứa chú thích cũ:** ~50 chỗ gọi `UI.kpi` (dashboard, hr, scm, production, logistics, menu, explorer, `app.js` danh sách chung) vẫn truyền câu dài làm tham số thứ 3 → hiện như dòng chú thích chân thẻ; nhiều chỗ còn chữ kỹ thuật trong chế độ dữ liệu thật ("Σ OrderLine.Quantity", "DeliveryExecution.Status = …", "RequestStatus = Pending/InProgress", "CustomerComplaint · theo SLA", "QcStatus = PASS", "dbo.…"). Sửa từng chỗ: số phụ **≤ 20 ký tự**, dạng "/ 2 · 100%", "· 96 đơn", "· còn 42 ngày"; câu giải thích → `title` (rê chuột) hoặc bỏ; bỏ hết chữ kỹ thuật.
> 3. **Thẻ trong ngăn trượt / hộp thoại bị bóp:** lưới chia cột theo bề rộng màn hình nên ngăn 720px vẫn 4 cột (~150px / thẻ, nhãn bị cắt, số tràn). Sửa: `.drawer .kpis, .modal .kpis{grid-template-columns:repeat(2,minmax(0,1fr))}` (hoặc container query theo khung chứa).
> 4. **Số chính bị thu nhỏ trong `.panel`:** quy tắc cũ `.panel .kpi-value` (26px, có lề) đứng sau quy tắc mới nên đè. Sửa: đổi class ô chỉ số tùy biến cũ (be3.js, integration.js) sang `kpi-mini-value` hoặc giới hạn `.panel .kpi-value:not(.kpi .kpi-value)`.
> 5. **Số phụ quá đậm:** `.kpi-sub` 15px / 500 như nhãn → 14–15px, **400**, màu xám nhạt.
> 6. **Thẻ dùng chữ làm số chính** (chi tiết đơn: "Khách hàng" = tên khách; thực đơn: "Thực đơn" = tên): thẻ chỉ số chỉ cho **con số**; thông tin chữ chuyển vào khối thông tin.
> 7. Nhãn dài bị cắt "…" → thêm `title` = nhãn đầy đủ.
>
> **AC UI-1R:** biểu tượng ở **bên trái**, nhãn + số chính + số phụ ở bên phải, căn giữa dọc, chữ căn trái; không số phụ nào > 20 ký tự hoặc chứa tên bảng / cột / mã trạng thái tiếng Anh; ngăn trượt / hộp thoại 2 cột; số chính 32–36px ở mọi nơi (kể cả trong `.panel`); `npm run build` xanh; **chụp ảnh** dải thẻ ở dashboard, danh sách khách hàng, hồ sơ khách hàng (ngăn trượt) ở 3 cỡ màn hình, đặt cạnh `docs/frontend-handoff/assets/kpi-card-standard.png` để so trước khi báo xong (ghi đường dẫn ảnh trong PROGRESS).

> Nguồn: DECISIONS **D-64** (c)–(d), mẫu `docs/frontend-handoff/assets/kpi-card-standard.png`. Anh giao Claude Code làm cả FE (D-64 a). Làm sau MA-1.8 (cùng sửa `core/ui.js`, `app.css`). Không thêm bảng / cột.

- **UI-1.1 Thành phần chung:** `UI.kpi` vẽ theo chuẩn D-64 c, thêm tham số **số phụ** (`sub`) – giữ chữ ký cũ để các chỗ gọi không vỡ. `app.css`: `.kpi` / `.kpi-icon` / `.kpi-value` / `.kpi-sub` mới, lưới 4 / 2 / 1 cột, biến màu cho giao diện tối. Rà **mọi chỗ gọi** `UI.kpi` (≈ 33 chỗ: dashboard 16, scm 6, sales 2, hr 2, production 2, explorer 2, menu 1, logistics 1, app.js 1): chú thích chân thẻ có nghĩa → chuyển thành số phụ ngắn; chú thích kỹ thuật (tên bảng, `tech(...)`) → bỏ.
- **UI-1.2 Danh sách khách hàng (`crm-customer`)** – thay dải "Tổng bản ghi" chung bằng 4 thẻ: **Tổng khách hàng** (người, xanh lá) · **Đang hoạt động** (dấu tích, xanh lá nhạt) số phụ "/ tổng · %" · **Hợp đồng hiệu lực** (văn bản, xanh dương) · **Suất TB/ngày (7 ngày)** (dao nĩa, cam) = tổng suất đơn Đã giao / Đã đối soát 7 ngày gần nhất ÷ 7 (sửa luôn cột "Suất TB/ngày" đang chia 5). Số liệu lấy từ máy chủ theo **đơn vị làm việc** và bộ lọc: thêm `Customer/GetSummary` (khung cũ, `Result<T>`, quyền xem `crm-customer`) nếu API hiện có chưa đủ.
- **UI-1.3 Hồ sơ khách hàng 360** – thay 4 thẻ cũ bằng: **Hợp đồng hiệu lực** (xanh lá) · **Suất TB/ngày (7 ngày)** (cam) · **Doanh thu đã đối soát** (xanh dương, số phụ "N đơn") · **Khiếu nại đang mở** (đỏ, số phụ "/ tổng · % đúng hạn"). Bổ sung trường còn thiếu vào `Customer/GetOverview` (`activeContractCount`, `openComplaintCount`, `complaintOnTimePct`, `reconciledOrderCount`) – chỉ thêm trường trả về, không đổi trường cũ.
- **UI-1.4 Kiểm tra:** `npm run build` xanh; test BE xanh (+ test cho `GetSummary` / trường mới của `GetOverview`); mở thử dashboard, danh sách / hồ sơ khách hàng, 1 màn kho, 1 màn nhân sự ở 3 cỡ màn hình và giao diện tối; cập nhật `docs/user-guide/02_KINH_DOANH.md` (khách hàng) + `13_BAN_DIEU_HANH.md` (dashboard) nếu nội dung thẻ đổi.

**AC:** mọi dải thẻ chỉ số hiển thị đúng chuẩn D-64 c (biểu tượng trái, nhãn + số chính + số phụ, không chú thích chân); danh sách khách hàng và hồ sơ 360 đúng 4 thẻ như trên, số khớp dữ liệu máy chủ theo đơn vị làm việc; không màn nào vỡ bố cục; build FE xanh, test BE xanh.

## ★ UI-2 – MÀN CHI TIẾT KHÁCH HÀNG 360 (D-66, anh chốt 2026-11-19) · `DONE` 2026-11-19 (nhánh `feat/MA-1-code-rules`; chờ anh mở thử trên trình duyệt)

> Nguồn: DECISIONS **D-66**; mô phỏng `docs/frontend-handoff/assets/customer-360-mockup.html`; bố cục tham khảo `src/app/pages/account-pages/account-overview` + `elements/short-cods/profile-account/account-overview-head`. Làm sau UI-1 (dùng thẻ chỉ số mới). Claude Code làm cả BE + FE (D-65). Không thêm bảng / cột.

- **UI-2.1 BE:** `Customer/GetOverview` thêm trường theo D-66 f(1) (kể cả danh sách cảnh báo – mỗi cảnh báo: mã, mức, nội dung tiếng Việt, liên kết màn); **mới** `Customer/GetTrend` (D-66 f(2)); `Order/GetAll` thêm `FromMealDate` / `ToMealDate`. Test cho từng phần; `full-flow.http` thêm bước gọi 2 API mới.
- **UI-2.2 FE – khung màn:** đăng ký `crm-customer-360` (ẩn menu), mở từ dòng danh sách khách hàng; phần đầu + nút theo quyền + 4 thẻ chỉ số (UI-1.3) + hàng tab theo D-66 b–c; ngăn xem nhanh hiện tại thêm nút **Mở hồ sơ đầy đủ**.
- **UI-2.3 FE – tab Tổng quan:** 6 khối theo D-66 d (bố cục 3 cột 5 / 4 / 3 rồi 5 / 7 như mô phỏng; màn hẹp xếp dọc); biểu đồ dùng thư viện biểu đồ engine đang có.
- **UI-2.4 FE – các tab còn lại:** gọi API theo D-66 e, tải khi mở tab; bảng có lọc ngày / trạng thái, bấm dòng mở chứng từ; tab Tài liệu có tải lên (≤ 20 MB, loại cho phép như `adm-document`); tab Lịch sử / Liên quan dùng `Common/GetHistory` / `GetRelated`.
- **UI-2.5 Kiểm tra + tài liệu:** build FE xanh, test BE xanh; mở thử với 1 khách doanh nghiệp + 1 khách cá nhân trên DB dev, 3 cỡ màn hình; người không có quyền một màn thì không thấy tab đó; cập nhật `docs/user-guide/02_KINH_DOANH.md` (mục khách hàng) và `10_DIEM_PHAT.md` nếu liên quan.

**AC:** bấm khách ở danh sách → mở màn 360 đầy đủ; số liệu khớp các màn gốc (đơn hàng, đối soát, khay hộp, phản ánh); khối Cần chú ý hiện đúng điều kiện; tab ẩn theo quyền; build FE + test BE xanh.

## ★ MENU-1 – BỎ TIỀN TỐ "DUYỆT – " Ở MENU NHÓM PHÊ DUYỆT (anh yêu cầu 2026-11-22) · `DONE` 2026-11-21

> Nhóm menu **Phê duyệt** (`#/group/approve`) đã nói rõ là duyệt → tên mục con bỏ tiền tố "Duyệt – ". Chỉ đổi **ObjectMenu.Name**; giữ nguyên Id, MetaLink, ShortName, OrderShow, quyền đã gán (UserRole). Không thêm bảng / cột.

| OrderShow | MetaLink | Tên cũ | Tên mới |
|---|---|---|---|
| 9901 | `#/wf-inbox?type=menu-recipe` | Duyệt – Công thức & phiên bản | **Công thức & phiên bản** |
| 9902 | `#/wf-inbox?type=crm-price-policy` | Duyệt – Chính sách giá theo KH/HĐ | **Chính sách giá theo KH/HĐ** |
| 9903 | `#/wf-inbox?type=crm-contract` | Duyệt – Hợp đồng suất ăn | **Hợp đồng suất ăn** |
| 9904 | `#/wf-inbox?type=menu-menu` | Duyệt – Thực đơn & phiên bản | **Thực đơn & phiên bản** |
| 9905 | `#/wf-inbox?type=menu-change-request` | Duyệt – Yêu cầu đổi món | **Yêu cầu đổi món** |
| 9906 | `#/wf-inbox?type=crm-order` | Duyệt – Đơn đặt suất ăn | **Đơn đặt suất ăn** |
| 9907 | `#/wf-inbox?type=scm-pr` | Duyệt – Đề nghị mua hàng | **Đề nghị mua hàng** |
| 9908 | `#/wf-inbox?type=scm-po` | Duyệt – Đơn mua hàng | **Đơn mua hàng** |
| 9909 | `#/wf-inbox?type=scm-adjustment` | Duyệt – Phiếu điều chỉnh tồn | **Phiếu điều chỉnh tồn** |
| 9910 | `#/wf-inbox?type=scm-stockcount` | Duyệt – Phiếu kiểm kê | **Phiếu kiểm kê** |
| 9911 | `#/wf-inbox?type=prod-plan` | Duyệt – Kế hoạch sản xuất theo ngày/ca | **Kế hoạch sản xuất theo ngày/ca** |
| 9912 | `#/wf-inbox?type=qc-inspection` | Duyệt – Phiếu QC đầu vào | **Phiếu QC đầu vào** |
| 9913 | `#/wf-inbox?type=hr-leave` | Duyệt – Đơn xin nghỉ | **Đơn xin nghỉ** |
| 9914 | `#/wf-inbox?type=hr-swap` | Duyệt – Đơn đổi ca | **Đơn đổi ca** |
| 9915 | `#/wf-inbox?type=wf-inbox` | Duyệt – Hộp phê duyệt tập trung | **Hộp phê duyệt tập trung** |
| 9916 | `#/wf-inbox?type=scm-po-director` | Duyệt – Đơn mua hàng (Giám đốc) | **Đơn mua hàng (Giám đốc)** |
| 9917 | `#/wf-inbox?type=scm-pr-l1` | Duyệt – Đề nghị mua hàng (cấp 1 – Trưởng bộ phận Mua hàng) | **Đề nghị mua hàng (cấp 1 – Trưởng bộ phận Mua hàng)** |
| 9918 | `#/wf-inbox?type=scm-pr-l2` | Duyệt – Đề nghị mua hàng (cấp 2 – Giám đốc vận hành) | **Đề nghị mua hàng (cấp 2 – Giám đốc vận hành)** |
| 9919 | `#/wf-inbox?type=scm-stockcount-l2` | Duyệt – Phiếu kiểm kê (cấp 2 – Kế toán) | **Phiếu kiểm kê (cấp 2 – Kế toán)** |

- **MENU-1.1 Seed:** `DB/seed/ObjectMenu_seed.sql` – 19 câu INSERT dùng tên mới; thêm khối **UPDATE idempotent** cho DB đã có: `UPDATE dbo.ObjectMenu SET Name = SUBSTRING(Name, 9, 500), ModifiedDate = @Now, ModifiedBy = @SeedBy WHERE MetaLink LIKE N'#/wf-inbox?type=%' AND Name LIKE N'Duyệt – %' AND IsDeleted = 0` (chạy lại không đổi gì; kiểm đúng độ dài tiền tố "Duyệt – " gồm dấu gạch dài + khoảng trắng; không đụng mục ngoài nhóm Phê duyệt). Ghi script dữ liệu riêng `DB/scripts/MENU1_rename_approve_menu.sql` (chế độ chạy thử in trước / sau).
- **MENU-1.2 Code nhắc tên cũ:** rà `"Duyệt – "` trong BE (vd `ApprovalRequestService` – tên bước duyệt = tên mục menu, chú thích dòng 343), FE (`hr.js` wf-inbox, ma trận quyền `adm-permission`, `catalog.js`), bot (`ApprovalActions` …), test (kỳ vọng tên bước) → dùng tên mới; tên bước của **yêu cầu duyệt đã tạo trước** giữ nguyên (không sửa dữ liệu lịch sử), chỉ yêu cầu mới mang tên mới. Hộp phê duyệt / bot hiển thị cột "Bước duyệt" vẫn rõ nghĩa (có thể ghép "Duyệt: " khi hiển thị nếu cần – không lưu vào tên menu).
- **MENU-1.3 Trùng tên:** sau khi bỏ tiền tố, một số tên trùng tên màn nghiệp vụ ở nhóm khác (vd "Đơn mua hàng", "Đề nghị mua hàng", "Phiếu kiểm kê", "Đơn đổi ca") – ma trận phân quyền `adm-permission` phải hiện **nhóm cha** (Phê duyệt / Mua hàng & Kho …) cạnh tên để không nhầm; kiểm tra màn tìm menu / tìm kiếm toàn cục cũng hiện nhóm cha.
- **MENU-1.4 Tài liệu:** sửa "Duyệt – …" trong `docs/user-guide/01_QUAN_TRI.md`, `12_QUAN_LY_DUYET.md`, `13_BAN_DIEU_HANH.md`, `20_BOT_TELEGRAM.md`, `90_CHENH_LECH.md`, `README.md`; ORG_STRUCTURE / BOT_TELEGRAM nếu có.

**AC:** menu nhóm Phê duyệt hiện đúng 19 tên mới (không còn "Duyệt – "); quyền của các nhóm không đổi (so số dòng UserRole trước / sau = nhau); script chạy lại lần 2 không đổi gì; ma trận phân quyền phân biệt được mục trùng tên nhờ nhóm cha; test xanh; HDSD cập nhật.

## ★ UI-2R – BỐ CỤC 1/3 – 2/3 + KHỐI THÔNG TIN RÚT GỌN CHO 3 MÀN CHI TIẾT (D-69, anh chốt 2026-11-22) · `DONE` 2026-11-21 (ảnh: `docs/frontend-handoff/assets/ui-2r/`)

> Nguồn: DECISIONS **D-69**; mô phỏng (mở bằng trình duyệt, bấm được nhóm gập): `docs/frontend-handoff/assets/customer-360-mockup.html`, `supplier-360-mockup.html`, `employee-360-mockup.html`. Chỉ sửa **tab Tổng quan** của `crm-customer-360` (UI-2), `scm-supplier-360` (UI-3), `adm-employee-360` (UI-4); phần đầu, thẻ chỉ số, các tab khác giữ nguyên. Claude Code làm FE (D-65); BE không đổi (dữ liệu đã có trong `GetOverview` / `GetById` – thiếu trường nào thì chỉ **thêm** trường trả về, không đổi trường cũ). Không thêm bảng / cột.

- **UI-2R.1 Thành phần chung khối thông tin** (`core/ui.js`, vd `UI.infoPanel({ key, title, onEdit, core: [{icon, html}], groups: [{key, title, summary, rows: [[label, value]], locked, open}] })` + `app.css`): theo D-69 b – 6 dòng cốt lõi, nhóm gập có dòng tóm tắt, nhớ nhóm mở theo `key` màn (localStorage trong try/catch), ẩn ô trống, nhóm trống "Chưa có thông tin · Bổ sung" (chỉ khi có quyền sửa), `tel:` / `mailto:`, nút sao chép, sticky; bàn phím: tiêu đề nhóm là `button` (Enter / Space mở gập, `aria-expanded`).
- **UI-2R.2 Bố cục hai cột chung** (`app.css`): `.dc-two{grid 1fr 2fr; gap 24px}`, `.dc-pair{grid 2 cột}`; < 1200px xếp dọc, tắt sticky.
- **UI-2R.3 Khách hàng 360** – tab Tổng quan theo D-69 c–d (khách cá nhân: nhóm Pháp lý theo D-69 c; ngày sinh / CCCD theo quyền). Bỏ khối thông tin / bố cục cũ.
- **UI-2R.4 Nhà cung cấp** – tab Tổng quan theo D-69 e–f.
- **UI-2R.5 Nhân viên** – tab Tổng quan theo D-69 g–h (nhóm Cá nhân chỉ hiện khi API trả dữ liệu cá nhân; không có thì không hiện nhóm, không hiện ô trống).
- **UI-2R.6 Kiểm tra + tài liệu:** build FE xanh; mở thử 3 màn trên DB dev (1 khách DN + 1 khách CN, 1 NCC có PO trễ, 1 nhân viên bếp bằng tài khoản NS và tài khoản tổ trưởng) ở 3 cỡ màn hình; **chụp ảnh** tab Tổng quan 3 màn đặt cạnh 3 file mô phỏng để so (ghi đường dẫn ảnh trong PROGRESS); cập nhật `docs/user-guide/02_KINH_DOANH.md`, `04_MUA_HANG.md`, `11_NHAN_SU.md` (mục màn chi tiết).

**AC:** tab Tổng quan 3 màn đúng bố cục 1/3 – 2/3 và thứ tự khối như mô phỏng; cột trái mặc định ≤ ~10 dòng (6 cốt lõi + tiêu đề nhóm), nhóm gập / mở được và được nhớ sau khi tải lại; không có dòng "–" / ô trống; thông tin cá nhân chỉ người có quyền thấy (kiểm cả API); màn < 1200px xếp dọc không vỡ; cả 3 màn dùng **cùng một** thành phần khối thông tin; build FE xanh, test BE xanh.

## ★ UI-5 – HOÀN THIỆN FORM KHÁCH HÀNG (D-63 r, anh chốt 2026-11-22) · `DONE` 2026-11-21 (đã gộp `main` 2026-11-21; script `DB/scripts/UI5_customer_catalogs.sql` anh chạy sau backup)

> Nguồn: rà soát form tạo mới khách hàng của chat tài liệu 2026-11-22 (form theo loại ở `core/ui.js` `CUSTOMER_FIELDS`, kiểm ở `CustomerService.CheckRules`). Claude Code làm cả BE + FE (D-65). Không thêm bảng / cột. Làm ngay sau task đang dở (mục ▶ THỨ TỰ LÀM TIẾP).

- **UI-5.1 NV phụ trách:** `UserAssignId` đang là ô gõ GUID ("Tham chiếu chưa xác định bảng đích (GAP)") → ô chọn **người dùng / nhân viên đang hoạt động** có tìm kiếm (xác định bảng đích của `Customer.UserAssignId` theo code cũ – User hoặc Employee – ghi vào `Store.target` / schema meta; máy chủ kiểm tồn tại).
- **UI-5.2 Ô ngày:** Ngày sinh, Ngày cấp CCCD, Ngày cấp ĐKKD (cột `datetime`) → ô **chỉ chọn ngày** (`type="date"`), gửi `yyyy-MM-dd`. Áp chung: cột datetime có tên `*Date` / `DateOfBirth` mà nghiệp vụ là ngày → ô ngày (danh sách cột trong một chỗ).
- **UI-5.3 Danh mục (D-63 r):** Ngành, Quy mô, Giới tính, Nguồn → ô chọn từ danh mục (catalog FE + hằng số BE dùng chung mã); máy chủ từ chối mã ngoài danh mục (thông báo tiếng Việt mới trong `ConstantHelper.Message`); chuẩn hóa dữ liệu cũ bằng script dữ liệu idempotent (chạy thử in trước).
- **UI-5.4 Chi nhánh phục vụ:** chỉ liệt kê đơn vị **loại chi nhánh** (`UnitType = BRANCH`), chữ "— Chọn chi nhánh —" (bỏ tên bảng tiếng Anh ở **mọi** ô chọn tham chiếu: dùng nhãn tiếng Việt của bảng); hiện dấu \* khi hệ thống đã có chi nhánh (lấy từ API – vd thêm cờ `branchRequired` vào `Common/GetCodeRules` hoặc API cấu hình form); có đúng 1 chi nhánh → chọn sẵn.
- **UI-5.5 Trạng thái:** *Đang hoạt động / Ngừng hoạt động* (`Active` / `Inactive`), mặc định Đang hoạt động, bỏ lựa chọn trống. Áp chung cho ô Status Active/Inactive của form sinh tự động.
- **UI-5.6 Chọn loại trước:** form tạo mới mở ra với **2 nút lớn Doanh nghiệp / Cá nhân**, chưa chọn sẵn; chọn xong mới hiện các ô theo loại (đổi loại vẫn được trước khi lưu, giữ dữ liệu đã nhập như hiện tại). Form sửa: giữ khóa loại (D-63 i).
- **UI-5.7 Kiểm tại ô:** kiểm MST (10 số / 10-3 số; cá nhân 10 số), CCCD 12 số, SĐT (`0`+9 số / `+84`+9 số, cho phép dấu cách / chấm / gạch), email, ngày (không sau hôm nay; ngày cấp CCCD ≥ ngày sinh) **khi rời ô**, lỗi đỏ dưới đúng ô; lỗi máy chủ trả về vẫn hiện đầu form và tô đỏ ô liên quan nếu nhận ra tên trường. Quy tắc giống hệt máy chủ (một bộ regex, ghi chú nguồn).
- **UI-5.8 Liên hệ chính + điểm giao đầu tiên (D-63 r):** BE – `Customer/Insert` (khách doanh nghiệp) tự tạo `CustomerContact` IsPrimary từ Người liên hệ / SĐT / email người liên hệ; nhận thêm khối tùy chọn `firstLocation` (tên, địa chỉ, giờ nhận từ – đến; từ < đến) → tạo `CustomerDeliveryLocation` trong **cùng transaction**. FE – khối **Điểm giao đầu tiên** (thu gọn, mở khi bấm "Thêm điểm giao ngay"). Sau khi lưu, báo "Đã tạo KHB-… · 1 liên hệ chính · 1 điểm giao".
- **UI-5.9 Nhỏ:** nhập Tên doanh nghiệp → tự điền Tên giao dịch khi ô này còn trống (vẫn sửa được).
- **UI-5.10 Kiểm tra + tài liệu:** test BE (danh mục sai mã, tạo liên hệ chính, điểm giao lỗi giờ → không tạo khách – rollback); build FE xanh; mở thử tạo 1 khách doanh nghiệp + 1 khách cá nhân trên DB dev; cập nhật `docs/user-guide/02_KINH_DOANH.md` (mục tạo khách hàng: thao tác, bảng trường bắt buộc / tùy chọn theo loại, lỗi thường gặp).

**AC:** không còn ô gõ GUID / chữ tiếng Anh / tên bảng trên form khách hàng; ô ngày chỉ chọn ngày; Ngành / Quy mô / Giới tính / Nguồn chỉ chọn trong danh mục (máy chủ chặn mã lạ); chi nhánh chỉ hiện chi nhánh, có dấu \* đúng điều kiện; phải chọn loại trước khi thấy các ô; lỗi định dạng hiện ngay tại ô; tạo khách doanh nghiệp sinh liên hệ chính, có điểm giao đầu tiên thì sinh điểm giao cùng lúc; test BE + build FE xanh; HDSD cập nhật.

## ★ UI-3 – MÀN CHI TIẾT NHÀ CUNG CẤP (D-67, anh chốt 2026-11-19) · `DONE` 2026-11-19 (nhánh `feat/MA-1-code-rules`; đã mở thử bằng Chrome headless trên DB dev – 1440px + 390px)

> Nguồn: DECISIONS **D-67**; mô phỏng `docs/frontend-handoff/assets/supplier-360-mockup.html`. Cùng khung với UI-2 (dùng lại thành phần phần đầu / thẻ chỉ số / hàng tab / khối Cần chú ý của UI-2 – tách thành hàm dùng chung). Làm sau UI-2. Claude Code làm cả BE + FE (D-65). Không thêm bảng / cột.

- **UI-3.1 BE:** `Supplier/GetOverview` thêm trường theo D-67 f(1) (cảnh báo: mã, mức, nội dung tiếng Việt, liên kết màn); **mới** `Supplier/GetTrend`; `PurchaseOrder/GetAll` thêm `FromOrderDate` / `ToOrderDate`, `GoodsReceipt/GetAll` thêm `FromReceiptDate` / `ToReceiptDate`. Test từng phần; `full-flow.http` thêm bước gọi API mới.
- **UI-3.2 FE – khung màn:** đăng ký `scm-supplier-360` (ẩn menu), mở từ dòng danh sách NCC; phần đầu + nút theo quyền + 4 thẻ chỉ số + hàng tab (D-67 b–c); hộp Tổng quan hiện tại thêm nút **Mở hồ sơ đầy đủ**.
- **UI-3.3 FE – tab Tổng quan:** 6 khối theo D-67 d (bố cục như mô phỏng; màn hẹp xếp dọc).
- **UI-3.4 FE – các tab còn lại:** theo D-67 e; tab Bảng giá có lọc "đang hiệu lực / tất cả", cột so giá thấp nhất của NCC khác cùng nguyên liệu, nút **Thêm giá NL** / **Ưu tiên** (theo quyền sửa); tab Sự cố có nút chuyển trạng thái như `qc-supplier-issue`; bấm dòng mở chứng từ.
- **UI-3.5 Kiểm tra + tài liệu:** build FE xanh, test BE xanh; mở thử 1 NCC có PO trễ / sự cố / lô tạm giữ trên DB dev, 3 cỡ màn hình; tab ẩn theo quyền; cập nhật `docs/user-guide/04_MUA_HANG.md` (mục NCC).

**AC:** bấm NCC ở danh sách → mở màn chi tiết; số liệu khớp màn gốc (PO, phiếu nhập, lô, sự cố); khối Cần chú ý đúng điều kiện; tab ẩn theo quyền; build FE + test BE xanh.

## ★ UI-4 – MÀN CHI TIẾT NHÂN VIÊN (D-68, anh chốt 2026-11-19) · `DONE` 2026-11-19 (nhánh `feat/MA-1-code-rules`; mở thử Chrome headless trên DB dev – 1440px + 390px)

> Nguồn: DECISIONS **D-68**; mô phỏng `docs/frontend-handoff/assets/employee-360-mockup.html`. Dùng lại thành phần chung tách ở UI-2 / UI-3. Làm sau UI-3. Claude Code làm cả BE + FE (D-65). Không thêm bảng / cột (GAP-HR-06, GAP-HR-07 chờ anh).

- **UI-4.1 BE:** **mới** `Employee/GetOverview` theo D-68 g(1) – thông tin cá nhân lọc theo quyền Sửa `adm-employee` ở máy chủ (D-68 e); `ShiftAssignment/GetAll` thêm `FromDate` / `ToDate`. Test (kể cả test người không có quyền Sửa không nhận ngày sinh / địa chỉ); `full-flow.http` thêm bước gọi.
- **UI-4.2 FE – khung màn:** đăng ký `adm-employee-360` (ẩn menu), mở từ dòng danh sách nhân viên; phần đầu + nút theo quyền + 4 thẻ chỉ số + hàng tab (D-68 b–c). Nút **Tạo mã liên kết bot** dùng chung thành phần của FE-E.1 (hiện mã + hạn 72 giờ + link / QR).
- **UI-4.3 FE – tab Tổng quan:** 6 khối theo D-68 d.
- **UI-4.4 FE – các tab còn lại:** Lịch ca (lưới tuần, ‹ › , bấm ô mở phân ca như `hr-shift-assign`), Chấm công (bảng công + chi tiết theo khoảng ngày), Nghỉ phép / Đổi ca (danh sách + Duyệt / Từ chối / Hủy theo quyền), Công việc (`Common/GetRelated`), Tài khoản và bot, Tài liệu (loại "Giấy khám sức khỏe" có sẵn trong danh sách loại – GAP-HR-07), Lịch sử.
- **UI-4.5 Kiểm tra + tài liệu:** build FE xanh, test BE xanh; mở thử với 1 nhân viên bếp + 1 tài xế trên DB dev bằng tài khoản NS và tài khoản tổ trưởng (không thấy thông tin cá nhân); cập nhật `docs/user-guide/11_NHAN_SU.md`.

**AC:** bấm nhân viên ở danh sách → mở màn chi tiết; số ca / giờ công / vắng / muộn khớp bảng công; thông tin cá nhân chỉ người có quyền Sửa hồ sơ thấy (kiểm cả API); tab ẩn theo quyền; build FE + test BE xanh.

## ★ FE-E – VIỆC FRONTEND TỒN ĐỌNG (D-65, anh chốt 2026-11-19) · `DONE` 2026-11-19 (FE-E.4 làm cùng UG-1) – FE-E.1 → FE-E.3 xong 2026-11-19 (QR: D-70; quyền màn tích hợp: D-71 chờ anh); còn FE-E.4 (giao diện UG-1)

> Claude Code làm toàn bộ FE (D-65). Làm sau UI-1. Giao diện ở engine `src/assets/delicare/js` (D-65 b). Kiểm tra theo D-65 c.

- **FE-E.1 = FE-BOT-1 (+ UG-1 C-02, C-23 phần giao diện):** menu người dùng (góc phải) thêm **Liên kết Telegram**: trạng thái (`TelegramLink/GetMine`), **Tạo mã** (mã + hạn 15′ + link `t.me/<bot>?start=<mã>` + ảnh QR), **Hủy liên kết** (`Unlink`). `adm-employee`: nút **Tạo mã liên kết bot** trên dòng (`CreateCodeForEmployee`, mã 72 giờ) – chỉ hiện khi có quyền Sửa `adm-employee`.
- **FE-E.2 = FE-BOT-3:** ảnh QR trên phiếu in (nhập, xuất, kiểm kê, giao, POD, đối soát) và **in QR hàng loạt** cho cổng / dây chuyền / xe / khu kho / khu vệ sinh (`CHAMCONG`, `LINE_`, `VEH_`, `LOC_`, `CLEAN`) từ link `start=…` do BE trả; vẽ QR phía trình duyệt (không thêm thư viện ngoài nếu engine đã có; nếu chưa có → hỏi theo mẫu D-49).
- **FE-E.3 = FE-BOT-2 (+ UG-1 C-13):** bỏ ẩn và hoàn thiện `int-channel`, `int-bot`, `int-identity`, `int-outbound` (nút **Gửi lại** tin lỗi), `int-log` – chỉ nhóm quản trị (FIN-IT); cảnh báo OUTBOUND_FAILED mở đúng `int-outbound`. Hộp thư hội thoại / dòng thời gian: làm khi BE có API (Catalog 216–220), chưa có → ghi GAP, không tự thêm bảng.
- **FE-E.4 = phần giao diện UG-1:** các mục C-xx của `docs/user-guide/90_CHENH_LECH.md` có sửa giao diện (C-01, C-06, C-07 web, C-08, C-09, C-11, C-12, C-15, C-16, C-20, C-05) – làm cùng phần BE của từng mục trong UG-1; đánh ✅ trong file 90 khi xong.

**AC:** mỗi mục: `npm run build` xanh, mở thử được trên dữ liệu thật (DB dev), quyền đúng (ẩn nút khi không có quyền, máy chủ vẫn chặn), HDSD cập nhật; mục BE kèm theo: test xanh, `full-flow.http` xanh.

## ★ UG-1 – RÀ SOÁT VÀ SỬA CHÊNH LỆCH TÀI LIỆU – CODE (từ hướng dẫn sử dụng, 2026-11-19) · `DONE` 2026-11-19 trừ C-22 (anh chọn chức năng bot làm tiếp)

> Nguồn: `docs/user-guide/90_CHENH_LECH.md` (C-01 … C-23, có file:dòng và đề xuất). Bộ hướng dẫn sử dụng theo vai trò ở `docs/user-guide/` (README, 00–13, 20, 91) viết theo code ngày 2026-11-19.
> **Thứ tự:** làm sau BOT-2.9 đang dở, trừ các mục **Cao** (C-01, C-02, C-06, C-07, C-08) được chen ngay khi anh đồng ý. Mục ghi **Anh** (C-17, C-18, C-19, C-22, C-23) **chỉ làm khi anh chốt** (ghi D-63 trở đi vào DECISIONS.md). Không tạo bảng / cột / FK / schema; quyền chỉ sửa bằng seed `UserRole_seed.sql`.

- **UG-1.1 (Cao, code):** C-07 bot "Xong mẻ" ghi sản lượng + nút **Ghi sản lượng** cho mẻ Chờ QC trên web; C-08 hộp QC đầu vào thêm **Số lượng đạt** (HOLD) và **Xác nhận không rã đông** (FROZEN có điều kiện).
- **UG-1.2 (Cao, code):** C-02 giao diện liên kết Telegram (menu người dùng: tạo mã / trạng thái / hủy liên kết; `adm-employee`: tạo mã cho nhân viên); C-06 form người dùng có **Nhóm quyền** + hiện mật khẩu tạm một lần; C-01 nút **Yêu cầu sửa sau chốt** gọi `Order/RequestChange`.
- **UG-1.3 (TB, code):** C-09, C-11, C-12, C-15, C-16, C-20, C-05, C-13 (theo đề xuất từng mục).
- ✅ **UG-1.4 (code + tài liệu) – DONE 2026-11-19 (STATUS_CATALOG.md chờ anh):** C-10 đồng bộ `data/catalog.js` và `STATUS_CATALOG.md` theo `*Status.cs`; C-03, C-04, C-14, C-21; sửa REVIEW_BUSINESS_FLOWS.md cho khớp code.
- ✅ **UG-1.5:** C-17, C-18, C-19, C-23 xong (D-69); **C-22 chờ anh** chọn chức năng bot.

**AC:** mỗi mục C-xx đã xử lý được đánh ✅ trong `90_CHENH_LECH.md` kèm commit / ghi chú; đoạn hướng dẫn liên quan (cột "Ảnh hưởng HDSD") được sửa cho khớp; build FE / BE xanh, test xanh, full-flow không giảm; mục chờ anh có số D-xx trước khi làm.


## ★ ĐỢT BOT-3 – CHỨC NĂNG BOT CÒN THIẾU (UG-1 C-22, anh chọn cả 4 nhóm 2026-11-19 – D-72) · `DONE` 2026-11-20

> Nguồn: BOT_TELEGRAM §3.2 / §3.3 (mục ghi *(kế hoạch)*). Mỗi chức năng = API web có sẵn gọi bằng JWT của chính người dùng (quyền như web); chỗ thiếu API mới thêm BE.

- ✅ **BOT-3.1 – Xem nhanh (API có sẵn) · DONE 2026-11-19:** 🥬 NL của ca (`/nlca`), ⚠️ Thiếu hụt (`/thieuhut`), 🔍 Truy xuất lô / mẻ (`/truyxuatlo` + nút ở Bếp / QA – GlobalSearch → MaterialLot/Trace · ProductionBatch/Trace), 🧭 Lịch xe (`/lichxe`), 📦 Hộp khay hôm nay (`/hopkhay`), 📅 Thực đơn tuần (`/thucdontuan`), 📋 Việc tôi đã báo (`/viecdabao` – thêm `CustomerComplaint/GetList?mine=true`), 🧾 Công tháng (`/congthang`, KT + NS), ⏳ Đơn đang chờ (`/donchoduyet`), 🆔 `/chatid` (chat riêng + trong nhóm, chỉ quản trị). "Chuyến trực tiếp" = 🗺 Giao vận hôm nay → thẻ chuyến (đã có). Test `MessagingBot3Tests`.
- ✅ **BOT-3.2 – Thao tác ghi · DONE 2026-11-20 (thêm API `DeliveryRoute/Reassign`, `ShiftAssignment/GetGrid myUnit`):** 🔁 Đổi xe / tài xế (`DeliveryRoute/Update` khi chuyến Kế hoạch); ✅ Điểm phát nhận hàng / ký nhận (`DeliveryStop/Deliver`); 🧼 Lô vệ sinh hộp khay (`CleaningBatch/Create · RecordResults · Complete`); 🔄 Lập lại kế hoạch (`ProductionPlan/Generate`, xác nhận 2 bước); ✏️ / 🔄 Đề xuất đổi món – ý kiến khách (`MenuChangeRequest/Create`); 📣 Báo thiếu suất / phản ánh thay khách (luồng 📣 Ghi phản ánh cho Kinh doanh); 🍱 Chia suất / thừa (`PackingOrder/RecordResult`, `ProductionBatch/RecordWaste`); 👥 Ai chưa vào **tổ tôi** + ✍️ Chấm hộ cho tổ trưởng (lọc theo đơn vị của người dùng).
- ✅ **BOT-3.3 – Cần API mới · DONE 2026-11-20 (`ImportExcel` loại AttendanceMachine + bot nhận file, `ProductionBatch/CreateExtra`, job `PeriodReport`):** 📥 Nhập file máy chấm công (chưa có API nhập – thêm loại nhập ImportExcel cho AttendanceEvent); 🍲 Nấu thêm (chưa có API tạo mẻ bổ sung); 📊 Báo cáo tuần / tháng gửi CEO (job + nội dung bản tin).

## ★ ĐỢT BOT – BOT TELEGRAM NỘI BỘ (anh chốt 2026-11-18, D-62) · `DOING` – ƯU TIÊN HIỆN TẠI (BOT-1.1 → 1.7 (gồm 1.7d), 2.1 → 2.4 DONE; còn BOT-2.9 – cần server test + token bot thật)

> **Tài liệu bắt buộc đọc trước:** `docs/claude/BOT_SOURCE_ARCHITECTURE.md` (kiến trúc ứng dụng + source – **làm đúng cây thư mục, tên project, tên container**) và `docs/claude/BOT_TELEGRAM.md` (tính năng theo bot / người dùng, trải nghiệm, danh mục thông báo, mức rủi ro duyệt, leo thang). Thay T40 (bỏ phụ thuộc T33) và phần Telegram của T23.2.
> **Luật chung của đợt:**
> - Khung cũ D-30 (`api/<Controller>/<Action>`, Controller → Service, `Result<T>`, `[FromForm]`), quyền ObjectMenu D-31, **không bảng / cột / FK / schema mới** (thiếu → GAP PROPOSED ở DECISIONS D-62), SQL Server 2022 (D-61), không API tra cấu trúc DB (D-58), **không thêm gói NuGet** (cần thì hỏi anh).
> - **`AppDelicare.Messaging` không ProjectReference nào** (không Services / Data / Model), không chuỗi kết nối DB; mọi việc gọi `api` qua HTTP. Tất cả bot (VH, DH; sau này Zalo, Facebook) chạy chung **1 container `messaging`**. Bot Trợ lý (Hermes) **không** thuộc đợt này.
> - Bot gọi API nghiệp vụ bằng **JWT của chính user** (token exchange 10′); Service lõi chỉ ghi `OutboundMessage` (mã mẫu tin + dữ liệu, outbox trong transaction); câu chữ ở `AppDelicare.Messaging/Presentation`.
> - Bí mật (token bot, secret webhook, khóa client, khóa ký mã) chỉ ở user-secrets / env – repo chỉ có placeholder.
> - Mỗi task: nhánh `feat/BOT-x.y-<slug>`, build 0 lỗi, test xanh, file `docs/api-tests/bot-*.http` chạy được trên DB dev (payload update Telegram giả lập), handoff `docs/frontend-handoff/BOT-x.y.md` khi có API / màn cho FE, cập nhật Catalog cột J / K / L / M (dòng 214–220, 85, 113, 162, 181, 204 khi chạm tới), câu hỏi → mẫu D-49 vào DECISIONS khối D-62, làm theo "Đề xuất". **Không push / merge khi anh chưa duyệt.**
> - Test thật với Telegram: dùng **bộ bot DEV** chế độ **Polling** trên máy anh (token anh tự đặt vào user-secrets); test tự động dùng client Telegram giả.
> **Thứ tự:** BOT-1.1 → 1.2 → 1.3 → 1.4 → 1.5 → 1.6 → 1.7 (R1 cốt lõi) → BOT-2.1 → 2.2 → 2.3 → 2.4 → 2.9 (R1 hoàn thiện). BOT-3.x sau go-live.

| Task | Nội dung | Tiêu chí nghiệm thu (AC) |
|---|---|---|
| **BOT-1.1** · `DONE` 2026-11-18 (đã gộp `release/pilot` + `main` @ f297747 – anh duyệt; PROGRESS BOT-1.1) · Lõi: kênh tin nhắn (api) | Seed `DB/seed/Integration_Telegram_seed.sql` (add-only): `IntegrationChannel` mỗi bot 1 dòng (`TELEGRAM_VH`, `TELEGRAM_DH`, `TELEGRAM_TRO_LY`, theo `EnvironmentCode`), `BotConfiguration` (`VH` / `DH` / `TRO_LY`, `SecretReference` = tên biến môi trường), ObjectMenu `#/bot-van-hanh`, `#/bot-dieu-hanh` (+ UserRole theo ma trận ORG_STRUCTURE, PROPOSED). `ClientKeyFilter` (`X-Client-Key`, so hash `Clients:<code>:KeyHash`). `BotChannelController` (nội bộ): `ReceiveUpdate`, `ClaimInbound`, `CompleteInbound`, `ClaimOutbound`, `MarkOutbound`. `Services/Implement/Messaging`: `BotChannelService` (chống trùng `IdempotencyKey` bằng `UPDLOCK, HOLDLOCK`; claim `READPAST`; trạng thái mới PROPOSED: Inbound `Processing` / `Ignored`, Outbound `Sending` / `Blocked` / `Cancelled`; `IntegrationLog` IN / OUT), `NotificationService.Enqueue` (outbox trong transaction đang mở, `PayloadJson = {template, data, buttons, scope, level, notBefore?}`, `CorrelationId` = mã sự việc), `NotificationCatalog` (khung + vài mẫu đầu), `RecipientResolver`, `MessagingRules`. API web: `InboundMessage/Reprocess` (216), `OutboundMessage/Enqueue` (217, gửi tay), `Retry` (218), `GetConversation` (219). Che `ConfigJson` / bí mật ở các controller CRUD tích hợp; `IntegrationLog` chỉ đọc. Nginx: `location /api/BotChannel/ { return 404; }`. | Gửi trùng update → 1 dòng; khóa client sai → 401; claim 2 lần song song không trả trùng dòng; rollback nghiệp vụ → không có `OutboundMessage`; API CRUD không trả token / secret; `.http` chạy được; test xanh |
| **BOT-1.2** · `DONE` 2026-11-18 (đã gộp `release/pilot` + `main` @ f297747 – anh duyệt; PROGRESS BOT-1.2) · Lõi: định danh, liên kết, token | `Services/Implement/Identity`: `LinkCodeSigner` (HMAC, base64url ≤ 64, hạn cấu hình), `PhoneMatcher` (chuẩn hóa +84 / 0; khớp **duy nhất** `Employee.Phone` / `Person.PhoneNumber` → User), `TelegramLinkService`, `UserTokenIssuer` (JWT 10′ của chính user qua `JwtFactoryService` cũ, claim `client=MESSAGING`; kiểm User `IsActive`, `IdentityStatus = Active`), `ClientCredentialService`. `BotChannel`: `LinkByCode`, `LinkByPhone`, `ResolveIdentity`, `IssueUserToken`, `TouchIdentity` (LastSeenAt, Blocked), `Employee*` (lịch ca / chấm công / đơn của chính mình cho người chỉ có EmployeeId – ghi dưới `system`, ghi chú nguồn). `TelegramLinkController`: `CreateCode` (user cho mình), `CreateCodeForEmployee` (HR – quyền Sửa trên màn hồ sơ nhân viên, MetaLink lấy theo ObjectMenu hiện có), `GetMine`, `Unlink`; `UserExternalIdentity/Link`, `Unlink` (admin, Catalog 220). Quy tắc: người có quyền duyệt (mục A) hoặc IsAdmin **bắt buộc** thêm mã web sau khi khớp số điện thoại (BOT_TELEGRAM §2). | Mã hết hạn / sửa chữ ký → từ chối; số khớp 0 hoặc > 1 hồ sơ → từ chối, gợi ý liên hệ HR; người có quyền duyệt chỉ khớp số → chưa được dùng chức năng duyệt cho tới khi nhập mã; User bị khóa → không cấp token; mỗi lần cấp token có `IntegrationLog` TOKEN; test xanh |
| **BOT-1.3** · `DONE` 2026-11-18 (đã gộp `release/pilot` + `main` @ e8b2d8c; Docker thật + bot DEV thật chưa chạy – máy chưa có Docker / token; PROGRESS BOT-1.3) · Container `messaging` (khung) | Project `AppDelicare.Messaging` (thêm vào `.sln`) đúng cây §5.1: `Program.cs` (`POST /tg/{botCode}`, `/health`, `--healthcheck`), `Options/`, `Channels/Telegram/*` (client, parser, secret validator, webhook endpoint → `ReceiveUpdate` → 200 / 503, polling, startup setWebhook), `Core/*` (`DelicareApiClient`, `BotChannelApi`, `UserTokenCache`, `BotUserContext`, `Contracts/`), `Processing/*` (`InboundPump` xử lý ngay + quét 5″, `UpdateDispatcher`, `OutboundPump`: giới hạn 25 tin/giây, 1 tin/giây/chat, 429 `retry_after`, 403 → Blocked, thử lại giãn cách), `Presentation/` (mẫu trung tính + `TelegramFormatter`), `Audiences/Internal/Chung/OnboardingActions` + `HelpActions` (đăng ký QR cổng + chia sẻ số điện thoại, `/start <mã>`, `/menu`, `/huylienket`). Dockerfile; thêm service `messaging` (1 bản, `restart: unless-stopped`, healthcheck) vào `deploy/docker/docker-compose.production.yml`, `docker-compose.staging.yml` **và `docker-compose.yml` ở gốc repo** (server dev / test; `.env.example` gốc thêm biến placeholder); nginx `/tg/` → `messaging:8080`; `*.env.example` thêm biến (placeholder). Test `AppDelicare.Tests/Messaging` với client Telegram giả + API giả. | Không có ProjectReference nào trong `AppDelicare.Messaging.csproj`; không có chuỗi kết nối DB; secret sai → 401, không ghi gì; tắt `api` → webhook trả 503; khởi động lại `messaging` không mất tin (quét Claim); polling chạy được trên máy anh với bot DEV; đăng ký bằng số điện thoại chạy hết luồng; test xanh |
| **BOT-1.4** · `DONE` 2026-11-18 (đã gộp `release/pilot` + `main` @ 2c449b1; chấm công vị trí làm luôn ở đây; thẻ duyệt chủ động phát ở BOT-1.7; PROGRESS BOT-1.4) · Khung trải nghiệm + duyệt | `Experience/`: `KeyboardBuilder` (3 nút cố định; ≤ 6 chức năng → phẳng), `MenuNavigator` (phân hệ → chức năng bằng sửa tin, ⬅️), `ContextKeyboard`, `CodeSearch` (tiền tố mã theo cấu hình, lấy đúng tiền tố trong code), `CommandScopeSync` (`setMyCommands` theo chat), `InputWidgets` (nút số, bước ±, checklist bấm trong 1 tin), `TaskCard` (nút trên thẻ; sửa đồng loạt theo `CorrelationId` – `api` trả danh sách tin cùng sự việc). `Audiences/Internal/BotAction` + `BotActionRegistry`. `Chung/ApprovalActions`: chờ tôi duyệt (`ApprovalRequest/GetMyPending` – chỉ bước người đó đúng là người duyệt), chi tiết, duyệt **theo mức rủi ro** (BOT_TELEGRAM §4: thấp = 1 chạm + `UndoWindow` 10″ + duyệt hàng loạt; cao = 2 bước; từ chối = 2 bước + lý do qua ForceReply / `ReplyContext`). `Callback/CallbackData` ≤ 64 byte. | Admin toàn quyền vẫn chỉ 3 nút, tới mọi chức năng ≤ 2 lần bấm; người ≤ 6 chức năng thấy bàn phím phẳng; lệnh "/" mỗi chat chỉ gồm lệnh được phép; bấm duyệt 2 lần → 1 quyết định; hoàn tác trong 10″ → không gọi API; thẻ duyệt ở mọi người cùng cấp đổi trạng thái; nút của người không có quyền → API từ chối, bot báo rõ; test xanh |
| **BOT-1.5** · **1.5a `DONE`** 2026-11-18 (chung: lịch ca, xin nghỉ, QR cổng; Bếp; ATTP – nhánh `feat/BOT-1.5a-vanhanh-bep-attp`) · **1.5b `DONE`** 2026-11-18 (Giao vận: chuyến của tôi, xuất phát, đến điểm, giao, không giao được, nhận hộp về, giao vận hôm nay; Điểm phát: xe đến hôm nay, đối soát suất – nhánh `feat/BOT-1.5b-giaovan-diemphat`) · **1.5c `DONE`** 2026-11-18 (đổi ca + api GetSwapCandidates, phản ánh, ai chưa vào ca / chấm hộ, chức năng chỉ xem Kho / Kế hoạch / Kinh doanh / Kế toán / QA / Quản trị – nhánh `feat/BOT-1.5c-vanhanh-con-lai`; phần để sau: PROGRESS BOT-1.5c) · Bot Vận hành – chức năng | `Audiences/Internal/Chung/SelfServiceActions` (chấm công vị trí – chặn vị trí chuyển tiếp / cũ > 120″, QR cổng `start=CHAMCONG`; lịch ca; xin nghỉ; đổi ca; đơn của tôi) + `VanHanh/*Actions` theo BOT_TELEGRAM §3.2: Bếp, ATTP, GiaoVan (điều phối + tài xế: chuyến của tôi, xuất phát, đến điểm bằng bàn phím theo việc, số suất + nhiệt độ, ảnh ký nhận, nhận hộp), DiemPhat, Kho, KeHoach, KinhDoanh, Cskh, KeToan, NhanSu, QuanTri + `Flows/*`. Điền sẵn số đã biết (IoT, kế hoạch, FEFO, số hộp đã giao). Thiếu API "của tôi" → thêm action vào controller cũ (D-30), ghi PROPOSED. Có thể tách BOT-1.5a (chung + Bếp + ATTP), 1.5b (Giao vận + điểm phát), 1.5c (còn lại). | Mỗi nhóm người dùng §3.2 làm được đủ chức năng của mình bằng tài khoản thử; không chức năng nào ghi dữ liệu mà không qua API nghiệp vụ; dữ liệu ghi qua bot khớp khi xem trên web; `.http` + test xanh |
| **BOT-1.6** · `DONE` 2026-11-18 (nhánh `feat/BOT-1.6-dieuhanh`; PROGRESS BOT-1.6) · Bot Điều hành – chức năng | `DieuHanh/*`: tổng quan, chờ tôi duyệt (gom mọi loại), việc liên phòng đang mở, báo cáo (cấp LD / QL – `Dashboard/GetExecutive`, KPI), tiến độ bếp, giao vận trực tiếp, tồn & thiếu hụt, truy xuất + phần chung. Quyền vào bot = `#/bot-dieu-hanh`. Bot VH gửi nút mở bot DH cho người có quyền sau khi liên kết. | Mỗi vai §3.3 thấy đúng việc duyệt / tin của mình; CEO / COO dùng được chỉ với bot DH; test xanh |
| **BOT-1.7** · **`DONE`** 2026-11-18 (sự kiện tức thời – nhánh `feat/BOT-1.7-thong-bao`; 1.7d (QR `start=DOC_…` trên phiếu in, nhóm chat `ConfigJson.groups`, DELIVERY_TEMP_LOW) DONE – nhánh `feat/BOT-1.7d-qr-phieu-nhom`; tin theo lịch ở BOT-2.3) · Thông báo nghiệp vụ (thẻ việc) | Hoàn thiện `NotificationCatalog` theo BOT_TELEGRAM §5.1 (phạm vi, mức, người nhận, bot đích, nút). Sửa Service phát sự kiện (chỉ thêm `Enqueue`, không đổi nghiệp vụ): ApprovalRequest, ShiftAssignment/Confirm (**`notificationSent = true`**, D-55 k, Catalog 204), FoodSafetyChecklistResult (lỗi trọng yếu, D-54 d, Catalog 162), IotTemperature, ProductionBatch, DeliveryRoute/Dispatch (Catalog 181), DeliveryStop, MaterialRequirement, CustomerComplaint, Order, Menu/Publish (Catalog 85), PurchaseOrder, AttendanceEvent, LeaveRequest. `DailyJobs` LockOrders / Mrp ghi thông báo kết quả. QR cổng + QR trên phiếu in (link `start=DOC_…` trong `PrintService`; ảnh QR do FE sinh – không thêm thư viện BE). Nhóm chat theo `ConfigJson.groups` (không gửi nút duyệt / dữ liệu cá nhân vào nhóm). | Mỗi sự kiện §5.1 tới đúng người, đúng bot, đúng mức (🟡 gửi im lặng); 1 sự kiện không tới 2 bot của cùng một người; chạy lại job / gọi lại API không gửi trùng (`IdempotencyKey`); test xanh; Catalog cập nhật |
| **BOT-2.1** · **`DONE`** 2026-11-18 (nhánh `feat/BOT-2.1-the-ca`; PROGRESS BOT-2.1) · Thẻ ca / Thẻ ngày | `Experience/ShiftCard` + job `ShiftBrief` (15′ trước ca, cấu hình): 1 tin / người / ca, **ghim**, cập nhật khi có sự kiện (≤ 1 lần / 5′), nút theo giai đoạn ca; Thẻ ngày 07:00 cho quản lý (gộp bản tin sáng). Nút ⚡ Ca của tôi làm mới thẻ. | Mỗi người có đúng 1 thẻ ghim / ca; không gửi tin mới khi cập nhật; nút đổi theo giai đoạn; test với đồng hồ giả |
| **BOT-2.2** · **`DONE`** 2026-11-18 (nhánh `feat/BOT-2.1-the-ca`, commit riêng; PROGRESS BOT-2.2) · Sự việc liên phòng: nhận xử lý, ghi chú, leo thang | `Chung/IncidentActions`: [Nhận xử lý] / [Đã xử lý xong], trả lời vào thẻ → nối ghi chú vào thẻ mọi người (phản ánh → `ComplaintHistory`; khác → `PayloadJson`, GAP-INT-04). Job `Escalation` (mỗi phút) theo `Messaging:Escalation` (BOT_TELEGRAM §5.2). Job `LeaveHandover`: nhắc người duyệt trước ngày nghỉ. `INCIDENT_UNCLAIMED` cho COO / PMO. | Người bấm trước được ghi, người sau thấy tên người nhận; ghi chú hiện ở mọi người liên quan; 🔴 không ai nhận → leo thang đúng bậc và thời gian (đồng hồ giả); test xanh |
| **BOT-2.3** · **`DONE`** 2026-11-18 (nhánh `feat/BOT-2.1-the-ca`; ShiftReminder = Thẻ ca BOT-2.1; PERIOD_REPORT tuần / tháng để sau – PROGRESS BOT-2.3) · Giờ yên lặng theo ca + lịch thông báo | `MessagingRules` giờ yên lặng theo phân ca (BOT_TELEGRAM §5.3, `notBefore`); `BotJobs`: MorningBrief 07:00, DailyReport 17:00, RemindUnconfirmedOrders 19:00 (Telegram cho NV KD – thay phần bot của T23.2), IngredientsTonight 21:00, ProgressWatch 15′ (mẻ trễ, xe chưa rời, điểm trễ), ShiftReminder + NotCheckedIn mỗi giờ, ApprovalOverdue, ComplaintSla (mở lại D-59 a). | Tin 🟡 / ⚪ không tới người ngoài ca (trừ bậc leo thang 🔴); chạy lại job không gửi trùng; lịch đúng giờ VN; test xanh |
| **BOT-2.4** · **`DONE`** 2026-11-18 (nhánh `feat/BOT-2.4-qr-vitri`; vị trí xử lý ở messaging bằng JWT tài xế qua Service giao vận cũ thay cho `BotChannel/ReportLocation` – DECISIONS p39) · QR vật thể + vị trí trực tiếp tài xế | `start=LINE_…`, `VEH_…`, `LOC_…`, `CLEAN` (mở đúng việc tại chỗ). Vị trí trực tiếp: `messaging` nhận cập nhật vị trí, **chỉ gọi `BotChannel/ReportLocation` khi vào / ra bán kính** (mặc định 150 m, so `CustomerDeliveryLocation.Latitude / Longitude` và vị trí bếp) → `api` ghi xuất phát / giờ đến qua Service giao vận cũ, gửi thẻ [📷 Ảnh ký nhận] [Giao thiếu]; nút 📍 Đến điểm luôn còn. Không lưu từng điểm vị trí. | Vào bán kính → ghi giờ đến 1 lần; mất tín hiệu → bấm tay vẫn được; QR mở đúng việc theo quyền; test xanh |
| **BOT-UX-1** · `DONE` 2026-11-21 (anh duyệt 2026-11-20; đã gộp `main` 2026-11-21) · Bảng điều khiển duy nhất + điều hướng Quay lại + thống nhất cửa vào | Chỉ `AppDelicare.Messaging` (Experience/, Conversations/, Processing/, Channels/Telegram/), không đổi bảng / cột / API của api, đúng D-62 / BOT_SOURCE_ARCHITECTURE. (1) `PanelRegistry` (bộ nhớ, khóa chat + bot): messageId, giờ tạo, ngăn xếp điều hướng, nháp. (2) Mọi điều hướng = editMessageText trên panel; đầu panel có đường dẫn; hàng cuối [◀ Quay lại] [🏠 Menu] [✖ Đóng]. (3) Xong thao tác → phiếu kết quả + [Tiếp tục] [🏠 Menu]. (4) /menu, /ca, /chamcong, phím bàn phím dưới: xóa tin lệnh, xóa panel cũ (< 48h; quá hạn sửa "Menu đã đóng"), panel mới ở cuối – tối đa 1 panel / chat. (5) Nút trên panel cũ → "Menu này đã cũ" + panel mới. (6) Nhập tự do: ưu tiên nút; bắt buộc gõ → ForceReply, xong xóa câu hỏi + câu trả lời. (7) Tin hệ thống (OutboundPump) không bị panel xóa / sửa. (8) Chỉ 4 lệnh /menu /ca /chamcong /help; bàn phím 3 phím is_persistent khi liên kết và /start. (9) Màn đầu ⭐ Hay dùng 4 chức năng gần nhất. (10) Chỉ chat riêng; nhóm giữ hành vi cũ. | tools/bot-smoke (Telegram giả) + test: 5 lần điều hướng → 1 tin có nút; Quay lại cấp 3 → 2 → 1, đang nhập dở Quay lại → không có bản ghi mới; /menu 3 lần → 1 panel ở cuối, tin lệnh bị xóa; panel cũ → "Menu này đã cũ" + panel mới; thẻ việc / thẻ ca xen giữa không bị xóa / sửa; getMyCommands đúng 4 lệnh, reply keyboard is_persistent; dotnet test xanh; PROGRESS BOT-UX-1; BOT_TELEGRAM §1 |
| **BOT-2.9** · `DOING` – phần chạy được ở máy xong 2026-11-19 (test 482 / 482 gồm 73 test messaging; `bot-1.2-identity.http` 23 / 23 trên API thử với khóa tạm; `bot-1.1-channel.http` 12 / 21 do DB dev có liên kết bot thật của admin + hàng đợi tin tồn – không phải lỗi code); **còn: server test + token bot thật (cần anh)** · Gộp, kiểm thử, triển khai TEST | Chạy lại mọi `bot-*.http`, test xanh; dựng `messaging` trên server test (bộ bot TEST, webhook); smoke theo từng vai BOT_TELEGRAM §3; cập nhật `deploy/README.md`, UAT_CHECKLIST mục bot; **dừng, báo anh** (không push / gộp `main` khi chưa duyệt). | Mọi vai làm được luồng chính trên bot TEST; báo cáo kết quả cho anh |
| BOT-3.x · `DEFERRED(sau go-live)` | 3.1 Bàn giao ca trong Thẻ ca (lưu Obsidian khi có T50) · 3.2 Công tuần của tôi + nút góp ý · 3.3 Đo số liệu (thời gian nhận cảnh báo, số lần bấm, chức năng không dùng) · 3.4 Mini App (FE) cho biểu mẫu dài / bảng điều hành | – |

**Việc của anh (không phải Claude Code):** tạo bot qua @BotFather – bộ DEV trước (VH, DH, TRO_LY), sau đó TEST, PROD; đặt token vào user-secrets / env; in QR cổng; cấp số điện thoại trong hồ sơ nhân viên.

## ★★★ ĐỢT BE-2 – HOÀN THIỆN API BACKEND (anh chốt 2026-11-16, D-43, D-44) – **XONG 2026-11-17 (BE2.1–BE2.7, BE2.9; BE2.8 để sau D-45) – chờ anh duyệt / chốt PROPOSED (PROGRESS mục BE2.9)**

> **⚠ D-44 (2026-11-16): KHÔNG CHẠY PILOT.** Mục tiêu là **hoàn thiện toàn bộ luồng nghiệp vụ R1**. Mọi mục `DEFERRED(PILOT)` (bảng trong mục ★★ bên dưới và trong từng task) **trở thành TODO** và nằm trong BE-2, **trừ** nhóm "Để sau" cuối mục này. Không còn giới hạn "1–2 khách, 1 bếp, ca trưa", không có UAT-PILOT riêng: kiểm thử theo **toàn luồng**. Thứ tự sau BE-2: **FE toàn bộ màn hình luồng (FE-ALL)** → **UAT toàn luồng** → production (T48.2) → mới làm nhóm "Để sau".

**Mục tiêu:** làm nốt mọi API backend còn thiếu theo `DELICARE_API_Specification.md` / `DELICARE_API_Catalog.xlsx`, **trừ** tích hợp bot, dashboard / KPI, báo cáo (để sau). Khung code cũ (D-30), phân quyền cũ (D-31), không thêm bảng / cột (thiếu → GAP).
**Frontend:** Claude Code **chưa làm FE** trong đợt này (D-40 kéo dài tới hết BE-2). Bạn frontend làm song song dựa trên `docs/frontend-handoff/` → **mỗi task phải cập nhật handoff** (route, request / response mẫu, quyền, trạng thái) và `.http`.
**Câu hỏi PROPOSED chưa được anh chốt:** làm theo phương án PROPOSED đang ghi trong DECISIONS, ghi rõ trong PROGRESS; chỉ dừng (BLOCKED) với mục ghi "cần anh".
**Catalog:** máy không có openpyxl → cuối mỗi task ghi trong PROGRESS bảng "STT catalog → Trạng thái / API thực tế / Task" để em cập nhật Excel.

**Làm đúng thứ tự:**

| # | Task | Phạm vi (STT dòng trong Catalog) |
|---|---|---|
| BE2.1 | **Lấp chỗ thiếu của luồng Pilot** · `DONE` 2026-11-17 (nhánh `feat/BE2.1-flow-gaps`, đã gộp `release/pilot`; PROPOSED D-50 (a)–(h)) | Đóng gói: `PackingOrder` / `PackingResult` sinh từ mẻ, start / result / cancel (171–175); xếp hàng lên xe `DeliveryLoading` + trạng thái Loading (180); đơn giao tay / gán – chuyển tuyến / tách / hủy (184–187); lập chuyến **nhiều xe, theo khu vực** (177–179); `DeliveryExecution` GET (188); kế hoạch SX **sinh theo ca** (137); đề nghị mua chọn dòng + bufferPercent (107); ghi nhiệt độ tự do `TemperatureLog` (165); danh sách đơn dạng board + statusCounts (90), thực đơn có orderedMeals / assignedCustomerCount (82); hao hụt đủ nghiệp vụ (156); danh sách phản ánh đủ trường + isOverdue (197); chi tiết + hủy yêu cầu duyệt (211, 213); truy xuất lô 2 chiều dạng API (118, 119) |
| BE2.2 | **Hợp đồng, dự báo, đổi món** (T13 mở rộng) · `DONE` 2026-11-17 (nhánh `feat/BE2.2-contract-forecast`, đã gộp `release/pilot`; PROPOSED D-51 (a)–(j); duyệt hợp đồng / sửa đơn sau giờ chốt chạy engine 1 cấp T12 – BE2.3 chuyển nhiều cấp) | Tạo hợp đồng kèm suất + sinh số / gửi duyệt / chấm dứt (78–80); dự báo: ma trận / nhập hàng loạt / công bố + gợi ý số suất trong mẫu tạo đơn, lệch dự báo > 10% (87–89, 92, 96); yêu cầu đổi món `MenuChangeRequest` (104); sửa đơn sau giờ chốt qua duyệt `ORDER_CHANGE_AFTER_CUTOFF` |
| BE2.3 | **Duyệt nhiều cấp** (T12 mở rộng) + áp vào nghiệp vụ · `DONE` 2026-11-17 (nhánh `feat/BE2.3-multilevel-approval`, đã gộp `release/pilot`; PROPOSED D-52 (a)–(j)) | Engine nhiều bước theo `ApprovalStep`, hết hạn (cờ), hủy; áp cho: hợp đồng (79), kiểm kê lệch > 2% submit / post (131, 132), điều chỉnh kho tay, PO nhiều cấp theo ngưỡng. **Đề nghị mua 2 cấp (110) theo D-46** – làm đầu tiên trong BE2.3 (cấp 1 Trưởng mua hàng, cấp 2 COO; `ConvertToPurchaseOrder` chỉ khi Đã duyệt; cập nhật `pilot-flow.http` / test cho bước duyệt) |
| BE2.4 | **Kho & sản xuất mở rộng** · `DONE` 2026-11-17 (nhánh `feat/BE2.4-inventory-production`, đã gộp `release/pilot`; PROPOSED D-53 (a)–(h)) | Giữ chỗ `MaterialReservation` khi phát hành / xuất (126); hàng sắp về từ PO cho MRP; chênh lệch tiêu hao (135); xử lý lô hết hạn (logic gọi được bằng action, job ở BE2.8); đóng / hủy lệnh SX (144, 145); giá thành thực tế + giá vốn suất (152–154); công đoạn theo tổ bếp (Sơ chế → Nấu → Chia suất, người / tổ) |
| BE2.5 | **ATTP, vệ sinh, hộp / khay** (T18 phần nghiệp vụ, T20) · `DONE` 2026-11-17 (nhánh `feat/BE2.5-foodsafety-packaging`, đã gộp `release/pilot`; PROPOSED D-54 (a)–(h)) | Chạy checklist + ghi kết quả, lỗi trọng yếu (161, 162); mẻ vệ sinh khay 6 bước (167–169); chuyển trạng thái sự cố NCC (170); thu hồi hộp / khay + kiểm tra + sổ bao bì `PackagingLedger` OUT / IN khi giao / thu (190, 194–196) |
| BE2.6 | **Nhân sự** (T21) · `DONE` 2026-11-17 (nhánh `feat/BE2.6-hr`, đã gộp `release/pilot`; PROPOSED D-55 (a)–(k), GAP-HR-02…05) | Lưới ca, phân ca, phân ca hàng loạt, xác nhận (201–204, **không gửi Telegram**); chấm công check-in / out GPS (KitchenWorkstation lat / long / bán kính) (205); bảng công (206 – dạng danh sách, không làm báo cáo); nghỉ phép, đổi ca, hủy + duyệt qua engine (207–209) |
| BE2.7 | **API dùng chung + mẫu in** · `DONE` 2026-11-17 (nhánh `feat/BE2.7-common-print`, đã gộp `release/pilot`; PROPOSED D-56 (a)–(j), GAP-DOC-01, GAP-PRINT-01 – mẫu in xlsx / docx) · **BE2.7.1 `DONE` 2026-11-17: PDF cho 6 mẫu in + in hàng loạt (anh chốt PDFsharp-MigraDoc MIT + font Noto Sans OFL – D-56 b, GAP-PRINT-01 đã xử lý; PDF mặc định – PROPOSED D-56 k; nhánh `feat/BE2.7.1-pdf`, đã gộp `release/pilot`)** | Thống kê theo trạng thái (2); xuất Excel danh sách (8); tệp đính kèm: tải lên / danh sách / tải xuống `DocumentFile` (19–21); lịch sử bản ghi gộp AuditChange + ApprovalDecision (15); mẫu in: phiếu nhập, phiếu xuất, phiếu kiểm kê, phiếu giao, biên bản POD, biên bản đối soát, in hàng loạt (24–30) – theo mẫu thật trong `BUSINESS_DOCS.md`. Refresh token (32): **ĐỂ SAU (D-47)** – không làm trong BE-2 |
| BE2.8 | ~~Job tự động lõi~~ → **ĐỂ SAU (D-45)** | Không làm job trong BE-2. Giữ các nút bấm tay đã có (`Order/LockByMealDate`, `MaterialRequirement/Calculate` / `CalculateTopUp`); các kiểm tra hết hạn làm **tại lúc thao tác** (chặn xuất lô hết hạn – đã có; `isOverdue` phản ánh – đã có; hợp đồng / yêu cầu duyệt quá hạn: kiểm khi tạo đơn / khi duyệt). D-05 không còn chặn. |
| BE2.9 | **Gộp, kiểm tra, deploy** · `DONE` 2026-11-17 (nhánh `feat/BE2.9-full-flow` từ `release/pilot` @ 7b0fc43, chưa push / chưa gộp `main`; `full-flow.http` 216 / 216 trên dev + staging; test 243 / 243; staging đã cập nhật rồi dừng – **chờ anh duyệt**) | Mỗi task: build 0 lỗi, toàn bộ test xanh, `.http` mới chạy được trên DB dev, cập nhật `DB/README.md` + `Deploy-Database.ps1` + `db-init.sh` nếu có SP / seed mới. Cuối đợt: đổi `pilot-flow.http` thành `full-flow.http` – luồng đầy đủ (hợp đồng → dự báo → đơn → MRP → mua → nhận + QC → kho → sản xuất theo tổ / ca → đóng gói → xếp xe → giao → thu hộp / khay → đối soát → giá thành) chạy hết; deploy staging; **dừng, báo anh** (không push / gộp `main` khi anh chưa duyệt) |

### T48.3 – Tự cập nhật DB cho server Docker của dev FE · làm **ngay sau BE2.1** · `DONE` 2026-11-16 (nhánh `feat/T48.3-db-autoupdate`; Docker thật chưa chạy – máy không có Docker, xem PROGRESS) (anh chốt 2026-11-16)
Mục tiêu: dev FE `git pull` xong là DB trên server có đủ SP / seed mới, không phải tự chạy file SQL.
1. **Một danh sách thứ tự script duy nhất:** `deploy/db/scripts.order` (SP, seed theo thứ tự). `Deploy-Database.ps1` và `deploy/docker/db-init.sh` cùng đọc file này – bỏ danh sách lặp trong 2 file.
2. **`db-init.sh` idempotent như PS1:** SP `CREATE OR ALTER`, seed chỉ thêm (add-only), sqlcmd `-f 65001`; chạy nhiều lần không sai dữ liệu. Sửa hash mật khẩu admin dev từ MD5 sang `PBKDF2$100000$salt$hash` (cùng hàm với T48.0). Không `CREATE/ALTER/DROP TABLE`, không xóa dữ liệu.
3. **Lệnh cho dev FE** (ghi vào `deploy/docker/README.md` + `docs/frontend-handoff/README.md`): `git pull && docker compose -f deploy/docker/docker-compose.staging.yml run --rm db-init`. Thêm `deploy/docker/update-db.sh` gói lệnh này + ghi log; tùy chọn cron mỗi 5 phút (`*/5 * * * * /path/update-db.sh >> update-db.log 2>&1`), chỉ chạy khi `git pull` có commit mới đụng `DB/` hoặc `deploy/db/`.
4. **Dự phòng server không ra Internet:** `Deploy-Database.ps1 -Server <ip,port>` chạy từ máy dev FE trỏ vào DB server (ghi cách dùng, không ghi mật khẩu vào repo – lấy từ biến môi trường).
5. **Kiểm tra:** chạy `db-init` 2 lần liên tiếp trên DB dev Docker sạch và DB đã có dữ liệu → 0 lỗi, số dòng seed không đổi ở lần 2; đăng nhập admin dev được.
6. Báo anh; không chạy lên DB server của dev FE (không phải DB dev của em) – anh / dev FE tự chạy lệnh ở bước 3.

**Để sau – làm SAU KHI hoàn thiện luồng (KHÔNG làm trong BE-2 / FE-ALL):**
- ~~Dashboard / KPI / cảnh báo, biểu đồ 100 / 157 / 163, 16–18, DB Explorer~~ → **chuyển vào BE-3 (D-57, 2026-11-17)**.
- **Báo cáo:** báo cáo NCR, báo cáo truy xuất, báo cáo hao hụt / giá thành tổng hợp, bảng công dạng báo cáo (T18 phần báo cáo).
- **Bot / tích hợp:** 214–220, gửi PO cho NCC qua email / Zalo (113), nhắc khách 19:00, gửi Telegram phân ca / duyệt (T30–T51, T23.2). (IoT 166 → BE3.2.)
- **Refresh token (D-47):** phương án đề xuất ở GAP-AUTH-01 (không lưu DB).
- **Job tự động (D-45)** – **T23.1 lõi `DONE` 2026-11-17** (khóa đơn 20:00, MRP + đề nghị mua 20:30, hết hạn lô / hợp đồng, chốt vắng); phần còn lại cùng đợt bot / KPI: khóa đơn 20:00, tính nhu cầu 20:30, đề xuất mua 21:00, hết hạn lô / hợp đồng / duyệt, theo dõi SLA, nhắc khách 19:00, KPI 22:00 (T23.1, T23.2; cần chốt D-05 nơi lưu lịch job).
- **Hermes + quản lý agent:** Tool Gateway / MCP, 12 agent theo phòng ban (`docs/claude/AGENTS.md`), lịch chạy agent, Mission Control, Obsidian second brain (T30–T35, T40, T41, T43, T50, T51, T60, T61).

**Frontend (D-49, 2026-11-16): dev FE khác làm FE-ALL và commit lên git. Claude Code KHÔNG tự làm FE-x – kể cả khi `/next`.** Việc của em về frontend chỉ là: viết handoff `docs/frontend-handoff/` cho mỗi API mới, và task GHEP-FE khi anh yêu cầu.

### REF-1 – Danh mục chọn sẵn đồng nhất (UAT 2026-11-21, anh chốt GAP-REF-01…06 2026-11-22) · `DONE (2026-11-22)`
- Một nguồn `Model/Helper/ReferenceCatalogs.cs` (Thực thể.Cột → `ReferenceLists`); máy chủ kiểm Insert / Update chung (`ReferenceListFilter`), nghiệp vụ riêng (hợp đồng, đóng gói, sổ bao bì, thu hồi, phản ánh) và **nhập Excel** (Loại NCC / Kiểu bảo quản / Loại kho); `MetaData/GetReferenceLists` trả `listColumn` → giao diện vẽ ô chọn.
- appsettings: + KITCHEN (Kho bếp), + ComplaintChannel / ComplaintCategory; bỏ CustomerType, AdjustmentReason, QcResult (GAP-REF-06).
- FE: ô chọn danh mục mọi form; 49 ô tham chiếu chưa xác định bảng đích → ô chọn; form tải sẵn bảng đích.
- Dữ liệu: `DB/scripts/REF1_normalize_catalog_codes.sql` (DryRun bắt buộc) – mã khác hoa thường / nhãn → mã; giá trị ngoài danh mục chỉ liệt kê. DB dev: 0 dòng cần đổi; ngoài danh mục: HĐ `HD-261122-001` loại suất "01", 132 dòng loại khay "[DEMO] hộp giữ nhiệt" (giữ – thu hồi tính theo chuỗi này).
- Còn mở: GAP-REF-05 (Hình thức phục vụ, Phân khúc khách chưa có danh mục; Vùng / Khu vực khách chưa có bảng).

### GHEP-FE – Ghép luồng frontend + backend · **CHỈ LÀM KHI ANH YÊU CẦU** · `DONE (2026-11-21, anh yêu cầu – nhánh ghep/2026-11-21 → main)`
1. `git fetch`; xem commit / nhánh FE mới (`git log --stat -- app-delicare-client/`). Không sửa code FE của bạn kia khi chưa có yêu cầu; không rebase / force-push nhánh của người khác.
2. Gộp vào nhánh `ghep/<ngày>` (không gộp thẳng `main`); xung đột → giữ phần của người sửa file đó, báo anh chỗ nào.
3. Soát FE gọi API: URL `Controller/Action` đúng khung cũ, tham số `[FromForm]`, `Result<T>`, quyền (`permission()` / ScreenMap, menu `#/<screen-id>`). Lệch → **sửa phía backend nếu backend sai**; FE sai → ghi danh sách cho dev FE (file / dòng / sửa thế nào), chỉ sửa FE nếu anh đồng ý.
4. Chạy `npm run build` + backend build + test; chạy luồng `full-flow.http` và đi tay luồng trên giao diện (staging) theo `UAT_CHECKLIST.md`.
5. Báo anh: màn hình nào chạy hết luồng, lỗi nào thuộc BE (đã sửa) / FE (cần bạn kia sửa), đề xuất bước tiếp. Không push / gộp `main` khi anh chưa duyệt.

   - **Kết quả 2026-11-21:** ghép 5 commit develop (14–17/11). 19 file engine / shell giao diện của develop trùng bản mockup đã ghép (8ba62901) → giữ main; 14 service xung đột do kiểm "Mã không được để trống" – đã có quy tắc mã D-63 → giữ main; lấy thêm kiểm SupplierMaterial / CustomerContact / danh mục, ImportController, màn master-data Angular, `AppDelicare/Database/*.sql` (CHƯA chạy – trùng vai trò seed / SP của `DB/`, cần rà trước khi dùng). Test 514 / 514, build FE xanh.
Sau GHEP-FE đủ luồng → UAT toàn luồng theo `UAT_CHECKLIST.md` → T48.2 production.

## ★★ LÁT PILOT – LỊCH SỬ (backend P0–P9 đã xong; **D-44 bỏ chạy Pilot** – các mục DEFERRED(PILOT) dưới đây chuyển vào BE-2) (D-34)

**Mục tiêu:** chạy thử sớm **một luồng thông suốt** Đơn hàng → Nhu cầu NL → Mua → Nhận + QC → Kho → Sản xuất → Giao + đối soát suất, với danh mục thật, 1–2 khách, 1 bếp. Mỗi bước làm **bản tối thiểu** (bấm nút thay job, duyệt 1 cấp). **Giữ ngay** mọi kiểm soát ATTP: nhiệt độ lõi ≥ 70°C, nhiệt độ giao ≥ 60°C, lưu mẫu, chặn lô hết hạn / KHONG_DAT.

> **⚠ THỨ TỰ MỚI 2026-11-16 (D-40): LÀM HẾT BACKEND PILOT TRƯỚC, FRONTEND SAU.**
> - **Tạm dừng frontend ngay:** FE-3 đang làm dở → commit phần đã có (`wip(FE-3): …`) trên nhánh `feat/FE-3-menu-masterdata`, ghi PROGRESS phần xong / chưa xong, **không làm thêm FE**. Không xóa code FE đã làm (FE-0, FE-1, FE-2 nếu có).
> - **Làm lần lượt backend:** T45R.8 (**chỉ phần backend**: script tắt menu D-37, `Person/Insert` 1 bước D-38, mật khẩu tạm D-39 + test; bỏ qua phần giao diện) → T11 → T12 → T13 + T46B.1 → T16 → T14 → T15 → T17 → T19 → T50R1 (`pilot-flow.http` chạy hết luồng bằng API) → deploy staging backend.
> - Mỗi task: cập nhật `DELICARE_API_Catalog.xlsx`, `.http`, `docs/frontend-handoff/` (route, request / response mẫu cho từng màn) để làm FE sau không phải tra lại.
> - **Xong toàn bộ backend Pilot → dừng, báo anh.** Sau đó mới làm tiếp FE: FE-3 (phần còn lại) → phần FE của T45R.8 → FE-P2 → FE-P3 → FE-P4 → FE-P5 → FE-P7 → FE-P8 → UAT-PILOT trên giao diện.
> - Các chỗ "→ **FE-Px**" trong bảng dưới chỉ là ghi chú màn hình tương ứng, **không làm ngay**.

> **⚠ T-MERGE – GỘP NHÁNH PILOT (anh chốt 2026-11-16, D-41) – làm NGAY SAU T19, TRƯỚC T50R1.**
> **Điều phối 3 thư mục làm việc song song** (`Delicare-ai`, `wt-T11`, `wt-T17`, `wt-T45R8`):
> - **Chỉ phiên ở `wt-T17`** làm tiếp: xong T19 → T-MERGE → T50R1 → deploy staging → dừng báo anh.
> - Phiên ở **`wt-T11`** và **`wt-T45R8`**: **DỪNG**, không nhận task mới (T17 đã DONE ở `wt-T17` – không làm lại). Commit hết thay đổi đang dở rồi báo "đã dừng".
>
> **T-MERGE – các bước:**
> 1. Tạo nhánh `release/pilot` từ `main`. Xác định nhánh "đầu" chứa đủ lịch sử bằng `git merge-base` / `git branch --contains` (dự kiến: `feat/T15.1-po-rules` [T48.0 → T11 → T12 → T13 → T16 → T14 → T15 → T14.1 → T15.1], `feat/T19-delivery` [T17 → T19, tách từ T15], `feat/T45R.8-admin` [FE-0 → FE-1 → FE-2 → FE-3 WIP + T45R.8], cùng các nhánh FE nếu không nằm trong T45R.8). Gộp lần lượt bằng `git merge --no-ff`, **không rebase, không force**.
> 2. Giải xung đột:
>    - Code (`ConstantHelper`, `ScreenMap`, DI, seed menu / quyền, SearchRequest, SP…): **giữ cả hai phía** (hợp), không bỏ logic của nhánh nào; sau đó build + test.
>    - `PROGRESS.md`: một mục "Trạng thái hiện tại" duy nhất, đúng thực tế sau gộp; nhật ký giữ **đủ mọi mục** theo thứ tự thời gian, không trùng.
>    - `DECISIONS.md`, `ROADMAP.md`, `GAP`: hợp đủ các dòng; **kiểm trùng mã** (D-xx, GAP-xx) – nếu hai nhánh dùng cùng mã cho hai nội dung khác nhau thì đánh lại mã cho bản sau và sửa mọi chỗ tham chiếu; ghi bảng đổi mã vào PROGRESS.
>    - `DELICARE_API_Catalog.xlsx` (file nhị phân): lấy bản của `main`/mới nhất, **không tự gộp**; ghi PROGRESS "cần cập nhật Catalog" (em / anh cập nhật sau).
>    - Script SQL trong `DB/sp`, `DB/seed`: liệt kê đầy đủ theo thứ tự chạy vào `DB/README.md` mục "Pilot".
> 3. Kiểm tra trên **một bản gộp duy nhất**: `dotnet build --no-incremental` 0 lỗi; `dotnet test` **toàn bộ xanh** (số test ≥ tổng các nhánh, không mất test); chạy lại mọi SP / seed mới lên DB dev theo thứ tự trong `DB/README.md`; chạy các file `docs/api-tests/*.http`; `npm run build` của `app-delicare-client` 0 lỗi (FE-3 WIP vẫn phải build được).
> 4. Dọn: sau khi `release/pilot` xanh → `git worktree remove` 3 thư mục `wt-*` (giữ nhánh, không xóa nhánh). **Không push, không gộp vào `main`** – báo anh danh sách nhánh đã gộp, số test, xung đột đã giải, để anh duyệt rồi mới push / gộp `main`.
> 5. T50R1 làm trên `release/pilot`.

**Làm đúng thứ tự này** (đứng trước bảng R1 bên dưới; task trong bảng R1 không có ở đây thì chờ sau Pilot):

| # | Task | Phạm vi PILOT (phần còn lại của task giữ `TODO` sau Pilot) |
|---|---|---|
| P0 | **T48.0 – Băm mật khẩu (D-29)** | Bắt buộc trước khi người thật dùng. Xem mô tả task T48.0. |
| P0 | T45R.6 | Code đã DONE. **Cần anh:** chạy T4 trong Visual Studio một lần để xác nhận. Không chặn task khác. |
| FE | **FE-0 → FE-3** (D-35) | FE-0, FE-1 DONE (chờ thử tay). **FE-3 TẠM DỪNG (D-40)** – làm tiếp sau khi xong backend Pilot. |
| P0b | **T45R.8 – Hoàn thiện quản trị** (D-37, D-38, D-39) | Tắt 26 menu cũ; tạo người dùng 1 bước (đúng 1 tài khoản); mật khẩu tạm khi reset / tạo tài khoản. **Backend DONE 2026-11-16** (D-40); phần giao diện làm cùng đợt FE. |
| P1 | T11 – Mã chứng từ | Đủ các loại mã dùng trong luồng (PH-, XN-, PO-, NL-, ME-, CX-, PA-, DN-). |
| P2 | T12 – Duyệt **1 cấp** → **FE-P2** | Tạo / duyệt / từ chối / thực thi; quyền duyệt = mục menu "Phê duyệt" (D-31). Chỉ áp cho **PO** và **đơn sửa lệch > 30%**. Nhiều cấp, hết hạn tự động → sau Pilot. |
| P3 | T13 + T46B.1 – Thực đơn tuần + Đơn hàng → **FE-P3** · `DONE(PILOT)` 2026-11-16 (backend) | Thực đơn tuần (buổi × lựa chọn, FC%); nhập suất; action **`Lock`** (bấm tay) áp hạn 20h D-1; tính tiền theo ContractMeal / PricePolicie; in **Thực đơn gửi khách**. |
| P4 | T16 – Sổ kho → **FE-P4** · `DONE(PILOT)` 2026-11-16 (backend) | IN / OUT / ADJUSTMENT / REVERSAL, tồn, gợi ý lô FEFO/FIFO (D-24), chặn lô hết hạn / KHONG_DAT. Kiểm kê: ghi nhận được, **duyệt lệch > 2% → sau Pilot** (admin điều chỉnh tay). |
| P5 | T14 – Tính nhu cầu NL · `DONE(PILOT)` 2026-11-16 (backend) | Action **`Calculate`** (bấm tay): đơn đã khóa × công thức đã khóa × hao hụt, trừ tồn khả dụng (+ SafetyStock theo D-12 đề xuất) → PurchaseRequest nháp, gợi ý NCC. |
| P6 | T15 – Mua → Nhận → QC đầu vào → **FE-P5** (gồm cả màn T14) · `DONE(PILOT)` 2026-11-16 (backend) | PR → PO (duyệt 1 cấp) → GoodsReceipt → QC DAT / DAT_MOT_PHAN / KHONG_DAT → MaterialLot `NL-…` → nhập kho; nhiệt độ nhận hàng. In **Đơn đặt hàng NCC**, **Phiếu trước sản xuất**. |
| P7 | T17 – Sản xuất (bản theo mẻ) → **FE-P7** · `DONE(PILOT)` 2026-11-16 (backend) | Kế hoạch mẻ `ME-…`, cấp NL cho mẻ (FEFO), bắt đầu / xong mẻ, **nhiệt độ lõi ≥ 70°C (chặn đóng gói)**, **lưu mẫu** (SAMPLE_RETAINED), sản lượng + hao hụt. In **Phiếu sản xuất**. Công đoạn theo tổ → sau Pilot. |
| P8 | T19 – Giao + đối soát (bản tối thiểu) → **FE-P8** · `DONE(PILOT)` 2026-11-16 (backend) | Chuyến `CX-…`, điểm dừng, **nhiệt độ giao ≥ 60°C**, POD, **suất thực tế phục vụ** → OrderReconciliation; phiếu phản ánh `PA-…` cơ bản (tạo / xử lý / đóng). Cảnh báo SLA tự động → sau Pilot. |
| P9 | T50R1 (phần Pilot) + T48.1 – **kết thúc đợt backend, dừng báo anh** · `DONE(PILOT)` 2026-11-17 (backend: `pilot-flow.http` chạy thật trên DB dev, test HTTP end-to-end, UAT_CHECKLIST PILOT, staging backend + bản build Angular; phần thao tác trên giao diện của UAT → sau FE, D-40) | `docs/api-tests/pilot-flow.http` chạy hết P3 → P8 bằng 1 bộ dữ liệu `[DEMO]`; `UAT_CHECKLIST.md` mục **PILOT** (cả thao tác trên giao diện); deploy staging backend + bản build Angular. → **Dừng, báo anh UAT-PILOT.** |

**`DEFERRED(PILOT)` – làm sau khi chạy thử, lúc chạy thử dùng cách thay thế:**

| Nhóm | Task | Lúc Pilot |
|---|---|---|
| Job tự động 19h / 20h / 20h30 / 21h / 5h30, hết hạn duyệt, SLA | T23 | Nút bấm tay |
| KPI, dashboard điều hành | T22, T47B.3 | Danh sách lọc theo ngày |
| Báo cáo NCR, báo cáo truy xuất 2 chiều | T18 (báo cáo) | Dữ liệu vẫn ghi đủ |
| Checklist ATTP theo ca, 5S | T18 (còn lại) | Checklist giấy |
| Hộp / khay, vệ sinh 6 bước | T20 | Sổ giấy |
| Nhân sự: chấm công GPS, nghỉ phép, đổi ca | T21 | Cách làm hiện tại |
| Duyệt nhiều cấp, duyệt kiểm kê lệch > 2% | T12 (mở rộng), T16 | Duyệt 1 cấp / admin điều chỉnh |
| Công đoạn theo tổ bếp | T17 (mở rộng) | Theo dõi theo mẻ |
| Mẫu in / xuất file khác | T46B, T47B | — |
| Bot Telegram / Zalo, Tool Gateway, Obsidian | T30–T51 | `DEFERRED(R2)` như cũ |
| Production chính thức | T48.2 | Chạy thử trên staging |

**Phạm vi chạy thử (D-35):** 1–2 khách, 1 bếp, ca trưa.

Quy ước: khi làm task có phần Pilot, ghi rõ trong PROGRESS phần nào DONE, phần nào để `DEFERRED(PILOT)`; trạng thái task trong ROADMAP ghi `DONE(PILOT)` khi xong phạm vi Pilot.


**Frontend Angular (`app-delicare-client/`): từ 2026-11-16 làm các màn hình Pilot theo khung Angular cũ (D-35, CLAUDE.md 4.4); màn hình ngoài Pilot vẫn `DEFERRED(FE)`.** Backend làm theo khung code cũ (D-30), đủ nghiệp vụ mô tả trong `DELICARE_API_Specification.md`. Sau khi xong backend anh sẽ giao frontend. Các task màn hình Angular (T42, T44-UI, T45, T46, T47) chuyển sang `DEFERRED(FE)`; thay bằng task API tương ứng: T45B, T46B, T47B.

**Chọn task theo THỨ TỰ BẢNG DƯỚI**, không theo số task. Hết mỗi lát:
- API chạy trên staging backend;
- Swagger đầy đủ;
- có **Postman / `.http` collection** trong `docs/api-tests/` để thử từng luồng;
- cập nhật `docs/claude/UAT_CHECKLIST.md` (kịch bản thử bằng API);
- báo anh.

| Lát | Backend giao được | Task theo thứ tự |
|---|---|---|
| R1.0 Nền móng | API an toàn, đúng 124 bảng, phân quyền thật, test, staging backend | T00 → T01 → T04 → T02 → T03 → T05 → T06 → T07 → T08 → T09 → **T48.1 (staging backend)** |
| R1.1 Danh mục và quản trị (P0–P2) | API quản trị, nhóm quyền, menu; danh mục khách, điểm giao, hợp đồng, NCC, nguyên liệu, đơn vị, kho, món, công thức, ca | T10 → T21.1 (seed Shift) → T45B.1–.4 → **T45R (về khung cũ)** → T45B.5 → **T45R.6 (đổi tên Generator)** → T49 (API import danh mục) → **UAT-1 (API)** |
| R1.2 Đơn hàng (P3) | Hợp đồng, thực đơn tuần (buổi × lựa chọn, FC), đơn hàng, chốt 20h, duyệt | T11 → T12 → T13 → T46B.1 (API P3 + in thực đơn) → **UAT-2** |
| R1.3 Kho và mua hàng (P4) | Nhập kho + QC, FEFO, kiểm kê, MRP, báo giá → PO, job 20h / 20h30 / 21h | T16 → T14 → T15 → T23.1 → T46B.2 (API P4 + in phiếu trước SX, đơn đặt hàng) → **UAT-3** |
| R1.4 Sản xuất và ATTP (P5–P6) | Kế hoạch mẻ, cấp NL, nhiệt độ lõi, lưu mẫu, checklist, truy xuất | T17 → T18 → T46B.3 (API P5 + in phiếu sản xuất) → T47B.1 (API P6) → **UAT-4** |
| R1.5 Giao vận, phản ánh, hộp/khay (P7) | Chuyến, điểm dừng, nhiệt độ giao, POD, phản ánh SLA, hộp/khay, vệ sinh | T19 → T20 → T47B.2 (API P7) → **UAT-5** |
| R1.6 Nhân sự, KPI, dashboard (P8) | Ca, chấm công, nghỉ / đổi ca, KPI, API dashboard điều hành | T21 → T22 → T23.2 → T47B.3 (API P8 + dashboard) → **UAT-6** |
| R1.7 Go-live backend | Production backend, tài liệu API cho team frontend | T48.2 → T50R1 |

**`DEFERRED(FE)`** – chờ anh giao frontend: T42, phần UI của T44, T45, T46, T47 (màn hình Angular).

**`DEFERRED(R2)`:**
- T30–T35: Tool Gateway / MCP / `POST /api/gateway` (T35 tùy chọn).
- T40–T41: Telegram, Zalo.
- T43: cấu hình agent.
- T50–T51: Obsidian.
- T60–T61: chỉ nạp dữ liệu demo Sheets (tùy chọn).
- GAP R2 (D-21…D-23): bán lẻ / POS, bảo trì thiết bị, CRM marketing, sổ CAPA, xuất dữ liệu sang MISA.
- T62–T63: **hủy**, vì Sheets là demo, không có cắt chuyển (D-16).

**Quy ước tách task:**
- T21.1 = chỉ phần seed Shift của T21.
- T23.1 / T23.2 = các job tương ứng trong T23.
- T46B.1 = API P3, T46B.2 = API P4, T46B.3 = API P5.
- T47B.1 = API P6, T47B.2 = API P7, T47B.3 = API P8 + dashboard.

Danh sách màn hình mỗi pha lấy theo `docs/mockup/js/data/catalog.js` (cột phase / screen_id). Endpoint và logic theo `docs/reference/DELICARE_API_Specification.md` + `DELICARE_API_Catalog.csv`.

---

## GIAI ĐOẠN 0 – Chuẩn bị

### T00 – Khởi tạo môi trường làm việc · `DONE` (2026-11-14)
- **Cài lệnh tắt:** nếu chưa có thư mục `.claude/` ở gốc repo thì sao chép `docs/claude/claude-config/commands/*` → `.claude/commands/` và `docs/claude/claude-config/settings.json` → `.claude/settings.json`. Báo anh: từ phiên sau có thể gõ `/next`, `/task T13`, `/status`, `/verify`, `/gap`.
- Repo đã có git. Chạy `git status`, `git branch -a`, `git log --oneline -5`, `git remote -v`. Ghi lại nhánh hiện tại và các file đang sửa dở.
  - **Không đụng** vào các file anh đang sửa dở. Ví dụ ngày 10/11: `IngredientService`, `CompanyService`, `SupplierService`, `WarehouseService`…
  - Nếu có thay đổi chưa commit: hỏi anh trước khi tạo nhánh.
- `.gitignore` ở gốc đã được cập nhật sẵn (bin/obj, node_modules, backup, `*.local.md`…). Kiểm tra `git status` để chắc `docs/claude/LOCAL_DEV.local.md` và `backup-db/` **không** bị theo dõi.
- Tạo nhánh `chore/T00-claude-setup`. Commit `CLAUDE.md`, `docs/`, `.claude/` và `.gitignore`.
- Build và ghi kết quả vào PROGRESS (baseline):
  - `dotnet --info`
  - `dotnet build AppDelicare/AppDelicare.sln`: số lỗi và số cảnh báo
  - **Không** build hay cài gì trong `app-delicare-client/` (ngoài phạm vi).
- **DB dev = `AppDelicare` trên SQL Server local** (xem LOCAL_DEV.local.md):
  - Kết nối bằng connection string trong `appsettings.json` / user-secrets (không in mật khẩu).
  - Truy vấn **chỉ đọc** `INFORMATION_SCHEMA.TABLES/COLUMNS`, so với `docs/reference/20261113_structure_db_appdelicare.sql`. Báo: DB đang có bao nhiêu bảng (91 = chưa nâng cấp; 124 = đã chạy script nâng cấp), lệch cột nào.
  - Nếu DB chưa nâng cấp: **dừng và hỏi anh** (D-14) có chạy `DB/migrations/20261113_restore_structure_db_appdelicare.sql` không. Nếu anh đồng ý: backup trước (`BACKUP DATABASE` ra thư mục `backup-db/`), chạy script, kiểm tra lại đủ 124 bảng.
  - **Lấy stored procedure hiện có ra file:** đọc `sys.procedures` + `OBJECT_DEFINITION` (chỉ đọc), ghi mỗi SP thành `DB/sp/<tên>.sql` dạng `CREATE OR ALTER`. Cùng làm với function / view nếu có. Liệt kê SP mà code gọi (`QueryMultipleUsingStoreProcAsync("usp...")`) nhưng **không có** trong DB.
  - Chạy WebApi, gọi `POST api/Authentication/Login` bằng tài khoản dev admin (LOCAL_DEV.local.md) qua Swagger hoặc `.http`, rồi gọi thử 1 API có `[Authorize]`.
- Liệt kê bí mật đang nằm trong code **theo vị trí file** (không ghi giá trị).
- AC:
  - [x] Nhánh `chore/T00-claude-setup` có commit tài liệu.
  - [x] PROGRESS có kết quả build backend.
  - [x] Có báo cáo lệch giữa DB dev và DDL 124 bảng (hoặc ghi rõ chưa kết nối được và lý do).
  - [x] `DB/sp/` chứa toàn bộ SP lấy từ DB dev; có danh sách SP code gọi mà DB thiếu.
  - [x] Đăng nhập qua API thành công, nhận token, gọi được API có bảo vệ.
  - [x] Có danh sách vị trí bí mật.

---

## GIAI ĐOẠN 1 – Làm sạch lõi (CODE_AUDIT A1–A12)

### T01 – Đưa bí mật ra khỏi code · phụ thuộc T00 · `DONE` (2026-11-14)
- Chuyển ra cấu hình: khóa ký JWT (`ConfigureServices.cs`), key ApplicationInsights QuickPulse, license GemBox (`Startup.cs`), connection string. Đọc từ `IConfiguration`: `Jwt:SigningKey`, `ApplicationInsights:QuickPulseApiKey`, `Licenses:GemBox`.
- Bật `dotnet user-secrets` cho WebApi. Tạo `appsettings.Development.json.example` chỉ chứa placeholder.
- Bật `RequireExpirationTime = true`. Thời hạn token lấy từ cấu hình.
- Thêm `docs/claude/SECRETS_TODO.md`: danh sách khóa anh cần **tạo mới / đổi** (không ghi giá trị cũ).
- AC: grep repo không còn chuỗi bí mật cũ; app chạy được khi secret nằm trong user-secrets; thiếu secret thì báo lỗi rõ lúc khởi động.

### T02 – Định danh người thao tác (ICurrentActor) · phụ thuộc T00 · `DONE` (2026-11-14)
- Bỏ hành vi fallback "user đầu tiên" trong `AuthenticatedService`. Tạo `ICurrentActor { ActorType, UserId?, AgentCode?, CorrelationId }`.
  - HTTP: lấy từ claim.
  - Job: actor `SYSTEM` (User dịch vụ cấu hình `SystemUsers:JobUserId`).
  - ToolGateway: actor `AGENT`.
- Viết lại `UserInfo` bằng truy vấn có tham số.
- `DatabaseContext.SaveChangesAsync` dùng `ICurrentActor`. Không có actor khi ghi thì ném `UnauthorizedActorException`.
- Middleware gắn `CorrelationId` (header `X-Correlation-Id` hoặc tự sinh) và đưa vào Serilog scope.
- AC: unit test chứng minh ghi khi không có actor thì bị chặn; request có token thì CreatedBy đúng; log có CorrelationId.

### T03 – Pipeline HTTP chuẩn · phụ thuộc T01 · `DONE` (2026-11-14)
- Sửa thứ tự middleware: ExceptionHandler → HttpsRedirection → Static → Routing → CORS (danh sách origin lấy từ `Cors:AllowedOrigins`) → Authentication → Authorization → Endpoints. Bỏ `ServiceLocator.BuildServiceProvider`. Bỏ `CorsMiddleware` thừa.
- Global exception handler: lỗi chưa bắt → log + 500, không lộ stack trace.
- Áp dụng **quy ước spec mục 1.2** cho toàn bộ route `/api/v1`: envelope `{success, data, message, errors, traceId}`, ProblemDetails / mã lỗi theo Phụ lục A, phân trang chuẩn, camelCase, lọc cột deprecated và `PassWord`. Làm thành base class / filter / middleware dùng chung (`ApiControllerBase`, `EnvelopeResultFilter`, `ExceptionToErrorMiddleware`).
- Hỗ trợ `If-Match` / ETag (ModifiedDate), `Idempotency-Key` (lưu kết quả lệnh theo key – nơi lưu: `IntegrationLog` với `IdempotencyKey` có sẵn, PROPOSED), header `X-Branch-Id`.
- Route CRUD cũ sinh tự động: không đụng ở task này (sẽ được thay dần bằng v1 ở T45B…).
- **D-30 (2026-11-15):** phần v1 của task này (envelope, ApiControllerBase, Idempotency, ETag, X-Branch-Id) **không dùng nữa**, T45R đưa vào `_obsolete/`. Giữ phần chung: thứ tự middleware, CORS, exception handler, Swagger + JWT, CorrelationId.
- Bật Swagger mô tả đầy đủ (XML comments), JWT trong Swagger, nhóm theo module.
- AC: swagger chạy; test tích hợp cho 1 endpoint v1 mẫu: 200 đúng envelope, 400 / 422 đúng mã lỗi, 409 khi If-Match lệch, Idempotency-Key gửi 2 lần chỉ ghi 1 lần.

### T04 – Nâng gói lên .NET 8 đồng bộ · phụ thuộc T00 · `DONE` (2026-11-14)
- EF Core 8.x, `Microsoft.AspNetCore.Authentication.JwtBearer` 8.x, `Microsoft.AspNetCore.OpenApi` 8.x, Swashbuckle 6.5+, `Microsoft.IdentityModel.Tokens` cùng họ, `EFCore.BulkExtensions` 8.x. Không nâng gói thương mại (EvoPdf, Syncfusion, GemBox) nếu không cần.
- AC: build 0 lỗi; số cảnh báo không tăng so với baseline; app chạy.

### T05 – Bộ sinh mã mới + đồng bộ entity với 124 bảng · phụ thuộc T04 · `DONE` (2026-11-14)
- Tạo `tools/AppDelicare.CodeGen` (console .NET 8). Đọc `docs/reference/20261113_structure_db_appdelicare.sql` (parse `CREATE TABLE [dbo].[X]`, cột, kiểu, null, default). Sinh đúng **mẫu file hiện có**: Entity (kế thừa BaseEntity, `[Table("TênThật")]`), Request, SearchRequest, ViewModel, IRepository/Repository, I/Service, Controller, mục UnitOfWork, DbSet, cấu hình AutoMapper, SP `DB/sp/usp<Entity>_GetAll.sql` (CREATE OR ALTER, phân trang, sort, SearchText).
- Tên class: bỏ hậu tố legacy (`Dishe`→`Dish`…) theo bảng ở CODE_AUDIT mục C. Kiểu `decimal(p,s)` sinh kèm `[Precision]`.
- Cặp cột trùng: theo `docs/reference/DEPRECATED_COLUMNS.md`. Thêm `DeletedBy` vào BaseEntity, cấu hình EF bỏ cột không tồn tại theo từng bảng.
- Chạy cho các entity **hiện có**, so sánh diff:
  - Sửa sai lệch cột.
  - Gắn `[Table]`.
  - Bỏ `IdentityDbContext` nếu không dùng (xác minh trước).
  - Entity không có bảng (`AppConfig, Area, Branch, Department, Employees, Leads, Opportunity, Products, Region`) chuyển vào `_obsolete/` (kèm repository, service, controller liên quan) và bỏ khỏi DbContext. Không cần xét Angular (D-20).
- Ghi `tools/AppDelicare.CodeGen/README.md`: cách chạy và cờ `--only <Table>`, `--skip-existing`.
- AC: build xanh; test "mọi DbSet map tới bảng có trong DDL và mọi property map tới cột có thật" (đọc DDL trong test) xanh; không còn entity map tới bảng không tồn tại.

### T06 – Sinh mã cho 35 bảng còn thiếu · phụ thuộc T05 · `DONE` (2026-11-14)
- Dùng CodeGen sinh CRUD cho danh sách CODE_AUDIT A1(c), trừ 2 bảng `_Legacy_20261113` (chỉ sinh entity đọc).
- Với các sổ chỉ ghi thêm (`IntegrationLog`, `ApprovalDecision`, `TemperatureLog`, `PackagingLedger`, `AttendanceEvent`, `DeliveryEvent`, `ComplaintHistory`) **không sinh** Update/Delete (xem T08).
- AC: build xanh; script SP nằm trong `DB/sp/`; DbSet đủ 124 − 2 legacy (hoặc đủ 124 nếu có entity đọc).

### T07 – Phân quyền theo UserRole × ObjectMenu · phụ thuộc T05 · `DONE` (2026-11-14)
- Attribute `[Permission("<MetaLink>", PermissionAction.View|Add|Edit|Delete|Print|Export|Import)]` + `PermissionFilter`. Đọc UserRole theo UserGroup của user, cache 5′. `User.IsAdmin` bỏ qua kiểm tra.
- Gắn attribute cho mọi controller (CodeGen cập nhật template). MetaLink = `#/<screen-id>` theo `screen_id` của mockup (`docs/mockup/js/data/catalog.js`), đúng quy ước spec mục 1.2.
- Script seed `DB/seed/ObjectMenu_seed.sql` theo menu P0–P8 của mockup (`[DEMO]` không cần vì đây là dữ liệu cấu hình; ghi nguồn).
- **Seed cơ cấu tổ chức thật** theo `docs/claude/ORG_STRUCTURE.md`: `OrganizationUnit` (cây CEO → khối → bộ phận → tổ), `UserGroup` (mỗi vai trò), `UserRole` theo ma trận quyền mục 2 (PROPOSED, D-25). Các tổ bếp đồng thời seed `KitchenWorkstation`. Script idempotent theo Code / Name.
- Thay kiểm tra xóa bằng SQL động bằng `ReferenceGuard`: danh sách quan hệ logic (cột `XxxId` → bảng `Xxx`) sinh từ DDL; khi xóa mềm thì đếm tham chiếu bằng truy vấn có tham số.
- AC: test – user không có AllowAdd gọi Insert thì 403; admin thì 200; xóa bản ghi đang được tham chiếu thì 409; seed tổ chức chạy 2 lần không nhân bản; user nhóm OPS-SCM không duyệt được kế hoạch sản xuất.

### T08 – Khóa sổ chỉ ghi thêm · phụ thuộc T06 · `DONE` (2026-11-14)
- Trong `DatabaseContext`: các entity sổ (CLAUDE.md 3.3) có `EntityState.Modified` / `Deleted` thì ném `AppendOnlyViolationException`.
- Bỏ endpoint Update/Delete của các sổ. Thêm `InventoryLedgerService.Reverse(transactionId, reason)` ghi bút toán đảo.
- AC: test cập nhật / xóa sổ bị chặn; bút toán đảo làm tồn trở về đúng.

### T09 – Dự án test · phụ thuộc T02 · `DONE` (2026-11-14)
- `AppDelicare.Tests` (xUnit, FluentAssertions). Unit test cho Application. Integration test dùng DB dev (connection `ConnectionStrings:TestDb`), mỗi test chạy trong transaction rồi rollback. Không có DB thì skip có thông báo.
- AC: `dotnet test` chạy được trên máy anh; README test.

---

## GIAI ĐOẠN 2 – Nghiệp vụ lõi (Application)

### T10 – Khung AppDelicare.Application · phụ thuộc T05, T09 · `DONE` (2026-11-14)
- Project mới. Cấu trúc thư mục theo miền (ARCHITECTURE §1).
- Mẫu `Command` / `Query` / `Handler` / `Validator` (FluentValidation). `IClock` (giờ VN). `BusinessRulesOptions` (BUSINESS_RULES). Dispatcher sự kiện sau commit. `Result` chuẩn.
- Controller `/api/v1/...` mỏng, chỉ gọi handler.
- AC: 1 use case mẫu (`Common.LookupQuery`) chạy end-to-end và có test.
- **D-30 (2026-11-15):** project này **không mở rộng**. Nghiệp vụ viết trong `AppDelicare.Services` theo khung cũ. T45R chuyển `IClock` / `BusinessRulesOptions` sang tầng cũ và đưa project vào `_obsolete/`.

### T11 – Mã chứng từ · phụ thuộc T10 · `DONE` (2026-11-16)
- `IDocumentNumberService` theo BUSINESS_RULES §11, dùng `sp_getapplock` + MAX cột đích.
- AC: test chạy song song 50 lần không trùng mã; đúng định dạng cho mọi loại.

### T12 – ApprovalService · phụ thuộc T10, T11 · `DONE` (2026-11-16: 1 cấp; 2026-11-17 BE2.3: nhiều cấp theo ApprovalStep, hạn = cờ, hủy ở mọi cấp)
- Tạo / duyệt / từ chối / hết hạn. Nhiều bước theo `ApproverRoleCode`. `ApprovalDecision` chỉ ghi thêm, có `PayloadHash` chống sửa payload. Khi được duyệt thì thực thi lệnh gốc (registry `BusinessType` → handler).
- Viết trong `ApprovalRequestService` / `ApprovalDecisionService` cũ (+ service dùng chung `ApprovalService` nếu cần, cùng mẫu). API: `GET api/ApprovalRequest/GetMyPending`, `POST api/ApprovalRequest/Decide`.
- AC: test tạo → duyệt → thực thi; từ chối không ghi; payload bị sửa thì từ chối.

### T13 – Đơn hàng · phụ thuộc T12 · `DONE(PILOT)` (2026-11-16: máy trạng thái, giờ chốt 20h D-1, lệch suất > 30% → duyệt, giá, số PH-; job, lệch so lịch sử 4 tuần, sửa sau giờ chốt qua duyệt, dự báo → DEFERRED(PILOT))
- Máy trạng thái Order. Submit / Confirm / Cancel. Kiểm tra giờ chốt 20h D-1. Kiểm tra lệch suất > 30%. Tính giá theo ContractMeal / PricePolicie.
- Chức năng theo spec mục P3 trong `DELICARE_API_Specification.md`, viết vào `OrderService` / `OrderLineService` + action `OrderController.Submit / Confirm / Cancel` (khung cũ).
- AC: test các nhánh cutoff và deviation; số tổng của đơn đúng.

### T14 – MRP và đề xuất mua · phụ thuộc T13 · `DONE(PILOT)` (2026-11-16: `MaterialRequirement/Calculate` bấm tay – đơn Locked × công thức khóa (snapshot) × hao hụt, quy đổi đơn vị, trừ tồn xuất được, + SafetyStock (D-12), MOQ, PurchaseRequest Draft PH- + gợi ý NCC, kế hoạch neo KH- (GAP-MRP-01…06); job 20:30 / 21:00, hàng sắp về từ PO, giữ chỗ MaterialReservation, duyệt PR 2 cấp → DEFERRED(PILOT) / T15)
- Tính MaterialRequirement từ đơn Locked × Recipe (đã khóa) × hao hụt, quy đổi đơn vị. Thiếu hụt so với tồn khả dụng và giữ chỗ. Sinh PurchaseRequest nháp, gợi ý NCC (SupplierMaterial).
- Khóa định mức: `Recipe.SnapshotJson` / `LockedAt` / `LockedBy`.
- AC: test số liệu với bộ dữ liệu mẫu nhỏ (tính tay được).

### T15 – Mua hàng, nhận hàng, QC đầu vào · phụ thuộc T14, T16 · `DONE(PILOT)` (2026-11-16: PR → PO (ConvertToPurchaseOrder theo NCC ưu tiên, PO- ), PO duyệt 1 cấp qua engine T12 (PURCHASE_ORDER, `#/wf-inbox?type=scm-po`), phiếu nhập PH- theo PO đã duyệt + nhiệt độ nhận, QC DAT / DAT_MOT_PHAN / KHONG_DAT, ghi sổ: lô NL-, nhập kho phần đạt, TemperatureLog RECEIVING, SupplierIssue; in Đơn đặt hàng NCC + Phiếu trước sản xuất (GAP-PUR-01…08). Duyệt PR 2 cấp, duyệt PO 2 cấp theo ngưỡng D-11, trả hàng NCC, gửi PO qua email / Zalo, hàng sắp về cho MRP → DEFERRED(PILOT))
- PR → PO (duyệt theo giá trị, ngưỡng PROPOSED trong cấu hình) → GoodsReceipt → QC (DAT / DAT_MOT_PHAN / KHONG_DAT) → MaterialLot `NL-…` → InventoryTransaction IN. SupplierIssue khi không đạt. TemperatureLog RECEIVING.
- AC: test lô KHONG_DAT không xuất được; DAT_MOT_PHAN chỉ nhập phần đạt.

### T16 – Sổ kho (InventoryLedgerService) · phụ thuộc T08, T11 · `DONE(PILOT)` (2026-11-16: IN / OUT / ADJUSTMENT / REVERSAL / TRANSFER, tồn + tính lại, FEFO / FIFO, chặn lô, phiếu xuất, kiểm kê ghi nhận; duyệt kiểm kê lệch > 2% (Pilot: chỉ admin ghi sổ), duyệt điều chỉnh, giữ chỗ MaterialReservation, job hết hạn lô → DEFERRED(PILOT))
- Post IN / OUT / ADJUSTMENT / REVERSAL. Tính lại InventoryBalance. Gợi ý lô xuất theo `InventoryPolicie.UseFefo`: FEFO (mặc định) hoặc FIFO theo ngày nhận (D-24). Luôn chặn lô hết hạn, chặn lô KHONG_DAT; cảnh báo khi còn lô nên xuất trước. Kiểm kê: lệch > 2% thì duyệt. Giữ chỗ (MaterialReservation).
- AC: test FEFO, chặn, tồn sau chuỗi giao dịch, đảo bút toán.

### T17 – Sản xuất · phụ thuộc T14, T16 · `DONE(PILOT)` (2026-11-16: kế hoạch KH- → `Approve` → `Release` (lệnh SX PH- + mẻ ME-, lịch ngược từ giờ xuất bếp, chia mẻ theo dây chuyền), xuất NL cho mẻ FEFO (`CreateMaterialIssue`), Start / Finish, nhiệt độ lõi ≥ 70 °C chặn đạt QC / đóng gói, lưu mẫu SAMPLE_RETAINED bắt buộc trước khi đạt, sản lượng / hao hụt, in Phiếu sản xuất (GAP-PRD-01, 03…08). Công đoạn theo tổ, nhiều ca / ngày, giữ chỗ MaterialReservation, đóng / hủy lệnh SX → DEFERRED(PILOT))
- Kế hoạch ngược từ giờ xuất bếp (cutoff − 60′), chia mẻ theo ProductionLine, mã `ME-…`, cấp NL cho mẻ (MaterialIssue FEFO), sự kiện mẻ (bắt đầu / xong), nhiệt độ lõi ≥ 70°C (TemperatureLog CORE), lưu mẫu (FoodSafetyChecklistResult SAMPLE_RETAINED), sản lượng / hao hụt (ProductionOutput / MealWasteRecord).
- **Công đoạn theo tổ** (ORG_STRUCTURE.md): mỗi mẻ đi qua Sơ chế → Nấu → Cơm/Canh (nếu có) → Chia suất/Đóng gói; mỗi công đoạn ghi bắt đầu / kết thúc / người làm / tổ (`KitchenWorkstationId`) bằng `FoodSafetyChecklistResult` hoặc sự kiện mẻ có sẵn (chọn theo DB_SCHEMA_124.md, thiếu thì GAP). API tiến độ bếp theo tổ.
- AC: test lập kế hoạch đúng giờ; mẻ < 70°C bị chặn đóng gói; không chuyển công đoạn khi công đoạn trước chưa xong.

### T18 – ATTP và truy xuất · phụ thuộc T17 · `DONE(R1 nghiệp vụ)` (BE2.5 2026-11-17: checklist theo ca / mẻ + hạn tương đối, seed QA_FG / 5S / PREP…, lỗi trọng yếu giữ mẻ; truy xuất 2 chiều – BE2.1. **Báo cáo NCR tổng hợp → nhóm "Để sau" (báo cáo)**)
- Checklist theo ca với hạn tương đối (BUSINESS_RULES §6). Seed checklist **kiểm soát thành phẩm** (QA 4.3): cảm quan, trọng lượng suất, lưu mẫu + người ký; checklist **5S / vệ sinh bếp** (QA 4.4). Truy xuất xuôi lô → mẻ → đơn / khách và ngược mẻ → lô.
- **Báo cáo NCR tổng hợp** (QA 4.5, GAP-QA-01 tạm): gom SupplierIssue, CustomerComplaint, các dòng checklist / nhiệt độ không đạt kèm CorrectiveAction, lọc theo kỳ / bộ phận.
- AC: test truy xuất 2 chiều trên dữ liệu mẫu.

### T19 – Giao vận và phản ánh · phụ thuộc T17 · `DONE(PILOT)` (2026-11-16: chuyến CX- + đơn giao PH- + điểm dừng, nhiệt độ xuất phát / bàn giao ≥ 60 °C chặn xuất phát / bàn giao (TemperatureLog DELIVERY + DeliveryEvent), mẻ chưa đóng gói / chưa lưu mẫu chặn xuất phát (D-42), POD, đơn → Delivered, đối soát suất phục vụ → Reconciled, phản ánh PA- tạo / giao xử lý / đã xử lý / đóng / từ chối, in biên bản giao hàng (GAP-DLV-01…07). Cảnh báo SLA tự động, báo cáo suất theo điểm, xếp hàng (DeliveryLoading / Execution), tách đơn giao, PackingOrder → DEFERRED(PILOT))
- DeliveryRoute `CX-…`, DeliveryStop, DeliveryEvent, nhiệt độ ≥ 60°C, thiếu / thừa suất, ProofOfDeliverie. CustomerComplaint `PA-…` với SLA 4/24/72h.
- **Điểm phát suất (mô hình a, D-21)**: nhân viên điểm (nhóm OPS-POS) xác nhận nhận hàng, ghi **suất thực tế phục vụ** → `OrderReconciliation` (đặt / giao / thực tế / chênh lệch); báo cáo suất theo điểm, ngày, buổi.
- AC: test SLA và cảnh báo nhiệt độ.

### T20 – Hộp / khay và vệ sinh · phụ thuộc T19 · `DONE` (BE2.5 2026-11-17: sổ bao bì OUT khi bàn giao / IN khi thu hồi / LOSS ghi tay / AVAILABLE khi vệ sinh đạt, tỷ lệ thất thoát theo khách + cờ vượt ngưỡng, mẻ vệ sinh VS- 6 bước đi lần lượt – D-54. Sự kiện vượt ngưỡng tự động (job / bot) → để sau)
- PackagingLedger OUT / RETURN / LOSS, tỷ lệ thất thoát theo khách, sự kiện vượt ngưỡng. CleaningBatche `VS-…` 6 bước.
- AC: test tỷ lệ thất thoát; lô vệ sinh phải đủ 6 bước mới hoàn tất.

### T21 – Nhân sự · phụ thuộc T10 · `DONE` 2026-11-17 qua BE2.6 (T21.1 seed Shift: `DONE` 2026-11-14)
- Seed Shift (TRUA, TOI, DEM_23H, SANG_6H). ShiftAssignment, AttendanceEvent (GPS so với KitchenWorkstation, QR, máy, chấm hộ), LeaveRequest, ShiftSwapRequest (duyệt).
- AC: test hợp lệ theo bán kính; chấm hộ phải ghi rõ SourceChannel.

### T22 – KPI · phụ thuộc T13–T21 · `DONE` 2026-11-17 qua **BE3.4** (seed 11 chỉ tiêu, snapshot ngày / tháng, test công thức đủ 11 KPI trong `Kpi/KpiTests`; rà lại 2026-11-18)
- Seed KpiMetricDefinition (BUSINESS_RULES §12). Tính KpiDailySnapshot / KpiMonthlySnapshot. API dashboard theo spec mục 12.
- AC: test công thức cho từng KPI.

### T23 – Job tự động (`AppDelicare.Job`, D-48) · phụ thuộc T13, T14 · **T23.1 `DONE` 2026-11-17** (anh chọn làm sau BE-2; nhánh `feat/T23-jobs`; 5 job LockOrders / Mrp (+ đề nghị mua) / LotExpiry / ContractExpiry / MarkAbsent, `api/Job/GetAll` / `Run`, `Contract/ExpireOverdue`; ApprovalExpiry / ComplaintSla không làm – cờ `isOverdue` đã có, chờ bot; PROPOSED D-59) · T23.2 `TODO` (cùng đợt bot / KPI) (R1: T23.1 = LockOrders 20:00, Mrp 20:30, PurchaseProposal 21:00, ApprovalExpiry, ComplaintSlaWatch; T23.2 = KpiDailySnapshot 22:00, RemindUnconfirmedOrders 19:00 – trong R1 chỉ tạo danh sách + thông báo trong app, chưa gửi Zalo/Telegram) · **Phần gửi tin của T23.2 (RemindUnconfirmedOrders, ApprovalExpiry / ComplaintSla dạng thông báo) chuyển sang BOT-2.3 (D-62)**
- Project / container riêng `AppDelicare.Job` theo **ARCHITECTURE.md mục 1b** (Worker Service, không Hangfire, không thêm bảng, khóa `sp_getapplock`, DI dùng chung với WebApi, tài khoản `system`). Lịch theo AGENTS.md §2, múi giờ Asia/Ho_Chi_Minh. Mỗi lần chạy ghi `AnalyticsRefreshLog` (JobCode, RunId, Started / Completed, RunStatus, RowsProcessed).
- `JobController` (khung cũ) trong WebApi: `GetAll` (job, bật / tắt, lần chạy kế tiếp), `Run` (chỉ admin, gọi thẳng Service). docker-compose thêm service `job`; IIS: Windows Service.
- AC: chạy tay từng job được; job idempotent (chạy 2 lần không nhân đôi dữ liệu).

---

## GIAI ĐOẠN 3 – Cổng công cụ (Tool Gateway)

### T30 – Project AppDelicare.ToolGateway · phụ thuộc T10 · `DEFERRED(R2)`
- ASP.NET Core host riêng, gói MCP chính thức cho C# (`ModelContextProtocol` + `ModelContextProtocol.AspNetCore`, transport HTTP/SSE). Tham chiếu Application.
- AC: một MCP client (vd. MCP Inspector) liệt kê được tool `ping`.

### T31 – Định danh agent · phụ thuộc T30, T02 · `DEFERRED(R2)`
- Seed User dịch vụ cho 12 agent + UserGroup `AGENT_<CODE>` + `BotConfiguration`. Xác thực bằng API key (hash lưu ngoài repo; `BotConfiguration.SecretReference` chỉ chứa tên secret).
- AC: key sai thì 401; agent bị DISABLED thì 403.

### T32 – Công cụ đọc · phụ thuộc T31, các query ở giai đoạn 2 · `DEFERRED(R2)`
- Đăng ký 26 tool đọc theo tên hàm cũ (LEGACY_GATEWAY.md) + tool đọc mới theo AGENTS.md. Kiểm quyền theo agent và vai trò. Giới hạn số dòng. Che dữ liệu cá nhân.
- AC: test mỗi tool trả JSON đúng schema; agent gọi tool ngoài danh sách thì bị từ chối.

### T33 – Công cụ ghi 2 pha · phụ thuộc T12, T32 · `DEFERRED(R2)`
- `propose_*` → ApprovalRequest(AGENT_WRITE) → xác nhận (Telegram callback / Mission Control / tool `confirm` của người dùng thật) → `commit` idempotent. Hết hạn 15′.
- AC: test propose không ghi dữ liệu nghiệp vụ; commit 2 lần chỉ ghi 1 lần; quá hạn thì từ chối.

### T34 – Log và an toàn · phụ thuộc T32 · `DEFERRED(R2)`
- IntegrationLog cho mọi lệnh. Giới hạn tần suất theo agent. Công tắc chỉ đọc toàn cục. API xem log cho Mission Control.
- AC: mọi lệnh có log với CorrelationId; vượt giới hạn tần suất thì 429.

### T35 – POST /api/gateway tương thích · phụ thuộc T32, T33 · `DEFERRED(R2)` – **tùy chọn**, chỉ làm nếu anh yêu cầu (D-16)
- Nhận `{fn, args, telegram_id}`, luôn HTTP 200 `{ok,…}`, mã lỗi như cũ, ánh xạ 34 hàm. Phiếu: `kiem_tra_phieu` / `tao_phieu` / `xac_nhan_phieu` / `huy_phieu` / `duyet_phieu` dùng cột tiếng Việt cũ.
- AC: bộ test hợp đồng: 34 fn × (thành công, không quyền, tham số sai).

---

## GIAI ĐOẠN 4 – Kênh, Mission Control, agent

### T40 – Telegram · ~~phụ thuộc T33~~ · `REPLACED` → **ĐỢT BOT (D-62, 2026-11-18)** – nội dung dưới chỉ còn để tham khảo
- Webhook theo botCode, trả 200 ngay, lưu InboundMessage, xử lý bất đồng bộ. OutboundMessage worker (retry, idempotent). Nút inline xác nhận / từ chối cho AGENT_WRITE và Approval. Liên kết `UserExternalIdentity`.
- AC: test webhook giả lập; xác nhận bằng callback chạy đúng commit.

### T41 – Zalo OA · phụ thuộc T40 · `DEFERRED(R2)` (còn chờ D-06) · **Kiến trúc theo D-62:** kênh `Channels/Zalo` + `Audiences/Customer` trong `AppDelicare.Messaging`, API riêng cho khách (BOT_SOURCE_ARCHITECTURE.md §8)
- Webhook, gửi tin, các lệnh của khách (AGENTS.md CSKH).

### T42 – Angular: dọn template và khung DELICARE · phụ thuộc T05 · `DEFERRED(FE)`
- Ẩn hoặc gỡ route demo của template w3crm (pages/*) khỏi menu. Sidebar sinh từ ObjectMenu theo quyền. Đặt màu thương hiệu #91AF42 / #F07825 (theo mockup). Interceptor xử lý lỗi chuẩn (T03).
- AC: `npm run build` xanh; menu hiển thị theo quyền.

### T43 – Cấu hình agent · phụ thuộc T32 · `DEFERRED(R2)`
- Thư mục `agents/` (AGENTS.md §4): 12 file mô tả agent + `hermes.config.example.yaml` trỏ tới MCP của ToolGateway.
- AC: anh nạp được vào Hermes theo README trong `agents/`.

### T44 – Mission Control · `DEFERRED(FE)` (UI). Phần backend (API hộp duyệt, cảnh báo) đã nằm trong T12 / T46B / T47B
- Module Angular `/mission-control`: hộp duyệt, hàng chờ xác nhận lệnh agent, dòng hoạt động agent, trạng thái job, KPI hôm nay, cảnh báo.
- AC: duyệt / từ chối được từ UI; dữ liệu khớp API.

### T45 – Màn hình nghiệp vụ P0–P2 · `DEFERRED(FE)`
- Theo mockup và spec: quản trị, danh mục (khách, điểm giao, NCC, nguyên liệu, món, công thức, đơn vị…). Dùng mẫu module supplier. Có thể sinh khung bằng CodeGen (thêm template Angular).
- AC: CRUD chạy với API thật cho từng màn hình trong danh sách P0–P2 của `catalog.js`.

### T46 – Màn hình nghiệp vụ P3–P5 · `DEFERRED(FE)` (ghi chú bố cục bên dưới dùng lại cho T46B)
- Hợp đồng, thực đơn, đơn hàng, mua hàng, kho, kế hoạch sản xuất, giá thành.
- **Bám bố cục file thật** (BUSINESS_DOCS.md): thực đơn tuần theo buổi × lựa chọn có giá vốn / FC; nhập suất theo khách → địa điểm → vị trí; so sánh báo giá → PO.
- **Mẫu in** (GemBox Document / Excel export) theo file thật: Thực đơn gửi khách, Phiếu trước sản xuất, Phiếu sản xuất, Đơn đặt hàng NCC. Header pháp nhân lấy từ cấu hình.

### T47 – Màn hình nghiệp vụ P6–P8 · `DEFERRED(FE)`
- QC / ATTP, đóng gói / giao vận / phản ánh / hộp, nhân sự, KPI dashboard.


### T45R.8 – Hoàn thiện quản trị người dùng (D-37, D-38, D-39) · phụ thuộc T48.0, FE-1 · **Backend `DONE` (2026-11-16), FE `DONE` 2026-11-20** (tạo người dùng + nhóm + mật khẩu tạm: UG-1 C-06; nút **Lấy từ nhân viên** → Person/Insert; mật khẩu tạm → hộp **đổi mật khẩu bắt buộc** khi mở web)
Theo cơ chế cũ (D-30, D-31): chỉ sửa `PersonService`, `UserService`, `LoginService`, `EncriptFunctionsHelper`, controller cùng tên, DTO Custom; **không thêm bảng / cột**.

- **D-37 – Tắt menu cũ:** script `DB/seed/ObjectMenu_disable_legacy.sql` (idempotent) đặt `IsActive = 0` cho các mục ObjectMenu của app cũ (MetaLink kiểu cũ như `quan-ly-dang-nhap`, `quan-ly-vung`… – liệt kê đúng 26 mục đã phát hiện ở FE-0 theo Id + MetaLink trong comment). Không xóa, không đụng `UserRole`. Chạy trên DB dev, đưa vào `deploy` cho staging. Nhóm menu cấp 1 chỉ tắt khi không còn con đang bật.
- **D-38 – Tạo người dùng 1 bước, đúng 1 tài khoản:**
  - `PersonCustomRequest` thêm trường tùy chọn: `UserName`, `ListUserGroupId`, `IsActive` (chỉ là trường request, không thêm cột).
  - `PersonService.Insert` → `UserService.InsertUserAfter(personId, fullName, typeSave, userName?, listUserGroupId?)`: có `UserName` thì dùng (kiểm trùng, không dấu, không khoảng trắng), không có thì tự sinh như cũ; gán nhóm qua `UserClaimService.SaveAfterUser`; tất cả **trong cùng transaction** của Person. Mật khẩu ban đầu theo D-39 (mật khẩu tạm).
  - Action `Person/Insert` trả thêm thông tin tài khoản vừa tạo: `Result<PersonUserCreatedViewModel>` { PersonId, UserId, UserName, TempPassword } – ViewModel Custom.
  - `User/Insert` (gắn tài khoản cho hồ sơ có sẵn) **chặn** khi Person đã có User (1 Person ↔ tối đa 1 User); `UserName` không trùng (không phân biệt hoa thường).
  - Frontend `adm-user` → "Tạo mới" = **một form** gồm: Hồ sơ (Họ tên, SĐT, Ngày sinh, Giới tính, Địa chỉ) + Tài khoản (Tên đăng nhập – gợi ý tự sinh, Nhóm quyền, Đang dùng) → gọi `Person/Insert`; xong hiện popup mật khẩu tạm (D-39). Nút phụ "Tạo tài khoản cho hồ sơ có sẵn" chỉ liệt kê Person **chưa có** User (`Person/GetAllDataByKey` hoặc lọc trong SP).
  - Nút "Lấy từ nhân viên" trên form: chọn `Employee` để điền sẵn Họ tên / SĐT / Ngày sinh / Giới tính / Địa chỉ (chỉ điền form, **không** lưu liên kết – DB không có cột Employee↔Person/User; ghi **GAP-ADM-03 PROPOSED**).
  - `adm-person`: chỉ xem / sửa hồ sơ; bỏ nút Tạo mới (tránh tạo tài khoản ngoài ý muốn).
- **D-39 – Mật khẩu tạm khi reset và khi tạo tài khoản:**
  - Sinh mật khẩu tạm **12 ký tự** bằng RNG mật mã (`RandomNumberGenerator`), bỏ ký tự dễ nhầm (0/O, 1/l/I), có chữ hoa, chữ thường, số.
  - Lưu trong `User.PassWord` dạng `PBKDF2T$<hết hạn unix>$100000$<salt>$<hash>` (chữ **T** = tạm, hạn **24 giờ**) – không thêm cột.
  - `User/ResetPassword` (chỉ admin) trả `Result<string>`: `Data` = mật khẩu tạm, **chỉ trả một lần**, không log, không lưu bản rõ; ghi AuditChange "reset bởi ai, cho ai" (không có mật khẩu).
  - Đăng nhập bằng mật khẩu tạm: hết hạn → từ chối, thông báo "Mật khẩu tạm đã hết hạn, liên hệ quản trị"; còn hạn → đăng nhập được, response thêm trường `mustChangePassword = true` (chỉ thêm trường) và token có claim `pwd_temp`. `PermissionFilter` chặn mọi API (403, thông báo "Cần đổi mật khẩu") **trừ** `User/ChangePassword`, `User/GetCurrent`, `Authentication/*`.
  - `User/ChangePassword` thành công → lưu PBKDF2 thường (bỏ dấu tạm).
  - Frontend: popup hiện mật khẩu tạm + nút Sao chép + cảnh báo "chỉ hiện một lần"; đăng nhập có `mustChangePassword` → chuyển thẳng màn Đổi mật khẩu, không vào được màn khác.
- **AC:**
  - [x] Test: tạo người dùng 1 bước → đúng 1 Person + 1 User + nhóm đúng; `User/Insert` cho Person đã có User bị chặn; UserName trùng bị chặn. (`Admin/UserAccountTests`)
  - [x] Test: reset trả mật khẩu tạm 12 ký tự; đăng nhập bằng mật khẩu tạm → chỉ gọi được ChangePassword / GetCurrent; đổi xong dùng bình thường; mật khẩu tạm quá 24h bị từ chối; mật khẩu tạm không xuất hiện trong log / AuditChange. (`Admin/UserAccountTests`)
  - [x] Script D-37 chạy 2 lần không lỗi (DB dev: lần 1 tắt 21 + 5 = 26 mục, lần 2 tắt 0); menu cũ không còn trong `listMenu` (SP lọc `IsActive = 1`). Staging: anh chạy (đã thêm vào `deploy`).
  - [~] `dotnet build` / `dotnet test` xanh (backend: 0 lỗi, 140/140); `npm run build`, UAT_CHECKLIST PILOT-FE → làm cùng phần FE; Catalog dòng 35, 38 chưa cập nhật (không có openpyxl – gợi ý trạng thái trong PROGRESS).

### FE-0 → FE-P7 – Frontend Angular cho màn hình Pilot (D-35) · theo CLAUDE.md mục 4.4
Nguyên tắc chung cho mọi FE-x: chỉ làm màn hình có API "Đã xong" trong `DELICARE_API_Catalog.xlsx`; theo khung Angular cũ (mẫu `main/unit-measure`, `services/unit-measure.service.ts`); route = screen-id; bố cục theo `docs/mockup/`; gọi đúng route trong `docs/frontend-handoff/`. AC chung: `npm run build` 0 lỗi; đăng nhập bằng user không phải admin thấy / ẩn đúng nút theo quyền; lỗi nghiệp vụ (`responseCode = 400`) hiện đúng thông báo; ghi PROGRESS + kịch bản thử tay vào `UAT_CHECKLIST.md` mục PILOT-FE.

- **FE-0 – Khung ứng dụng · `DONE` (2026-11-16, build 0 lỗi; chưa thử tay trên trình duyệt)**
  - Đăng nhập `api/Authentication/Login`; tải lại trang `api/User/GetCurrent`; đổi mật khẩu; đăng xuất. Interceptor JWT cũ giữ; 401 → về trang đăng nhập.
  - Sidebar từ `listMenu`; sửa helper `permission()` / `checkPermission()` bỏ tiền tố `#/` của MetaLink (một chỗ). Route mới = screen-id.
  - Gỡ route / menu demo w3crm (`pages/**`) và màn bệnh viện (`patient*`, `medical-*`) khỏi menu – không xóa file.
  - **Giao diện theo D-36 (CLAUDE.md 4.4 mục "Giao diện")**: ghi đè màu / font / cỡ chữ (14px thường, 13px bảng, dòng ≥ 44px, nút ≥ 36px), tên DELI-CARE + pháp nhân, sidebar nhóm theo menu cấp 1, bộ chọn chi nhánh `api/OrganizationUnit/GetBranches`, thành phần dùng chung `shared/page-header` và `shared/status-badge`, kiểu nút thao tác bảng, ngăn kéo chi tiết dùng chung (`shared/detail-drawer`).
  - Áp ngay cho màn hình cũ `main/unit-measure` làm mẫu chuẩn; chụp màn hình trước / sau, so với `docs/mockup` cùng loại màn, ghi vào PROGRESS. AC thêm: không còn chữ "phòng khám" / "W3CRM" trên giao diện; không có phần chỉ-để-trình-diễn của mockup.
  - Dropdown trạng thái dùng `api/MetaData/GetStatuses`, danh mục tham chiếu `api/MetaData/GetReferenceLists` (service dùng chung, cache trong phiên).
- **FE-1 – Quản trị (P0) · `DONE` (2026-11-16, build 0 lỗi; chưa thử tay trên trình duyệt)**: `adm-user` (tạo kèm hồ sơ Person, gán nhóm, khóa / mở, reset mật khẩu), `adm-usergroup`, `adm-permission` (ma trận `GetMatrix` / `Update` / `SaveAccessAll` / `CopyFromUserGroup` / `GetPreview`), `adm-objectmenu` (cây + kéo thả `UpdateOrder`), `adm-orgunit` (cây), `adm-employee`, `adm-position`. Rà soát màn cũ `main/user`, `main/user-group`, `main/object-menu`, `main/person` và bổ sung.
- **FE-2 – Danh mục (P1) · `TODO`**: `crm-customer` (+ drawer 360° `GetOverview`), `crm-contact`, `crm-location`, `crm-contract` + suất hợp đồng (CRUD `Contract` / `ContractMeal`), `scm-supplier` (+ `GetOverview`), `scm-supplier-material` (lọc theo ngày hiệu lực, 1 NCC ưu tiên), `scm-ingredient` (+ tồn `GetStock`), `scm-ingredient-cat`, `scm-uom`, `scm-uom-conversion` (thử quy đổi), `scm-warehouse` (+ sơ đồ `GetLayout`), `scm-zone`, `scm-location`, `scm-inv-policy`, `hr-shift`, `log-vehicle`.
- **FE-3 – Món, công thức, giá (P2) + nhập Excel · `TODO`**: `menu-dish-cat`, `menu-dish`, `menu-recipe` (phiên bản: tạo / dòng định lượng / gửi duyệt / duyệt – từ chối / khóa / giá vốn), `cost-standard` (tính lại), `crm-price-policy` (+ tra giá `Resolve`), màn **Nhập dữ liệu Excel** (`api/ImportExcel/GetTemplateList` / `GetTemplate` / `Import`, hiển thị lỗi từng dòng).
- **FE-P2 … FE-P8** – làm **ngay sau** backend tương ứng trong bảng Pilot (khi `.http` của task đó chạy được):
  - FE-P2 (sau T12): hộp duyệt `wf-inbox` (danh sách chờ duyệt của tôi, duyệt / từ chối).
  - FE-P3 (sau T13 + T46B.1): `menu-menu` (lịch tuần buổi × lựa chọn, FC%), `menu-assignment`, `crm-order` (tạo / sửa dòng / gửi / xác nhận / **Chốt**), in Thực đơn gửi khách.
  - FE-P4 (sau T16): `scm-inv-balance`, `scm-inv-txn`, `scm-lot`, `scm-issue` (gợi ý FEFO), kiểm kê (ghi nhận).
  - FE-P5 (sau T14 + T15): `scm-requirement` (nút Tính nhu cầu), `scm-pr`, `scm-po` (duyệt, in), `scm-gr` + `qc-inspection` (kết luận QC, nhiệt độ nhận), in Phiếu trước sản xuất.
  - FE-P7 (sau T17): `prod-plan`, `prod-order`, `prod-batch` (bắt đầu / xong / nhiệt độ lõi / lưu mẫu / QC), `prod-output`, `prod-waste`, `log-packing-order`, in Phiếu sản xuất.
  - FE-P8 (sau T19): `log-route`, `log-execution` / `log-pod` (nhiệt độ giao, xác nhận đã giao), `crm-reconciliation` (suất thực tế), `log-complaint` (cơ bản).

### T45R – Rà soát code cũ, chuyển nghiệp vụ v1 về khung cũ, gỡ lớp v1 · phụ thuộc T45B.4 · `DONE` (2026-11-15)
**Mục tiêu:** một khung duy nhất là code cũ (CLAUDE.md mục 4). Không mất nghiệp vụ nào đã làm ở T45B.1–.4. Không sinh API v1 mới.

- **T45R.1 – Rà soát code cũ + kiểm kê v1** → tạo `docs/claude/LEGACY_REVIEW.md`:
  - (a) Theo từng màn hình P0–P2 (`docs/mockup/js/data/catalog.js`) và nhóm Ca (`Shift`): controller / action cũ đang có, method service, SP, DTO; so với chức năng màn hình cần (mockup + spec) → cột **Có / Thiếu / Lỗi**.
  - (b) Mọi endpoint trong `Controllers/V1/**` (kể cả 39 resource của `CrudResources.Generated.cs`, `AuthAndMe`, `MetaController`, `AuditController`) và handler trong `AppDelicare.Application/**`: nghiệp vụ gì → tương đương cũ đã có chưa → **chuyển sang** action / method nào (file cụ thể).
  - Ví dụ đã thấy khi rà: v1 users lock/unlock ≡ `User/SaveIsActive`; reset ≡ `User/ResetPassword`; gán nhóm ≡ `UserRequest.ListUserGroupId` → `UserClaimService`; ma trận quyền ≡ `UserRole/GetByUserGroupId` + `UserRole/Update`; `/meta/menu` ≡ menu theo quyền từ `UserRoleService.GetByUserGroupId(…, false)`; danh sách / chi tiết / dropdown 39 resource ≡ `GetAll` / `GetById` / `GetAllData` của controller cũ.
- **T45R.2 – Quản trị P0:** chỉ bổ sung phần còn **thiếu** vào `UserService` / `UserClaimService` / `UserGroupService` / `UserRoleService` / `ObjectMenuService` / `OrganizationUnitService` / `LoginService` và controller cùng tên: vd. tạo user kèm Person (dùng `PersonService` + `InsertUserAfter` có sẵn), xem trước / sao chép quyền nhóm, quyền hiệu lực của 1 user, cây + sắp xếp ObjectMenu, cây OrganizationUnit, merge-conflicts (`CoreDataMergeConflict`, Id bigint), thông tin user đang đăng nhập.
  - **Phân quyền giữ cơ chế cũ (CLAUDE.md 4.3, D-31):**
    - Bỏ `Permissions:ApproverGroups` (appsettings) và `ScreenMap.MatrixGroup` dùng cho duyệt. Thay bằng mục `ObjectMenu` cấp 2 "Phê duyệt / Duyệt …" + `UserRole` (seed chỉ thêm phần thiếu); `PermissionService.IsAllowed(Approve)` = `AllowEdit` trên mục đó.
    - Chuyển `IPermissionService` sang `AppDelicare.Services`; gọi xóa cache từ `UserRoleService.Update`, `UserClaimService.SaveAfterUser`, `UserService` (Save / Delete / SaveIsActive / SaveIsAdmin), `UserGroupService.Save`, `ObjectMenuService` (Save / SaveIsActive). Bỏ sự kiện `PermissionsChanged` của Application.
    - Kiểm `LoginService` vẫn trả đúng các trường cũ (token, thông tin user, `listMenu`).
    - Test: đổi quyền qua `UserRole/Update` → lần gọi kế tiếp áp dụng ngay; khóa user (`SaveIsActive`) → 403 / không đăng nhập được; đổi tên nhóm không làm mất quyền duyệt; user 2 nhóm được quyền cộng dồn.
- **T45R.3 – Danh mục P1:** vào service / controller cũ của từng entity: overview khách / NCC (`GetOverview`), SupplierMaterial ưu tiên + hiệu lực theo ngày (lọc trong SP GetAll), Ingredient kèm tồn (SP), sơ đồ kho, `UomConversion` quy đổi (`Convert`), chặn ngừng điểm giao khi còn đơn.
- **T45R.4 – Món, công thức, giá, checklist P2 + Ca:** `RecipeService` / `RecipeController`: tạo phiên bản, dòng, `Submit`, `Lock` (`SnapshotJson` / `LockedAt` / `LockedBy`), `CalculateCost`; `StandardCost.Recalculate`; `PricePolicy` resolve + chặn chồng hiệu lực trong Insert / Update; Dish kèm công thức hiệu lực; checklist UNIQUE (Code, VersionNo); logic kiểm tra của `ShiftsController` v1 → `ShiftService`.
- **T45R.5 – Gỡ lớp v1 + test:**
  - Chuyển kỹ thuật dùng chung sang tầng cũ: `IClock`, `BusinessRulesOptions` (từ `Application/Common/ClockAndRules.cs`) → `AppDelicare.Data/Share` + `AppDelicare.Model/Option`; lookup cần thiết → `GetAllData` / SP.
  - `PermissionConvention`: bỏ nhánh `ApplyCrud`. Giữ `PermissionFilter`, `PermissionService`, `ScreenMap`.
  - Đưa vào `_obsolete/v1/` (không xóa, ghi PROGRESS): `Controllers/V1/**`, `Api/**` chỉ dùng cho v1 (ApiControllerBase, ApiEnvelope, EnvelopeResultFilter, ExceptionToErrorMiddleware, IdempotencyFilter, BranchContext, PageQuery, ApiJson…), project `AppDelicare.Application` (sau khi đã chuyển hết), phần sinh v1 trong `tools/AppDelicare.CodeGen` (nếu có). Gỡ đăng ký trong `Startup.cs` và `AppDelicare.sln`. `ProblemDetailsExceptionHandler`, `CorrelationIdMiddleware`, health check giữ.
  - Swagger: mô tả lại theo route `api/[controller]/<Action>`, bỏ chữ "legacy".
  - Test: viết lại test v1 thành test Service (và test tích hợp controller cũ) – giữ đủ kịch bản: 403 / 200 theo quyền, xóa đang tham chiếu bị chặn, khóa công thức, chồng hiệu lực giá, sổ append-only, đảo bút toán.
  - Cập nhật `docs/api-tests/*.http`, `docs/claude/UAT_CHECKLIST.md` (R1.0, R1.1) sang route cũ.
- **AC:**
  - [x] `LEGACY_REVIEW.md` đủ 2 bảng; mọi dòng v1 có cột "Chuyển sang" (không để trống).
  - [x] Swagger không còn route `/api/v1`; `grep -rn "api/v1" AppDelicare --include=*.cs` = 0 (ngoài `_obsolete`), trừ 2 test kiểm tra chính sự vắng mặt này.
  - [x] Mọi controller mới / sửa đúng mẫu `WarehouseController`; mỗi file 1 controller; không controller nào inject `DatabaseContext` / `IUnitOfWork`.
  - [x] Không có service quản trị user / nhóm / quyền / menu mới song song với service cũ.
  - [x] Không còn quyền nào cấu hình trong appsettings hoặc theo tên nhóm cứng; quyền duyệt gán được trên màn hình ma trận quyền cũ.
  - [x] Build 0 lỗi; `dotnet test` xanh. `.http` cập nhật theo route cũ; phần chạy trên DB dev đã kiểm bằng test SQL Server (chưa chạy tay từng request).
- Không cần dừng giữa các bước con, trừ khi rà soát thấy code cũ và mockup / spec mâu thuẫn mà DECISIONS chưa phân xử.

### T45R.6 – Đổi tên `ClinicManagement.Generator` → `AppDelicare.Generator` · phụ thuộc T45R.5 · `DONE` (2026-11-15, chờ anh chạy T4 trong Visual Studio)
- Làm trên nhánh riêng `chore/T45R.6-rename-generator`. Dùng `git mv` để giữ lịch sử.
- Đổi thư mục `AppDelicare/ClinicManagement.Generator` → `AppDelicare/AppDelicare.Generator` và 11 thư mục con `ClinicManagement.*` → `AppDelicare.*` (Data.Connection, Data.Interface, Data.Interface.UnitOfWork, Data.Repositories, Data.Repositories.UnitOfWork, Model.Entity, Model.Request, Model.SearchRequest, Model.ViewModels, Services.Implement, Services.Interface, WebApi.Controllers, WebApi.Infrastructure – đếm lại theo thực tế).
- Sửa mọi tham chiếu: `AppDelicare.sln` (đường dẫn project), `AppDelicare.Generator.csproj` (Include / `<None>` / `<Compile>` theo thư mục), `RunCopyFile.bat` (mọi dòng `copy` / `del`), các file `.tt` / `.ttinclude` (tên thư mục đầu ra, namespace nếu có), `App.config`, `.gitignore`, `CLAUDE.md` mục 2, `tools/AppDelicare.CodeGen/README.md`, tài liệu khác trong `docs/claude/`.
- **Không** chạy bộ sinh T4 (chỉ chạy được trong Visual Studio) và **không** sinh lại code. Không đổi nội dung template ngoài đường dẫn / tên.
- AC:
  - [x] `grep -rni "ClinicManagement" AppDelicare tools docs/claude CLAUDE.md` = 0 (trừ `_obsolete/`, `PROGRESS.md` phần lịch sử, `bin/`, `obj/`, `.vs/`).
  - [x] `dotnet build AppDelicare/AppDelicare.sln` 0 lỗi (project Generator nếu không build được bằng dotnet CLI như trước thì ghi rõ trong PROGRESS, không được tệ hơn baseline); `dotnet test` xanh.
  - [x] `RunCopyFile.bat` đọc lại từng dòng khớp thư mục mới (liệt kê trong PROGRESS).
  - [ ] **Cần anh:** mở solution bằng Visual Studio, chạy T4 một lần ở bản sao / nhánh này, xác nhận file sinh ra đúng chỗ → rồi mới merge.

### T45B – API màn hình P0–P2 (quản trị và danh mục) · phụ thuộc T07, T10 · `DONE` (2026-11-15: T45B.1–.4 làm theo v1 → T45R chuyển về khung cũ; T45B.5 xong. GAP-AUTH-01 refresh token để mở)
- Tách: **T45B.1** CRUD chuẩn generic cho 39 resource (`DONE` 2026-11-14) · **T45B.2** (`DONE` 2026-11-14 trừ /meta/statuses, /meta/reference-lists, auth/refresh → T45B.5) P0 riêng: users (tạo kèm Person + nhóm, lock/unlock, reset), ma trận quyền nhóm (GET/PUT/bulk/preview, effective-permissions), object-menus tree/reorder, org-units tree / branches, merge-conflicts (Id bigint), `/meta/menu`, `/meta/me`, `/meta/statuses`, `/meta/reference-lists` · **T45B.3** (`DONE` 2026-11-14) P1 riêng: customer overview / list mở rộng, supplier overview, supplier-materials ưu tiên + effectiveOn, ingredients kèm tồn, warehouse layout, UomService.Convert, delivery-location chặn ngừng khi còn đơn · **T45B.4** (`DONE` 2026-11-14) P2 riêng: dishes kèm activeRecipe, recipes phiên bản (tạo / dòng / submit / lock / cost), standard-costs recalculate, price-policies resolve + chặn chồng hiệu lực, checklist UNIQUE (Code, VersionNo) · **T45B.5** `docs/frontend-handoff/P0-P2.md` + `.http'
- Với **mỗi màn hình P0–P2** trong `catalog.js`: đủ endpoint theo `DELICARE_API_Specification.md`, gồm danh sách (lọc / phân trang / sắp xếp), chi tiết, thêm / sửa / xóa mềm, dropdown lookup, import / export nếu spec có.
- Quan hệ logic kiểm bằng `LOGICAL_RELATIONS.md`. Phân quyền `[Permission]`. Swagger mô tả.
- ~~Toàn bộ theo `/api/v1/...`~~ → **D-30: toàn bộ theo khung cũ** `api/[controller]/<Action>`; bổ sung vào controller / service cũ. Không gỡ endpoint cũ.
- **T45B.5 (`DONE` 2026-11-15):** `docs/frontend-handoff/P0-P2.md` (265 action sinh từ code), `MetaData/GetStatuses` (47 bộ, CodeGen từ STATUS_CATALOG), `MetaData/GetReferenceLists` (appsettings ReferenceLists), `.http`, UAT R1.1. Nội dung gốc: `docs/frontend-handoff/P0-P2.md` (màn hình → chức năng spec → action cũ, request / response mẫu) + `.http`; danh mục trạng thái (STATUS_CATALOG) và danh sách tham chiếu cho dropdown viết thành action kiểu cũ (vd. controller không gắn bảng theo mẫu `DashboardController`).
- AC: bảng đối chiếu `docs/frontend-handoff/P0-P2.md` (màn hình → endpoint → trạng thái); test tích hợp cho mỗi nhóm; file `.http`.

### T46B – API màn hình P3–P5 · phụ thuộc T13–T17 · `DONE` qua T13–T17, BE2.1–BE2.4, BE2.7 / BE2.7.1 (rà 2026-11-18: Catalog P3, P5 đủ; P4 còn 1 dòng `Một phần` – STT 113 gửi PO cho NCC qua email / Zalo, làm cùng kênh tin nhắn D-62 / R2; 4 mẫu in Menu/ExportForCustomer, ExportPreProduction, ExportProductionSheet, PurchaseOrder/ExportOrder) (T46B.1 `DONE(PILOT)` 2026-11-16: thực đơn tuần + FC%, phân công, đơn hàng, in Thực đơn gửi khách; hợp đồng Submit / Terminate, dự báo, đổi món → sau Pilot)
- T46B.1 P3 (hợp đồng, thực đơn tuần buổi × lựa chọn + giá vốn / FC, đơn hàng theo khách → địa điểm → vị trí). T46B.2 P4 (mua hàng: báo giá → so sánh → PO; kho). T46B.3 P5 (kế hoạch sản xuất, giá thành).
- **API in / xuất file** theo mẫu thật (BUSINESS_DOCS.md), dùng GemBox / Excel export có sẵn: Thực đơn gửi khách, Phiếu trước sản xuất, Phiếu sản xuất, Đơn đặt hàng NCC. Header pháp nhân lấy từ cấu hình. Trả file (docx / xlsx / pdf).
- AC: như T45B cho P3–P5 (khung cũ, D-30); mỗi mẫu in có test sinh file không lỗi. In / xuất file: action `Print*` / `Export*` trên controller của chứng từ, dùng `ExportToExcelHelper` / `TemplateExcelHelper` / GemBox có sẵn.

### T47B – API màn hình P6–P8 · phụ thuộc T18–T22 · `DONE` phần API nghiệp vụ qua T18–T21, BE2.1, BE2.3, BE2.5, BE2.6, BE3.3–BE3.5 (rà 2026-11-18: Catalog P6, P7 đủ; P8 duyệt / nhân sự / KPI đủ; dashboard 231–232 xong ở BE3.5). Phần bot / kênh tin nhắn (STT 214–220) chuyển sang **ĐỢT BOT** (D-62)
- T47B.1 P6 (QC / ATTP, checklist, nhiệt độ, truy xuất). T47B.2 P7 (đóng gói, giao vận, POD, phản ánh, hộp / khay, vệ sinh). T47B.3 P8 (nhân sự, KPI, **API dashboard điều hành**, hộp duyệt ApprovalRequest của user, cảnh báo nghiệp vụ).
- AC: như T45B cho P6–P8 (khung cũ, D-30).

### T48.0 – Băm mật khẩu an toàn (D-29) · phụ thuộc T45R · `DONE` (2026-11-16)
- Theo **cơ chế cũ (D-31)**: chỉ sửa trong `EncriptFunctionsHelper`, `LoginService`, `UserService` (Insert / ResetPassword / ChangePassword). Không đổi API, không đổi request / response, **không thêm bảng / cột** (hash mới vẫn lưu trong `User.PassWord`).
- Hash mới: PBKDF2 có sẵn trong .NET (`Rfc2898DeriveBytes.Pbkdf2`, SHA-256, salt ngẫu nhiên 16 byte, ≥ 100.000 vòng), lưu dạng chuỗi tự mô tả, vd. `PBKDF2$<vòng>$<salt base64>$<hash base64>` – kiểm tra độ dài cột `PassWord` trong DDL trước (không đủ thì dừng, ghi GAP).
- Đăng nhập: nếu chuỗi lưu là hash mới → kiểm PBKDF2 (so sánh thời gian hằng `CryptographicOperations.FixedTimeEquals`); nếu là MD5 cũ → kiểm MD5, đúng thì **tự nâng lên PBKDF2** ngay trong lần đăng nhập đó. User cũ không phải đổi mật khẩu.
- Reset / đổi / tạo mật khẩu luôn ghi hash mới. Giữ chính sách GAP-ADM-02.
- Không in mật khẩu / hash ra log.
- AC:
  - [x] Test: user hash MD5 cũ đăng nhập được và sau đó chuỗi lưu là PBKDF2; đăng nhập lần 2 vẫn được.
  - [x] Test: sai mật khẩu → từ chối (cả hash cũ lẫn mới); 2 user cùng mật khẩu có hash khác nhau.
  - [x] Test: ResetPassword / ChangePassword / tạo user ghi PBKDF2.
  - [x] Build 0 lỗi, test xanh; Swagger / route không đổi.

### T48 – Triển khai (staging → production) · phụ thuộc T03 · `TODO`
- **T48.1 staging (làm ngay trong R1.0):** `DONE` (2026-11-14 – IIS / LocalDB chạy thử OK; Docker chưa chạy được vì máy dev chưa có Docker). **Cập nhật 2026-11-17 (P9):** staging LocalDB chạy bản backend Pilot + bản build Angular trong `wwwroot` (deploy/README.md mục 8–9); IIS thật vẫn chờ anh / IT (cần quyền admin).
  - Cấu hình môi trường `Development` / `Staging` / `Production` (appsettings theo môi trường, bí mật qua biến môi trường).
  - **D-15 chưa chốt → không được chặn.** Chuẩn bị cả hai cách ở mức tối thiểu, không phụ thuộc hạ tầng cụ thể:
    - (a) **IIS:** `dotnet publish -c Release`, web.config, hướng dẫn cài .NET 8 Hosting Bundle.
    - (b) **Docker:** Dockerfile cho API + `docker-compose.staging.yml` (API + SQL Server container cho staging thử nghiệm). **Không** đưa Angular vào.
  - Staging đầu tiên có thể chạy ngay trên máy anh bằng docker-compose hoặc IIS Express.
  - Health check `/health` (API + DB). Script SQL triển khai: chạy `DB/sp/*.sql` và `DB/seed/*.sql` theo thứ tự, idempotent.
  - CI (nếu repo có remote GitHub / GitLab / Azure DevOps): pipeline build + test cho mỗi PR. Không tự deploy production.
  - Viết `deploy/README.md`: từng bước anh (hoặc IT) làm theo.
- **T48.2 production (R1.7)** – phụ thuộc **T48.4** (Linux + Docker, D-60):
  - Checklist go-live: backup DB, chạy script, cấu hình HTTPS, CORS đúng domain, tạo tài khoản admin, phân quyền nhóm, theo dõi log.
  - Phương án quay lui.
- AC: staging backend chạy được từ máy sạch theo README; `/health` xanh; Swagger staging mở được; đăng nhập qua API và gọi được API danh mục.

### T49 – Nạp dữ liệu danh mục ban đầu · phụ thuộc T45B · `DONE` (2026-11-15)
- **API** (không làm màn hình) **Import Excel** cho danh mục: khách, điểm giao, NCC, nguyên liệu, món, công thức / định mức, đơn vị, kho, nhân viên, xe. Dùng `ImportService` / `TemplateExcelHelper` sẵn có. Mỗi danh mục có một file mẫu.
- Cột của file mẫu theo bảng `dbo` đích, đặt tên tiếng Việt dễ hiểu. **Ưu tiên tương thích file thật của anh** (BUSINESS_DOCS.md): import trực tiếp sheet "Ngân hàng món" và "Chi tiết món và hao hụt, giá" của `Rule Menu.xlsx` → DishCategorie / Dishe / Ingredient / Recipe / RecipeLine; bảng giá NCC từ `Tool_Tong hop bao gia` → Supplier / SupplierMaterial. Người dùng tự chạy import; Claude Code chỉ test bằng file mẫu tự tạo `[DEMO]`, không nạp dữ liệu thật.
- Kiểm tra trùng mã, báo lỗi từng dòng, idempotent theo mã.
- AC: nạp file mẫu thành công; nạp lại không trùng; dòng lỗi được báo rõ.

### T50R1 – Tài liệu API và UAT backend · song song các lát · `DONE` cho BE-2 / BE-3 (rà 2026-11-18: mỗi đợt có `docs/api-tests/*.http` + mục UAT_CHECKLIST + handoff; toàn luồng `full-flow.http`) – đợt BOT tự thêm `bot-*.http` + UAT (phần Pilot `DONE(PILOT)` 2026-11-17: `docs/api-tests/pilot-flow.http`, `UAT_CHECKLIST.md` mục PILOT, `docs/frontend-handoff/README.md`, test `Pilot/PilotFlowSqlServerTests`; các lát R1 khác sau Pilot)
- `docs/claude/UAT_CHECKLIST.md`: mỗi lát có kịch bản thử **bằng API** (vai trò / token, request theo thứ tự, kết quả mong đợi), lấy từ luồng trong `DELICARE_Business_Workflow.md`.
- `docs/api-tests/*.http` (hoặc Postman collection): chạy được từng luồng end-to-end.
- `docs/frontend-handoff/`: mỗi màn hình mockup ↔ danh sách endpoint, input / output mẫu, để team Angular dùng khi anh giao frontend.
- AC: mỗi lát R1 có checklist UAT + file `.http` trước khi báo anh.

---

## GIAI ĐOẠN 5 – Second Brain

### T50 – Obsidian exporter · phụ thuộc T22 · `DEFERRED(R2)`
- Trong `AppDelicare.Services` (Knowledge) + job ở `AppDelicare.Job` (**D-62: bỏ project AppDelicare.Integration**): ghi markdown vào `Obsidian:VaultPath` theo cấu trúc số (ARCHITECTURE §4). Template trong `templates/obsidian/`. Chạy hằng đêm và theo sự kiện. Không bao giờ ghi vào `BOD Bảo mật`.
- AC: chạy với vault thử tạo đúng thư mục / file, có frontmatter; chạy lại không nhân bản file.

### T51 – Vùng ghi của agent · phụ thuộc T50, T33 · `DEFERRED(R2)`
- Tool `vault_append_log` (96), `vault_save_interaction` (97), `vault_create_task` (98). Ghi qua lõi, có log. Tool đọc `vault_search` loại trừ BOD.
- AC: test agent không ghi được ngoài 96 / 97 / 98.

---

## GIAI ĐOẠN 6 – Chuyển dữ liệu và cắt chuyển

### T60 – Bảng ánh xạ cấp cột · phụ thuộc T06 · `DEFERRED(R2)`
- Hoàn thiện MIGRATION_SHEETS_TO_DBO.md tới cấp cột (Sheets col → dbo table.col, chuyển kiểu, tra mã). Ghi mọi GAP vào DECISIONS.
- AC: mọi cột của 42 sổ có đích hoặc lý do bỏ.

### T61 – Importer · phụ thuộc T60 · `DEFERRED(R2)`
- Console `tools/AppDelicare.Importer`: đọc thư mục sao lưu xlsx / csv, nạp idempotent, báo cáo số dòng / lỗi, ghi IntegrationLog. Chạy được hằng đêm.
- AC: nạp 2 lần cùng bộ dữ liệu không sinh trùng; báo cáo lệch.

### T62 – Đối soát hằng ngày · `CANCELLED (D-16: Sheets là demo)`
- So KPI ngày SQL với `KPI_Ngay` Sheets, xuất báo cáo khác biệt.

### T63 – Kế hoạch cắt chuyển · `CANCELLED (D-16)`
- Checklist đổi URL Hermes và webhook bot, chuyển Sheets sang chỉ đọc, phương án quay lui. Viết `docs/claude/CUTOVER.md`.
