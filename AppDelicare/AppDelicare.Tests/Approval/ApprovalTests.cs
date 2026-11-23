using System.Net;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Approval
{
    /// <summary>
    /// T12 – duyệt 1 cấp (Pilot): tạo (gửi duyệt) → hộp duyệt → duyệt → thực thi; từ chối không thực thi; nội dung bị sửa thì không duyệt được;
    /// quyền duyệt = mục menu #/wf-inbox?type=&lt;screen&gt; (D-31). Nghiệp vụ thử: phiên bản công thức (RECIPE_VERSION).
    /// </summary>
    public class ApprovalTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ApprovalTests(InMemoryApiFactory factory) => _factory = factory;

        /// <summary>Món + nguyên liệu + công thức Draft có 1 dòng, đã gửi duyệt (bởi admin). Trả RecipeId.</summary>
        private async Task<Guid> SubmittedRecipeAsync(string code)
        {
            Guid dish = Guid.NewGuid(), ing = Guid.NewGuid(), kg = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = code, Name = "Món " + code, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Name = "kg-" + code });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = code + "-NL", Name = "NL " + code });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            Guid recipe = (await admin.PostFormAsync("/api/Recipe/CreateVersion", ("DishId", dish))).Ok().GetProperty("id").GetGuid();
            (await admin.PostFormAsync("/api/RecipeLine/Insert", ("RecipeId", recipe), ("IngredientId", ing), ("Quantity", 10m), ("UnitId", kg))).Ok();
            (await admin.PostFormAsync("/api/Recipe/Submit", ("id", recipe))).Ok();
            return recipe;
        }

        /// <summary>User thường; withApprove = có quyền trên mục "Phê duyệt › Công thức".</summary>
        private async Task<Guid> UserAsync(bool withApprove)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "ap-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "AP-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                if (withApprove)
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/wf-inbox?type=menu-recipe");
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Công thức", MetaLink = "#/wf-inbox?type=menu-recipe", IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowEdit = true });
                }
            });
            return userId;
        }

        private Task<ApprovalRequest> RequestOfAsync(Guid recipe)
            => _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == recipe));

        [Fact]
        public async Task Submit_creates_xn_request_with_step_and_approver_sees_it_others_do_not()
        {
            var recipe = await SubmittedRecipeAsync("AP-INBOX");
            var request = await RequestOfAsync(recipe);
            request.RequestNo.Should().MatchRegex(@"^XN-\d{6}-\d{3}$");
            request.PayloadJson.Should().Contain(recipe.ToString());
            (await _factory.QueryAsync(db => db.ApprovalStep.CountAsync(s => s.ApprovalRequestId == request.Id && s.DecisionStatus == "Waiting"))).Should().Be(1);

            using var approver = _factory.CreateAuthorizedClient(await UserAsync(withApprove: true));
            var mine = await approver.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=menu-recipe");
            var item = mine.ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == request.Id);
            item.GetProperty("title").GetString().Should().Contain("AP-INBOX");
            item.GetProperty("screenId").GetString().Should().Be("menu-recipe");

            using var other = _factory.CreateAuthorizedClient(await UserAsync(withApprove: false));
            (await other.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.GetArrayLength().Should().Be(0);
            (await other.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request.Id), ("Approve", true))).Fail("không có quyền");
            (await RequestOfAsync(recipe)).RequestStatus.Should().Be("Pending");
        }

        /// <summary>BOT-1.4: explicitOnly (bot "Chờ tôi duyệt") – admin không có quyền Duyệt được cấp rõ thì không thấy (không ngập); người được cấp vẫn thấy.</summary>
        [Fact]
        public async Task Explicit_only_pending_excludes_admin_bypass_but_keeps_real_approver()
        {
            var recipe = await SubmittedRecipeAsync("AP-EXPL");
            var request = await RequestOfAsync(recipe);
            using var admin = _factory.CreateAuthorizedClient();
            (await admin.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == request.Id);
            (await admin.GetResultAsync("/api/ApprovalRequest/GetMyPending?explicitOnly=true")).ListData.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == request.Id);
            using var approver = _factory.CreateAuthorizedClient(await UserAsync(withApprove: true));
            (await approver.GetResultAsync("/api/ApprovalRequest/GetMyPending?explicitOnly=true")).ListData.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == request.Id);
        }

        [Fact]
        public async Task Approve_executes_the_business_action_and_writes_an_append_only_decision()
        {
            var recipe = await SubmittedRecipeAsync("AP-OK");
            var request = await RequestOfAsync(recipe);
            var approverId = await UserAsync(withApprove: true);
            using var approver = _factory.CreateAuthorizedClient(approverId);

            (await approver.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request.Id), ("Approve", true), ("Note", "OK"))).Ok();

            (await _factory.QueryAsync(db => db.Recipe.AsNoTracking().SingleAsync(r => r.Id == recipe))).Status.Should().Be("Approved");
            (await RequestOfAsync(recipe)).RequestStatus.Should().Be("Approved");
            var decision = await _factory.QueryAsync(db => db.ApprovalDecision.AsNoTracking().SingleAsync(d => d.ApprovalRequestId == request.Id));
            decision.DecisionCode.Should().Be("APPROVE");
            decision.DecidedBy.Should().Be(approverId);
            decision.PayloadHash.Should().HaveLength(64);
            (await approver.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request.Id), ("Approve", true))).Fail("không còn chờ duyệt");
        }

        [Fact]
        public async Task Reject_needs_a_reason_and_does_not_execute()
        {
            var recipe = await SubmittedRecipeAsync("AP-NO");
            var request = await RequestOfAsync(recipe);
            using var admin = _factory.CreateAuthorizedClient();

            (await admin.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request.Id), ("Approve", false))).Fail("lý do");
            (await admin.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request.Id), ("Approve", false), ("Note", "Thiếu rau"))).Ok();

            var r = await _factory.QueryAsync(db => db.Recipe.AsNoTracking().SingleAsync(x => x.Id == recipe));
            r.Status.Should().Be("Draft");
            r.ApprovedAt.Should().BeNull("từ chối không thực thi duyệt");
            (await RequestOfAsync(recipe)).RequestStatus.Should().Be("Rejected");
        }

        [Fact]
        public async Task Changed_payload_after_submit_cannot_be_approved_and_nothing_is_written()
        {
            var recipe = await SubmittedRecipeAsync("AP-TAMPER");
            var request = await RequestOfAsync(recipe);
            // Sửa định lượng thẳng trong DB sau khi gửi duyệt (bỏ qua chặn Draft của RecipeLineService).
            await _factory.SeedAsync(async db => (await db.RecipeLine.FirstAsync(l => l.RecipeId == recipe)).Quantity = 999);
            using var admin = _factory.CreateAuthorizedClient();

            (await admin.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request.Id), ("Approve", true))).Fail("đã thay đổi");

            (await RequestOfAsync(recipe)).RequestStatus.Should().Be("Pending");
            (await _factory.QueryAsync(db => db.ApprovalDecision.CountAsync(d => d.ApprovalRequestId == request.Id))).Should().Be(0);
            (await _factory.QueryAsync(db => db.Recipe.AsNoTracking().SingleAsync(x => x.Id == recipe))).Status.Should().Be("PendingApproval");
        }

        [Fact]
        public async Task Crud_update_cannot_change_request_status_and_recipe_approve_route_uses_the_engine()
        {
            var recipe = await SubmittedRecipeAsync("AP-CRUD");
            var request = await RequestOfAsync(recipe);
            using var admin = _factory.CreateAuthorizedClient();

            await admin.PutFormAsync("/api/ApprovalRequest/Update", ("Id", request.Id), ("RequestNo", request.RequestNo), ("BusinessType", "HACK"), ("RequestStatus", "Approved"), ("RequestedAt", DateTime.Today));
            var kept = await RequestOfAsync(recipe);
            kept.RequestStatus.Should().Be("Pending");
            kept.BusinessType.Should().Be("RECIPE_VERSION", "CRUD không đổi được nghiệp vụ của yêu cầu");

            (await admin.PostFormAsync("/api/Recipe/Approve", ("Id", recipe))).Ok();
            (await _factory.QueryAsync(db => db.ApprovalDecision.CountAsync(d => d.ApprovalRequestId == request.Id))).Should().Be(1);
            (await admin.PostAsync("/api/ApprovalRequest/GetMyPending", null)).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        }
    }
}
