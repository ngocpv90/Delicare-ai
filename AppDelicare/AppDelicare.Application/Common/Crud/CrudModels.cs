using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;

namespace AppDelicare.Application.Common.Crud
{
    /// <summary>Một cột trong DDL (sinh bởi CodeGen).</summary>
    /// <param name="Column">Tên cột SQL.</param>
    /// <param name="Property">Tên property C#.</param>
    /// <param name="SqlType">Kiểu SQL (nvarchar, uniqueidentifier, decimal…).</param>
    /// <param name="MaxLength">Độ dài tối đa (chuỗi), null = max.</param>
    /// <param name="Required">NOT NULL, không DEFAULT, không phải cột audit.</param>
    /// <param name="HasDefault">Có DEFAULT trong DDL.</param>
    /// <param name="IsBase">Cột của BaseEntity (Id, Created*, Modified*, Deleted*, IsDeleted).</param>
    /// <param name="IsDeprecated">Cột cũ – không trả / không nhận qua API, ghi song song từ <paramref name="ReplacedBy"/>.</param>
    /// <param name="RefTable">Bảng đích theo LOGICAL_RELATIONS (null = không xác định).</param>
    /// <param name="ReplacedBy">Property thay thế của cột deprecated.</param>
    public sealed record ColumnInfo(string Column, string Property, string SqlType, int? MaxLength, bool Required, bool HasDefault,
        bool IsBase, bool IsDeprecated, string? RefTable, string? ReplacedBy = null)
    {
        public string Field => char.ToLowerInvariant(Property[0]) + Property[1..];
        public bool IsString => SqlType is "nvarchar" or "varchar" or "nchar" or "char" or "sysname";
        public bool IsDate => SqlType is "date" or "datetime" or "datetime2" or "smalldatetime";
        public static readonly IReadOnlySet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PassWord", "Password", "PasswordHash", "IsDeleted", "DeletedDate", "DeletedBy", "DeleteddBy",
        };
    }

    public sealed record TableInfo(string Table, string Class, string? LabelColumn, IReadOnlyList<string> SearchProperties,
        IReadOnlyList<string[]> Uniques, IReadOnlyList<ColumnInfo> Columns)
    {
        public ColumnInfo? FindByField(string field) => Columns.FirstOrDefault(c => c.Field.Equals(field, StringComparison.OrdinalIgnoreCase));
        public ColumnInfo? Find(string property) => Columns.FirstOrDefault(c => c.Property.Equals(property, StringComparison.OrdinalIgnoreCase));

        /// <summary>Cột trả ra API: không deprecated, không ẩn (mật khẩu, cột xóa mềm).</summary>
        public IEnumerable<ColumnInfo> Visible => Columns.Where(c => !c.IsDeprecated && !ColumnInfo.Hidden.Contains(c.Property));

        /// <summary>Cột client được ghi: cột nghiệp vụ hiển thị (không audit, không Id).</summary>
        public IEnumerable<ColumnInfo> Writable => Visible.Where(c => !c.IsBase);

        public bool Has(string property) => Find(property) != null;

        /// <summary>Bắt buộc: NOT NULL không DEFAULT, hoặc là cột thay thế của một cột deprecated NOT NULL (vd. FromUnitId thay FromUomId).</summary>
        public bool IsRequired(ColumnInfo c) => c.Required || Columns.Any(o => o.IsDeprecated && o.Required && o.ReplacedBy == c.Property);
    }

    /// <summary>Ngữ cảnh cho luật kiểm tra riêng của từng resource.</summary>
    /// <param name="Values">Giá trị sau khi ghép (cũ + mới), key = property C#.</param>
    /// <param name="Id">Id bản ghi đang sửa (null khi tạo mới).</param>
    public sealed record CrudRuleContext(IReadOnlyDictionary<string, object?> Values, Guid? Id, DatabaseContext Db, CancellationToken CancellationToken)
    {
        public T? Get<T>(string property) => Values.TryGetValue(property, out var v) && v is T t ? t : default;
        public decimal? Dec(string property) => Values.TryGetValue(property, out var v) && v != null ? Convert.ToDecimal(v) : null;
    }

    /// <summary>Ngữ cảnh thêm trường tính toán.</summary>
    public sealed record CrudEnrichContext(IReadOnlyList<IDictionary<string, object?>> Items, DatabaseContext Db, CancellationToken CancellationToken)
    {
        public List<Guid> Ids => Items.Select(i => (Guid)i["id"]!).ToList();
    }

    /// <summary>
    /// Một resource v1 (một màn hình mockup): <c>/api/v1/{Module}/{Path}</c> trên bảng <see cref="Table"/>,
    /// quyền theo <see cref="Screen"/> (MetaLink <c>#/&lt;screen&gt;</c>).
    /// </summary>
    public sealed record ResourceDefinition(string Screen, string Module, string Path, string Table)
    {
        public bool CanCreate { get; init; } = true;
        public bool CanUpdate { get; init; } = true;
        public bool CanDelete { get; init; } = true;
        /// <summary>Luật kiểm tra riêng theo spec; ném <see cref="BusinessRuleException"/>.</summary>
        public Func<CrudRuleContext, Task>? Rules { get; init; }
        /// <summary>Thêm trường tính toán vào item (danh sách và chi tiết), vd. availableQuantity.</summary>
        public Func<CrudEnrichContext, Task>? Enrich { get; init; }
        /// <summary>Chạy sau khi lưu (cùng request), vd. tắt dòng ưu tiên cũ.</summary>
        public Func<CrudRuleContext, Task>? AfterSave { get; init; }
        /// <summary>Bộ lọc riêng theo key query (key này không đi qua lọc theo cột).</summary>
        public IReadOnlyDictionary<string, Func<IQueryable, string, IQueryable>> CustomFilters { get; init; } = new Dictionary<string, Func<IQueryable, string, IQueryable>>();
        public string Route => $"api/v1/{Module}/{Path}";
        public bool ReadOnly => !CanCreate && !CanUpdate && !CanDelete;
    }

    /// <summary>
    /// T45B.1 – resource CRUD chuẩn của màn hình P0–P2 (catalog.js × DELICARE_API_Specification.md mục 3–5).
    /// Màn có logic riêng (User, ma trận quyền, merge-conflict…) làm ở T45B.2–4.
    /// </summary>
    public static class ResourceRegistry
    {
        private static BusinessRuleException Invalid(string field, string message) => new(400, "VALIDATION_FAILED", message, field);
        private static BusinessRuleException Rule(string field, string message) => new(422, "BUSINESS_RULE", message, field);

        public static readonly IReadOnlyList<ResourceDefinition> All =
        [
            // ---- P0 – admin ------------------------------------------------------------------------
            new("adm-company", "admin", "companies", "Company"),
            new("adm-orgunit", "admin", "org-units", "OrganizationUnit") { Rules = OrgUnitNoCycle },
            new("adm-employee", "admin", "employees", "Employee"),
            new("adm-position", "admin", "positions", "Position"),
            new("adm-admin-unit", "admin", "administrative-provinces", "AdministrativeProvince"),
            new("adm-admin-unit", "admin", "administrative-districts", "AdministrativeDistrict"),
            new("adm-admin-unit", "admin", "administrative-wards", "AdministrativeWard"),
            new("adm-admin-unit", "admin", "administrative-units", "AdministrativeUnit"),
            new("adm-admin-unit", "admin", "administrative-regions", "AdministrativeRegion"),
            new("adm-document", "admin", "document-files", "DocumentFile") { CanUpdate = false },
            new("adm-usergroup", "admin", "user-groups", "UserGroup"),
            new("adm-objectmenu", "admin", "object-menus", "ObjectMenu") { Rules = ObjectMenuUniqueLink },
            new("adm-person", "admin", "persons", "Person") { CanCreate = false, CanDelete = false },
            new("adm-audit", "admin", "audit-changes", "AuditChange") { CanCreate = false, CanUpdate = false, CanDelete = false },
            new("adm-access-log", "admin", "access-logs", "LocationTracking") { CanCreate = false, CanUpdate = false, CanDelete = false },

            // ---- P1 – master data ------------------------------------------------------------------
            new("crm-customer", "sales", "customers", "Customer") { Enrich = MasterData.P1Hooks.CustomerEnrich, Rules = MasterData.P1Hooks.CustomerRules },
            new("crm-contact", "sales", "customer-contacts", "CustomerContact"),
            new("crm-location", "sales", "delivery-locations", "CustomerDeliveryLocation") { Rules = async c => { await DeliveryLocationRules(c); await MasterData.P1Hooks.DeliveryLocationInactiveGuard(c); } },
            new("scm-supplier", "scm", "suppliers", "Supplier"),
            new("scm-ingredient", "scm", "ingredients", "Ingredient") { Rules = IngredientRules, Enrich = MasterData.P1Hooks.IngredientEnrich },
            new("scm-ingredient-cat", "scm", "ingredient-categories", "IngredientCategorie"),
            new("scm-supplier-material", "scm", "supplier-materials", "SupplierMaterial")
            {
                AfterSave = MasterData.P1Hooks.SupplierMaterialPreferred,
                CustomFilters = new Dictionary<string, Func<IQueryable, string, IQueryable>> { ["effectiveOn"] = MasterData.P1Hooks.SupplierMaterialEffectiveOn },
            },
            new("scm-uom", "scm", "units", "UnitMeasure"),
            new("scm-uom-conversion", "scm", "uom-conversions", "UomConversion") { Rules = UomConversionRules },
            new("scm-warehouse", "scm", "warehouses", "Warehouse"),
            new("scm-zone", "scm", "storage-zones", "StorageZone"),
            new("scm-location", "scm", "storage-locations", "StorageLocation"),
            new("scm-inv-policy", "scm", "inventory-policies", "InventoryPolicie") { Rules = InventoryPolicyRules },
            new("log-vehicle", "logistics", "vehicles", "Vehicle"),

            // ---- P2 – món, công thức, chính sách ---------------------------------------------------
            new("menu-dish-cat", "menu", "dish-categories", "DishCategorie"),
            new("menu-dish", "menu", "dishes", "Dishe"),
            new("menu-recipe", "menu", "recipes", "Recipe") { CanCreate = false, CanUpdate = false, CanDelete = false },
            new("menu-recipe-line", "menu", "recipe-lines", "RecipeLine") { CanCreate = false, CanUpdate = false, CanDelete = false },
            new("cost-standard", "production", "standard-costs", "StandardCost") { CanCreate = false, CanUpdate = false, CanDelete = false },
            new("crm-price-policy", "sales", "price-policies", "PricePolicie") { Rules = PricePolicyRules },
            new("qc-checklist", "qc", "checklists", "FoodSafetyChecklist"),
            new("qc-checklist-item", "qc", "checklist-items", "FoodSafetyChecklistItem"),
            new("prod-line", "production", "lines", "ProductionLine"),
            new("prod-workstation", "production", "workstations", "KitchenWorkstation"),
        ];

        public static ResourceDefinition? Find(string module, string path)
            => All.FirstOrDefault(r => r.Module.Equals(module, StringComparison.OrdinalIgnoreCase) && r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        // ---- Luật riêng theo spec (PROPOSED nếu spec ghi đề xuất) ---------------------------------------

        /// <summary>Spec 3.5: chặn vòng lặp cha – con khi đổi ParentId.</summary>
        private static async Task OrgUnitNoCycle(CrudRuleContext c)
        {
            Guid? parent = c.Get<Guid?>("ParentId") ?? (c.Values.TryGetValue("ParentId", out var p) && p is Guid g ? g : null);
            if (parent == null || c.Id == null)
            {
                return;
            }
            var seen = new HashSet<Guid>();
            Guid? cursor = parent;
            while (cursor != null)
            {
                if (cursor == c.Id)
                {
                    throw Rule("parentId", "Đơn vị cha không được là chính nó hoặc đơn vị con của nó.");
                }
                if (!seen.Add(cursor.Value))
                {
                    break;
                }
                Guid current = cursor.Value;
                cursor = await Task.FromResult(c.Db.OrganizationUnit.Where(o => o.Id == current).Select(o => o.ParentId).FirstOrDefault());
            }
        }

        /// <summary>Spec 3.4: MetaLink duy nhất.</summary>
        private static Task ObjectMenuUniqueLink(CrudRuleContext c)
        {
            string? link = c.Values.TryGetValue("MetaLink", out var v) ? v as string : null;
            if (!string.IsNullOrWhiteSpace(link)
                && c.Db.ObjectMenu.Any(m => !m.IsDeleted && m.MetaLink == link && m.Id != c.Id))
            {
                throw new BusinessRuleException(409, "DUPLICATE", $"MetaLink '{link}' đã tồn tại.", "metaLink");
            }
            return Task.CompletedTask;
        }

        /// <summary>Spec 4.2: khung giờ giao hợp lệ, tọa độ trong phạm vi.</summary>
        private static Task DeliveryLocationRules(CrudRuleContext c)
        {
            if (c.Values.TryGetValue("DeliveryWindowFrom", out var f) && f is TimeSpan from
                && c.Values.TryGetValue("DeliveryWindowTo", out var t) && t is TimeSpan to && from >= to)
            {
                throw Rule("deliveryWindowTo", "Giờ giao từ phải nhỏ hơn giờ giao đến.");
            }
            if (c.Dec("Latitude") is { } lat && (lat < -90 || lat > 90))
            {
                throw Invalid("latitude", "Vĩ độ phải trong [-90, 90].");
            }
            if (c.Dec("Longitude") is { } lng && (lng < -180 || lng > 180))
            {
                throw Invalid("longitude", "Kinh độ phải trong [-180, 180].");
            }
            return Task.CompletedTask;
        }

        /// <summary>Spec 4.4: YieldPercent ∈ (0, 100]; StandardWastePercent ∈ [0, 100); ShelfLifeDays &gt; 0 khi quản lý hạn.</summary>
        private static Task IngredientRules(CrudRuleContext c)
        {
            if (c.Dec("YieldPercent") is { } y && (y <= 0 || y > 100))
            {
                throw Invalid("yieldPercent", "Tỷ lệ thu hồi phải trong (0, 100].");
            }
            if (c.Dec("StandardWastePercent") is { } w && (w < 0 || w >= 100))
            {
                throw Invalid("standardWastePercent", "Tỷ lệ hao hụt chuẩn phải trong [0, 100).");
            }
            if (c.Values.TryGetValue("IsExpiryManaged", out var m) && m is true && !(c.Dec("ShelfLifeDays") > 0))
            {
                throw Rule("shelfLifeDays", "Nguyên liệu quản lý hạn dùng phải có số ngày hạn dùng > 0.");
            }
            return Task.CompletedTask;
        }

        /// <summary>Spec 4.5: ConversionFactor &gt; 0, FromUnitId ≠ ToUnitId, không trùng cặp đang hiệu lực.</summary>
        private static Task UomConversionRules(CrudRuleContext c)
        {
            if (c.Dec("ConversionFactor") is { } k && k <= 0)
            {
                throw Invalid("conversionFactor", "Hệ số quy đổi phải > 0.");
            }
            Guid? from = c.Values.TryGetValue("FromUnitId", out var f) ? f as Guid? : null;
            Guid? to = c.Values.TryGetValue("ToUnitId", out var t) ? t as Guid? : null;
            if (from != null && from == to)
            {
                throw Rule("toUnitId", "Đơn vị nguồn và đích phải khác nhau.");
            }
            if (from != null && to != null && c.Db.UomConversion.Any(x => !x.IsDeleted && x.FromUnitId == from && x.ToUnitId == to && x.Id != c.Id))
            {
                throw new BusinessRuleException(409, "DUPLICATE", "Cặp quy đổi đơn vị này đã tồn tại.", "toUnitId");
            }
            return Task.CompletedTask;
        }

        /// <summary>Spec 4.6: MinimumStock ≤ SafetyStock ≤ ReorderPoint ≤ MaximumStock; một dòng cho (IngredientId, WarehouseId).</summary>
        private static Task InventoryPolicyRules(CrudRuleContext c)
        {
            var chain = new[] { ("MinimumStock", "minimumStock"), ("SafetyStock", "safetyStock"), ("ReorderPoint", "reorderPoint"), ("MaximumStock", "maximumStock") }
                .Select(x => (x.Item2, Value: c.Dec(x.Item1))).Where(x => x.Value != null).ToList();
            for (int i = 1; i < chain.Count; i++)
            {
                if (chain[i - 1].Value > chain[i].Value)
                {
                    throw Rule(chain[i].Item1, "Phải thỏa MinimumStock ≤ SafetyStock ≤ ReorderPoint ≤ MaximumStock.");
                }
            }
            Guid? ingredient = c.Values.TryGetValue("IngredientId", out var ig) ? ig as Guid? : null;
            Guid? warehouse = c.Values.TryGetValue("WarehouseId", out var wh) ? wh as Guid? : null;
            if (ingredient != null && c.Db.InventoryPolicy.Any(x => !x.IsDeleted && x.IngredientId == ingredient && x.WarehouseId == warehouse && x.Id != c.Id))
            {
                throw new BusinessRuleException(409, "DUPLICATE", "Đã có chính sách tồn cho nguyên liệu này tại kho này.", "ingredientId");
            }
            return Task.CompletedTask;
        }

        /// <summary>Spec 5.4: UnitPrice &gt; 0.</summary>
        private static Task PricePolicyRules(CrudRuleContext c)
        {
            if (c.Dec("UnitPrice") is { } price && price <= 0)
            {
                throw Invalid("unitPrice", "Đơn giá phải > 0.");
            }
            return Task.CompletedTask;
        }
    }
}
