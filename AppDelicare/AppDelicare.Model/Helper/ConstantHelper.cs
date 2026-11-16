namespace AppDelicare.Model.Helper
{
    public class ConstantHelper
    {
        public static readonly string ConnectionStrings = "ConnectionStrings";
        public static readonly string MainConnectionString = "MainConnectionString";
        public static readonly string Import = "Import";
        public static readonly string UploadFile = "UploadFile";
        public static readonly string Underlined = "_";
        public static readonly string Minus = "-";
        public static readonly string Space = " ";
        public static readonly string Source = "/";
        public static readonly string Colon = ":";
        public static readonly string Comma = ",";
        public static readonly string CommaSpace = ", ";
        public static readonly string Dot = ".";
        public static readonly string SemiColon = ";";
        public static readonly string OpeningParenthesis = "(";
        public static readonly string CloseParenthesis = ")";
        public static readonly string OpenBracket = "[";
        public static readonly string CloseBracket = "]";
        public static readonly string SourceN = "\n";
        public static readonly string Break = "<br />";
        public static readonly string Page = "@Page";
        public static readonly string PageSize = "@PageSize";
        public static readonly string SortField = "@SortField";
        public static readonly string SortOrder = "@SortOrder";
        public static readonly string FormatDate_ddMMyyyy = "dd/MM/yyyy";
        public static readonly string FormatDate_ddMMyyyyhmmss = "ddMMyyyyhmmss";
        public static readonly string FormatDate_ddMMyyyyhmmss1 = "dd/MM/yyyy h:mm:ss";
        public static readonly string DownloadExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public static readonly string xlsx = ".xlsx";
        public static readonly string xls = ".xls";
        public static readonly string docx = ".docx";
        public static readonly string Zero = "0";
        public static readonly string One = "1";
        public static readonly string NotFound = "Không tìm thấy";

        public static class JwtClaimIdentifiers
        {
            public const string Rol = "rol", Id = "id";
        }

        public static class JwtClaims
        {
            public const string ApiAccess = "api_access";
        }

        public static class Message
        {
            public static readonly string GetAllSuccessfully = "Lấy thông tin dữ liệu thành công";
            public static readonly string Successfully = "Successfully";
            public static readonly string InternalServer = "Internal Server Error";
            public static readonly string ModelStateMessage = "Model State Invalid";
            public static readonly string ImportSuccess = "Import dữ liệu thành công";
            public static readonly string SaveSuccess = "Lưu thông tin thành công";
            public static readonly string CannotImport = "Không thể tải dữ liệu lên, vui lòng liên hệ với quản trị viên";
            public static readonly string ImportFileNull = "Không thể tải dữ liệu lên, do trong file không có nội dung";
            public static readonly string FileCorrectFormat = "File tải lên chưa đúng định dạng, hệ thống chỉ tiếp nhận file có đuôi là xls, xlsx";
            public static readonly string FileRequired = "Yêu cầu chọn file";
            public static readonly string UserInActiveMessage = "Tài khoản hoặc mật khẩu không đúng";
            public static readonly string AccountInActive = "Tài khoản của bạn đang bị khóa, vui lòng liên hệ với quản trị viên";
            public static readonly string AccountDeleted = "Tài khoản của bạn đã bị xóa, vui lòng liên hệ với quản trị viên";
            public static readonly string ReportSuccess = "Gửi báo cáo thành công";
            public static readonly string RecordNotFoundMessage = "Không tìm thấy bản ghi";
            public static readonly string NameExist = "Tên {0} đã tồn tại, vui lòng nhập tên khác";
            public static readonly string MetaLinkExist = "Đường dẫn {0} đã tồn tại, vui lòng nhập đường dẫn khác";
            public static readonly string CodeExist = "Mã {0} đã tồn tại, vui lòng nhập mã khác";
            public static readonly string DeleteExistFail = "Không thể xóa dữ liệu danh mục này do dữ liệu đang được sử dụng";
            public static readonly string DeleteSuccess = "Xóa thông tin thành công";
            public static readonly string UploadFileSuccess = "Upload file thành công";
            public static readonly string UploadFileFail = "Đã xảy ra lỗi trong quá trình upload file";
            public static readonly string UserNameExist = "UserName {0} đã tồn tại, vui lòng nhập username khác";
            public static readonly string ResetPassword = "Đặt lại mật khẩu thành công";
            public static readonly string NotExist = " {0} không tồn tại trong cơ sở dữ liệu";
            // T45R – thông báo nghiệp vụ bổ sung (khung cũ, D-30).
            public static readonly string CannotLockYourself = "Không thể tự khóa tài khoản của chính mình";
            public static readonly string GroupNotFound = "Không tìm thấy nhóm người dùng";
            public static readonly string MenuNotFound = "Menu không tồn tại: {0}";
            public static readonly string MenuCycle = "Sắp xếp tạo vòng lặp cha – con";
            public static readonly string ParentCycle = "Đơn vị cha không được là chính nó hoặc đơn vị con của nó";
            public static readonly string AlreadyResolved = "Xung đột này đã được xử lý";
            public static readonly string ResolutionNoteRequired = "Ghi chú xử lý là bắt buộc (tối đa 1000 ký tự)";
            public static readonly string InvalidStatusForAction = "Chỉ {0} khi công thức ở trạng thái {1} (hiện tại: {2})";
            public static readonly string RecipeHasOpenVersion = "Món đang có bản công thức nháp / chờ duyệt – hoàn tất bản đó trước";
            public static readonly string RecipeNeedsLine = "Công thức phải có ít nhất 1 dòng định lượng";
            public static readonly string RejectReasonRequired = "Từ chối phải ghi lý do";
            public static readonly string SourceRecipeNotInDish = "Công thức nguồn không thuộc món này";
            public static readonly string QuantityMustBePositive = "Định lượng phải lớn hơn 0";
            public static readonly string WastePercentRange = "Hao hụt phải trong khoảng [0, 100)";
            public static readonly string UomConversionMissing = "Chưa có quy đổi giữa 2 đơn vị này";
            public static readonly string PriceNotFound = "Không tìm thấy đơn giá áp dụng cho khách / hợp đồng / loại suất này";
            public static readonly string PriceOverlap = "Chồng thời gian hiệu lực với chính sách giá khác cùng khách / hợp đồng / loại suất";
            public static readonly string PriceMustBePositive = "Đơn giá phải lớn hơn 0";
            public static readonly string CifExist = "CIF {0} đã tồn tại";
            public static readonly string DeliveryWindowInvalid = "Giờ giao từ phải nhỏ hơn giờ giao đến";
            public static readonly string LatitudeInvalid = "Vĩ độ phải trong [-90, 90]";
            public static readonly string LongitudeInvalid = "Kinh độ phải trong [-180, 180]";
            public static readonly string LocationInUse = "Còn đơn giao chưa hoàn tất tại điểm này – không ngừng hoạt động được";
            public static readonly string YieldPercentRange = "Tỷ lệ thu hồi phải trong (0, 100]";
            public static readonly string StandardWasteRange = "Tỷ lệ hao hụt chuẩn phải trong [0, 100)";
            public static readonly string ShelfLifeRequired = "Nguyên liệu quản lý hạn dùng phải có số ngày hạn dùng lớn hơn 0";
            public static readonly string ConversionFactorPositive = "Hệ số quy đổi phải lớn hơn 0";
            public static readonly string ConversionSameUnit = "Đơn vị nguồn và đích phải khác nhau";
            public static readonly string ConversionExist = "Cặp quy đổi đơn vị này đã tồn tại";
            public static readonly string StockChainInvalid = "Phải thỏa Tồn tối thiểu ≤ Tồn an toàn ≤ Điểm đặt hàng ≤ Tồn tối đa";
            public static readonly string InventoryPolicyExist = "Đã có chính sách tồn cho nguyên liệu này tại kho này";
            public static readonly string ShiftTimeSame = "Giờ bắt đầu và giờ kết thúc không được trùng nhau";
            public static readonly string ShiftTimeRange = "Giờ phải trong khoảng 00:00:00–23:59:59";
            public static readonly string ChecklistVersionExist = "Checklist mã {0} phiên bản {1} đã tồn tại";
            public static readonly string ReasonRequired = "Lý do là bắt buộc (3–1000 ký tự)";
            // T12 – duyệt
            public static readonly string ApprovalAlreadyPending = "Đã có yêu cầu duyệt đang chờ cho chứng từ này";
            public static readonly string ApprovalNotPending = "Yêu cầu không còn chờ duyệt (trạng thái: {0})";
            public static readonly string ApprovalNoRight = "Bạn không có quyền duyệt loại yêu cầu này";
            public static readonly string ApprovalPayloadChanged = "Nội dung chứng từ đã thay đổi sau khi gửi duyệt – gửi duyệt lại";
            // T13 – thực đơn tuần, phân công thực đơn, đơn hàng (P3)
            public static readonly string StatusActionInvalid = "Chỉ {0} khi {1} ở trạng thái {2} (hiện tại: {3})";
            public static readonly string MenuLineDayOfWeekRange = "Thứ phải trong khoảng 1 (Thứ 2) – 7 (Chủ nhật)";
            public static readonly string MenuLineOptionRange = "Lựa chọn (SequenceNo) phải trong khoảng 1 – 9";
            public static readonly string MenuLineMealTypeRequired = "Dòng thực đơn phải có loại suất (MealType)";
            public static readonly string MenuDishNoLockedRecipe = "Cảnh báo: món {0} chưa có công thức đang khóa – chưa tính được giá vốn";
            public static readonly string MenuNeedsLine = "Thực đơn phải có ít nhất 1 món";
            public static readonly string MenuPublishMissingDays = "Chưa đủ món để công bố – thiếu: {0}";
            public static readonly string EffectiveRangeInvalid = "Ngày hiệu lực từ phải nhỏ hơn hoặc bằng ngày hiệu lực đến";
            public static readonly string MenuAssignmentOverlap = "Chồng thời gian hiệu lực với phân công thực đơn khác cùng khách / điểm giao / loại suất";
            public static readonly string MenuAssignmentContractMismatch = "Hợp đồng không thuộc khách hàng này";
            public static readonly string MenuAssignmentMealTypeNotInContract = "Loại suất {0} không có trong suất của hợp đồng";
            public static readonly string MenuAssignmentLocationMismatch = "Điểm giao không thuộc khách hàng này";
            public static readonly string MenuNotAssigned = "Không có thực đơn được gán cho khách / điểm giao / loại suất / ngày này";
            public static readonly string NoActiveContract = "Khách hàng không có hợp đồng đang hiệu lực tại ngày {0}";
            public static readonly string ContractNotActiveOnDate = "Hợp đồng không ở trạng thái hiệu lực tại ngày {0}";
            public static readonly string OrderAfterCutoff = "Đã quá giờ chốt đơn ({0}) cho ngày ăn {1} – không {2} được. Cần thay đổi sau giờ chốt: liên hệ điều phối";
            public static readonly string OrderCutoffNotReached = "Chưa tới giờ chốt đơn ({0}) cho ngày ăn {1}";
            public static readonly string OrderNeedsLine = "Đơn phải có ít nhất 1 dòng số suất lớn hơn 0";
            public static readonly string OrderQuantityNegative = "Số suất không được âm";
            public static readonly string OrderLineLocationMismatch = "Mỗi đơn chỉ cho 1 điểm giao – dòng có điểm giao khác đầu đơn";
            public static readonly string OrderLineDuplicate = "Trùng dòng suất (cùng suất hợp đồng và ca)";
            public static readonly string OrderContractMealInvalid = "Suất hợp đồng không thuộc hợp đồng của đơn";
            public static readonly string OrderLineNotInOrder = "Dòng {0} không thuộc đơn này";
            public static readonly string OrderLineIncomplete = "Mọi dòng phải có loại suất, ca và số suất lớn hơn 0";
            public static readonly string OrderDuplicate = "Đã có đơn {0} cho khách / điểm giao / ngày ăn này – sửa đơn đó thay vì tạo mới";
            public static readonly string OrderShiftNotFound = "Không xác định được ca giao cho loại suất {0} – chọn ca";
            public static readonly string OrderCancelReasonRequired = "Hủy đơn phải ghi lý do";
            public static readonly string OrderLineCrudBlocked = "Dòng đơn chỉ sửa qua Order/Create và Order/UpdateLines (tính lại tiền, giờ chốt, lệch suất)";
            public static readonly string OrderInsertBlocked = "Tạo đơn qua Order/Create (sinh số PH-, tính giá, thực đơn, giờ chốt)";
            public static readonly string OrderDeviationRequested = "Số suất lệch {0}% (> {1}%) so với đơn đã xác nhận – đã gửi duyệt {2}, số mới chỉ ghi khi được duyệt";
            public static readonly string OrderDeviationPending = "Đơn đang có yêu cầu duyệt lệch suất {0} – chờ duyệt / từ chối trước khi sửa tiếp";
            // T16 – sổ kho, xuất kho, kiểm kê (P4)
            public static readonly string LedgerInsertBlocked = "Sổ kho chỉ ghi thêm qua nghiệp vụ: điều chỉnh (InventoryTransaction/Adjust), chuyển kho (Transfer), xuất kho (MaterialIssue/Post), kiểm kê (StockCount/Post), đảo bút toán (Reverse)";
            public static readonly string BalanceCrudBlocked = "Tồn kho được tính từ sổ cái – không thêm / sửa / xóa trực tiếp (quản trị: InventoryBalance/RebuildBalance)";
            public static readonly string WarehouseNotFound = "Không tìm thấy kho";
            public static readonly string StorageLocationNotInWarehouse = "Vị trí lưu kho không thuộc kho đã chọn";
            public static readonly string IngredientNotFound = "Không tìm thấy nguyên liệu";
            public static readonly string LotNotFound = "Không tìm thấy lô";
            public static readonly string LotIngredientMismatch = "Lô {0} không thuộc nguyên liệu đã chọn";
            public static readonly string UnitRequired = "Không xác định được đơn vị tính – chọn đơn vị";
            public static readonly string UnitMismatch = "Đơn vị tính khác đơn vị của tồn kho hiện có ({0}) – Pilot chưa quy đổi đơn vị khi ghi sổ";
            public static readonly string QuantityPositive = "Số lượng phải lớn hơn 0";
            public static readonly string AdjustQuantityNonZero = "Số lượng điều chỉnh phải khác 0 (dương = tăng, âm = giảm)";
            public static readonly string TransferSameLocation = "Kho / vị trí nhận phải khác kho / vị trí chuyển";
            public static readonly string InsufficientAvailable = "Không đủ tồn khả dụng của {0}: còn {1}, cần {2}";
            public static readonly string LotHoldInvalid = "Chỉ tạm giữ lô đang khả dụng (hiện tại: {0})";
            public static readonly string LotReleaseInvalid = "Chỉ mở lô đang tạm giữ (hiện tại: {0})";
            public static readonly string LotNumberRequired = "Nhập số lô (hoặc chọn nhà cung cấp có mã để sinh số NL-)";
            public static readonly string LotQcFailedLocked = "Lô có kết quả QC {0} – không đổi kết quả QC qua sửa danh mục (chỉ qua luồng QC nhập hàng)";
            public static readonly string LotIngredientLocked = "Lô đã có giao dịch kho – không đổi nguyên liệu của lô";
            public static readonly string IssueNeedsLine = "Phiếu xuất phải có ít nhất 1 dòng số lượng lớn hơn 0";
            public static readonly string IssueInsertBlocked = "Tạo phiếu xuất qua MaterialIssue/Create (sinh số PH-, kiểm lô FEFO / FIFO, hạn dùng, QC)";
            public static readonly string IssueLineCrudBlocked = "Dòng phiếu xuất chỉ ghi qua MaterialIssue/Create";
            public static readonly string IssueCancelReasonRequired = "Hủy phiếu xuất phải ghi lý do";
            public static readonly string ProductionOrderNotFound = "Không tìm thấy lệnh sản xuất";
            public static readonly string LotNotFirstWarning = "Cảnh báo: còn lô {0} ({1}) nên xuất trước lô {2} theo {3}";
            public static readonly string StockCountInsertBlocked = "Tạo phiếu kiểm kê qua StockCount/Create (sinh số PH-, lấy tồn hệ thống)";
            public static readonly string StockCountLineCrudBlocked = "Dòng kiểm kê chỉ ghi qua StockCount/RecordCounts";
            public static readonly string CountTypeInvalid = "Loại kiểm kê phải là DAILY / WEEKLY / MONTHLY / ADHOC";
            public static readonly string PhysicalQuantityNegative = "Số lượng thực tế không được âm";
            public static readonly string CountLineNotInCount = "Dòng {0} không thuộc phiếu kiểm kê này";
            public static readonly string VarianceReasonRequired = "Dòng {0} lệch {1} – phải ghi lý do chênh lệch";
            public static readonly string VarianceNeedsAdmin = "Chênh lệch kiểm kê {0}% vượt ngưỡng {1}% – Pilot: chỉ quản trị viên được ghi sổ (duyệt kiểm kê để sau Pilot)";
            // T14 – tính nhu cầu NL (MRP) + đề nghị mua nháp (P5)
            public static readonly string MrpCrudBlocked = "Nhu cầu nguyên liệu chỉ ghi qua MaterialRequirement/Calculate (tính lại theo ngày sản xuất)";
            public static readonly string MrpDateRequired = "Chọn ngày sản xuất";
            public static readonly string OrganizationUnitNotFound = "Không tìm thấy chi nhánh / đơn vị";
            public static readonly string MrpWarehouseNotInUnit = "Kho đã chọn không thuộc chi nhánh đã chọn";
            public static readonly string MrpPlanNotDraft = "Kế hoạch sản xuất {0} đã ở trạng thái {1} – không tính lại nhu cầu";
            public static readonly string MrpPurchaseRequestLocked = "Đề nghị mua {0} của lần tính trước đang ở trạng thái {1} – không tính lại được (từ chối / hủy đề nghị mua trước)";
            public static readonly string MrpNoLockedOrder = "Không có đơn Đã chốt cho ngày {0} – chốt đơn (Order/Lock, Order/LockByMealDate) trước khi tính nhu cầu";
            public static readonly string MrpNotCalculated = "Chưa tính nhu cầu nguyên liệu cho ngày {0}";
            public static readonly string MrpOrdersNotLocked = "Cảnh báo: còn {0} đơn chưa chốt của ngày {1} (không được tính)";
            public static readonly string MrpOrderLineNoMenu = "Cảnh báo: đơn {0} (loại suất {1}) chưa gắn thực đơn – bỏ qua {2} suất";
            public static readonly string MrpMenuNoDish = "Cảnh báo: thực đơn {0} không có món cho loại suất {1} thứ {2} – bỏ qua {3} suất của đơn {4}";
            public static readonly string MrpDishNoRecipe = "Món chưa có công thức đã khóa / đã duyệt – không tính nguyên liệu";
            public static readonly string MrpRecipeNotLocked = "Công thức chưa khóa (Đã duyệt) – dùng dòng định lượng hiện tại";
            public static readonly string MrpUnitMissing = "Cảnh báo: nguyên liệu {0} thiếu quy đổi đơn vị {1} → {2} – phần này không được tính";
            public static readonly string MrpNoSupplier = "Chưa có NCC đang hiệu lực cho nguyên liệu này";
            public static readonly string MrpLeadTimeWarning = "Cảnh báo: NCC {0} cần {1} ngày giao – đặt hôm nay không kịp ngày {2}";
            // T15 – mua hàng, nhận hàng, QC đầu vào (P6)
            public static readonly string PurchaseRequestStatusLocked = "Trạng thái đề nghị mua chỉ đổi qua thao tác nghiệp vụ (ConvertToPurchaseOrder / Cancel)";
            public static readonly string PurchaseRequestNeedsLine = "Đề nghị mua chưa có dòng nguyên liệu";
            public static readonly string PurchaseRequestNoSupplier = "Chưa có NCC đang hiệu lực (SupplierMaterial) cho: {0} – thêm bảng giá NCC rồi lập PO";
            public static readonly string PurchaseRequestLineLocked = "Chỉ sửa dòng của đề nghị mua Nháp (đề nghị hiện ở trạng thái {0})";
            public static readonly string PurchaseOrderStatusLocked = "Trạng thái, số và tổng tiền của PO chỉ đổi qua thao tác nghiệp vụ (UpdateLines / Submit / Approve / Send / Cancel / Close)";
            public static readonly string PurchaseOrderLineCrudBlocked = "Dòng PO chỉ ghi qua PurchaseOrder/UpdateLines (PO Nháp)";
            public static readonly string PurchaseOrderNeedsLine = "PO chưa có dòng nguyên liệu (số lượng > 0)";
            public static readonly string PurchaseOrderDuplicateIngredient = "Mỗi nguyên liệu chỉ có 1 dòng trên PO (trùng: {0})";
            public static readonly string PurchaseOrderLineInvalid = "Số lượng phải > 0 và đơn giá không âm";
            public static readonly string PurchaseOrderLineNotInOrder = "Dòng {0} không thuộc PO này";
            public static readonly string PurchaseOrderFromRequestNoDelete = "PO lập từ đề nghị mua không xóa được – dùng PurchaseOrder/Cancel";
            public static readonly string PurchaseOrderHasReceipt = "PO đã có phiếu nhập {0} – không hủy được (đóng PO bằng Close)";
            public static readonly string PurchaseOrderPendingReceipt = "PO còn phiếu nhập chưa ghi sổ: {0}";
            public static readonly string PurchaseOrderNotPending = "PO không có yêu cầu duyệt đang chờ";
            public static readonly string PurchaseOrderAutoApproved = "PO tổng {0} ≤ ngưỡng tự duyệt {1} – đã duyệt tự động";
            public static readonly string PurchaseOrderSubmitted = "Đã gửi duyệt PO (yêu cầu {0})";
            public static readonly string CancelReasonRequired = "Nhập lý do hủy (tối thiểu 3 ký tự)";
            public static readonly string SupplierNotFound = "Không tìm thấy nhà cung cấp";
            public static readonly string SupplierInactive = "Nhà cung cấp đang ngừng giao dịch";
            public static readonly string GoodsReceiptInsertBlocked = "Phiếu nhập chỉ lập qua GoodsReceipt/Create (theo PO đã duyệt)";
            public static readonly string GoodsReceiptLineCrudBlocked = "Dòng phiếu nhập chỉ ghi qua GoodsReceipt/Create và QcInspection/Conclude";
            public static readonly string GoodsReceiptDeleteBlocked = "Phiếu nhập không xóa – dùng GoodsReceipt/Cancel khi chưa ghi sổ";
            public static readonly string GoodsReceiptPoNotReceivable = "Chỉ nhận hàng theo PO Đã duyệt / Đã gửi NCC / Nhận một phần (PO {0} đang {1})";
            public static readonly string GoodsReceiptNeedsLine = "Phiếu nhập phải có ít nhất 1 dòng số lượng nhận > 0";
            public static readonly string GoodsReceiptOverReceive = "{0}: nhận {1} vượt số còn được nhận {2} (đặt {3}, cho phép vượt {4}%)";
            public static readonly string GoodsReceiptDuplicateLine = "Dòng PO {0} xuất hiện nhiều lần trong phiếu nhập";
            public static readonly string GoodsReceiptDateInvalid = "{0}: ngày sản xuất sau hạn dùng";
            public static readonly string GoodsReceiptNotConcluded = "Phiếu nhập chưa kết luận QC (QcInspection/Conclude) – chưa ghi sổ được";
            public static readonly string QcInspectionCrudBlocked = "Phiếu QC đầu vào chỉ tạo cùng phiếu nhập và ghi kết quả qua QcInspection/Conclude";
            public static readonly string QcResultInvalid = "{0}: kết quả QC phải là DAT / DAT_MOT_PHAN / KHONG_DAT";
            public static readonly string QcLineMissing = "Chưa có kết quả QC cho dòng: {0}";
            public static readonly string QcPartialQuantityInvalid = "{0}: DAT_MOT_PHAN cần số lượng đạt > 0 và < số nhận ({1})";
            public static readonly string QcRemarkRequired = "{0}: kết quả {1} bắt buộc ghi nhận xét / lý do";
            public static readonly string QcExpiredLot = "{0}: hạn dùng {1} trước ngày nhận – chỉ kết luận KHONG_DAT";
            public static readonly string QcTemperatureRemarkRequired = "{0}: nhiệt độ nhận {1} °C vượt ngưỡng {2} °C – kết luận KHONG_DAT hoặc ghi lý do chấp nhận / hành động khắc phục";
            public static readonly string ReceivingTempOutOfRange = "Cảnh báo: {0} nhiệt độ nhận {1} °C vượt ngưỡng {2} °C";
            // T17 – sản xuất theo mẻ (P7)
            public static readonly string PlanInsertBlocked = "Kế hoạch sản xuất được tạo bằng MaterialRequirement/Calculate (KH- theo ngày sản xuất)";
            public static readonly string PlanUpdateBlocked = "Trạng thái / số / ngày kế hoạch chỉ đổi qua thao tác nghiệp vụ (Approve / Release) – dòng món sửa qua ProductionPlanLine khi kế hoạch còn Nháp";
            public static readonly string PlanNeedsLine = "Kế hoạch chưa có dòng món có số phần lớn hơn 0";
            public static readonly string PlanLineDraftOnly = "Chỉ sửa dòng kế hoạch khi kế hoạch ở trạng thái Nháp";
            public static readonly string PlanLineDuplicateDish = "Món này đã có dòng trong kế hoạch";
            public static readonly string ShiftNotFound = "Không tìm thấy ca sản xuất (ca giao) – chọn ca hoặc cấu hình BusinessRules:ProductionDefaultShiftCode";
            public static readonly string ProductionLineNotFound = "Không tìm thấy dây chuyền sản xuất đang hoạt động";
            public static readonly string DispatchTimeInvalid = "Giờ xuất bếp phải dạng HH:mm";
            public static readonly string DispatchTimeMissing = "Không xác định được giờ xuất bếp: điểm giao chưa có giờ giao, ca chưa có giờ bắt đầu – nhập giờ xuất bếp (DispatchTime)";
            public static readonly string ReleaseNoRecipe = "Món chưa có công thức đã khóa / đã duyệt – không phát hành được: {0}";
            public static readonly string ReleaseRecipeNotLocked = "Cảnh báo: món {0} dùng công thức Đã duyệt chưa khóa – lệnh SX lưu ảnh chụp định lượng hiện tại";
            public static readonly string ReleaseStartInPast = "Cảnh báo: mẻ {0} có giờ bắt đầu kế hoạch {1} đã qua";
            public static readonly string ProductionCrudBlocked = "{0} chỉ tạo / đổi trạng thái qua thao tác nghiệp vụ ({1})";
            public static readonly string BatchNotFound = "Không tìm thấy mẻ nấu";
            public static readonly string BatchIssueExists = "Mẻ {0} đã có phiếu xuất {1} ({2}) – xuất thêm qua MaterialIssue/Create (ghi lệnh SX) hoặc hủy phiếu cũ";
            public static readonly string BatchIssueNoMaterial = "Lệnh SX không có ảnh chụp định lượng – không tính được nguyên liệu của mẻ";
            public static readonly string BatchIssueUnitMissing = "Nguyên liệu {0} thiếu quy đổi đơn vị {1} → {2} (đơn vị tồn kho)";
            public static readonly string BatchNotIssuedWarning = "Cảnh báo: mẻ {0} chưa có phiếu xuất nguyên liệu đã ghi sổ";
            public static readonly string CoreTempCorrectiveRequired = "Nhiệt độ lõi {0} °C dưới ngưỡng {1} °C – phải ghi hành động khắc phục (nấu tiếp / hâm lại rồi đo lại)";
            public static readonly string CoreTempBelowWarning = "Cảnh báo: nhiệt độ lõi {0} °C dưới ngưỡng {1} °C – mẻ bị chặn đạt QC / đóng gói tới khi đo lại đạt";
            public static readonly string CoreTempMissing = "Chưa đo nhiệt độ lõi (TemperatureLog CORE)";
            public static readonly string CoreTempBelow = "Nhiệt độ lõi đo gần nhất {0} °C dưới ngưỡng {1} °C – nấu tiếp / hâm lại và đo lại";
            public static readonly string SampleMissing = "Chưa lưu mẫu thức ăn (SAMPLE_RETAINED)";
            public static readonly string SampleAlready = "Mẻ đã ghi lưu mẫu lúc {0}";
            public static readonly string SampleChecklistMissing = "Chưa có mục checklist SAMPLE_RETAINED – chạy DB/seed/FoodSafetyChecklist_seed.sql";
            public static readonly string OutputMissing = "Chưa ghi sản lượng mẻ (ProductionBatch/RecordOutput)";
            public static readonly string OutputQuantityInvalid = "Số phần nấu được phải lớn hơn 0; số loại từ 0 tới số nấu được";
            public static readonly string BatchCannotRelease = "Mẻ {0} chưa đạt điều kiện ATTP: {1}";
            public static readonly string BatchCannotPack = "Mẻ {0} chưa được đóng gói: {1}";
            public static readonly string BatchReasonRequired = "Phải ghi lý do (nhận xét QC)";
            public static readonly string WasteQuantityRequired = "Nhập số suất hoặc số kg hao hụt (lớn hơn 0)";
            public static readonly string WasteTypeInvalid = "Loại hao hụt không hợp lệ ({0})";
            public static readonly string WasteBatchViaAction = "Hao hụt gắn mẻ ghi qua ProductionBatch/RecordWaste (cập nhật số hao hụt của mẻ)";
            public static readonly string WorkstationNotFound = "Không tìm thấy trạm bếp";
        }

        public class TypeSave
        {
            public const string Insert = "Insert";
            public const string Update = "Update";
            public const string Delete = "Delete";
            public const string UpdateProfile = "UpdateProfile";
            public const string ResetPassword = "ResetPassword";
            public const string ChangePassword = "ChangePassword";
            public const string UpdateResult = "UpdateResult";
        }

        public class FolderName
        {
            public const string PERSON_IMAGE = "PERSON_IMAGE";
            public const string TEST_DEMO_UPLOAD = "TEST_DEMO_UPLOAD";
        }

        public class ExcelStyle
        {
            public static readonly string BackgroundColor = "#16548d";
            public static readonly string FontColor = "#fff";
            public static readonly string FontName = "Calibri";
        }
    }
}
