using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using ClosedXML.Excel;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-2 (D-131 e): tải Excel theo mẫu "Thêm bằng Excel" của AMIS – mỗi loại chứng từ 1 sheet, mỗi dòng hàng 1 dòng Excel (đầu chứng từ lặp lại).
    /// Tiêu đề cột theo cấu hình <c>Integration:Misa:ExcelColumns</c>[loại] = ["Tiêu đề cột:trường", …] (trường: date, number, desc, objectCode, objectName,
    /// code, name, unit, qty, price, amount, vat, target, note); trống = bộ cột mặc định. Khi anh đặt mẫu trống tải từ AMIS vào Delicare-Share\MISA\ thì khai đúng tiêu đề.
    /// Chỉ chứng từ Sẵn sàng / Chờ lập / Đã sửa được tải; tải xong ghi AuditChange "Exported" (→ Chờ lập trên AMIS).
    /// </summary>
    public partial class AccountingVoucherService
    {
        private static readonly string[] DefaultColumns =
        [
            "Ngày chứng từ:date", "Số chứng từ:number", "Diễn giải:desc", "Mã đối tượng:objectCode", "Tên đối tượng:objectName", "Mã hàng:code", "Tên hàng:name",
            "ĐVT:unit", "Số lượng:qty", "Đơn giá:price", "Thành tiền:amount", "% thuế GTGT:vat", "Đối tượng THCP / Lý do:target", "Ghi chú:note",
        ];

        private string[] ColumnsOf(string type) => _misa.ExcelColumns.TryGetValue(type, out var c) && c.Count > 0 ? [.. c] : DefaultColumns;

        private static object? Field(string field, VoucherModel v, VoucherLineModel? l) => field switch
        {
            "date" => v.Date,
            "number" => v.Number,
            "desc" => $"{v.TypeName} {v.Number}" + (v.ObjectName != null ? " – " + v.ObjectName : string.Empty),
            "objectCode" => v.ObjectCode,
            "objectName" => v.ObjectName,
            "code" => l?.Code,
            "name" => l?.Name,
            "unit" => l?.Unit,
            "qty" => l?.Quantity,
            "price" => l?.UnitPrice,
            "amount" => l?.Amount,
            "vat" => l?.VatPct,
            "target" => l?.Target,
            "note" => l?.Note ?? v.Note,
            _ => null,
        };

        public async Task<IResult<FileContentModel>> ExportExcel(VoucherActionRequest request)
        {
            try
            {
                var (list, _, _, _, m) = await ComputeAsync(request.Month);
                var keys = request.ListKey.ToHashSet();
                var pick = list.Where(v => keys.Contains(v.Key) && v.Type != VoucherType.Timesheet && v.Source != "Nghiệp vụ gốc đã hủy sau khi gửi"
                    && v.Status is VoucherStatus.Ready or VoucherStatus.Waiting or VoucherStatus.Changed or VoucherStatus.Created).ToList();
                if (pick.Count == 0)
                {
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.VoucherNothingToExport);
                }
                using var wb = new XLWorkbook();
                foreach (var g in pick.GroupBy(v => v.Type).OrderBy(g => g.Key))
                {
                    string name = VoucherType.Label(g.Key);
                    var ws = wb.AddWorksheet(name.Length > 31 ? name[..31] : name);
                    var cols = ColumnsOf(g.Key).Select(c => { int i = c.LastIndexOf(':'); return (Head: i > 0 ? c[..i] : c, Field: i > 0 ? c[(i + 1)..] : c); }).ToList();
                    for (int c = 0; c < cols.Count; c++)
                    {
                        ws.Cell(1, c + 1).Value = cols[c].Head;
                    }
                    ws.Row(1).Style.Font.Bold = true;
                    int r = 2;
                    foreach (var v in g)
                    {
                        var lines = v.ListLine.Count > 0 ? v.ListLine.Cast<VoucherLineModel?>().ToList() : [null];
                        foreach (var l in lines)
                        {
                            for (int c = 0; c < cols.Count; c++)
                            {
                                var cell = ws.Cell(r, c + 1);
                                switch (Field(cols[c].Field, v, l))
                                {
                                    case DateTime d: cell.Value = d; cell.Style.DateFormat.Format = "dd/MM/yyyy"; break;
                                    case decimal n: cell.Value = n; break;
                                    case string s: cell.Value = s; break;
                                    default: break;
                                }
                            }
                            r++;
                        }
                    }
                    ws.Columns().AdjustToContents(1, 300);
                }
                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                await WriteLogAsync(pick, m, ActionExported);
                return Result<FileContentModel>.Success(new FileContentModel
                {
                    Content = ms.ToArray(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName = $"Chung-tu-AMIS-{m}-{_clock.Now:yyyyMMdd-HHmm}.xlsx",
                }, string.Format(ConstantHelper.Message.VoucherExported, pick.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportExcel: ", ex.ToString()));
                throw;
            }
        }
    }
}
