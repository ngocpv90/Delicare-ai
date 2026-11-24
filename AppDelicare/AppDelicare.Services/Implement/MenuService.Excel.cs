using System.Globalization;
using System.Text;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.11 (D-89 b – cách tay "tải Excel"): mẫu sheet "1. Menu" do hệ thống xuất (Buổi · Lựa chọn · Nhóm món · T2…CN, mỗi ô một tên món) → tải lên:
    /// khớp tên món với danh mục (không phân biệt dấu / hoa thường), món không khớp báo đỏ + gợi ý món gần giống; xem trước rồi mới ghi (chỉ thực đơn Nháp).
    /// </summary>
    public partial class MenuService
    {
        private const string MenuSheet = "1. Menu";

        public static string NormName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }
            var sb = new StringBuilder();
            foreach (char c in s.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(char.IsWhiteSpace(c) ? ' ' : c);
                }
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>Mẫu Excel của tuần (điền sẵn món đang có).</summary>
        public async Task<IResult<byte[]>> ExportWeekTemplate(MenuWeekRequest request)
        {
            try
            {
                var w = await BuildWeekAsync(OrderService.MondayOf(request.WeekStart ?? _clock.Today.AddDays(7)));
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add(MenuSheet);
                string[] head = ["Buổi", "Lựa chọn", "Nhóm món", "T2", "T3", "T4", "T5", "T6", "T7", "CN"];
                for (int i = 0; i < head.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = i >= 3 ? $"{head[i]} {w.ListDate[i - 3]:dd/MM}" : head[i];
                }
                ws.Range(1, 1, 1, head.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#91AF42")).Font.SetFontColor(XLColor.White);
                int r = 2;
                foreach (var meal in w.ListMeal)
                {
                    foreach (var opt in meal.ListOption)
                    {
                        foreach (var row in opt.ListRow)
                        {
                            ws.Cell(r, 1).Value = meal.MealType;
                            ws.Cell(r, 2).Value = opt.OptionNo;
                            ws.Cell(r, 3).Value = row.CategoryName;
                            for (int d = 0; d < 7; d++)
                            {
                                ws.Cell(r, 4 + d).Value = row.ListCell[d].DishName ?? string.Empty;
                            }
                            r++;
                        }
                    }
                }
                ws.Cell(r + 1, 1).Value = "Buổi: SANG / TRUA / TOI / DEM · Lựa chọn: 1, 2, 3… · ghi đúng tên món trong danh mục (không phân biệt dấu); ô trống = không đổi.";
                ws.Columns(1, 3).Width = 14;
                ws.Columns(4, 10).Width = 24;
                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return Result<byte[]>.Success(stream.ToArray(), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportWeekTemplate: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Đọc mẫu: <paramref name="apply"/> = false → chỉ xem trước khớp món; true → ghi các ô đã khớp (và ô có <paramref name="picks"/> chọn tay) vào thực đơn Nháp.</summary>
        public async Task<IResult<MenuImportModel>> ImportWeek(Guid menuId, Stream file, bool apply, IReadOnlyDictionary<string, Guid>? picks = null)
        {
            try
            {
                var menu = await _unitOfWork.DbContext.Menu.AsNoTracking().FirstOrDefaultAsync(m => m.Id == menuId && !m.IsDeleted);
                if (menu == null)
                {
                    return Result<MenuImportModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                if (apply && menu.Status != MenuStatus.Draft)
                {
                    return Result<MenuImportModel>.Fail(ConstantHelper.Message.MenuWeekNotDraft);
                }
                XLWorkbook wb;
                try
                {
                    wb = new XLWorkbook(file);
                }
                catch (Exception)
                {
                    return Result<MenuImportModel>.Fail(ConstantHelper.Message.MenuImportInvalid);
                }
                using (wb)
                {
                    var ws = wb.Worksheets.FirstOrDefault(s => s.Name == MenuSheet) ?? wb.Worksheets.FirstOrDefault();
                    if (ws == null || NormName(ws.Cell(1, 1).GetString()) != "buoi")
                    {
                        return Result<MenuImportModel>.Fail(ConstantHelper.Message.MenuImportInvalid);
                    }
                    var dishes = await _unitOfWork.DbContext.Dish.AsNoTracking().Where(d => !d.IsDeleted && (d.Status == null || d.Status == "Active"))
                        .Select(d => new { d.Id, d.Name }).ToListAsync();
                    var byNorm = dishes.GroupBy(d => NormName(d.Name)).ToDictionary(g => g.Key, g => g.First());
                    var model = new MenuImportModel { MenuId = menuId };
                    int last = ws.LastRowUsed()?.RowNumber() ?? 1;
                    for (int r = 2; r <= last; r++)
                    {
                        string meal = ws.Cell(r, 1).GetString().Trim().ToUpperInvariant();
                        if (meal is not ("SANG" or "TRUA" or "TOI" or "DEM"))
                        {
                            continue;
                        }
                        int option = int.TryParse(ws.Cell(r, 2).GetString(), out var o) && o > 0 ? o : 1;
                        for (int d = 0; d < 7; d++)
                        {
                            string text = ws.Cell(r, 4 + d).GetString().Trim();
                            if (text.Length == 0)
                            {
                                continue;
                            }
                            string key = $"{r}|{d + 1}";
                            var cell = new MenuImportCellModel { Key = key, Row = r, MealType = meal, OptionNo = option, Day = d + 1, Text = text, Category = ws.Cell(r, 3).GetString().Trim() };
                            string n = NormName(text);
                            if (picks != null && picks.TryGetValue(key, out var picked))
                            {
                                cell.DishId = picked;
                                cell.DishName = dishes.FirstOrDefault(x => x.Id == picked)?.Name;
                            }
                            else if (byNorm.TryGetValue(n, out var hit))
                            {
                                cell.DishId = hit.Id;
                                cell.DishName = hit.Name;
                            }
                            else
                            {
                                // Gợi ý gần giống: chứa nhau trước, rồi nhiều từ chung nhất.
                                var words = n.Split(' ').ToHashSet();
                                cell.ListSuggestion = dishes.Select(x => new { x, nn = NormName(x.Name) })
                                    .Select(x => new { x.x, score = (x.nn.Contains(n) || n.Contains(x.nn) ? 100 : 0) + x.nn.Split(' ').Count(words.Contains) })
                                    .Where(x => x.score > 0).OrderByDescending(x => x.score).ThenBy(x => x.x.Name).Take(3)
                                    .Select(x => new MenuDishSuggestionModel { DishId = x.x.Id, DishName = x.x.Name }).ToList();
                            }
                            model.ListCell.Add(cell);
                        }
                    }
                    model.Matched = model.ListCell.Count(c => c.DishId != null);
                    model.Unmatched = model.ListCell.Count - model.Matched;
                    if (apply)
                    {
                        foreach (var c in model.ListCell.Where(c => c.DishId != null))
                        {
                            var cat = await _unitOfWork.DbContext.Dish.AsNoTracking().Where(x => x.Id == c.DishId).Select(x => x.DishCategoryId ?? x.CategoryId).FirstOrDefaultAsync();
                            await ApplyCellAsync(menuId, c.MealType!, c.OptionNo, cat, c.Day, c.DishId);
                            await _unitOfWork.MenuLineRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                            model.Written++;
                        }
                    }
                    return Result<MenuImportModel>.Success(model, apply ? string.Format(ConstantHelper.Message.MenuImportApplied, model.Written, model.Unmatched) : string.Format(ConstantHelper.Message.MenuImportPreview, model.Matched, model.Unmatched));
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportWeek: ", ex.ToString()));
                throw;
            }
        }
    }
}
