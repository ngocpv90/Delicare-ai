namespace AppDelicare.Model.Option
{
    /// <summary>
    /// RETAIL-1 (docs/frontend-handoff/RETAIL-1.md mục 3) – <c>BusinessRules:Retail</c>. Giá trị đọc từ cấu hình, không hard-code trong logic.
    /// PROPOSED – BA/PO CONFIRM.
    /// </summary>
    public sealed class RetailRulesOptions
    {
        /// <summary>Giờ chốt: đặt trước giờ này → nhận sớm nhất ngày mai; sau → ngày kia.</summary>
        public int OrderCutoffHour { get; set; } = 20;
        /// <summary>Đặt trước tối đa bao nhiêu ngày.</summary>
        public int MaxDaysAhead { get; set; } = 14;
        public int MaxLines { get; set; } = 20;
        public int MaxQuantityPerLine { get; set; } = 999;
        /// <summary>Chống spam đơn WEB: số đơn tối đa / SĐT / cửa sổ.</summary>
        public int SubmitLimitPerPhone { get; set; } = 3;
        /// <summary>Chống spam đơn WEB: số đơn tối đa / IP / cửa sổ.</summary>
        public int SubmitLimitPerIp { get; set; } = 10;
        /// <summary>Tra cứu đơn: số lần tối đa / IP / cửa sổ.</summary>
        public int TrackLimitPerIp { get; set; } = 20;
        public int LimitWindowMinutes { get; set; } = 60;
        /// <summary>Gửi lại cùng ClientRequestId trong khoảng này → trả lại đơn cũ.</summary>
        public int DuplicateRequestHours { get; set; } = 24;
        /// <summary>Báo đơn WEB mới qua bot cho người có quyền màn "Đơn khách lẻ".</summary>
        public bool NotifyNewOrder { get; set; } = true;
        /// <summary>RETAIL-2: thời hạn phiên đăng nhập của khách lẻ (ngày).</summary>
        public int SessionDays { get; set; } = 30;
        public int PasswordMinLength { get; set; } = 6;
        /// <summary>RETAIL-2: đăng nhập sai bao nhiêu lần (theo SĐT) thì khóa tạm.</summary>
        public int LoginFailLimit { get; set; } = 5;
        public int LoginLockMinutes { get; set; } = 15;
        /// <summary>RETAIL-2: số tài khoản tạo tối đa / IP / cửa sổ LimitWindowMinutes.</summary>
        public int RegisterLimitPerIp { get; set; } = 5;
    }
}
