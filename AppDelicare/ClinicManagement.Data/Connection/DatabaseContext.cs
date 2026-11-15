using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using System.Text;

namespace AppDelicare.Data.Connection
{
	/// <summary>DbContext chính. DbSet + cấu hình theo bảng nằm ở DatabaseContext.Generated.cs (CodeGen, T05).
    /// Không còn kế thừa IdentityDbContext: các bảng AspNet* không có trong 124 bảng và không có code dùng Identity store (A11).</summary>
	public partial class DatabaseContext(DbContextOptions<DatabaseContext> options, ICurrentActor currentActor) : DbContext(options)
    {
        private readonly ICurrentActor _currentActor = currentActor;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            OnModelCreatingGenerated(modelBuilder);
        }

        partial void OnModelCreatingGenerated(ModelBuilder modelBuilder);

        public async Task<int> SaveChangesAsync(string typeSave, CancellationToken cancellationToken = new CancellationToken())
        {
            // T02: không có người thao tác thì chặn ghi (không còn fallback "user đầu tiên").
            if (!HasPendingChanges())
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            EnsureAppendOnly();
            var actor = _currentActor.RequireActor();
            var user = actor.UserId;
            var auditHeader = BuildAuditHeader(actor);
            var date = DateTime.Now;
            foreach (var entry in ChangeTracker.Entries<BaseEntity>().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedDate = date;
                        entry.Entity.CreatedBy = user;
                        entry.Entity.IsDeleted = false;
                        break;
                    case EntityState.Modified:
                        if (typeSave == ConstantHelper.TypeSave.Update || typeSave == ConstantHelper.TypeSave.UpdateProfile)
                        {
                            entry.Entity.ModifiedDate = date;
                            entry.Entity.ModifiedBy = user;
                        }
                        if (typeSave == ConstantHelper.TypeSave.Delete)
                        {
                            entry.Entity.DeletedDate = date;
                            // DEPRECATED_COLUMNS: ghi cả DeletedBy (mới) và DeleteddBy (cũ); bảng thiếu cột nào thì EF Ignore cột đó.
                            entry.Entity.DeletedBy = user;
                            entry.Entity.DeleteddBy = user;
                            entry.Entity.IsDeleted = true;
                        }
                        break;
                    default:
                        break;
                }
            }
            var modifiedEntities = ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).ToList();
            foreach (var modifiedEntity in modifiedEntities)
            {
                AuditChange auditLog = new()
                {
                    IdChange = modifiedEntity.Entity.Id,
                    EntityName = modifiedEntity.Entity.GetType().Name,
                    Action = modifiedEntity.State.ToString(),
                    CreatedDate = date,
                    CreatedBy = user,
                    Changes = auditHeader + GetChanges(modifiedEntity, modifiedEntity.State),
                    IsDeleted = false,
                };
                AuditChange.Add(auditLog);
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>T02: mọi đường ghi khác (SaveChanges / SaveChangesAsync không typeSave) cũng phải có người thao tác.</summary>
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            EnsureActorForPendingChanges();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            EnsureActorForPendingChanges();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private bool HasPendingChanges()
            => ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        private void EnsureActorForPendingChanges()
        {
            EnsureAppendOnly();
            if (HasPendingChanges())
            {
                _currentActor.RequireActor();
            }
        }

        /// <summary>T08 – sổ chỉ ghi thêm (CLAUDE.md 3.3): chỉ được INSERT.</summary>
        public static readonly IReadOnlySet<Type> AppendOnlyTypes = new HashSet<Type>
        {
            typeof(InventoryTransaction), typeof(PackagingLedger), typeof(TemperatureLog), typeof(AuditChange), typeof(IntegrationLog),
            typeof(ApprovalDecision), typeof(AttendanceEvent), typeof(DeliveryEvent), typeof(ComplaintHistory),
        };

        private void EnsureAppendOnly()
        {
            var violation = ChangeTracker.Entries()
                .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted && AppendOnlyTypes.Contains(e.Entity.GetType()));
            if (violation != null)
            {
                throw new AppendOnlyViolationException(violation.Entity.GetType().Name);
            }
        }

        /// <summary>
        /// GAP-AUD-01: AuditChange không có cột loại người thao tác / CorrelationId → ghi ở dòng đầu cột Changes.
        /// </summary>
        private string BuildAuditHeader(ActorInfo actor)
            => $"- Actor: {actor} | CorrelationId: {_currentActor.CorrelationId} {ConstantHelper.Break}" + Environment.NewLine;

        private static string GetChanges(EntityEntry entity, EntityState state)
        {
            StringBuilder changes = new();
            foreach (var property in entity.OriginalValues.Properties)
            {
                var originalValue = state == EntityState.Modified ? entity.OriginalValues[property] : ConstantHelper.Space;
                var currentValue = entity.CurrentValues[property];
                if (!Equals(originalValue, currentValue))
                {
                    if (state == EntityState.Added && !string.IsNullOrEmpty(currentValue?.ToString()?.Trim()))
                    {
                        changes.AppendLine($"- {property.Name}: '{currentValue}' {ConstantHelper.Break}");
                    }
                    if (state == EntityState.Modified)
                    {
                        changes.AppendLine($"- {property.Name}: From '{originalValue}' to '{currentValue}' {ConstantHelper.Break}");
                    }
                }
            }
            return changes.ToString();
        }
    }
}
