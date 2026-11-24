using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Jobs
{
    // UI-6.11 (D-89 a, b): thứ Hai 06:00 tự lập nháp thực đơn chung tuần sau (nhân bản tuần N − MenuCycleWeeks); 08:00 bot bếp / kế hoạch "đã có bản nháp – N ô cần xem".

    public sealed class MenuAutoDraftJob(IMenuService menus) : IScheduledJob
    {
        public string Code => "MenuAutoDraft";
        public string Name => "Tự lập nháp thực đơn tuần sau";
        public string Description => "06:00 thứ Hai – tạo thực đơn chung tuần sau (Nháp) bằng cách nhân bản tuần cùng vị trí trong chu kỳ 4 tuần; đã có thì bỏ qua.";
        public string DefaultAt => "06:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if (today.DayOfWeek != DayOfWeek.Monday)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải thứ Hai");
            }
            var r = await menus.AutoDraft(today.AddDays(7));
            return new JobOutcome(r.ResponseCode == 200 ? JobRunStatus.Succeeded : JobRunStatus.Failed, r.Data?.Created == true ? 1 : 0, today.AddDays(7), r.ResponseMessage);
        }
    }

    public sealed class MenuDraftNoticeJob(IMenuService menus, IUnitOfWork unitOfWork, INotificationService notifications) : IScheduledJob
    {
        public string Code => "MenuDraftNotice";
        public string Name => "Báo bếp thực đơn tuần sau đã có nháp";
        public string Description => "08:00 thứ Hai – báo bếp / kế hoạch: thực đơn tuần sau đã có bản nháp, số ô cần xem (trống / thiếu công thức / FC vượt / trùng món).";
        public string DefaultAt => "08:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if (today.DayOfWeek != DayOfWeek.Monday)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải thứ Hai");
            }
            DateTime monday = today.AddDays(7);
            var w = (await menus.GetWeek(new MenuWeekRequest { WeekStart = monday })).Data;
            if (w?.MenuId == null)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, monday, "Tuần sau chưa có thực đơn");
            }
            int need = w.EmptyCells + w.MissingRecipe + w.FcOver + w.DuplicateMain;
            string text = $"🍽 Thực đơn tuần {monday:dd/MM} – {monday.AddDays(6):dd/MM} ({w.MenuCode}) đã có bản nháp – {need} ô cần xem"
                + $" ({w.EmptyCells} trống, {w.MissingRecipe} thiếu công thức, {w.FcOver} FC vượt, {w.DuplicateMain} trùng món). Hạn công bố thứ Năm {w.PublishDeadline:dd/MM}.";
            int sent = await BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.ManualText, ["#/menu-menu"], new { text },
                $"MENU_DRAFT:{monday:yyyyMMdd}", requireAdd: true, botCode: MessagingCode.BotDieuHanh);
            return new JobOutcome(JobRunStatus.Succeeded, sent, monday, $"{w.MenuCode}: {need} ô cần xem – {sent} tin");
        }
    }
}
