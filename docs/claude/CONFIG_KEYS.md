# Khóa cấu hình (CONFIG_KEYS)

> Sinh tự động bởi `node tools/config-keys.mjs` từ `AppDelicare.Model/Option/*.cs` (UI-6.47, D-125 d) – **đừng sửa tay**; thêm / đổi khóa thì sửa lớp cấu hình rồi chạy lại.
>
> - Khóa = đường dẫn trong `appsettings.json` (vd `BusinessRules:Hr:AutoMarkAbsentTime`); biến môi trường thay `:` bằng `__`.
> - Khóa **không ghi** trong appsettings / môi trường → dùng **mặc định** ở cột dưới; khi API khởi động, log cảnh báo một dòng liệt kê các khóa `BusinessRules` đang dùng mặc định (`ConfigDefaultsCheck`) – không làm sập.
> - Lịch job: `Jobs:<Mã job>` (`Enabled`, `At` "HH:mm" giờ VN) – xem màn **Job tự động** (`adm-job`).
> - Bí mật (chuỗi kết nối, token bot, khóa API) **không** ghi ở đây – xem `docs/claude/SECRETS_TODO.md`.

Tổng: **284 khóa** trong 14 section.

## Cờ "đường lùi" của đợt UI-6 – bỏ cờ sau UAT (UI-6.49 (4), D-127 d)

Mọi cờ dưới đây **mặc định = hành vi mới** ở mọi bản appsettings (dev, Staging, Production, Docker không ghi đè – test `FallbackFlagsUi649Tests` khóa lại). Đặt giá trị ở cột "Hành vi cũ" chỉ khi cần lùi tạm trong UAT; **bỏ cờ sau UAT** (xóa thuộc tính + nhánh code cũ).

| Khóa | Mặc định (mới) | Hành vi cũ | Task | Việc sau UAT |
|---|---|---|---|---|
| `BusinessRules:Hr:SwapConsentRequired` | true | false | UI-6.39 | Bỏ cờ sau UAT |
| `BusinessRules:Hr:AutoMarkAbsent` | true | false | UI-6.38 | Bỏ cờ sau UAT |
| `BusinessRules:PurchaseAutoCreatePoOnApprove` | true | false | UI-6.18 | Bỏ cờ sau UAT |
| `BusinessRules:PurchaseOrderAutoApproveFromApprovedRequest` | true | false | UI-6.18 | Bỏ cờ sau UAT |
| `BusinessRules:ProductionAutoCloseOrder` | true | false | UI-6.24 | Bỏ cờ sau UAT |
| `BusinessRules:ProductionAutoRecordOverProduction` | true | false | UI-6.49 | Bỏ cờ sau UAT |
| `BusinessRules:PackingManualCreate` | false | true | UI-6.34 | Bỏ cờ sau UAT |
| `BusinessRules:DeliveryAutoPlanAfterLock` | true | false | UI-6.34 | Bỏ cờ sau UAT |
| `BusinessRules:PackagingManualOutIn` | false | true | UI-6.35 | Bỏ cờ sau UAT |
| `BusinessRules:EmployeeStatusViaUpdate` | false | true | UI-6.36 | Bỏ cờ sau UAT |
| `BusinessRules:ForecastAutoSuggest` | true | false | UI-6.5 | Bỏ cờ sau UAT |
| `BusinessRules:ForecastAutoPublish` | true | false | UI-6.5 | Bỏ cờ sau UAT |

## Agent

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Agent:Enabled` | bool | true | Tắt hết agent (ToolGateway trả 503) – nghiệp vụ lõi vẫn chạy đúng giờ. | – |
| `Agent:ReadOnly` | bool | – | Chặn mọi công cụ ghi propose_* (chỉ đọc). | – |
| `Agent:RateLimitPerMinute` | int | 60 | Số lời gọi tối đa / phút / agent (vượt → 429). 0 = không giới hạn. | – |
| `Agent:MaxRows` | int | 50 | Số dòng tối đa một công cụ đọc trả về (AI-3). | – |
| `Agent:AskingBotCode` | string? | – | Mã bot Telegram nơi người hỏi liên kết (tra X-On-Behalf-Of); trống = mọi kênh Telegram. | – |
| `Agent:AskingGroups` | string? | – | AI-7: mã nhóm (UserGroup.Code, cách nhau dấu phẩy) được hỏi Trợ lý qua bot; trống = mọi người đã liên kết. Người hỏi chưa liên kết luôn bị từ chối. | – |
| `Agent:ProposalTtlMinutes` | int | 15 | AI-9: hạn xác nhận đề xuất ghi của agent (phút) – quá hạn đề xuất bị hủy, agent đề xuất lại. | – |
| `Agent:HermesWebhookUrl` | string? | – | AI-4: webhook API server Hermes nhận sự kiện gấp; trống = tắt. | – |
| `Agent:WebhookTimeoutSeconds` | int | 3 | Thời hạn chờ webhook Hermes (giây) – quá hạn thì bỏ qua, nghiệp vụ vẫn chạy. | – |
| `Agent:SchedulesFile` | string | "app-delicare-agents/schedules.json" | AI-6: tệp lịch chạy agent (một nguồn cho màn Lịch chạy + cron Hermes); tương đối = tìm từ ContentRoot lên 5 cấp (Docker: gắn thư mục app-delicare-agents). | – |
| `Agent:MonthlyBudget` | decimal? | – | AI-13 (D-133 PROPOSED – anh điền): ngân sách chi phí AI / tháng (VND); trống / 0 = không cảnh báo ngân sách. | D-133 |
| `Agent:UsdToVnd` | decimal | 25500m | AI-13: tỷ giá quy đổi chi phí Hermes báo bằng USD sang VND. | – |
| `Agent:CostAlertPct` | decimal | 80m | AI-13: ngưỡng % ngân sách tháng gửi cảnh báo CEO / FIN-IT (một lần / tháng; tới 100% gửi thêm một lần). | – |
| `Agent:HermesApiUrl` | string? | – | AI-14: API server Hermes kiểu OpenAI cho chat trên web, gồm /v1 (vd http://hermes-vivi:8642/v1); trống = chat tắt, màn báo tiếng Việt. | – |
| `Agent:HermesApiUrlByAgent` | Dictionary<string, string> | new(StringComparer.OrdinalIgnoreCase) | AI-15: agent ở container Hermes khác → URL riêng theo mã (vd KITCHEN → http://hermes-van-hanh:8642/v1). Không có mục → HermesApiUrl. | – |
| `Agent:HermesApiKeyEnv` | string | "HERMES_API_KEY" | AI-14: TÊN biến môi trường giữ khóa gọi API Hermes (không ghi khóa ở đây). | – |
| `Agent:ChatTimeoutSeconds` | int | 60 | AI-14: thời gian chờ Hermes trả lời 1 câu chat (giây). | – |

## BusinessRules

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `BusinessRules:TimeZone` | string | "Asia/Ho_Chi_Minh" | – | – |
| `BusinessRules:Hr:CheckInEarlyMinutes` | int | 120 | Được vào ca sớm nhất bao nhiêu phút trước giờ bắt đầu ca. | – |
| `BusinessRules:Hr:CheckInLateMinutes` | int | 0 | Sau giờ kết thúc ca bao nhiêu phút vẫn còn vào ca được (đi muộn rất nhiều); quá → không tìm thấy ca. | – |
| `BusinessRules:Hr:LateGraceMinutes` | int | 5 | Đi muộn quá N phút mới ghi "Đi muộn". | – |
| `BusinessRules:Hr:EarlyLeaveGraceMinutes` | int | 5 | Về sớm hơn giờ kết thúc ca quá N phút mới ghi "Về sớm". | – |
| `BusinessRules:Hr:RejectOutsideRadius` | bool | – | true = chấm GPS ngoài bán kính bếp bị chặn (không ghi); false = vẫn ghi với IsValid = false + cảnh báo (HR xem lại trên bảng công). | – |
| `BusinessRules:Hr:DefaultRadiusMeters` | decimal | 150m | Bán kính mặc định (m) khi trạm bếp có tọa độ nhưng chưa nhập RadiusMeters. | – |
| `BusinessRules:Hr:BulkAssignMaxDays` | int | 31 | Phân ca hàng loạt tối đa N ngày / lần. | – |
| `BusinessRules:Hr:TimesheetMaxDays` | int | 62 | Bảng công tối đa N ngày / lần. | – |
| `BusinessRules:Hr:LeaveMaxBackdateDays` | int | 7 | Đơn nghỉ được lập lùi tối đa N ngày (nghỉ ốm đột xuất). | – |
| `BusinessRules:Hr:LeaveMaxDays` | int | 30 | Một đơn nghỉ tối đa N ngày. | – |
| `BusinessRules:Hr:GridDays` | int | 7 | Lưới ca: số ngày mặc định (spec: 7). | – |
| `BusinessRules:Hr:MaxShiftsPerWeek` | int | 6 | UI-6.37 (D-118 d): cảnh báo khi một người có ≥ N ca / tuần (chỉ cảnh báo). | UI-6.37 |
| `BusinessRules:Hr:SupplementWindowHours` | int | 3 | UI-6.38 (D-119 d): bổ sung giờ – tối đa N giờ sau kết thúc ca, ca trong N ngày gần nhất. | UI-6.38 |
| `BusinessRules:Hr:SwapConsentRequired` | bool | true | UI-6.39 (D-120): đơn đổi / nhường ca cần người nhận đồng ý trước (hạn N giờ, quá → tự hủy). | UI-6.39 |
| `BusinessRules:Hr:SwapConsentHours` | int | 24 | – | – |
| `BusinessRules:Hr:AnnualLeaveDays` | int | 12 | UI-6.39: phép năm = N ngày / năm (loại nghỉ AnnualLeaveTypeCode); chỉ cảnh báo khi vượt (AnnualLeaveBlockOver = false). | UI-6.39 |
| `BusinessRules:Hr:AnnualLeaveTypeCode` | string | "ANNUAL" | – | – |
| `BusinessRules:Hr:AnnualLeaveBlockOver` | bool | – | – | – |
| `BusinessRules:Hr:SupplementMaxDays` | int | 3 | – | – |
| `BusinessRules:Hr:AutoMarkAbsent` | bool | true | UI-6.38 (D-119 f): tự chuyển Vắng hằng ngày (job MarkAbsent) – giờ chạy mặc định 23:30. | UI-6.38 |
| `BusinessRules:Hr:AutoMarkAbsentTime` | string | "23:30" | – | – |
| `BusinessRules:Hr:ShiftMinStaff` | Dictionary<string, Dictionary<string, int>> | [] | UI-6.37 (D-118 c): định biên { "mã đơn vị": { "mã ca": n, "T7:mã ca": n, "CN:mã ca": n } } – thiếu cấu hình → không tính thiếu. | UI-6.37 |
| `BusinessRules:Hr:ScheduleRemindDay` | int | 5 | UI-6.37 (D-118 f): nhắc lịch tuần sau – thứ (1 = Thứ 2 … 7 = CN; mặc định 5 = Thứ 6) và giờ. | UI-6.37 |
| `BusinessRules:Hr:ScheduleRemindTime` | string | "15:00" | – | – |
| `BusinessRules:Hr:LegacyEmployeeMatch` | bool | true | HR-ACC-1.3 (D-82 i): true = khi tài khoản chưa gắn nhân viên qua Person (Employee.PersonId), dùng tạm cách đoán cũ (liên kết bot → tên đăng nhập = mã NV → SĐT) và ghi nhật ký cảnh báo mỗi lần. Giữ 1 bản phát hành rồi … | HR-ACC-1.3 |
| `BusinessRules:Common:ExportMaxRows` | int | 10000 | Xuất Excel / CSV danh sách: tối đa N dòng / lần (nhiều hơn → cắt, kèm dòng ghi chú cuối file). | – |
| `BusinessRules:Common:PrintBatchMaxItems` | int | 100 | In hàng loạt: tối đa N phiếu / lần (spec 2.5: 100). | – |
| `BusinessRules:Common:PrintDefaultFormat` | string | "pdf" | BE2.7.1: mẫu in khi client không gửi format (pdf \| xlsx \| docx). Spec 2.5 = PDF → mặc định "pdf" (PROPOSED – D-56 k). | BE2.7 |
| `BusinessRules:Common:HistoryMaxItems` | int | 500 | Lịch sử bản ghi: tối đa N mục (mới nhất trước). | – |
| `BusinessRules:Charts:DefaultRangeDays` | int | 30 | Đối soát / hao hụt: không gửi from / to → N ngày gần nhất (tính cả hôm nay). | – |
| `BusinessRules:Charts:MaxRangeDays` | int | 366 | Đối soát / hao hụt: khoảng tối đa 1 lần gọi (ngày). | – |
| `BusinessRules:Charts:TemperatureDefaultHours` | int | 48 | Chuỗi nhiệt độ: không gửi from / to → N giờ gần nhất (spec 9.3: 48 giờ). | – |
| `BusinessRules:Charts:TemperatureMaxRangeDays` | int | 31 | Chuỗi nhiệt độ: khoảng tối đa 1 lần gọi (ngày). | – |
| `BusinessRules:Charts:TemperatureMaxPoints` | int | 2000 | Chuỗi nhiệt độ: quá N lần đo → gộp theo khoảng phút (trung bình, min / max, đạt khi mọi lần đo đạt). | – |
| `BusinessRules:Alerts:CacheSeconds` | int | 60 | Cache kết quả chuông theo (người, chi nhánh) – giây (D-57 h: FE hỏi định kỳ 60 giây). | D-57 |
| `BusinessRules:Alerts:TemperatureHours` | int | 48 | Nhiệt độ ngoài ngưỡng: tính các lần đo trong N giờ gần nhất (spec 12.1: 48 giờ). | – |
| `BusinessRules:Alerts:LotExpiryDays` | int | 2 | Lô Khả dụng còn tồn hết hạn trong N ngày tới (tính cả hôm nay; spec 12.1: 2 ngày). | – |
| `BusinessRules:Alerts:SearchMinLength` | int | 2 | Tìm kiếm toàn cục: số ký tự tối thiểu của từ khóa (spec: ≥ 2). | – |
| `BusinessRules:Alerts:SearchMaxLength` | int | 100 | Tìm kiếm toàn cục: độ dài tối đa của từ khóa. | – |
| `BusinessRules:Alerts:SearchMaxPerType` | int | 5 | Tìm kiếm toàn cục: tối đa N bản ghi mỗi loại (spec: 5). | – |
| `BusinessRules:Alerts:SearchMaxScreens` | int | 10 | Tìm kiếm toàn cục: tối đa N màn hình (chức năng) trả về. | – |
| `BusinessRules:Alerts:RelatedMaxItems` | int | 8 | Tab Liên quan: tối đa N dòng mỗi nhóm (spec: 8); count vẫn là tổng. | – |
| `BusinessRules:Kpi:OnTimeToleranceMinutes` | int | 15 | DEL_ONTIME: giao trễ tối đa N phút so với giờ giao dự kiến (DeliveryOrder.PlannedDeliveryTime) vẫn tính đúng giờ. | – |
| `BusinessRules:Kpi:DailyDefaultRangeDays` | int | 30 | KPI ngày: không gửi from / to → N ngày gần nhất (tính cả hôm nay). | – |
| `BusinessRules:Kpi:DailyMaxRangeDays` | int | 366 | KPI ngày: khoảng tối đa 1 lần gọi (ngày). | – |
| `BusinessRules:Kpi:MonthlyDefaultMonths` | int | 12 | KPI tháng: không gửi fromMonth / toMonth → N tháng gần nhất (tính cả tháng này). | – |
| `BusinessRules:Kpi:MonthlyMaxMonths` | int | 36 | KPI tháng: khoảng tối đa 1 lần gọi (tháng). | – |
| `BusinessRules:Dashboard:CacheSeconds` | int | 60 | Cache phần số liệu chung (không gồm cảnh báo theo người) theo (ngày, ca, chi nhánh) – giây (spec 12.1: 60 giây). 0 = không cache. | – |
| `BusinessRules:Dashboard:ChartDays` | int | 7 | Biểu đồ suất ăn: số ngày tính tới ngày chọn (spec 12.1: 7 ngày). | – |
| `BusinessRules:Dashboard:OrdersTake` | int | 6 | Khối "đơn hàng trong ngày": số đơn đầu tiên trả về (spec 12.1: 6). | – |
| `BusinessRules:Dashboard:JourneyMaxDocuments` | int | 20 | Hành trình đơn: tối đa N chứng từ mỗi chặng (tổng vẫn trả ở documentCount). | – |
| `BusinessRules:Retail:OrderCutoffHour` | int | 20 | Giờ chốt: đặt trước giờ này → nhận sớm nhất ngày mai; sau → ngày kia. | – |
| `BusinessRules:Retail:MaxDaysAhead` | int | 14 | Đặt trước tối đa bao nhiêu ngày. | – |
| `BusinessRules:Retail:MaxLines` | int | 20 | – | – |
| `BusinessRules:Retail:MaxQuantityPerLine` | int | 999 | – | – |
| `BusinessRules:Retail:SubmitLimitPerPhone` | int | 3 | Chống spam đơn WEB: số đơn tối đa / SĐT / cửa sổ. | – |
| `BusinessRules:Retail:SubmitLimitPerIp` | int | 10 | Chống spam đơn WEB: số đơn tối đa / IP / cửa sổ. | – |
| `BusinessRules:Retail:TrackLimitPerIp` | int | 20 | Tra cứu đơn: số lần tối đa / IP / cửa sổ. | – |
| `BusinessRules:Retail:LimitWindowMinutes` | int | 60 | – | – |
| `BusinessRules:Retail:DuplicateRequestHours` | int | 24 | Gửi lại cùng ClientRequestId trong khoảng này → trả lại đơn cũ. | – |
| `BusinessRules:Retail:NotifyNewOrder` | bool | true | Báo đơn WEB mới qua bot cho người có quyền màn "Đơn khách lẻ". | – |
| `BusinessRules:Retail:SessionDays` | int | 30 | RETAIL-2: thời hạn phiên đăng nhập của khách lẻ (ngày). | – |
| `BusinessRules:Retail:PasswordMinLength` | int | 6 | – | – |
| `BusinessRules:Retail:LoginFailLimit` | int | 5 | RETAIL-2: đăng nhập sai bao nhiêu lần (theo SĐT) thì khóa tạm. | – |
| `BusinessRules:Retail:LoginLockMinutes` | int | 15 | – | – |
| `BusinessRules:Retail:RegisterLimitPerIp` | int | 5 | RETAIL-2: số tài khoản tạo tối đa / IP / cửa sổ LimitWindowMinutes. | – |
| `BusinessRules:Pl:RevenueChangePct` | decimal | 5m | Doanh thu đổi so tháng trước từ bao nhiêu % thì nêu (tăng / giảm). | – |
| `BusinessRules:Pl:CostGapPct` | decimal | 2m | Giá vốn tăng nhanh hơn doanh thu từ bao nhiêu điểm % thì cảnh báo. | – |
| `BusinessRules:Pl:ItemIncreasePct` | decimal | 20m | Khoản chi phí tăng từ bao nhiêu % so tháng trước thì vào danh sách "tăng mạnh nhất". | – |
| `BusinessRules:Pl:ItemMinAmount` | decimal | 10_000_000m | … và tăng ít nhất bao nhiêu đồng (bỏ khoản nhỏ). | – |
| `BusinessRules:Pl:MarginWarnPct` | decimal | 5m | Biên lợi nhuận trước thuế dưới bao nhiêu % doanh thu thì cảnh báo. | – |
| `BusinessRules:Pl:FoodCostWarnPct` | decimal | 55m | % NVL / doanh thu (lớp tạm tính) trên bao nhiêu thì cảnh báo. | – |
| `BusinessRules:Pl:UnclassifiedWarnPct` | decimal | 15m | RPT-1.5 đối chiếu: phần "khác / chưa phân loại" của 1 tài khoản (621, 627, 641, 642) từ bao nhiêu % thì cảnh báo. | – |
| `BusinessRules:Pl:EstRevenueGapPct` | decimal | 5m | RPT-1.5 đối chiếu: doanh thu sổ AMIS lệch doanh thu Delicare (tạm tính) từ bao nhiêu % thì cảnh báo. | – |
| `BusinessRules:OrderCutoffHourPreviousDay` | int | 20 | – | – |
| `BusinessRules:OrderLockJobTime` | string | "20:00" | – | – |
| `BusinessRules:MrpJobTime` | string | "20:30" | – | – |
| `BusinessRules:PurchaseProposalJobTime` | string | "21:00" | – | – |
| `BusinessRules:StockCountVariancePctNeedApproval` | decimal | 2.0m | – | – |
| `BusinessRules:PortionDeviationPctNeedApproval` | decimal | 30.0m | – | – |
| `BusinessRules:TransportMinutesBeforeCutoff` | int | 60 | – | – |
| `BusinessRules:CoreTempMinC` | decimal | 70.0m | – | – |
| `BusinessRules:DeliveryTempMinC` | decimal | 60.0m | – | – |
| `BusinessRules:KpiOnTimeDeliveryTargetPct` | decimal | 95.0m | – | – |
| `BusinessRules:KpiIngredientToRevenueMaxPct` | decimal | 55.0m | – | – |
| `BusinessRules:KpiContainerLossMaxPct` | decimal | 3.0m | – | – |
| `BusinessRules:ComplaintSlaHours` | Dictionary<string, int> | new() { ["HIGH"] = 4, ["MEDIUM"] = 24, ["LOW"] = 72 } | – | – |
| `BusinessRules:ChecklistOffsetsMinutes` | Dictionary<string, int> | – | – | – |
| `BusinessRules:ExpiringLotDays` | int | 7 | – | – |
| `BusinessRules:ShortageForecastDays` | int | 3 | – | – |
| `BusinessRules:PurchaseHorizonDays` | int | 7 | BIZ-1.3 (D-77 c): số ngày tính nhu cầu nguyên liệu dự kiến (D+1 → D+N) – kế hoạch mua theo ngày cần hàng. | BIZ-1.3 |
| `BusinessRules:PurchaseOrderLeadBufferDays` | int | 1 | BIZ-1.3: ngày phải đặt = ngày cần − lead time NCC ưu tiên − số ngày đệm này. | BIZ-1.3 |
| `BusinessRules:RecipeLaborCostPerServing` | decimal | – | Spec 5.2 bước 4: chi phí nhân công / suất khi khóa công thức (PROPOSED – BA/PO CONFIRM). | – |
| `BusinessRules:RecipeOverheadCostPerServing` | decimal | – | Spec 5.2 bước 4: chi phí chung / suất (PROPOSED – BA/PO CONFIRM). | – |
| `BusinessRules:RecipeStandardServings` | decimal | 100 | Số suất chuẩn của định lượng công thức khi Recipe.StandardOutputQty trống (spec 5.2: 100 suất). | – |
| `BusinessRules:MealTypeDefaultShift` | Dictionary<string, string> | – | T13: ca giao mặc định theo loại suất (MealType → Shift.Code), D-19 / BUSINESS_RULES mục 0 (PROPOSED – BA/PO CONFIRM). | T13 |
| `BusinessRules:MrpIncludeSafetyStock` | bool | true | T14 / D-12: MRP cộng tồn an toàn (InventoryPolicie.SafetyStock, không có thì Ingredient.SafetyStock) vào nhu cầu (PROPOSED – BA/PO CONFIRM). | T14 |
| `BusinessRules:MrpOptionBufferPct` | decimal | 15m | T14.1 (anh chốt 2026-11-16, GAP-MRP-03): thực đơn có ≥ 2 lựa chọn cho cùng loại suất → số suất chia đều cho các lựa chọn rồi cộng thêm % dự phòng này cho mỗi lựa chọn (0 = tắt). Thực đơn 1 lựa chọn không cộng. | T14.1 |
| `BusinessRules:MrpOptionQtyBufferPct` | decimal | – | ORD-OPT-1.6 (D-74 e, anh chốt 2026-11-22): % dự phòng cho dòng đơn ĐÃ đăng ký theo lựa chọn (OrderLine.MenuOptionNo) – suất món = suất lựa chọn × (1 + % ÷ 100). Mặc định 0 (số khách đăng ký là số thật). Dòng cũ NULL v… | D-74 |
| `BusinessRules:PurchaseOrderAutoApproveMaxAmount` | decimal | – | T15: PO có tổng tiền ≤ ngưỡng này được tự duyệt khi gửi (0 = mọi PO phải duyệt) – PROPOSED – BA/PO CONFIRM (GAP-PUR-03). | T15 |
| `BusinessRules:PurchaseOrderDirectorApprovalAmount` | decimal | 50_000_000m | T15.1 / D-11 (anh chốt 2026-11-16): PO có tổng tiền ≥ ngưỡng này phải do Giám đốc duyệt – quyền Duyệt trên mục menu #/wf-inbox?type=scm-po-director; dưới ngưỡng duyệt trên #/wf-inbox?type=scm-po. Vẫn duyệt 1 cấp. | T15.1 |
| `BusinessRules:PurchaseOrderAutoApproveFromRequest` | bool | true | BIZ-2.2 (D-77 – B3, C2): PO lập từ đề nghị mua Đã duyệt tự Đã duyệt khi đúng NCC gợi ý, đơn giá = bảng giá, số ≤ đề nghị, tổng &lt; ngưỡng Giám đốc. | BIZ-2.2 |
| `BusinessRules:PurchaseAutoCreatePoOnApprove` | bool | true | UI-6.18 B (D-99): duyệt cấp cuối đề nghị mua → hệ thống tự lập đơn mua (NCC theo quy tắc gợi ý chung). | UI-6.18 |
| `BusinessRules:PurchaseOrderAutoApproveFromApprovedRequest` | bool | true | UI-6.18 B (D-99): đơn mua tự lập từ đề nghị đã duyệt được tự duyệt khi mọi dòng NCC ưu tiên + giá không cao hơn ước tính + tổng &lt; ngưỡng Giám đốc. | UI-6.18 |
| `BusinessRules:ProductionAutoReleaseAfterLock` | bool | true | BIZ-2.4 (D-77 – B5, C3): sau chốt đơn theo ngày → tự lập kế hoạch SX, tự duyệt + phát hành (trừ trường hợp phải chờ COO). | BIZ-2.4 |
| `BusinessRules:ProductionAutoDraftIssueOnRelease` | bool | true | BIZ-2.5 (D-77 – C4): phát hành lệnh → tự tạo phiếu xuất Nháp theo ca (số = giữ chỗ, lô FEFO). | BIZ-2.5 |
| `BusinessRules:PackagingIssueAutoDraft` | bool | true | ACC-6 (D-131 j): ghi kết quả đóng gói → tự lập phiếu xuất bao bì Nháp theo ca = suất đạt × định mức (công thức món bao bì BB-&lt;buổi&gt;). | D-131 |
| `BusinessRules:PackagingIssueAutoPost` | bool | true | ACC-6 (D-131 j): job PackagingIssueAutoPost tự ghi sổ phiếu xuất bao bì Nháp sau khi hết ca (kho không sửa / ghi sổ tay). | D-131 |
| `BusinessRules:GoodsReceiptAutoPostAfterQc` | bool | true | BIZ-2.6 (D-77 – C5): QC đầu vào kết luận Đạt / Đạt một phần → tự ghi sổ phiếu nhập (phần đạt). | BIZ-2.6 |
| `BusinessRules:BatchAutoPass` | bool | true | BIZ-2.7 (D-77 – C6): mẻ đủ điều kiện (lõi, sản lượng, lưu mẫu, không lỗi trọng yếu) tự Đạt QC. | BIZ-2.7 |
| `BusinessRules:PackingAutoGenerateOnPass` | bool | true | BIZ-2.8 (D-77 – C7): mẻ Đạt → tự sinh lệnh đóng gói (PackingOrder/GenerateFromBatch). | BIZ-2.8 |
| `BusinessRules:ProductionAutoCloseOrder` | bool | true | UI-6.24 (D-105): lệnh SX chuyển Hoàn thành (mẻ cuối đóng gói / loại) → hệ thống tự Đóng (chênh lệch tiêu hao, giá thành thực tế, giải phóng giữ chỗ dư). Tắt → đóng tay. | UI-6.24 |
| `BusinessRules:ProductionAutoRecordOverProduction` | bool | true | UI-6.49 (3) (QĐ-26): job đóng lệnh 22:00 tự ghi Nấu dư = suất đạt − đã chia − hỏng / loại − nấu dư đã ghi (mẻ có kết quả đóng gói). false = ghi tay như cũ (đường lùi – bỏ cờ sau UAT). | UI-6.49 |
| `BusinessRules:BatchLateStartMinutes` | int | 10 | UI-6.25 (D-106): mẻ quá giờ bắt đầu kế hoạch N phút mà chưa nấu → trễ (tô đỏ, khối Cần làm ngay). | UI-6.25 |
| `BusinessRules:BatchQcWaitWarnMinutes` | int | 20 | UI-6.25 (D-106): mẻ Chờ QC quá N phút → cần QA xem. | UI-6.25 |
| `BusinessRules:ProductionAutoCloseTime` | string | "22:00" | UI-6.24 (D-105): giờ job cuối ngày đóng lệnh Hoàn thành còn sót + gửi 1 tin chênh lệch tiêu hao vượt ngưỡng của ngày (HH:mm, giờ VN). | UI-6.24 |
| `BusinessRules:TemperatureManualDueMinutes` | int | 60 | UI-6.31 (D-112): khu đo tay phải có ≥ 1 lần đo mỗi ca – quá N phút đầu ca chưa đo → "đến giờ chưa đo" + tin bot. | UI-6.31 |
| `BusinessRules:IotSilenceMinutes` | int | 30 | UI-6.31: cảm biến (Iot:Devices) quá N phút không gửi số đo → "mất tín hiệu" (khu cần đo tay) + tin bot. | UI-6.31 |
| `BusinessRules:SystemChecklistCodes` | string[] | ["QA_FG"] | UI-6.33 (D-114): mã bộ kiểm tra hệ thống thêm (ngoài mã có giờ hạn, VS-KHAY, công đoạn, lưu mẫu) – không đổi mã / ngừng / xóa. | UI-6.33 |
| `BusinessRules:PackingManualCreate` | bool | – | UI-6.34 (D-115): cho lập tay lệnh đóng gói (mặc định tắt – hệ thống tự sinh theo mẻ). | UI-6.34 |
| `BusinessRules:DeliveryAutoPlanAfterLock` | bool | true | UI-6.34: tự lập chuyến theo khu vực cho ngày giao sau khi chốt đơn. | UI-6.34 |
| `BusinessRules:DispatchShortWarnMinutes` | int | 30 | UI-6.34: còn N phút tới giờ xuất bến mà chuyến chưa đủ thực đơn → cảnh báo điều phối. | UI-6.34 |
| `BusinessRules:DeliveryLateWarnMinutes` | int | 15 | UI-6.35 (D-116): còn N phút tới cuối khung nhận mà chưa giao → bot "sắp trễ". | UI-6.35 |
| `BusinessRules:ContainerReturnDueDays` | int | 3 | UI-6.35: phiếu Chờ thu hồi quá N ngày → "quá hạn thu" + tin gộp / ngày. | UI-6.35 |
| `BusinessRules:PackagingManualOutIn` | bool | – | UI-6.35: cho ghi tay sổ bao bì loại Giao cho khách / Khách trả (mặc định tắt – tự ghi khi bàn giao, thu hồi). | UI-6.35 |
| `BusinessRules:EmployeeStatusViaUpdate` | bool | – | UI-6.36 (D-117 e): true = cho đổi Trạng thái nhân viên qua Employee/Update như cũ (mặc định false – chỉ qua Resign / SetStatus). | UI-6.36 |
| `BusinessRules:AccountInactiveWarnDays` | int | 60 | UI-6.42 (D-123): tài khoản không đăng nhập ≥ N ngày → cảnh báo "lâu không đăng nhập" (Account:InactiveWarnDays). | UI-6.42 |
| `BusinessRules:ReconciliationAutoCreate` | bool | true | BIZ-2.9 (D-77 – C8): đơn Đã giao → tự tạo đối soát (suất phục vụ = suất khách nhận). | BIZ-2.9 |
| `BusinessRules:ReconciliationAutoCloseHours` | int | 24 | BIZ-2.9: đối soát Khớp, không phản ánh mở, quá số giờ này → tự chốt (0 = tắt). | BIZ-2.9 |
| `BusinessRules:ReconcilePromptMinutes` | int | 120 | UI-6.8 (D-86 c): số phút sau bàn giao bot hỏi người nhận ở điểm phát "Đã phục vụ đủ" (0 = tắt). | UI-6.8 |
| `BusinessRules:MealCostAutoOnReconcileClose` | bool | true | BIZ-2.9 (C9): chốt đối soát → tự tính giá vốn suất (MealCost/Calculate). | BIZ-2.9 |
| `BusinessRules:MenuReminderWeekday` | int | 3 | BIZ-3.4 (D-77 – C12): thứ chạy job nhắc thực đơn chung tuần sau (0 = Chủ nhật … 3 = thứ Tư). | BIZ-3.4 |
| `BusinessRules:OrderWeekReminderWeekday` | int | 5 | BIZ-3.4: thứ chạy job nhắc khách chưa đăng ký suất tuần sau (5 = thứ Sáu). | BIZ-3.4 |
| `BusinessRules:StandardCostAutoOnSupplierPrice` | bool | true | BIZ-3.3 (D-77 – C10): lưu bảng giá NCC → tự tính lại giá thành chuẩn món dùng nguyên liệu + cảnh báo FC % thực đơn. | BIZ-3.3 |
| `BusinessRules:SalesVatPct` | decimal | 8m | BIZ-3.2 (D-77 – A7): VAT bán hàng % trên bảng kê thanh toán. | BIZ-3.2 |
| `BusinessRules:BillingMonthDeviationPct` | decimal | 20m | UI-6.9 (D-87): ⚠ khi tiền suất ăn tháng này lệch quá % này so tháng trước. | UI-6.9 |
| `BusinessRules:MenuCycleWeeks` | int | 4 | UI-6.11 (D-89 b): chu kỳ thực đơn (tuần) – tự lập nháp tuần N = nhân bản tuần N − chu kỳ. | UI-6.11 |
| `BusinessRules:MenuNoRepeatDays` | int | 14 | UI-6.11 (D-89 d): gợi ý món ưu tiên món chưa dùng trong số ngày này. | UI-6.11 |
| `BusinessRules:BillingMonthNoticeDay` | int | 1 | UI-6.9: ngày trong tháng bot báo bảng kê tháng trước (1 = ngày 1). | UI-6.9 |
| `BusinessRules:ContainerLossFee` | Dictionary<string, decimal> | new(StringComparer.OrdinalIgnoreCase) | BIZ-3.2: phí / khay thất thoát vượt ngưỡng theo loại khay – CŨ: từ PKG-FEE phí đặt ở danh mục Loại hộp / khay (bảng PackagingType); mục này chỉ dùng cho loại không có trong danh mục. | BIZ-3.2 |
| `BusinessRules:GoodsReceiptOverReceivePct` | decimal | 10m | T15 (anh chốt 2026-11-16): được nhận vượt số đặt tối đa bao nhiêu % mỗi dòng PO (GAP-PUR-04). | T15 |
| `BusinessRules:ReceivingTemp` | Dictionary<string, ReceivingTempRule> | – | T15.1 (anh chốt 2026-11-16, thay ReceivingTempMaxC của T15): nhiệt độ nhận hàng theo Ingredient.StorageType, 3 vùng – ≤ AcceptMax: nhận; ≤ ConditionalMax: nhận có điều kiện (bắt buộc lý do, lô "ưu tiên dùng trước", sự… | T15.1 |
| `BusinessRules:SupplierIssueDueDays` | int | 2 | T15: hạn xử lý sự cố NCC (SupplierIssue.DueAt) = lúc ghi sổ + số ngày này – PROPOSED (spec 9.1: +2 ngày). | T15 |
| `BusinessRules:PurchaseVat:DefaultPct` | decimal | 8m | – | – |
| `BusinessRules:PurchaseVat:ByIngredientCategoryCode` | Dictionary<string, decimal> | new(StringComparer.OrdinalIgnoreCase) | – | – |
| `BusinessRules:ProductionDefaultShiftCode` | string | "TRUA" | T17.1 (anh chốt 2026-11-16 – Pilot 4 ca / ngày, phát hành tách theo ca của đơn): chỉ còn là ca (Shift.Code) cho dòng đơn chưa gán ca và món không có trong đơn khi chia kế hoạch theo ca (GAP-PRD-01). | T17.1 |
| `BusinessRules:ProductionPackingMinutes` | int | 30 | T17: số phút chia suất / đóng gói trước giờ xuất bếp – mẻ cuối của mỗi món phải nấu xong trước (giờ xuất bếp − số phút này). PROPOSED (GAP-PRD-07). | T17 |
| `BusinessRules:ProductionDefaultMinutesPerBatch` | int | 60 | T17: số phút mỗi mẻ khi dây chuyền chưa có MinutesPerBatch (hoặc không chọn dây chuyền). PROPOSED (GAP-PRD-07). | T17 |
| `BusinessRules:ProductionRequireSampleBeforeRelease` | bool | – | T17: bắt buộc lưu mẫu (SAMPLE_RETAINED) trước khi mẻ đạt QC. Anh chốt 2026-11-16: lưu mẫu bắt buộc TRƯỚC KHI GIAO, không phải trước khi đạt QC → mặc định false (đạt QC chỉ cảnh báo khi chưa lưu mẫu); chặn ở T19 Delive… | T17 |
| `BusinessRules:DeliveryRequirePackedBatches` | bool | true | T19: chặn xuất chuyến khi mẻ của ngày / ca chưa đóng gói xong (còn mẻ chưa Đã đóng gói / Không đạt) – PROPOSED – BA/PO CONFIRM (GAP-DLV-03). | T19 |
| `BusinessRules:DeliveryRequireSampleRetained` | bool | true | T19: chặn xuất chuyến khi có mẻ (đã đóng gói) của ngày / ca chưa lưu mẫu SAMPLE_RETAINED – anh chốt 2026-11-16. | T19 |
| `BusinessRules:ReconciliationBillingBasis` | string | "RECEIVED" | T19: cơ sở tính tiền khi đối soát: ORDERED / DELIVERED / ACCEPTED / SERVED – PROPOSED (spec 6.7: TotalAmount × suất nhận ÷ suất đặt; GAP-DLV-05). BIZ-1.4 (D-77 d): mặc định RECEIVED = suất khách ký nhận − suất lỗi Del… | T19 |
| `BusinessRules:ReconciliationVarianceRemarkPct` | decimal | 1.0m | T19: đóng đối soát có \|chênh lệch\| % lớn hơn ngưỡng này phải ghi chú (spec 6.7, GAP-F-11 tạm 1%) – PROPOSED. | T19 |
| `BusinessRules:DeliveryAutoComplaintOnRejected` | bool | true | T19: bàn giao có suất bị từ chối → tự tạo phản ánh PA- (SourceChannel POD, SHORTAGE) – spec 10.3, PROPOSED. | T19 |
| `BusinessRules:DeliveryRequireLoading` | bool | true | BE2.1 (spec 10.2 load / dispatch): xuất phát chỉ từ chuyến Đang xếp (Loading) và mọi đơn giao của chuyến đã có dòng xếp hàng Loaded (DeliveryRoute/Load). false = cho xuất phát thẳng từ Kế hoạch như T19. PROPOSED – BA/… | BE2.1 |
| `BusinessRules:ForecastVariancePctWarning` | decimal | 10.0m | BE2.2 (spec 6.6 confirm): \|đặt − dự báo\| / dự báo × 100 vượt ngưỡng này → cảnh báo + ghi nhận vào đơn (không chặn). PROPOSED – BA/PO CONFIRM. | BE2.2 |
| `BusinessRules:ForecastMaxDays` | int | 62 | BE2.2: số ngày tối đa của ma trận / công bố dự báo trong 1 lần gọi. | BE2.2 |
| `BusinessRules:ForecastLookbackWeeks` | int | 4 | FC-1.1 (D-76): số tuần nhìn lại khi tính dự báo gợi ý (trung bình suất thực nhận cùng thứ). | D-76 |
| `BusinessRules:ForecastAutoSuggest` | bool | true | UI-6.5 (D-84 d): thứ Tư 22:00 tự tạo gợi ý tuần sau + thứ Năm 08:00 báo KD (tắt → chỉ bấm tay). | UI-6.5 |
| `BusinessRules:ForecastAutoPublish` | bool | true | UI-6.5 (D-84 d): thứ Sáu 12:00 nhắc + 16:00 tự công bố bản Nháp tuần sau. | UI-6.5 |
| `BusinessRules:ForecastSuggestWeekday` | int | 3 | Thứ tạo gợi ý (3 = thứ Tư), thứ báo KD (4 = thứ Năm), thứ nhắc / tự công bố (5 = thứ Sáu) – giờ ở Jobs:&lt;Code&gt;. | – |
| `BusinessRules:ForecastNoticeWeekday` | int | 4 | – | – |
| `BusinessRules:ForecastPublishWeekday` | int | 5 | – | – |
| `BusinessRules:ForecastSuggestMaxDays` | int | 14 | FC-1.1: số ngày tối đa của 1 lần tạo dự báo gợi ý. | – |
| `BusinessRules:ApprovalSteps` | Dictionary<string, string[]> | new(StringComparer.OrdinalIgnoreCase) | BE2.3 (spec 2.3 "cấu hình mẫu quy trình trong file cấu hình", GAP-F-08): BusinessType → danh sách mục quyền duyệt theo thứ tự bước (mỗi phần tử = screen-id của mục #/wf-inbox?type=&lt;id&gt;). Có khóa → thay quy trình… | BE2.3 |
| `BusinessRules:ApprovalStepDueHours` | int | 48 | BE2.3: hạn xử lý mỗi bước duyệt (giờ, tính từ lúc bước được giao). 0 = không có hạn. Quá hạn chỉ là cờ (D-45: không job). | BE2.3 |
| `BusinessRules:ApprovalStepDueHoursByType` | Dictionary<string, int> | new(StringComparer.OrdinalIgnoreCase) | BE2.3: hạn riêng theo BusinessType (giờ) – ghi đè . | BE2.3 |
| `BusinessRules:ApprovalBlockOverdueDecision` | bool | – | BE2.3: true = bước đã quá hạn không duyệt / từ chối được nữa (người gửi hủy rồi gửi lại). Mặc định false = chỉ gắn cờ quá hạn. | BE2.3 |
| `BusinessRules:ApprovalRequireDistinctApprovers` | bool | true | BE2.3: duyệt nhiều cấp – một người không quyết định 2 bước của cùng yêu cầu (quản trị viên được miễn). | BE2.3 |
| `BusinessRules:ApprovalBlockSelfApproval` | bool | true | MENU-5.3 (D-79 a5, thay D-52 d): true = người gửi (ApprovalRequest.RequestedBy) không thấy / không duyệt được BẤT KỲ cấp nào của yêu cầu mình (hộp duyệt, Decide, bot); quản trị viên được miễn. Hủy yêu cầu của mình vẫn… | MENU-5.3 |
| `BusinessRules:PositionDefaultGroups` | Dictionary<string, string> | new(StringComparer.OrdinalIgnoreCase) | HR-ACC-1.6 (D-82 f, g): nhóm quyền gợi ý theo chức danh – mã chức danh (Position.Code) → tên nhóm quyền (UserGroup.Name, vd. "OPS-KIT"; nhiều nhóm cách nhau bằng dấu phẩy). Mặc định rỗng – IT điền (ví dụ ở appsettings… | HR-ACC-1.6 |
| `BusinessRules:ApprovalFlagPricePct` | decimal | 10m | APR-1.2 (D-81 f): cảnh báo PO khi giá dòng cao hơn lần mua trước cùng NCC ≥ N %. | APR-1.2 |
| `BusinessRules:SupplierOnTimeMinPct` | decimal | 90m | UI-6.18 C / UI-6.19 (D-99, D-100): NCC "đúng hẹn" khi tỉ lệ phiếu nhập không trễ ngày giao dự kiến của PO ≥ ngưỡng này (90 ngày gần nhất). | UI-6.18 |
| `BusinessRules:SupplierOnTimeLookbackDays` | int | 90 | – | – |
| `BusinessRules:ApprovalFlagStockCountLinePct` | decimal | 5m | APR-1.2 (D-81 f): cảnh báo kiểm kê khi dòng lệch &gt; N %. | APR-1.2 |
| `BusinessRules:ApprovalFlagRejectCount` | int | 2 | APR-1.2 (D-81 f): cảnh báo khi người gửi bị từ chối loại này ≥ N lần trong ngày. | APR-1.2 |
| `BusinessRules:ApprovalFlagRejectDays` | int | 30 | – | – |
| `BusinessRules:ApprovalBulkTypes` | string[] | ["LEAVE_REQUEST", "SHIFT_SWAP"] | APR-1.3 (D-81 d): loại được duyệt nhiều khi Bình thường (PO chỉ khi &lt; PurchaseOrderAutoApproveMaxAmount &gt; 0). | APR-1.3 |
| `BusinessRules:ApprovalDueSoonHours` | int | 8 | APR-1.3: "Hết hạn trong N giờ" (thẻ chỉ số, nhãn sắp hết hạn). | APR-1.3 |
| `BusinessRules:ApprovalRemindHours` | int | 4 | APR-1.3 (D-81 h): mỗi yêu cầu nhắc qua bot tối đa 1 lần / N giờ. | APR-1.3 |
| `BusinessRules:PurchaseOrderDirectorTwoLevel` | bool | – | BE2.3 (PO nhiều cấp theo ngưỡng): true = PO ≥ duyệt 2 cấp (scm-po → scm-po-director). Mặc định false = 1 cấp Giám đốc như D-11 (anh chốt 2026-11-16). | BE2.3 |
| `BusinessRules:StockAdjustmentRequireApproval` | bool | true | BE2.3 (spec 7.6 adjust, GAP-F-04): điều chỉnh tồn tay phải duyệt STOCK_ADJUSTMENT trước khi ghi sổ. false = ghi ngay như T16. | BE2.3 |
| `BusinessRules:ProductionReserveOnRelease` | bool | true | BE2.4 (spec 8.1 release bước 4, Catalog 126): phát hành kế hoạch thì giữ chỗ nguyên liệu (MaterialReservation) cho lệnh SX mới. false = không giữ chỗ (như T17). | BE2.4 |
| `BusinessRules:MrpIncludeIncomingPurchaseOrders` | bool | true | BE2.4 (spec 7.1 bước 5): MRP trừ hàng sắp về từ PO Đã duyệt / Đã gửi / Nhận một phần có ngày giao dự kiến ≤ ngày sản xuất (trừ phần đã nhận). | BE2.4 |
| `BusinessRules:ProductionRequireStages` | bool | – | BE2.4 (T17 công đoạn theo tổ): true = mọi mẻ bắt buộc đi qua công đoạn (Sơ chế → Nấu → Chia suất) – nấu xong cần công đoạn Nấu xong, đóng gói cần Chia suất xong. Mặc định false = chỉ kiểm khi mẻ đã bắt đầu ghi công đo… | BE2.4 |
| `BusinessRules:ProductionStageTeams` | Dictionary<string, string> | – | BE2.4: tổ mặc định của từng công đoạn (ItemCode mục checklist PRD_STAGE → OrganizationUnit.Code loại TEAM, ORG_STRUCTURE.md). | BE2.4 |
| `BusinessRules:ProductionLaborCostPerHour` | decimal | – | BE2.4 (spec 8.5: LaborCost = giờ công × đơn giá, chưa có bảng lương): đơn giá 1 giờ công (VND). &gt; 0 và mẻ có ghi công đoạn → nhân công = Σ giờ công đoạn × số người × đơn giá; còn lại nhân công = số phần nấu được × . | BE2.4 |
| `BusinessRules:ConsumptionVariancePctWarning` | decimal | 5.0m | BE2.4 (spec 7.9): \|chênh lệch tiêu hao\| % vượt ngưỡng này → đánh dấu isOverThreshold (không chặn đóng lệnh). | BE2.4 |
| `BusinessRules:FoodSafetyCriticalFailHoldsBatch` | bool | true | BE2.5 (spec 9.2): tiêu chí trọng yếu (IsCritical) KHÔNG ĐẠT gắn mẻ → giữ mẻ: Đạt → Chờ QC (QcStatus HOLD), mẻ đang nấu / chờ QC → HOLD; mẻ bị chặn đạt QC / đóng gói / xuất chuyến tới khi tiêu chí đó được kiểm lại ĐẠT.… | BE2.5 |
| `BusinessRules:CleaningChecklistCode` | string | "VS-KHAY" | BE2.5 (spec 9.4): mã bộ checklist vệ sinh khay / hộp 6 bước (seed DB/seed/FoodSafetyQa_seed.sql). | BE2.5 |
| `BusinessRules:CleaningChemicalRequiredItems` | string[] | ["KHU_KHUAN"] | BE2.5: bước vệ sinh phải ghi hóa chất đã dùng khi ĐẠT (ItemCode của checklist vệ sinh). | BE2.5 |
| `BusinessRules:CleaningAutoCreateOnInspect` | bool | true | BE2.5 (spec 10.4 inspect "khay dùng được → tự tạo CleaningBatche"): kiểm tra thu hồi xong tự lập mẻ vệ sinh VS- cho số khay dùng được. | BE2.5 |
| `BusinessRules:PackagingOutOnDelivery` | bool | true | BE2.5 (spec 10.3 pod "Ghi PackagingLedger OUT nếu đóng khay"): bàn giao không gửi số khay → tự ghi OUT = suất khách nhận × , loại = loại bao bì của kết quả đóng gói của đơn, không có thì . false = chỉ ghi khi bàn giao… | BE2.5 |
| `BusinessRules:PackagingDefaultType` | string | "KHAY" | – | – |
| `BusinessRules:PackagingUnitsPerMeal` | decimal | 1m | – | – |

## Clients

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Clients:KeyHash` | string? | – | – | – |
| `Clients:Enabled` | bool | true | – | – |

## DocumentStorage

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `DocumentStorage:RootPath` | string | "App_Data/DocumentFiles" | Thư mục gốc lưu tệp (tuyệt đối, hoặc tương đối với ContentRoot). Mặc định App_Data/DocumentFiles. | – |
| `DocumentStorage:MaxFileSizeMb` | int | 20 | Dung lượng tối đa 1 tệp (MB) – spec 2.4: 20 MB. | – |
| `DocumentStorage:AllowedExtensions` | List<string> | [".pdf", ".jpg", ".jpeg", ".png", ".xlsx", ".docx"] | Đuôi tệp được nhận (spec 2.4: pdf, jpg, png, xlsx, docx). Không bao giờ nhận tệp chạy được (.exe, .js, .html…) dù cấu hình. | – |
| `DocumentStorage:Categories` | List<string> | ["signature", "photo", "evidence", "contract", "health", … | Loại tài liệu (spec 2.4: signature, photo, evidence, contract, other). | – |

## Iot

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Iot:ApiKeys` | List<string> | [] | Khóa API hợp lệ (header X-Api-Key) – mỗi cổng / thiết bị 1 khóa được. BÍ MẬT. | – |
| `Iot:MaxItemsPerRequest` | int | 500 | Tối đa N lần đo / request (spec: 500). | – |
| `Iot:RateLimitPerMinute` | int | 60 | Tối đa N request / phút / khóa (quá → HTTP 429). | – |
| `Iot:MaxAgeDays` | int | 7 | Lần đo cũ hơn N ngày → từ chối (gửi bù khi mất mạng vẫn nhận trong N ngày). | – |
| `Iot:MaxFutureMinutes` | int | 5 | Lần đo ở tương lai quá N phút (lệch đồng hồ thiết bị) → từ chối. | – |
| `Iot:MinTemperatureC` | decimal | -60m | Khoảng nhiệt độ hợp lý của cảm biến – ngoài khoảng coi là lỗi cảm biến, từ chối. | – |
| `Iot:MaxTemperatureC` | decimal | 150m | – | – |
| `Iot:MeasurementType` | string | "STORAGE" | TemperatureLog.MeasurementType mặc định (ReferenceLists TemperatureMeasurementType). | – |
| `Iot:Devices` | Dictionary<string, IotDeviceOptions> | new(StringComparer.OrdinalIgnoreCase) | Mã thiết bị (DeviceCode, không phân biệt hoa thường) → khu vực / vị trí kho. | – |

## Jobs

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Jobs:Enabled` | bool | true | false = AppDelicare.Job không tự chạy (vẫn bấm tay được qua api/Job/Run). | – |
| `Jobs:At` | string? | – | Giờ chạy hằng ngày "HH:mm" (giờ VN). Trống = dùng giờ mặc định của job. | – |

## Knowledge

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Knowledge:VaultPath` | string | "App_Data/Knowledge" | Thư mục gốc của kho; tương đối thì tính từ ContentRoot (như DocumentStorage). | – |
| `Knowledge:MaxNotesPerQuery` | int | 5 | Số ghi chú tối đa một lần tìm của agent (§7.8-4). | – |
| `Knowledge:SnippetLength` | int | 400 | Độ dài đoạn trích trả cho agent (ký tự). | – |
| `Knowledge:LogRetentionDays` | int | 90 | AI-12: nhật ký agent (96) quá số ngày này được gộp thành tóm tắt tháng. | – |
| `Knowledge:BriefKeepMonths` | int | 13 | AI-12: bản tin ngày (10 Ban GĐ/Bản tin) giữ số tháng này, cũ hơn chuyển Lưu trữ bản tin. | – |
| `Knowledge:SyncTwoWayTraining` | bool | – | AI-18: cho phép đồng bộ HAI CHIỀU thư mục 80 Đào tạo với Obsidian máy cá nhân (tools/kb-sync). Mặc định tắt – mọi thư mục chỉ một chiều (máy chủ → máy). | – |

## Messaging

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Messaging:MaxClaim` | int | 20 | Tối đa N dòng mỗi lần ClaimInbound / ClaimOutbound. | – |
| `Messaging:LeaseMinutes` | int | 5 | Dòng đang Processing / Sending quá N phút (container messaging chết giữa chừng) → được lấy lại. | – |
| `Messaging:RetryDelaysMinutes` | List<int> | [] | Giãn cách thử lại tin gửi lỗi (phút) theo số lần đã lỗi; hết danh sách = không thử nữa. Mặc định (để trống) = . Khởi tạo RỖNG vì binder cấu hình .NET NỐI mảng trong appsettings vào danh sách mặc định (thành 10 phần tử). | – |
| `Messaging:MaxManualTextLength` | int | 4000 | Tối đa N ký tự nội dung tin gửi tay (Telegram: 4096). | – |
| `Messaging:ConversationMaxPageSize` | int | 200 | Tối đa N bản ghi / trang của GetConversation. | – |
| `Messaging:MaxUpdateLength` | int | 64000 | Kích thước tối đa PayloadJson của 1 update nhận vào (ký tự) – quá → từ chối. | – |
| `Messaging:LinkSigningKey` | string? | – | BOT-1.2: khóa ký mã liên kết (HMAC-SHA256) – BÍ MẬT ở env Messaging__LinkSigningKey (≥ 32 ký tự); trống = không tạo / nhận mã. | BOT-1.2 |
| `Messaging:UserCodeMinutes` | int | 15 | BOT-1.2 (D-62 p10): hạn mã user tự tạo (phút). | BOT-1.2 |
| `Messaging:EmployeeCodeHours` | int | 72 | BOT-1.2 (D-62 p10): hạn mã HR cấp cho nhân viên (giờ). | BOT-1.2 |
| `Messaging:UserTokenMinutes` | int | 10 | BOT-1.2: hạn JWT cấp thay mặt user cho bot (phút). | BOT-1.2 |
| `Messaging:QuietHours:Enabled` | bool | true | – | – |
| `Messaging:QuietHours:LeadMinutes` | int | 60 | – | – |
| `Messaging:QuietHours:DayStart` | string | "07:00" | – | – |
| `Messaging:QuietHours:DayEnd` | string | "21:00" | – | – |
| `Messaging:QuietHours:LookaheadDays` | int | 3 | Tìm ca kế tiếp trong N ngày; không có → coi như người không có phân ca. | – |
| `Messaging:Watch:BatchLateMinutes` | int | 15 | Mẻ chưa bắt đầu / chưa xong quá N phút so với kế hoạch → BATCH_LATE. | – |
| `Messaging:Watch:TripLateMinutes` | int | 10 | Xe chưa xuất phát quá N phút so với giờ dự kiến → TRIP_NOT_DEPARTED. | – |
| `Messaging:Watch:StopLateMinutes` | int | 15 | Điểm giao chưa đến quá N phút so với giờ dự kiến → STOP_LATE. | – |
| `Messaging:Watch:NotCheckedInMinutes` | int | 15 | Chưa vào ca quá N phút sau giờ bắt đầu → NOT_CHECKED_IN (1 tin / ca, gom danh sách). | – |
| `Messaging:Watch:ComplaintSlaWarnMinutes` | int | 60 | Phản ánh còn ≤ N phút tới hạn SLA → COMPLAINT_SLA (lần 2 khi đã quá hạn). | – |
| `Messaging:Watch:ApprovalOverdueEscalateHours` | int | 24 | Duyệt quá hạn: quá hạn → người duyệt của cấp; quá thêm N giờ → nhóm (bot DH). | – |
| `Messaging:Watch:ApprovalOverdueGroups` | List<string> | [] | Chưa có dữ liệu "cấp trên trực tiếp" → nhóm quyền theo tên (PROPOSED: COO). Khởi tạo rỗng (binder nối mảng). | – |
| `Messaging:Watch:ApprovalOverdueTopGroups` | List<string> | [] | BIZ-1.6 (D-77 f): bậc 3 – quá thêm ApprovalOverdueEscalateHours nữa (mặc định tổng 48 giờ sau hạn) → nhóm này (mặc định CEO). | BIZ-1.6 |
| `Messaging:ApprovalDigestHour` | int | 18 | APR-1.7 (D-81): giờ gửi tin tổng hợp duyệt cuối ngày (giờ VN, 0–23; &lt; 0 = tắt). Gửi ở lần RunWatch đầu tiên từ giờ này, 1 lần / người / ngày. | APR-1.7 |

## Integration

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Integration:Misa:ProductByMeal` | Dictionary<string, string> | – | Buổi / loại suất Delicare → mã thành phẩm AMIS (SANG, TRUA, TOI, DEM; COM_SUAT = cơm suất bán lẻ). | – |
| `Integration:Misa:WarehouseMap` | Dictionary<string, string> | new(StringComparer.OrdinalIgnoreCase) | Kho Delicare (mã hoặc tên) → mã kho AMIS. Nhiều kho cùng 1 mã AMIS → chuyển kho nội bộ giữa chúng không gửi sang AMIS. | – |
| `Integration:Misa:ReasonCodes` | Dictionary<string, string> | new(StringComparer.OrdinalIgnoreCase) | Lý do xuất / nhập Delicare → mã lý do (diễn giải) trên AMIS (vd. PRODUCTION, PACKAGING, WASTE, ADJUSTMENT, RETURN). | – |
| `Integration:Misa:ExcelColumns` | Dictionary<string, List<string>> | new(StringComparer.OrdinalIgnoreCase) | ACC-2 (D-131 e): cột Excel theo mẫu "Thêm bằng Excel" của AMIS – loại chứng từ (SALE, PURCHASE, ISSUE, FG_IN, FG_OUT, STOCK_OTHER) → ["Tiêu đề cột:trường"] (trường: date, number, desc, objectCode, objectName, code, na… | D-131 |
| `Integration:Misa:CloseDueDay` | int | 10 | ACC-4 (D-131 h): hạn khóa kỳ = ngày N tháng sau (10); bot nhắc Kế toán từ ngày N (5) nếu kỳ trước chưa khóa. | D-131 |
| `Integration:Misa:CloseRemindFromDay` | int | 5 | – | – |
| `Integration:Misa:SnapshotKeep` | int | 30 | Danh mục AMIS lưu tối đa N bản chụp (cũ hơn vẫn còn trong tài liệu đính kèm, chỉ không đọc). | – |

## Permissions

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Permissions:CacheMinutes` | int | 5 | Thời gian cache quyền của mỗi user (phút). | – |

## PurchasePlan

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `PurchasePlan:Enabled` | bool | true | Tắt cả job lập đề nghị tự động (màn vẫn xem được, bấm tay vẫn chạy). | – |
| `PurchasePlan:RunTimes` | string[] | [] | Giờ job lập / cập nhật đề nghị mua cho phần đến hạn đặt (lần 1 = job PurchasePlan, lần 2 = job PurchasePlanEvening – trước MrpJob 20:30). | – |
| `PurchasePlan:SendTime` | string | "15:00" | Giờ tự gửi đơn mua đã duyệt cho NCC (đơn duyệt sau giờ này gửi ngay khi duyệt). | – |
| `PurchasePlan:AutoSend` | bool | true | Tự gửi đơn mua cho NCC qua kênh tích hợp (false = chỉ đánh dấu chờ gửi tay). | – |
| `PurchasePlan:SubstituteMinSavingPct` | decimal | 5m | Thay thế rẻ hơn (giá trên 1 kg dùng được) ít nhất % này mới dùng; chênh ít hơn giữ nguyên liệu chuẩn. | – |
| `PurchasePlan:TolerancePct` | decimal | 10m | Đề nghị do hệ thống lập: số lượng ≤ nhu cầu tính × (1 + % này) mới coi là đúng định mức. | – |
| `PurchasePlan:SplitMaxCorrectionPct` | decimal | 15m | Chia NCC: tỷ lệ lần này = mục tiêu + (mục tiêu − lũy kế tháng), chặn trong ± số điểm này. | – |
| `PurchasePlan:SupplierConfirmHours` | int | 12 | Đơn đã gửi mà NCC chưa xác nhận trước giờ giao ít hơn số giờ này → bot báo Mua hàng. | – |
| `PurchasePlan:AutoSubmit` | bool | true | Tự gửi duyệt đề nghị do job lập (false = để Nháp cho người mua gửi). | – |

## ReferenceLists

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `ReferenceLists:Lists` | Dictionary<string, List<ReferenceItem>> | new(StringComparer.OrdinalIgnoreCase) | – | – |

## SystemUsers

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `SystemUsers:JobUserId` | string? | – | User.Id của tài khoản system (seed DB/seed/SystemUser_seed.sql). Trống = job không được ghi. | – |

## Zalo

| Khóa | Kiểu | Mặc định | Ý nghĩa | Task |
|---|---|---|---|---|
| `Zalo:BotCode` | string | "ZALO_KH" | Mã bot khách (BotConfiguration.BotCode, kênh ProviderCode ZALO). | – |
| `Zalo:AppId` | string? | – | App id của ứng dụng Zalo (không bí mật). | – |
| `Zalo:AppSecret` | string? | – | Khóa bí mật ứng dụng – đặt bằng biến môi trường Zalo__AppSecret. | – |
| `Zalo:RefreshToken` | string? | – | Token làm mới KHỞI TẠO (lần đầu sau khi cấp quyền OA) – biến môi trường Zalo__RefreshToken; sau đó dùng token lưu mã hóa trong DB. | – |
| `Zalo:TokenUrl` | string | "https://oauth.zaloapp.com/v4/oa/access_token" | – | – |
| `Zalo:LinkCodeHours` | int | 72 | Hạn mã liên kết khách (giờ) – KD gửi mã cho người liên hệ qua email / điện thoại. | – |
| `Zalo:MaxReplyLength` | int | 1900 | Độ dài tối đa 1 tin trả lời (Zalo giới hạn 2.000 ký tự). | – |
