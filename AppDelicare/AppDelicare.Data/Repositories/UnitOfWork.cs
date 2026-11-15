using Dapper;
using Microsoft.Extensions.Configuration;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Common;
using AppDelicare.Model.Helper;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AppDelicare.Data.Repositories
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>")]
	public partial class UnitOfWork : IUnitOfWork
    {
        protected readonly IConfiguration _config;
        protected string _cnStr = ConfigurationCommon.Instance.GetConfig<string>(ConstantHelper.ConnectionStrings, ConstantHelper.MainConnectionString);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public UnitOfWork(IConfiguration config, DatabaseContext context)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            _config = config;
            _cnStr = _config.GetConnectionString(ConnectionHelper.DatabaseName.MainConnectionString.ToString()) ?? string.Empty;
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            _cn = new SqlConnection(_cnStr);
            _context = context;
        }

        public string ConnectionString
        {
            get => _cnStr;
        }

        protected IDbConnection _cn;
        /// <summary>
        /// T45R (CODE_AUDIT A21): khi đang có transaction, Dapper phải dùng CHÍNH kết nối của DbContext để cùng transaction với EF.
        /// </summary>
        public IDbConnection Connection
        {
            get => _trans is SharedTransaction shared && shared.Connection != null ? shared.Connection : _cn;
        }

        protected DatabaseContext _context;
        public DatabaseContext DbContext
        {
            get => _context;
        }

        protected IDbTransaction? _trans = null;
        public IDbTransaction Transaction
        {
#pragma warning disable CS8603 // Possible null reference return.
            get => _trans is SharedTransaction shared ? shared.DbTransaction : _trans;
#pragma warning restore CS8603 // Possible null reference return.
        }

        public Task<IDbTransaction> OpenTransaction() => OpenSharedTransaction(null);

        public Task<IDbTransaction> OpenTransaction(IsolationLevel level) => OpenSharedTransaction(level);

        /// <summary>
        /// T45R (CODE_AUDIT A21). Trước đây transaction mở trên kết nối Dapper riêng nên các lần ghi EF (CommitAsync) không nằm trong nó
        /// (mỗi lần lưu commit ngay, rollback vô tác dụng). Nay:
        /// <list type="bullet">
        /// <item>transaction mở trên kết nối của DbContext → mọi CommitAsync / SaveChangesAsync và Dapper qua <see cref="Connection"/> cùng 1 transaction;</item>
        /// <item>service gọi lồng service (UserService → UserClaimService) dùng chung DbContext → tham gia transaction ngoài cùng, chỉ nơi mở mới commit / rollback;</item>
        /// <item>provider không quan hệ (InMemory – test) → không có transaction.</item>
        /// </list>
        /// </summary>
        private async Task<IDbTransaction> OpenSharedTransaction(IsolationLevel? level)
        {
            if (_trans != null)
            {
                throw new Exception("A transaction is already open, you need to use a new DbContext for parallel job.");
            }
            var database = _context.Database;
            if (!database.IsRelational())
            {
                _trans = new SharedTransaction(null, null, owner: false);
            }
            else if (database.CurrentTransaction is { } outer)
            {
                _trans = new SharedTransaction(outer, database.GetDbConnection(), owner: false);
            }
            else
            {
                var own = level == null ? await database.BeginTransactionAsync() : await database.BeginTransactionAsync(level.Value);
                _trans = new SharedTransaction(own, database.GetDbConnection(), owner: true);
            }
            return _trans;
        }

        public void CommitTransaction(bool disposeTrans = true)
        {
            if (_trans == null)
            {
                throw new Exception("DB Transaction is not present.");
            }
            _trans.Commit();
            if (disposeTrans)
            {
                _trans.Dispose();
                _trans = null;
            }
        }

        public void RollbackTransaction(bool disposeTrans = true)
        {
            if (_trans == null)
            {
                throw new Exception("DB Transaction is not present.");
            }
            _trans.Rollback();
            if (disposeTrans)
            {
                _trans.Dispose();
                _trans = null;
            }
        }

        /// <summary>Transaction EF dùng chung: chỉ bên mở (owner) mới commit / rollback / dispose transaction thật.</summary>
        private sealed class SharedTransaction(IDbContextTransaction? efTransaction, DbConnection? connection, bool owner) : IDbTransaction
        {
            private bool _completed;

            public DbConnection? Connection { get; } = connection;
            IDbConnection? IDbTransaction.Connection => Connection;
            public IsolationLevel IsolationLevel => efTransaction?.GetDbTransaction().IsolationLevel ?? IsolationLevel.Unspecified;
            /// <summary>Transaction ADO.NET để truyền cho Dapper.</summary>
            public DbTransaction? DbTransaction => efTransaction?.GetDbTransaction();

            public void Commit()
            {
                if (owner && !_completed)
                {
                    efTransaction!.Commit();
                }
                _completed = true;
            }

            public void Rollback()
            {
                if (owner && !_completed)
                {
                    efTransaction!.Rollback();
                }
                _completed = true;
            }

            public void Dispose()
            {
                if (owner)
                {
                    efTransaction!.Dispose(); // chưa commit → EF tự rollback
                }
            }
        }

        public void Dispose()
        {
            _context.Dispose();
            _trans?.Dispose();
            _cn?.Close();
            _cn?.Dispose();
        }

        public virtual void BeginTransaction() => _context.Database.BeginTransaction();

        public virtual void Commit() => _context.Database.CommitTransaction();

        public virtual void Rollback() => _context.Database.RollbackTransaction();

        public virtual int SaveChanges() => _context.SaveChanges();

        public virtual Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

		//Custom
	 
	 
	 
	 
	 
	 
	 
	 
	 
	 
	 
	 
	 
    }
}
