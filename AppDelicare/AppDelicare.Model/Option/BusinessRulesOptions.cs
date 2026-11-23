namespace AppDelicare.Model.Option
{
    /// <summary>Section <c>BusinessRules</c> của appsettings (BUSINESS_RULES.md). Đổi ngưỡng không cần sửa code.</summary>
    public sealed class BusinessRulesOptions
    {
        public const string Section = "BusinessRules";

        public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
        /// <summary>BE2.6 (T21): quy tắc nhân sự – section <c>BusinessRules:Hr</c> (PROPOSED – D-55).</summary>
        public HrRulesOptions Hr { get; set; } = new();
        /// <summary>BE2.7: giới hạn API dùng chung (xuất danh sách, in hàng loạt, lịch sử) – section <c>BusinessRules:Common</c> (PROPOSED – D-56).</summary>
        public CommonRulesOptions Common { get; set; } = new();
        /// <summary>BE3.1: giới hạn dữ liệu biểu đồ – section <c>BusinessRules:Charts</c> (PROPOSED – D-57).</summary>
        public ChartRulesOptions Charts { get; set; } = new();
        /// <summary>BE3.3: chuông cảnh báo, tìm kiếm toàn cục, tab Liên quan – section <c>BusinessRules:Alerts</c> (PROPOSED – D-57 h, i, p–u).</summary>
        public AlertRulesOptions Alerts { get; set; } = new();
        /// <summary>BE3.4: KPI (giao đúng giờ, khoảng xem mặc định / tối đa) – section <c>BusinessRules:Kpi</c> (PROPOSED – D-57 w–ab).</summary>
        public KpiRulesOptions Kpi { get; set; } = new();
        /// <summary>BE3.5: dashboard điều hành + hành trình đơn – section <c>BusinessRules:Dashboard</c> (PROPOSED – D-57 ad–ai).</summary>
        public DashboardRulesOptions Dashboard { get; set; } = new();
        public int OrderCutoffHourPreviousDay { get; set; } = 20;
        public string OrderLockJobTime { get; set; } = "20:00";
        public string MrpJobTime { get; set; } = "20:30";
        public string PurchaseProposalJobTime { get; set; } = "21:00";
        public decimal StockCountVariancePctNeedApproval { get; set; } = 2.0m;
        public decimal PortionDeviationPctNeedApproval { get; set; } = 30.0m;
        public int TransportMinutesBeforeCutoff { get; set; } = 60;
        public decimal CoreTempMinC { get; set; } = 70.0m;
        public decimal DeliveryTempMinC { get; set; } = 60.0m;
        public decimal KpiOnTimeDeliveryTargetPct { get; set; } = 95.0m;
        public decimal KpiIngredientToRevenueMaxPct { get; set; } = 55.0m;
        public decimal KpiContainerLossMaxPct { get; set; } = 3.0m;
        public Dictionary<string, int> ComplaintSlaHours { get; set; } = new() { ["HIGH"] = 4, ["MEDIUM"] = 24, ["LOW"] = 72 };
        public Dictionary<string, int> ChecklistOffsetsMinutes { get; set; } = new()
        {
            ["PREP"] = -150, ["PREP_AREA_CLEAN"] = -90, ["BLAST_CHILL"] = -20, ["KITCHEN_CLEAN"] = 90,
        };
        public int ExpiringLotDays { get; set; } = 7;
        public int ShortageForecastDays { get; set; } = 3;
        /// <summary>BIZ-1.3 (D-77 c): số ngày tính nhu cầu nguyên liệu dự kiến (D+1 → D+N) – kế hoạch mua theo ngày cần hàng.</summary>
        public int PurchaseHorizonDays { get; set; } = 7;
        /// <summary>BIZ-1.3: ngày phải đặt = ngày cần − lead time NCC ưu tiên − số ngày đệm này.</summary>
        public int PurchaseOrderLeadBufferDays { get; set; } = 1;
        /// <summary>Spec 5.2 bước 4: chi phí nhân công / suất khi khóa công thức (PROPOSED – BA/PO CONFIRM).</summary>
        public decimal RecipeLaborCostPerServing { get; set; }
        /// <summary>Spec 5.2 bước 4: chi phí chung / suất (PROPOSED – BA/PO CONFIRM).</summary>
        public decimal RecipeOverheadCostPerServing { get; set; }
        /// <summary>Số suất chuẩn của định lượng công thức khi Recipe.StandardOutputQty trống (spec 5.2: 100 suất).</summary>
        public decimal RecipeStandardServings { get; set; } = 100;
        /// <summary>T13: ca giao mặc định theo loại suất (MealType → Shift.Code), D-19 / BUSINESS_RULES mục 0 (PROPOSED – BA/PO CONFIRM).</summary>
        public Dictionary<string, string> MealTypeDefaultShift { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SANG"] = "SANG_6H", ["TRUA"] = "TRUA", ["TOI"] = "TOI", ["DEM"] = "DEM_23H",
        };
        /// <summary>T14 / D-12: MRP cộng tồn an toàn (InventoryPolicie.SafetyStock, không có thì Ingredient.SafetyStock) vào nhu cầu (PROPOSED – BA/PO CONFIRM).</summary>
        public bool MrpIncludeSafetyStock { get; set; } = true;
        /// <summary>
        /// T14.1 (anh chốt 2026-11-16, GAP-MRP-03): thực đơn có ≥ 2 lựa chọn cho cùng loại suất → số suất chia đều cho các lựa chọn rồi cộng thêm
        /// % dự phòng này cho mỗi lựa chọn (0 = tắt). Thực đơn 1 lựa chọn không cộng.
        /// </summary>
        public decimal MrpOptionBufferPct { get; set; } = 15m;
        /// <summary>
        /// ORD-OPT-1.6 (D-74 e, anh chốt 2026-11-22): % dự phòng cho dòng đơn ĐÃ đăng ký theo lựa chọn (<c>OrderLine.MenuOptionNo</c>) –
        /// suất món = suất lựa chọn × (1 + % ÷ 100). Mặc định 0 (số khách đăng ký là số thật). Dòng cũ NULL vẫn theo <see cref="MrpOptionBufferPct"/>.
        /// </summary>
        public decimal MrpOptionQtyBufferPct { get; set; }
        /// <summary>T15: PO có tổng tiền ≤ ngưỡng này được tự duyệt khi gửi (0 = mọi PO phải duyệt) – PROPOSED – BA/PO CONFIRM (GAP-PUR-03).</summary>
        public decimal PurchaseOrderAutoApproveMaxAmount { get; set; }
        /// <summary>
        /// T15.1 / D-11 (anh chốt 2026-11-16): PO có tổng tiền ≥ ngưỡng này phải do Giám đốc duyệt – quyền Duyệt trên mục menu
        /// <c>#/wf-inbox?type=scm-po-director</c>; dưới ngưỡng duyệt trên <c>#/wf-inbox?type=scm-po</c>. Vẫn duyệt 1 cấp.
        /// </summary>
        public decimal PurchaseOrderDirectorApprovalAmount { get; set; } = 50_000_000m;
        /// <summary>BIZ-2.2 (D-77 – B3, C2): PO lập từ đề nghị mua Đã duyệt tự Đã duyệt khi đúng NCC gợi ý, đơn giá = bảng giá, số ≤ đề nghị, tổng &lt; ngưỡng Giám đốc.</summary>
        public bool PurchaseOrderAutoApproveFromRequest { get; set; } = true;
        /// <summary>BIZ-2.4 (D-77 – B5, C3): sau chốt đơn theo ngày → tự lập kế hoạch SX, tự duyệt + phát hành (trừ trường hợp phải chờ COO).</summary>
        public bool ProductionAutoReleaseAfterLock { get; set; } = true;
        /// <summary>BIZ-2.5 (D-77 – C4): phát hành lệnh → tự tạo phiếu xuất Nháp theo ca (số = giữ chỗ, lô FEFO).</summary>
        public bool ProductionAutoDraftIssueOnRelease { get; set; } = true;
        /// <summary>BIZ-2.6 (D-77 – C5): QC đầu vào kết luận Đạt / Đạt một phần → tự ghi sổ phiếu nhập (phần đạt).</summary>
        public bool GoodsReceiptAutoPostAfterQc { get; set; } = true;
        /// <summary>BIZ-2.7 (D-77 – C6): mẻ đủ điều kiện (lõi, sản lượng, lưu mẫu, không lỗi trọng yếu) tự Đạt QC.</summary>
        public bool BatchAutoPass { get; set; } = true;
        /// <summary>BIZ-2.8 (D-77 – C7): mẻ Đạt → tự sinh lệnh đóng gói (PackingOrder/GenerateFromBatch).</summary>
        public bool PackingAutoGenerateOnPass { get; set; } = true;
        /// <summary>BIZ-2.9 (D-77 – C8): đơn Đã giao → tự tạo đối soát (suất phục vụ = suất khách nhận).</summary>
        public bool ReconciliationAutoCreate { get; set; } = true;
        /// <summary>BIZ-2.9: đối soát Khớp, không phản ánh mở, quá số giờ này → tự chốt (0 = tắt).</summary>
        public int ReconciliationAutoCloseHours { get; set; } = 24;
        /// <summary>BIZ-2.9 (C9): chốt đối soát → tự tính giá vốn suất (MealCost/Calculate).</summary>
        public bool MealCostAutoOnReconcileClose { get; set; } = true;
        /// <summary>BIZ-3.4 (D-77 – C12): thứ chạy job nhắc thực đơn chung tuần sau (0 = Chủ nhật … 3 = thứ Tư).</summary>
        public int MenuReminderWeekday { get; set; } = 3;
        /// <summary>BIZ-3.4: thứ chạy job nhắc khách chưa đăng ký suất tuần sau (5 = thứ Sáu).</summary>
        public int OrderWeekReminderWeekday { get; set; } = 5;
        /// <summary>BIZ-3.3 (D-77 – C10): lưu bảng giá NCC → tự tính lại giá thành chuẩn món dùng nguyên liệu + cảnh báo FC % thực đơn.</summary>
        public bool StandardCostAutoOnSupplierPrice { get; set; } = true;
        /// <summary>BIZ-3.2 (D-77 – A7): VAT bán hàng % trên bảng kê thanh toán.</summary>
        public decimal SalesVatPct { get; set; } = 8m;
        /// <summary>BIZ-3.2: phí / khay thất thoát vượt ngưỡng theo loại khay (PROPOSED – trống = 0, chưa tính).</summary>
        public Dictionary<string, decimal> ContainerLossFee { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>T15 (anh chốt 2026-11-16): được nhận vượt số đặt tối đa bao nhiêu % mỗi dòng PO (GAP-PUR-04).</summary>
        public decimal GoodsReceiptOverReceivePct { get; set; } = 10m;
        /// <summary>
        /// T15.1 (anh chốt 2026-11-16, thay <c>ReceivingTempMaxC</c> của T15): nhiệt độ nhận hàng theo Ingredient.StorageType, 3 vùng –
        /// ≤ AcceptMax: nhận; ≤ ConditionalMax: nhận có điều kiện (bắt buộc lý do, lô "ưu tiên dùng trước", sự cố NCC; FROZEN còn phải xác nhận
        /// "không có dấu hiệu rã đông"); &gt; ConditionalMax: loại (chỉ KHONG_DAT). Kiểu bảo quản không có trong bảng (DRY / AMBIENT) → không kiểm.
        /// </summary>
        public Dictionary<string, ReceivingTempRule> ReceivingTemp { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CHILLED"] = new ReceivingTempRule { AcceptMax = 5m, ConditionalMax = 8m },
            ["FROZEN"] = new ReceivingTempRule { AcceptMax = -18m, ConditionalMax = -15m, RequireNoThawConfirmation = true },
        };
        /// <summary>T15: hạn xử lý sự cố NCC (SupplierIssue.DueAt) = lúc ghi sổ + số ngày này – PROPOSED (spec 9.1: +2 ngày).</summary>
        public int SupplierIssueDueDays { get; set; } = 2;
        /// <summary>T15.1 (anh chốt 2026-11-16, phương án A – không lưu DB, GAP-PUR-09): % VAT in trên Đơn đặt hàng NCC và trả trong PurchaseOrder/GetDetail.</summary>
        public PurchaseVatOptions PurchaseVat { get; set; } = new();
        /// <summary>
        /// T17.1 (anh chốt 2026-11-16 – Pilot 4 ca / ngày, phát hành tách theo ca của đơn): chỉ còn là ca (Shift.Code) cho dòng đơn chưa gán ca
        /// và món không có trong đơn khi chia kế hoạch theo ca (GAP-PRD-01).
        /// </summary>
        public string ProductionDefaultShiftCode { get; set; } = "TRUA";
        /// <summary>T17: số phút chia suất / đóng gói trước giờ xuất bếp – mẻ cuối của mỗi món phải nấu xong trước (giờ xuất bếp − số phút này). PROPOSED (GAP-PRD-07).</summary>
        public int ProductionPackingMinutes { get; set; } = 30;
        /// <summary>T17: số phút mỗi mẻ khi dây chuyền chưa có MinutesPerBatch (hoặc không chọn dây chuyền). PROPOSED (GAP-PRD-07).</summary>
        public int ProductionDefaultMinutesPerBatch { get; set; } = 60;
        /// <summary>
        /// T17: bắt buộc lưu mẫu (SAMPLE_RETAINED) trước khi mẻ đạt QC. Anh chốt 2026-11-16: lưu mẫu bắt buộc TRƯỚC KHI GIAO, không phải trước khi đạt QC →
        /// mặc định false (đạt QC chỉ cảnh báo khi chưa lưu mẫu); chặn ở T19 <c>DeliveryRoute/Dispatch</c>.
        /// </summary>
        public bool ProductionRequireSampleBeforeRelease { get; set; }
        /// <summary>T19: chặn xuất chuyến khi mẻ của ngày / ca chưa đóng gói xong (còn mẻ chưa Đã đóng gói / Không đạt) – PROPOSED – BA/PO CONFIRM (GAP-DLV-03).</summary>
        public bool DeliveryRequirePackedBatches { get; set; } = true;
        /// <summary>T19: chặn xuất chuyến khi có mẻ (đã đóng gói) của ngày / ca chưa lưu mẫu SAMPLE_RETAINED – anh chốt 2026-11-16.</summary>
        public bool DeliveryRequireSampleRetained { get; set; } = true;
        /// <summary>T19: cơ sở tính tiền khi đối soát: ORDERED / DELIVERED / ACCEPTED / SERVED – PROPOSED (spec 6.7: TotalAmount × suất nhận ÷ suất đặt; GAP-DLV-05).</summary>
        /// <summary>BIZ-1.4 (D-77 d): mặc định RECEIVED = suất khách ký nhận − suất lỗi Delicare. Hợp đồng chưa có cột lưu kiểu riêng (GAP-BIZ-01) → dùng chung giá trị này.</summary>
        public string ReconciliationBillingBasis { get; set; } = "RECEIVED";
        /// <summary>T19: đóng đối soát có |chênh lệch| % lớn hơn ngưỡng này phải ghi chú (spec 6.7, GAP-F-11 tạm 1%) – PROPOSED.</summary>
        public decimal ReconciliationVarianceRemarkPct { get; set; } = 1.0m;
        /// <summary>T19: bàn giao có suất bị từ chối → tự tạo phản ánh PA- (SourceChannel POD, SHORTAGE) – spec 10.3, PROPOSED.</summary>
        public bool DeliveryAutoComplaintOnRejected { get; set; } = true;
        /// <summary>
        /// BE2.1 (spec 10.2 load / dispatch): xuất phát chỉ từ chuyến Đang xếp (Loading) và mọi đơn giao của chuyến đã có dòng xếp hàng Loaded
        /// (<c>DeliveryRoute/Load</c>). false = cho xuất phát thẳng từ Kế hoạch như T19. PROPOSED – BA/PO CONFIRM.
        /// </summary>
        public bool DeliveryRequireLoading { get; set; } = true;
        /// <summary>BE2.2 (spec 6.6 confirm): |đặt − dự báo| / dự báo × 100 vượt ngưỡng này → cảnh báo + ghi nhận vào đơn (không chặn). PROPOSED – BA/PO CONFIRM.</summary>
        public decimal ForecastVariancePctWarning { get; set; } = 10.0m;
        /// <summary>BE2.2: số ngày tối đa của ma trận / công bố dự báo trong 1 lần gọi.</summary>
        public int ForecastMaxDays { get; set; } = 62;
        /// <summary>FC-1.1 (D-76): số tuần nhìn lại khi tính dự báo gợi ý (trung bình suất thực nhận cùng thứ).</summary>
        public int ForecastLookbackWeeks { get; set; } = 4;
        /// <summary>FC-1.1: số ngày tối đa của 1 lần tạo dự báo gợi ý.</summary>
        public int ForecastSuggestMaxDays { get; set; } = 14;

        // ------------------------------------------------------------ BE2.3 – duyệt nhiều cấp (PROPOSED – BA/PO CONFIRM, D-52)
        /// <summary>
        /// BE2.3 (spec 2.3 "cấu hình mẫu quy trình trong file cấu hình", GAP-F-08): BusinessType → danh sách mục quyền duyệt theo thứ tự bước
        /// (mỗi phần tử = screen-id của mục <c>#/wf-inbox?type=&lt;id&gt;</c>). Có khóa → thay quy trình mặc định của handler (vd. CONTRACT 2 cấp).
        /// Để trống = mặc định của handler (PURCHASE_REQUEST 2 cấp D-46, STOCK_COUNT 2 cấp spec 2.3, PO theo ngưỡng D-11, còn lại 1 cấp).
        /// </summary>
        public Dictionary<string, string[]> ApprovalSteps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>BE2.3: hạn xử lý mỗi bước duyệt (giờ, tính từ lúc bước được giao). 0 = không có hạn. Quá hạn chỉ là cờ (D-45: không job).</summary>
        public int ApprovalStepDueHours { get; set; } = 48;
        /// <summary>BE2.3: hạn riêng theo BusinessType (giờ) – ghi đè <see cref="ApprovalStepDueHours"/>.</summary>
        public Dictionary<string, int> ApprovalStepDueHoursByType { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>BE2.3: true = bước đã quá hạn không duyệt / từ chối được nữa (người gửi hủy rồi gửi lại). Mặc định false = chỉ gắn cờ quá hạn.</summary>
        public bool ApprovalBlockOverdueDecision { get; set; }
        /// <summary>BE2.3: duyệt nhiều cấp – một người không quyết định 2 bước của cùng yêu cầu (quản trị viên được miễn).</summary>
        public bool ApprovalRequireDistinctApprovers { get; set; } = true;
        /// <summary>
        /// BE2.3 (PO nhiều cấp theo ngưỡng): true = PO ≥ <see cref="PurchaseOrderDirectorApprovalAmount"/> duyệt 2 cấp (<c>scm-po</c> → <c>scm-po-director</c>).
        /// Mặc định false = 1 cấp Giám đốc như D-11 (anh chốt 2026-11-16).
        /// </summary>
        public bool PurchaseOrderDirectorTwoLevel { get; set; }
        /// <summary>BE2.3 (spec 7.6 adjust, GAP-F-04): điều chỉnh tồn tay phải duyệt STOCK_ADJUSTMENT trước khi ghi sổ. false = ghi ngay như T16.</summary>
        public bool StockAdjustmentRequireApproval { get; set; } = true;

        // ------------------------------------------------------------ BE2.4 – kho & sản xuất mở rộng (PROPOSED – BA/PO CONFIRM, D-53)
        /// <summary>BE2.4 (spec 8.1 release bước 4, Catalog 126): phát hành kế hoạch thì giữ chỗ nguyên liệu (MaterialReservation) cho lệnh SX mới. false = không giữ chỗ (như T17).</summary>
        public bool ProductionReserveOnRelease { get; set; } = true;
        /// <summary>BE2.4 (spec 7.1 bước 5): MRP trừ hàng sắp về từ PO Đã duyệt / Đã gửi / Nhận một phần có ngày giao dự kiến ≤ ngày sản xuất (trừ phần đã nhận).</summary>
        public bool MrpIncludeIncomingPurchaseOrders { get; set; } = true;
        /// <summary>
        /// BE2.4 (T17 công đoạn theo tổ): true = mọi mẻ bắt buộc đi qua công đoạn (Sơ chế → Nấu → Chia suất) – nấu xong cần công đoạn Nấu xong, đóng gói cần Chia suất xong.
        /// Mặc định false = chỉ kiểm khi mẻ đã bắt đầu ghi công đoạn (mẻ không ghi công đoạn chạy như T17).
        /// </summary>
        public bool ProductionRequireStages { get; set; }
        /// <summary>BE2.4: tổ mặc định của từng công đoạn (ItemCode mục checklist PRD_STAGE → OrganizationUnit.Code loại TEAM, ORG_STRUCTURE.md).</summary>
        public Dictionary<string, string> ProductionStageTeams { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SO_CHE"] = "OPS-KIT-PREP", ["NAU"] = "OPS-KIT-COOK", ["COM_CANH"] = "OPS-KIT-RICE", ["CHIA_SUAT"] = "OPS-KIT-PORT",
        };
        /// <summary>
        /// BE2.4 (spec 8.5: LaborCost = giờ công × đơn giá, chưa có bảng lương): đơn giá 1 giờ công (VND). &gt; 0 và mẻ có ghi công đoạn → nhân công = Σ giờ công đoạn × số người × đơn giá;
        /// còn lại nhân công = số phần nấu được × <see cref="RecipeLaborCostPerServing"/>.
        /// </summary>
        public decimal ProductionLaborCostPerHour { get; set; }
        /// <summary>BE2.4 (spec 7.9): |chênh lệch tiêu hao| % vượt ngưỡng này → đánh dấu <c>isOverThreshold</c> (không chặn đóng lệnh).</summary>
        public decimal ConsumptionVariancePctWarning { get; set; } = 5.0m;

        // ------------------------------------------------------------ BE2.5 – ATTP, vệ sinh, hộp / khay (PROPOSED – BA/PO CONFIRM, D-54)
        /// <summary>
        /// BE2.5 (spec 9.2): tiêu chí trọng yếu (IsCritical) KHÔNG ĐẠT gắn mẻ → giữ mẻ: Đạt → Chờ QC (QcStatus HOLD), mẻ đang nấu / chờ QC → HOLD;
        /// mẻ bị chặn đạt QC / đóng gói / xuất chuyến tới khi tiêu chí đó được kiểm lại ĐẠT. false = chỉ cảnh báo.
        /// </summary>
        public bool FoodSafetyCriticalFailHoldsBatch { get; set; } = true;
        /// <summary>BE2.5 (spec 9.4): mã bộ checklist vệ sinh khay / hộp 6 bước (seed <c>DB/seed/FoodSafetyQa_seed.sql</c>).</summary>
        public string CleaningChecklistCode { get; set; } = "VS-KHAY";
        /// <summary>BE2.5: bước vệ sinh phải ghi hóa chất đã dùng khi ĐẠT (ItemCode của checklist vệ sinh).</summary>
        public string[] CleaningChemicalRequiredItems { get; set; } = ["KHU_KHUAN"];
        /// <summary>BE2.5 (spec 10.4 inspect "khay dùng được → tự tạo CleaningBatche"): kiểm tra thu hồi xong tự lập mẻ vệ sinh VS- cho số khay dùng được.</summary>
        public bool CleaningAutoCreateOnInspect { get; set; } = true;
        /// <summary>
        /// BE2.5 (spec 10.3 pod "Ghi PackagingLedger OUT nếu đóng khay"): bàn giao không gửi số khay → tự ghi OUT = suất khách nhận × <see cref="PackagingUnitsPerMeal"/>,
        /// loại = loại bao bì của kết quả đóng gói của đơn, không có thì <see cref="PackagingDefaultType"/>. false = chỉ ghi khi bàn giao gửi số khay.
        /// </summary>
        public bool PackagingOutOnDelivery { get; set; } = true;
        public string PackagingDefaultType { get; set; } = "KHAY";
        public decimal PackagingUnitsPerMeal { get; set; } = 1m;
    }

    /// <summary>T15.1: ngưỡng nhiệt độ nhận hàng của 1 kiểu bảo quản (°C).</summary>
    public sealed class ReceivingTempRule
    {
        /// <summary>Nhiệt độ ≤ ngưỡng này → nhận bình thường.</summary>
        public decimal AcceptMax { get; set; }
        /// <summary>AcceptMax &lt; nhiệt độ ≤ ngưỡng này → nhận có điều kiện; lớn hơn → loại (chỉ KHONG_DAT).</summary>
        public decimal ConditionalMax { get; set; }
        /// <summary>Nhận có điều kiện phải kèm xác nhận "không có dấu hiệu rã đông" (FROZEN).</summary>
        public bool RequireNoThawConfirmation { get; set; }
    }

    /// <summary>
    /// T15.1: VAT mua hàng – % mặc định và % theo mã nhóm nguyên liệu (<c>IngredientCategorie.Code</c>; không khớp thì xét nhóm cha).
    /// Chỉ tính khi đọc / in, không lưu (GAP-PUR-09).
    /// </summary>
    public sealed class PurchaseVatOptions
    {
        public decimal DefaultPct { get; set; } = 8m;
        public Dictionary<string, decimal> ByIngredientCategoryCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
