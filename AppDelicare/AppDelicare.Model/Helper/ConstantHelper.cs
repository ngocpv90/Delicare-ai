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
