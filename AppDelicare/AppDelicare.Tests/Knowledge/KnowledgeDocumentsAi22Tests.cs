using System.Text;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Knowledge;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Knowledge
{
    /// <summary>
    /// AI-22.5 (D-141 i): tab Tài liệu trên trang chi tiết – tài liệu kho gắn mã khách / hợp đồng / NCC / nhân viên, chỉ người có quyền vùng thấy, tải lên tại chỗ
    /// vào vùng mặc định (20-HD / 40-HD / 70-HS); nhắc hợp đồng sắp hết hạn (khách: Contract.EffectiveTo; NCC / lao động: het_han trong kho) ở mốc 30 / 7 / 1 ngày.
    /// </summary>
    public class KnowledgeDocumentsAi22Tests
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

        [Fact]
        public async Task Detail_page_documents_follow_area_rights_and_contracts_near_expiry_are_noticed()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-knowledge-" + Guid.NewGuid().ToString("N"));
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Knowledge:VaultPath", vault));
            f.Clock.FixedNow = new DateTime(2026, 11, 28, 6, 0, 0);
            f.CreateAuthorizedClient().Dispose();
            Guid customer = Guid.NewGuid(), contract = Guid.NewGuid(), supplier = Guid.NewGuid(), employee = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KHB-A225", CIF = "KHB-A225", Name = "Khách A225", DisplayName = "Khách A225", CustomerType = "BUSINESS", Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-261128-A25", Status = ContractStatus.Active, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 12, 28) });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NC-A225", Name = "NCC A225", Status = "Active" });
                db.Employee.Add(new Employee { Id = employee, EmployeeCode = "NV-A225", FullName = "NV A225", OrganizationUnitId = Guid.NewGuid() });
                return Task.CompletedTask;
            });
            Guid sales = await UserAsync(f, "kd-a225", "20-HD");
            Guid buyer = await UserAsync(f, "mh-a225", "40-HD");
            Guid hr = await UserAsync(f, "ns-a225", "70-HS");
            Guid qa = await UserAsync(f, "qa-a225");

            // Trang chi tiết: vùng tải lên mặc định theo loại đối tượng + quyền Sửa vùng đó.
            var c0 = (await As(f, sales, s => s.GetDocuments("customer", customer))).Data!;
            (c0.Code, c0.UploadFolder, c0.CanUpload).Should().Be(("KHB-A225", "20-HD", true));
            (await As(f, qa, s => s.GetDocuments("customer", customer))).Data!.CanUpload.Should().BeFalse();
            (await As(f, hr, s => s.GetDocuments("employee", employee))).Data!.UploadFolder.Should().Be("70-HS");

            Task<Data.Interface.IResult<KnowledgeSaveResultModel>> Up(IKnowledgeService s, string folder, string type, string title, string code, string? hetHan)
                => s.UploadFile(new KnowledgeUploadRequest { Folder = folder, Type = type, Title = title, DoiTuong = code, HetHan = hetHan, Ngay = "2026-01-01" }, "hd.pdf", new MemoryStream(Pdf), Pdf.Length);
            (await As(f, sales, s => Up(s, "20-HD", "hop-dong-kh", "Hợp đồng suất ăn 2026", "KHB-A225", "2026-12-28"))).ResponseCode.Should().Be(200);
            (await As(f, sales, s => Up(s, "20-HD", "hop-dong-kh", "Phụ lục giá 2026", "HD-261128-A25", null))).ResponseCode.Should().Be(200);
            (await As(f, buyer, s => Up(s, "40-HD", "hop-dong-ncc", "Hợp đồng cung cấp thịt", "NC-A225", "2026-12-05"))).ResponseCode.Should().Be(200);
            (await As(f, hr, s => Up(s, "70-HS", "hop-dong-ld", "HĐLĐ NV-A225", "NV-A225", "2026-11-29"))).ResponseCode.Should().Be(200);

            var cust = (await As(f, sales, s => s.GetDocuments("customer", customer))).Data!.ListDocument;
            cust.Should().ContainSingle().Which.Should().Match<KnowledgeDocumentModel>(d => d.Title == "Hợp đồng suất ăn 2026" && d.ExpiresOn == new DateTime(2026, 12, 28) && d.CanDownload);
            (await As(f, sales, s => s.GetDocuments("contract", contract))).Data!.ListDocument.Select(d => d.Title).Should().Equal("Phụ lục giá 2026");
            (await As(f, qa, s => s.GetDocuments("customer", customer))).Data!.ListDocument.Should().BeEmpty("không có quyền vùng 20 › Hợp đồng");
            (await As(f, sales, s => s.GetDocuments("employee", employee))).Data!.ListDocument.Should().BeEmpty("hồ sơ cá nhân chỉ Nhân sự");
            (await As(f, hr, s => s.GetDocuments("employee", employee))).Data!.ListDocument.Should().ContainSingle();
            (await As(f, buyer, s => s.GetDocuments("supplier", supplier))).Data!.ListDocument.Should().ContainSingle();
            (await As(f, sales, s => s.GetDocuments("order", customer))).ResponseCode.Should().Be(400);

            // Nhắc hết hạn: mốc 30 / 7 / 1 ngày – khách (Contract.EffectiveTo 28/12 = 30 ngày), NCC (05/12 = 7 ngày), HĐLĐ (29/11 = 1 ngày).
            using var scope = f.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IKnowledgeService>();
            var exp = svc.ExpiringDocuments(new DateTime(2026, 11, 28), [30, 7, 1]);
            exp.Keys.Should().BeEquivalentTo(["20-HD", "40-HD", "70-HS"]);
            exp["40-HD"].Single().Should().Contain("Hợp đồng cung cấp thịt").And.Contain("05/12/2026");
            svc.ExpiringDocuments(new DateTime(2026, 11, 28), [10]).Should().BeEmpty();
            var job = scope.ServiceProvider.GetServices<IScheduledJob>().Single(j => j.Code == "ContractExpiryNotice");
            var outcome = await job.RunAsync(f.Clock.FixedNow!.Value, CancellationToken.None);
            outcome.Message.Should().StartWith("Nhắc 4 hợp đồng", "1 hợp đồng lõi + 3 tài liệu kho");
        }
    }
}
