using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-16 (D-133 đợt 3): công cụ đọc cho PKG (hộp khay, vệ sinh) và ACC (chứng từ kế toán, đối chiếu AMIS, giá vốn & lãi gộp). ACC CHỈ ĐỌC số liệu hệ thống / đối chiếu AMIS
    /// (D-131) – không thay MISA (D-23), không xuất Excel AMIS (thao tác ghi lịch sử gửi). Mỗi công cụ gọi đúng method Service của màn web, quyền Xem màn đó.
    /// PLAN dùng lại ke_hoach_ngay / thuc_don_tuan / du_bao_thieu_hut (thêm PLAN vào danh sách agent).
    /// </summary>
    public static partial class AgentToolRegistry
    {
        private static string? Month(AgentToolContext ctx, System.Text.Json.JsonElement a)
            => Str(a, "thang") is { } m ? DateTime.TryParse(m.Length == 7 ? m + "-01" : m, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
                ? d.ToString("yyyy-MM") : throw new AgentToolException("Tháng dạng yyyy-MM") : null;

        private static IEnumerable<AgentToolDef> Wave3Tools() =>
        [
            // ---------------- PKG – hộp khay & vệ sinh
            new AgentToolDef
            {
                Name = "hop_khay", ScreenId = "log-packaging-ledger", Agents = ["VIVI", "PKG", "LOG", "ACC"],
                Description = "Hộp / khay khách đang giữ (như tab Số dư của màn Sổ hộp khay): đã giao, đã trả, hỏng, thất thoát, quá hạn trả, tỷ lệ thất thoát kỳ và vượt ngưỡng. Lọc theo khách / loại / chỉ vượt ngưỡng.",
                InputSchemaJson = Schema(("ma_khach", "string", "Mã khách (trống = mọi khách)"), ("loai_hop", "string", "Loại hộp / khay"), ("chi_vuot_nguong", "boolean", "Chỉ khách vượt ngưỡng thất thoát"),
                    ("tu_ngay", "string", "Đầu kỳ tính tỷ lệ (trống = đầu tháng)"), ("den_ngay", "string", "Cuối kỳ (trống = hôm nay)")),
                RunAsync = async (ctx, a) =>
                {
                    Guid? customer = null;
                    if (Str(a, "ma_khach") is { } code)
                    {
                        customer = await Svc<AppDelicare.Data.Interface.IUnitOfWork>(ctx).DbContext.Customer.AsNoTracking().Where(c => !c.IsDeleted && (c.Code == code || c.CIF == code)).Select(c => (Guid?)c.Id).FirstOrDefaultAsync()
                            ?? throw new AgentToolException($"Không có khách hàng «{code}»");
                    }
                    DateTime to = Date(ctx, a, "den_ngay");
                    var b = Data(await Svc<IPackagingLedgerService>(ctx).GetBalance(new PackagingBalanceRequest
                    {
                        CustomerId = customer, PackagingType = Str(a, "loai_hop"), From = Str(a, "tu_ngay") == null ? new DateTime(to.Year, to.Month, 1) : Date(ctx, a, "tu_ngay"), To = to,
                        OverThresholdOnly = a.ValueKind == System.Text.Json.JsonValueKind.Object && a.TryGetProperty("chi_vuot_nguong", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.True,
                    })) as PackagingBalanceModel;
                    return b == null ? null : new { b.From, b.To, b.TotalOutstanding, b.OverThresholdCount, b.OverdueTotal, ListItem = b.ListItem.OrderByDescending(i => i.Outstanding).Take(MaxRows(ctx)).ToList(), TongDong = b.ListItem.Count };
                },
            },
            new AgentToolDef
            {
                Name = "lo_ve_sinh", ScreenId = "qc-cleaning", Agents = ["PKG", "QA"],
                Description = "Lô vệ sinh hộp / khay của 1 ngày (như màn Vệ sinh hộp / khay): lô đang làm / xong / QC đạt / loại, bước đang dừng, khay sạch sẵn sàng.",
                InputSchemaJson = Schema(("ngay", "string", DateDesc)),
                RunAsync = async (ctx, a) => Data(await Svc<ICleaningBatchService>(ctx).GetDayBoard(Date(ctx, a, "ngay"))),
            },
            // ---------------- ACC – kế toán (chỉ đọc)
            new AgentToolDef
            {
                Name = "chung_tu_ke_toan", ScreenId = "acc-voucher", Agents = ["ACC"],
                Description = "Chứng từ kế toán tháng tính từ giao dịch đã hoàn tất (như màn Chứng từ kế toán): theo loại (bán hàng, nhập mua, xuất kho…), số tiền, trạng thái đã xuất AMIS. Chỉ đọc.",
                InputSchemaJson = Schema(("thang", "string", "Tháng yyyy-MM (trống = tháng này)"), ("loai", "string", "Tab loại chứng từ (trống = tất cả)")),
                RunAsync = async (ctx, a) => Data(await Svc<IAccountingVoucherService>(ctx).GetMonth(Month(ctx, a), Str(a, "loai"))),
            },
            new AgentToolDef
            {
                Name = "doi_chieu_amis", ScreenId = "acc-amis-recon", Agents = ["ACC"],
                Description = "Đối chiếu số liệu DELI-CARE với AMIS / MISA tháng (như màn Đối chiếu AMIS, D-131): khớp / lệch theo nhóm, dòng lệch. Chỉ đọc – không sửa MISA.",
                InputSchemaJson = Schema(("thang", "string", "Tháng yyyy-MM (trống = tháng này)")),
                RunAsync = async (ctx, a) => Data(await Svc<IAmisReconService>(ctx).GetMonth(Month(ctx, a))),
            },
            new AgentToolDef
            {
                Name = "gia_von_lai_gop", ScreenId = "cost-meal", Agents = ["VIVI", "ACC", "PLAN"],
                Description = "Giá vốn & lãi gộp suất ăn tháng (như màn Giá vốn & lãi gộp): doanh thu, giá vốn, lãi gộp, food cost % theo khách / món / ngày.",
                InputSchemaJson = Schema(("thang", "string", "Tháng yyyy-MM (trống = tháng này)"), ("nhom_theo", "string", "customer | dish | day (mặc định customer)")),
                RunAsync = async (ctx, a) => Data(await Svc<IMealMarginService>(ctx).GetMargin(new MealMarginRequest { Month = Month(ctx, a), GroupBy = Str(a, "nhom_theo") ?? "customer" })),
            },
        ];
    }
}
