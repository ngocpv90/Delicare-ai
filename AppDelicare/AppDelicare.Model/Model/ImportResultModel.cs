namespace AppDelicare.Model.Model
{
    /// <summary>T49: kết quả nhập Excel – số dòng thêm / cập nhật / bỏ qua và lỗi từng dòng.</summary>
    public class ImportResultModel
    {
        public string? TableName { get; set; }
        /// <summary>Số dòng dữ liệu đã đọc (không tính dòng trống).</summary>
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        /// <summary>Dòng hợp lệ nhưng không đổi gì (đã có, cùng dữ liệu) hoặc không áp dụng.</summary>
        public int Skipped { get; set; }
        public List<ImportRowError> ListError { get; set; } = [];
        /// <summary>Ghi chú thêm (vd. phần của file thật chưa nạp được và lý do).</summary>
        public List<string> ListNote { get; set; } = [];
    }

    public class ImportRowError
    {
        /// <summary>Số dòng trong Excel (bắt đầu từ 1, gồm dòng tiêu đề).</summary>
        public int Row { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>T49: mô tả một loại file nhập (mẫu tự sinh hoặc file vận hành thật).</summary>
    public class ImportTemplateModel
    {
        public string TableName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>Màn hình cần quyền Import (MetaLink #/screen-id).</summary>
        public string Screen { get; set; } = string.Empty;
        /// <summary>true = có file mẫu tải về (GetTemplate); false = đọc file vận hành thật (sheet cố định).</summary>
        public bool HasTemplate { get; set; }
        public string? SheetName { get; set; }
        public List<ImportColumnModel> ListColumn { get; set; } = [];
    }

    public class ImportColumnModel
    {
        public string Header { get; set; } = string.Empty;
        public bool Required { get; set; }
        public string? Note { get; set; }
    }
}
