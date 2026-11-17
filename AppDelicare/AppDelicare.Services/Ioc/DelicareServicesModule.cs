using System.Reflection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using Autofac;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Ioc
{
    /// <summary>
    /// T23 (D-48): đăng ký DI dùng chung cho AppDelicare.WebApi và AppDelicare.Job (trước ở WebApi/Ioc/ComponentRegistrar – giữ nguyên quy tắc):
    /// lớp *Service / *Repository / *Options theo interface đầu tiên, IApprovalHandler, IScheduledJob, UnitOfWork, CurrentActor.
    /// </summary>
    public class DelicareServicesModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            Assembly services = typeof(ILoginService).Assembly;
            builder.RegisterAssemblyTypes(services).Where(x => x.IsClass && x.Name.EndsWith("Service")).As(t => t.GetInterfaces().Length != 0 ? t.GetInterfaces()[0] : t);
            builder.RegisterAssemblyTypes(typeof(IDapperRepository).Assembly).Where(x => x.IsClass && x.Name.EndsWith("Repository")).As(t => t.GetInterfaces().Length != 0 ? t.GetInterfaces()[0] : t);
            builder.RegisterAssemblyTypes(typeof(JwtIssuerOptions).Assembly).Where(x => x.IsClass && x.Name.EndsWith("Options")).As(t => t.GetInterfaces().Length != 0 ? t.GetInterfaces()[0] : t);

            // T12: bộ xử lý duyệt theo BusinessType (IApprovalHandler) – registry cho ApprovalRequestService.
            builder.RegisterAssemblyTypes(services)
                .Where(x => x.IsClass && !x.IsAbstract && typeof(IApprovalHandler).IsAssignableFrom(x)).As<IApprovalHandler>();
            // T23: job hằng ngày (IScheduledJob) – JobRunService nhận IEnumerable<IScheduledJob>.
            builder.RegisterAssemblyTypes(services)
                .Where(x => x.IsClass && !x.IsAbstract && typeof(IScheduledJob).IsAssignableFrom(x)).As<IScheduledJob>();

            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>();
            builder.RegisterType<HttpContextAccessor>().As<IHttpContextAccessor>().SingleInstance();
            builder.RegisterType<DbContext>().As<DbContext>();
            builder.RegisterType<Data.Share.CurrentActor>().As<Data.Share.ICurrentActor>();
            builder.RegisterType<Data.Share.AuthenticatedService>().As<Data.Share.IAuthenticatedService>();
        }
    }
}
