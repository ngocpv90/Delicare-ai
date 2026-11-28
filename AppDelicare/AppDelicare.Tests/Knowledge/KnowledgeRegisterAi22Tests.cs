using System.Text;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Tests.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Knowledge
{
    /// <summary>
    /// AI-22.4 (D-141 i): sổ công văn đến / đi – số vào sổ theo năm VD / VI-YYYY-NNNN đếm từ phiếu mô tả (không trùng kể cả tải đồng thời), giao người xử lý + hạn → việc theo dõi 98;
    /// người được giao thấy và bấm Xong việc của mình dù không có quyền thư mục 98; biên bản họp BH-YYYY-NNN, mỗi việc giao → việc 98; việc sắp / quá hạn được nhắc.
    /// </summary>
    public class KnowledgeRegisterAi22Tests
    {
        private static async Task<Guid> UserAsync(InMemoryApiFactory f, string name, params string[] editFolders)
        {
            Guid u = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = u, UserName = name, IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = name, Code = name.ToUpperInvariant() });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                foreach (string folder in editFolders)
                {
                    string link = "#/knowledge?folder=" + folder.ToLowerInvariant();
                    var m = await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(x => x.MetaLink == link);
                    if (m == null)
                    {
                        m = new ObjectMenu { Id = Guid.NewGuid(), Name = folder, MetaLink = link, LevelShow = 2, IsActive = true };
                        db.ObjectMenu.Add(m);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m.Id, AllowAccess = true, AllowEdit = true, AllowAdd = true, AllowExport = true });
                }
            });
            return u;
        }

        private static async Task<T> As<T>(InMemoryApiFactory f, Guid user, Func<IKnowledgeService, Task<T>> run)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(user));
            return await run(scope.ServiceProvider.GetRequiredService<IKnowledgeService>());
        }

        private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF\n");

        private static Task<Data.Interface.IResult<KnowledgeSaveResultModel>> Dispatch(IKnowledgeService s, string type, string title, Guid? handler = null, string? due = null)
            => s.UploadFile(new KnowledgeUploadRequest { Folder = "05", Type = type, Title = title, SoHieu = "125/CV-SYT", Ngay = "2026-11-28", NoiGuiNhan = "Sở Y tế Hà Nội", HandlerId = handler, HandlerDue = due },
                "cv.pdf", new MemoryStream(Pdf), Pdf.Length);

        [Fact]
        public async Task Dispatch_book_numbers_per_year_assigns_follow_up_and_meeting_tasks_reach_the_assignee()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.Clock.FixedNow = new DateTime(2026, 11, 28, 8, 0, 0);
            f.CreateAuthorizedClient().Dispose();
            Guid clerk = await UserAsync(f, "vt-a224", "05", "15");
            Guid handler = await UserAsync(f, "xl-a224");

            (await As(f, clerk, s => Dispatch(s, "cong-van-den", "Kiểm tra ATTP quý IV", Guid.NewGuid(), "2026-12-05"))).ResponseCode.Should().Be(400, "người xử lý không tồn tại");
            (await As(f, clerk, s => Dispatch(s, "cong-van-den", "Kiểm tra ATTP quý IV", handler))).ResponseCode.Should().Be(400, "giao việc cần hạn");
            var r1 = await As(f, clerk, s => Dispatch(s, "cong-van-den", "Kiểm tra ATTP quý IV", handler, "2026-11-29"));
            r1.ResponseCode.Should().Be(200, r1.ResponseMessage);
            r1.ResponseMessage.Should().StartWith("Đã vào sổ VD-2026-0001 · giao xl-a224");
            (await As(f, clerk, s => Dispatch(s, "cong-van-di", "Trả lời Sở Y tế"))).ResponseMessage.Should().StartWith("Đã vào sổ VI-2026-0001");

            // Không trùng số khi tải đồng thời.
            var parallel = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => As(f, clerk, s => Dispatch(s, "cong-van-den", "Công văn " + i))));
            parallel.Select(p => p.ResponseMessage).Should().OnlyHaveUniqueItems().And.AllSatisfy(m => m.Should().StartWith("Đã vào sổ VD-2026-000"));

            var book = (await As(f, clerk, s => s.GetRegister("05", 2026))).ListData!;
            book.Select(b => b.RegisterNo).Should().BeEquivalentTo(["VD-2026-0001", "VD-2026-0002", "VD-2026-0003", "VD-2026-0004", "VD-2026-0005", "VD-2026-0006", "VI-2026-0001"]);
            var first = book.Single(b => b.RegisterNo == "VD-2026-0001");
            (first.Party, first.Handler, first.Due, first.TaskStatus, first.SoHieu).Should().Be(("Sở Y tế Hà Nội", "xl-a224", new DateTime(2026, 11, 29), "dang-theo-doi", "125/CV-SYT"));
            (await As(f, handler, s => s.GetRegister("05", null))).ResponseCode.Should().Be(403, "người xử lý không tự có quyền sổ công văn");

            // Biên bản họp: việc thiếu hạn → lỗi; đủ → BH-2026-001 + 2 việc theo dõi.
            (await As(f, clerk, s => s.CreateMeeting(new KnowledgeMeetingRequest { Title = "Giao ban tuần 48", Date = "2026-11-28", Tasks = [new() { Task = "Rà kho lạnh", AssigneeId = handler }] }))).ResponseCode.Should().Be(400);
            var m = await As(f, clerk, s => s.CreateMeeting(new KnowledgeMeetingRequest
            {
                Title = "Giao ban tuần 48", Date = "2026-11-28", Chair = "COO", Attendees = "Bếp, Kho, QA", Content = "Tình hình tuần", Conclusion = "Tăng kiểm kho lạnh",
                Tasks = [new() { Task = "Rà nhiệt độ kho lạnh", AssigneeId = handler, Due = "2026-11-27" }, new() { Task = "Cập nhật SOP nhận hàng", AssigneeId = handler, Due = "2026-12-10" }],
            }));
            m.ResponseCode.Should().Be(200, m.ResponseMessage);
            m.ResponseMessage.Should().Be("Đã lập biên bản BH-2026-001 · giao 2 việc theo dõi");
            File.ReadAllText(Path.Combine(vault, m.Data!.Path)).Should().Contain("| 1 | Rà nhiệt độ kho lạnh | xl-a224 | 27/11/2026 |");
            (await As(f, clerk, s => s.GetRegister("15", 2026))).ListData!.Single().TaskCount.Should().Be(2);

            // Người được giao: thấy 3 việc của mình (không có quyền thư mục 98), bấm Xong được.
            var mine = (await As(f, handler, s => s.GetMyTasks(false))).ListData!;
            mine.Select(t => t.Title).Should().BeEquivalentTo(["Xử lý công văn VD-2026-0001: Kiểm tra ATTP quý IV", "Rà nhiệt độ kho lạnh", "Cập nhật SOP nhận hàng"]);
            mine.Single(t => t.Title == "Rà nhiệt độ kho lạnh").Overdue.Should().BeTrue();
            using (var scope = f.Services.CreateScope())
            {
                var due = await scope.ServiceProvider.GetRequiredService<IKnowledgeService>().DueTasksByAssigneeAsync();
                due[handler].Should().HaveCount(2, "hạn ≤ ngày mai: công văn (29/11) + việc quá hạn (27/11); việc 10/12 chưa nhắc");
            }
            string cv = mine.Single(t => t.Title.StartsWith("Xử lý công văn")).Path;
            (await As(f, handler, s => s.SetMyTask(new KnowledgeTaskSetRequest { Path = cv, Status = "xong" }))).ResponseCode.Should().Be(200);
            (await As(f, handler, s => s.GetMyTasks(false))).ListData!.Should().HaveCount(2);
            (await As(f, clerk, s => s.GetRegister("05", 2026))).ListData!.Single(b => b.RegisterNo == "VD-2026-0001").TaskStatus.Should().Be("xong");
            Guid other = await UserAsync(f, "khac-a224");
            (await As(f, other, s => s.SetMyTask(new KnowledgeTaskSetRequest { Path = cv, Status = "đang theo dõi" }))).ResponseCode.Should().Be(404, "người khác không sửa việc của người được giao");
        }
    }
}
