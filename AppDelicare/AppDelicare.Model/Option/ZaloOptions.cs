namespace AppDelicare.Model.Option
{
    /// <summary>
    /// AI-19 (D-133 đợt 4, D-06): Zalo OA cho khách hàng. Mục cấu hình <c>Zalo</c> của api. Bí mật CHỈ ở biến môi trường (<c>Zalo__AppSecret</c>, <c>Zalo__RefreshToken</c>) –
    /// không ghi vào appsettings / repo. Token truy cập + token làm mới (Zalo đổi mỗi lần làm mới) lưu MÃ HÓA ở <c>IntegrationChannel.ConfigJson</c> của kênh ZALO_OA.
    /// </summary>
    public sealed class ZaloOptions
    {
        public const string Section = "Zalo";

        /// <summary>Mã bot khách (BotConfiguration.BotCode, kênh ProviderCode ZALO).</summary>
        public string BotCode { get; set; } = "ZALO_KH";

        /// <summary>App id của ứng dụng Zalo (không bí mật).</summary>
        public string? AppId { get; set; }

        /// <summary>Khóa bí mật ứng dụng – đặt bằng biến môi trường Zalo__AppSecret.</summary>
        public string? AppSecret { get; set; }

        /// <summary>Token làm mới KHỞI TẠO (lần đầu sau khi cấp quyền OA) – biến môi trường Zalo__RefreshToken; sau đó dùng token lưu mã hóa trong DB.</summary>
        public string? RefreshToken { get; set; }

        public string TokenUrl { get; set; } = "https://oauth.zaloapp.com/v4/oa/access_token";

        /// <summary>Hạn mã liên kết khách (giờ) – KD gửi mã cho người liên hệ qua email / điện thoại.</summary>
        public int LinkCodeHours { get; set; } = 72;

        /// <summary>Độ dài tối đa 1 tin trả lời (Zalo giới hạn 2.000 ký tự).</summary>
        public int MaxReplyLength { get; set; } = 1900;
    }
}
