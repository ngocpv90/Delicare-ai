namespace AppDelicare.Model.Model
{
    /// <summary>FE-E.2 (D-65): 1 mã QR do máy chủ vẽ (SVG) – Common/GetQrBatch.</summary>
    public class QrCodeModel
    {
        public string Text { get; set; } = string.Empty;
        public string? Svg { get; set; }
        /// <summary>Lỗi của riêng mục này (nội dung rỗng / quá dài) – các mục khác vẫn trả.</summary>
        public string? Error { get; set; }
    }

    public class QrBatchRequest
    {
        /// <summary>Nội dung cần vẽ (≤ 200 mục, mỗi mục ≤ 512 ký tự).</summary>
        public List<string> ListText { get; set; } = [];
    }

    /// <summary>FE-E.2 (FE-BOT-3): QR dán tại hiện trường – link mở bot Vận hành (BOT-2.4 / BOT-1.5a).</summary>
    public class FieldQrModel
    {
        /// <summary>CHAMCONG · LINE · VEH · LOC · CLEAN.</summary>
        public string Group { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string Label { get; set; } = string.Empty;
        /// <summary>Tham số /start (vd. LINE_DC01).</summary>
        public string Payload { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string? Svg { get; set; }
    }
}
