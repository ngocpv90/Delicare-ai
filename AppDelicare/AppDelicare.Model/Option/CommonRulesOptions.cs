namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BE2.7 (spec 2.1 / 2.2 / 2.5): giới hạn của API dùng chung – section <c>BusinessRules:Common</c>. PROPOSED – BA/PO CONFIRM (D-56).
    /// </summary>
    public sealed class CommonRulesOptions
    {
        /// <summary>Xuất Excel / CSV danh sách: tối đa N dòng / lần (nhiều hơn → cắt, kèm dòng ghi chú cuối file).</summary>
        public int ExportMaxRows { get; set; } = 10000;

        /// <summary>In hàng loạt: tối đa N phiếu / lần (spec 2.5: 100).</summary>
        public int PrintBatchMaxItems { get; set; } = 100;

        /// <summary>Lịch sử bản ghi: tối đa N mục (mới nhất trước).</summary>
        public int HistoryMaxItems { get; set; } = 500;
    }
}
