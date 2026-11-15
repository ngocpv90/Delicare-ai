using Dapper;
using Microsoft.Extensions.Configuration;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Common;
using AppDelicare.Model.Helper;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

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
        public IDbConnection Connection
        {
            get => _cn;
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
            get => _trans;
#pragma warning restore CS8603 // Possible null reference return.
        }

        public async Task<IDbTransaction> OpenTransaction()
        {
            if (_trans != null)
            {
                throw new Exception("A transaction is already open, you need to use a new DbContext for parallel job.");
            }
            if (_cn.State == ConnectionState.Closed)
            {
                if (_cn is not DbConnection)
                {
                    throw new Exception("Connection object does not support OpenAsync.");
                }
                await ((DbConnection)_cn).OpenAsync();
            }
            _trans = _cn.BeginTransaction();
            return _trans;
        }

        public async Task<IDbTransaction> OpenTransaction(IsolationLevel level)
        {
            if (_trans != null)
            {
                throw new Exception("A transaction is already open, you need to use a new DbContext for parallel job.");
            }
            if (_cn.State == ConnectionState.Closed)
            {
                if (_cn is not DbConnection)
                {
                    throw new Exception("Connection object does not support OpenAsync.");
                }
                await ((DbConnection)_cn).OpenAsync();
            }
            _trans = _cn.BeginTransaction(level);
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
            }
            if (disposeTrans)
            {
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
            }
            if (disposeTrans)
            {
                _trans = null;
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
