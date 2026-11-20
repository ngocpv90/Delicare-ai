using System.Text.RegularExpressions;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// MA-1.7 (D-63 k, BUSINESS_RULES §11.3): bảng phân nhóm MỌI cột mã – nơi duy nhất (cạnh <see cref="DocumentNumberService"/>).
    /// <list type="bullet">
    /// <item><b>G1</b> – máy chủ tự sinh: lấy thẳng từ <c>DocumentNumberService.Kinds</c> (tiền tố + cột đích), giao diện chỉ đọc.</item>
    /// <item><b>G2</b> – mã nghiệp vụ người đặt: chữ hoa không dấu A–Z 0–9 <c>-</c> <c>_</c>, không trùng, không sửa khi đã được dùng.</item>
    /// <item><b>G3</b> – không phải mã định danh: không áp quy tắc.</item>
    /// </list>
    /// Thêm bảng / cột mã (tên kết thúc <c>Code</c> / <c>No</c> / <c>Number</c>) mà chưa xếp nhóm ở đây → test <c>CodeRulesTests</c> đỏ.
    /// </summary>
    public static partial class CodeRules
    {
        public const string G1 = "G1";
        public const string G2 = "G2";
        public const string G3 = "G3";

        /// <summary>G2 – mã người đặt (Entity C#, thuộc tính).</summary>
        private static readonly (Type Entity, string Property, string Note)[] UserCodes =
        [
            (typeof(Company), "Code", "pháp nhân"),
            (typeof(OrganizationUnit), "Code", "đơn vị / chi nhánh"),
            (typeof(Position), "Code", "chức danh"),
            (typeof(Shift), "Code", "ca – hậu tố mã mẻ / vệ sinh (CA1…)"),
            (typeof(UnitMeasure), "Code", "đơn vị tính (KG…)"),
            (typeof(DishCategory), "Code", "nhóm món"),
            (typeof(IngredientCategory), "Code", "nhóm nguyên liệu"),
            (typeof(ProductionLine), "Code", "dây chuyền – QR LINE_"),
            (typeof(KitchenWorkstation), "Code", "trạm bếp"),
            (typeof(StorageZone), "Code", "khu kho"),
            (typeof(StorageLocation), "Code", "vị trí kệ – QR LOC_"),
            (typeof(FoodSafetyChecklist), "Code", "bộ kiểm tra ATTP"),
            (typeof(FoodSafetyChecklistItem), "ItemCode", "mục kiểm tra ATTP"),
            (typeof(SupplierMaterial), "SupplierMaterialCode", "mã hàng của NCC"),
            (typeof(BotConfiguration), "BotCode", "bot"),
            (typeof(IntegrationChannel), "ChannelCode", "kênh tích hợp"),
            (typeof(KpiMetricDefinition), "MetricCode", "chỉ tiêu KPI"),
            (typeof(AdministrativeProvince), "Code", "mã hành chính nhà nước"),
            (typeof(AdministrativeDistrict), "Code", "mã hành chính nhà nước"),
            (typeof(AdministrativeWard), "Code", "mã hành chính nhà nước"),
        ];

        /// <summary>G3 – không phải mã định danh (mã loại / trạng thái / tham chiếu / thứ tự / phiên bản / bảng cũ).</summary>
        private static readonly (Type Entity, string Property)[] NotIdentifiers =
        [
            // Mã loại / trạng thái / tiền tệ / đơn vị.
            (typeof(ApprovalItem), "ActionCode"), (typeof(KpiMetricDefinition), "AggregationCode"), (typeof(ApprovalStep), "ApproverRoleCode"),
            (typeof(FoodSafetyChecklist), "AreaCode"), (typeof(CustomerComplaint), "CategoryCode"), (typeof(PackagingLedger), "ConditionCode"),
            (typeof(ActualCost), "CurrencyCode"), (typeof(PricePolicy), "CurrencyCode"), (typeof(StandardCost), "CurrencyCode"),
            (typeof(ApprovalDecision), "DecisionCode"), (typeof(TemperatureLog), "DeviceCode"), (typeof(IntegrationLog), "DirectionCode"),
            (typeof(KpiMetricDefinition), "DomainCode"), (typeof(IntegrationChannel), "EnvironmentCode"), (typeof(IntegrationLog), "ErrorCode"),
            (typeof(FoodSafetyChecklist), "FrequencyCode"), (typeof(AnalyticsRefreshLog), "JobCode"), (typeof(LeaveRequest), "LeaveTypeCode"),
            (typeof(CoreDataMergeConflict), "MigrationCode"), (typeof(IntegrationLog), "OperationCode"), (typeof(ApprovalRequest), "PriorityCode"),
            (typeof(IntegrationChannel), "ProviderCode"), (typeof(CleaningChecklistResult), "ResultCode"), (typeof(FoodSafetyChecklistResult), "ResultCode"),
            (typeof(FoodSafetyChecklistItem), "UnitCode"), (typeof(KpiMetricDefinition), "UnitCode"),
            // Số giấy tờ của bên ngoài (không do hệ thống cấp).
            (typeof(Customer), "TaxCode"), (typeof(Company), "TaxCode"), (typeof(Supplier), "TaxCode"),
            (typeof(Customer), "BusinessRegistrationNumber"), (typeof(Customer), "IdentityNumber"),
            (typeof(Customer), "PhoneNumber"), (typeof(Person), "PhoneNumber"),
            // Cột sao chép / tham chiếu mã của bảng khác.
            (typeof(DeliveryExecution), "VehicleCode"), (typeof(DeliveryLoading), "VehicleCode"), (typeof(PackagingLedger), "ReferenceNo"),
            (typeof(GoodsReceiptLine), "LotNumber"), (typeof(KpiDailySnapshot), "MetricCode"), (typeof(KpiMonthlySnapshot), "MetricCode"),
            (typeof(DeliveryRoute), "Code"),
            // Số thứ tự / phiên bản.
            (typeof(ApprovalStep), "StepNo"), (typeof(ApprovalRequest), "CurrentStepNo"),
            (typeof(DeliveryStop), "SequenceNo"), (typeof(MenuLine), "SequenceNo"), (typeof(RecipeLine), "SequenceNo"),
            (typeof(Menu), "VersionCode"), (typeof(Recipe), "VersionCode"),
            (typeof(Menu), "VersionNo"), (typeof(Recipe), "VersionNo"), (typeof(FoodSafetyChecklist), "VersionNo"), (typeof(Forecast), "VersionNo"),
            (typeof(KpiMetricDefinition), "VersionNo"),
            // Bảng cũ / ngoài phạm vi (CLAUDE.md 3.1 – không xóa).
            (typeof(UnitsOfMeasure_Legacy_20261113), "Code"), (typeof(ActualCosts_Legacy_20261113), "CurrencyCode"),
            (typeof(Patient), "CouponCode"), (typeof(Patient), "PhoneNumber"), (typeof(TestDemo), "FieldNumber"),
        ];

        /// <summary>Mọi cột mã đã xếp nhóm (Entity, thuộc tính) → quy tắc.</summary>
        public static IReadOnlyList<CodeRuleModel> All { get; } = Build();

        private static List<CodeRuleModel> Build()
        {
            var list = new List<CodeRuleModel>();
            foreach (var kind in DocumentNumberService.Kinds)
            {
                foreach (var (entity, properties) in kind.Targets)
                {
                    foreach (string property in properties)
                    {
                        list.Add(new CodeRuleModel
                        {
                            Entity = entity.Name, Table = TableOf(entity), Column = property, Group = G1, Kind = kind.Kind.ToString(),
                            Prefix = kind.Prefix, Pattern = kind.Pattern, Catalog = kind.Catalog,
                            Note = entity == typeof(Employee) ? "chỉ nhân viên mới; mã = tên đăng nhập" : null,
                        });
                    }
                }
            }
            list.AddRange(UserCodes.Select(x => new CodeRuleModel
            {
                Entity = x.Entity.Name, Table = TableOf(x.Entity), Column = x.Property, Group = G2, Pattern = "A–Z 0–9 - _", Note = x.Note,
            }));
            list.AddRange(NotIdentifiers.Select(x => new CodeRuleModel { Entity = x.Entity.Name, Table = TableOf(x.Entity), Column = x.Property, Group = G3 }));
            return list;
        }

        private static string TableOf(Type entity)
            => entity.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute), false)
                .OfType<System.ComponentModel.DataAnnotations.Schema.TableAttribute>().FirstOrDefault()?.Name ?? entity.Name;

        public static CodeRuleModel? Find(Type entity, string property) => All.FirstOrDefault(x => x.Entity == entity.Name && x.Column == property);

        /// <summary>
        /// G2: chuẩn hóa mã người đặt (bỏ dấu, chữ hoa, bỏ khoảng trắng hai đầu) rồi kiểm chỉ gồm A–Z 0–9 <c>-</c> <c>_</c>.
        /// Trả (mã đã chuẩn hóa, lỗi). Mã trống → lỗi bắt buộc.
        /// </summary>
        public static (string? Code, string? Error) NormalizeUserCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return (null, ConstantHelper.Message.UserCodeRequired);
            }
            string c = CommonHelper.RemoveSign4VietnameseString(code.Trim()).ToUpperInvariant();
            return UserCodePattern().IsMatch(c) ? (c, null) : (null, string.Format(ConstantHelper.Message.UserCodeInvalid, code.Trim()));
        }

        [GeneratedRegex("^[A-Z0-9_-]{1,50}$")]
        private static partial Regex UserCodePattern();
    }
}
