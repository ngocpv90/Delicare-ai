using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.WebApi.Infrastructure
{
    public static class DatabaseConnection
    {
        public static void InitConnection(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<DatabaseContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString(ConnectionHelper.DatabaseName.MainConnectionString.ToString()));
            });
        }
    }
}
