using GemBox.Document;

namespace AppDelicare.WebApi.Infrastructure
{
    /// <summary>
    /// GemBox chỉ cho <c>ComponentInfo.SetLicense</c> MỘT lần mỗi tiến trình ("The serial key can be set only once").
    /// Host khởi động lại trong cùng tiến trình (test, IIS recycle trong process) phải bỏ qua lần sau.
    /// </summary>
    public static class GemBoxLicense
    {
        private static readonly object Gate = new();
        private static bool _applied;

        public static void Apply(string? configuredLicense)
        {
            lock (Gate)
            {
                if (_applied)
                {
                    return;
                }
                string license = configuredLicense ?? string.Empty;
                if (string.IsNullOrWhiteSpace(license))
                {
                    Serilog.Log.Warning("Chưa cấu hình '{Key}': GemBox.Document chạy chế độ FREE-LIMITED.", StartupSecrets.GemBoxLicense);
                    license = "FREE-LIMITED-KEY";
                }
                ComponentInfo.SetLicense(license);
                ComponentInfo.FreeLimitReached += (sender, e) => e.FreeLimitReachedAction = FreeLimitReachedAction.ContinueAsTrial;
                _applied = true;
            }
        }
    }
}
