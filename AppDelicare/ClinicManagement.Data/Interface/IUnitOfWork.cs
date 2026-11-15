using AppDelicare.Data.Connection;
using System.Data;

namespace AppDelicare.Data.Interface
{
	public partial interface IUnitOfWork : IDisposable
    {
		public IDbConnection Connection { get; }
		public DatabaseContext DbContext { get; }
        public IDbTransaction Transaction { get; }
        public string ConnectionString { get; }
        public Task<IDbTransaction> OpenTransaction();
        public Task<IDbTransaction> OpenTransaction(IsolationLevel level);
        public void CommitTransaction(bool disposeTrans = true);
        public void RollbackTransaction(bool disposeTrans = true);
        public void BeginTransaction();
        public int SaveChanges();
        public void Commit();
        public void Rollback();
        public Task<int> SaveChangesAsync();
		//Custom

    }
}
