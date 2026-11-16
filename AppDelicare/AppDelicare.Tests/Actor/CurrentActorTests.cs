using System.Security.Claims;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Actor
{
    /// <summary>T02 – người thao tác bắt buộc khi ghi; không còn fallback "user đầu tiên".</summary>
    public class CurrentActorTests
    {
        private static (DatabaseContext Db, CurrentActor Actor, HttpContextAccessor Http) Create(Guid? userIdClaim = null)
        {
            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            if (userIdClaim.HasValue)
            {
                http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(CurrentActor.UserIdClaim, userIdClaim.Value.ToString())], "Test"));
            }
            http.HttpContext.Items[CurrentActor.CorrelationIdItemKey] = "corr-test-01";
            var actor = new CurrentActor(http);
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase("t02-" + Guid.NewGuid())
                .Options;
            return (new DatabaseContext(options, actor), actor, http);
        }

        private static TestDemo NewDemo() => new() { Id = Guid.NewGuid(), FieldString = "[DEMO] T02" };

        [Fact]
        public async Task Write_without_actor_is_blocked_on_typed_SaveChangesAsync()
        {
            var (db, _, _) = Create(userIdClaim: null);
            db.TestDemo.Add(NewDemo());

            Func<Task> act = () => db.SaveChangesAsync(ConstantHelper.TypeSave.Insert);

            await act.Should().ThrowAsync<UnauthorizedActorException>();
            db.ChangeTracker.Clear();
            (await db.TestDemo.CountAsync()).Should().Be(0);
        }

        [Fact]
        public void Write_without_actor_is_blocked_on_plain_SaveChanges()
        {
            var (db, _, _) = Create(userIdClaim: null);
            db.TestDemo.Add(NewDemo());

            Action act = () => db.SaveChanges();

            act.Should().Throw<UnauthorizedActorException>();
        }

        [Fact]
        public async Task Write_without_actor_is_blocked_on_plain_SaveChangesAsync()
        {
            var (db, _, _) = Create(userIdClaim: null);
            db.TestDemo.Add(NewDemo());

            Func<Task> act = () => db.SaveChangesAsync();

            await act.Should().ThrowAsync<UnauthorizedActorException>();
        }

        [Fact]
        public async Task Request_with_token_sets_CreatedBy_and_audit_from_claim()
        {
            var userId = Guid.NewGuid();
            var (db, _, _) = Create(userId);
            var demo = NewDemo();
            db.TestDemo.Add(demo);

            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert);

            var saved = await db.TestDemo.SingleAsync(x => x.Id == demo.Id);
            saved.CreatedBy.Should().Be(userId);
            var audit = await db.AuditChange.SingleAsync(x => x.IdChange == demo.Id);
            audit.CreatedBy.Should().Be(userId);
            audit.Changes.Should().Contain($"Actor: USER:{userId}").And.Contain("CorrelationId: corr-test-01");
        }

        [Fact]
        public async Task Update_and_soft_delete_record_modifier_from_claim()
        {
            var userId = Guid.NewGuid();
            var (db, _, _) = Create(userId);
            var demo = NewDemo();
            db.TestDemo.Add(demo);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert);

            demo.FieldString = "[DEMO] T02 updated";
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            demo.ModifiedBy.Should().Be(userId);

            db.Entry(demo).State = EntityState.Modified;
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Delete);
            demo.IsDeleted.Should().BeTrue();
            demo.DeleteddBy.Should().Be(userId);
        }

        [Fact]
        public async Task System_scope_allows_job_write_without_http_user()
        {
            var jobUserId = Guid.NewGuid();
            var (db, actor, http) = Create(userIdClaim: null);
            http.HttpContext = null; // job: không có HTTP request
            var demo = NewDemo();
            db.TestDemo.Add(demo);

            using (actor.BeginScope(ActorInfo.ForSystem(jobUserId), "job-run-1"))
            {
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert);
            }

            demo.CreatedBy.Should().Be(jobUserId);
            var audit = await db.AuditChange.SingleAsync(x => x.IdChange == demo.Id);
            audit.Changes.Should().Contain($"Actor: SYSTEM:{jobUserId}").And.Contain("CorrelationId: job-run-1");
            actor.Actor.Should().BeNull("scope đã đóng thì không còn người thao tác");
        }

        [Fact]
        public void Agent_actor_requires_agent_code_and_scopes_nest()
        {
            var http = new HttpContextAccessor();
            var actor = new CurrentActor(http);
            var outer = ActorInfo.ForSystem(Guid.NewGuid());
            var inner = ActorInfo.ForAgent(Guid.NewGuid(), "vivi");

            using (actor.BeginScope(outer))
            {
                using (actor.BeginScope(inner))
                {
                    actor.Actor.Should().Be(inner);
                    actor.Actor!.ToString().Should().StartWith("AGENT:vivi:");
                }
                actor.Actor.Should().Be(outer);
            }
            actor.Actor.Should().BeNull();

            Action noCode = () => ActorInfo.ForAgent(Guid.NewGuid(), " ");
            noCode.Should().Throw<ArgumentException>();
            Action emptyUser = () => ActorInfo.ForUser(Guid.Empty);
            emptyUser.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Invalid_claim_does_not_produce_an_actor()
        {
            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("id", "not-a-guid")], "Test"));
            var actor = new CurrentActor(http);

            actor.Actor.Should().BeNull();
            Action act = () => actor.RequireActor();
            act.Should().Throw<UnauthorizedActorException>();
        }
    }
}
