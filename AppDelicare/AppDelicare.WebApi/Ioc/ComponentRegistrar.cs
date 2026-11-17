using Autofac;

namespace AppDelicare.WebApi.Ioc
{
    /// <summary>
    /// T23 (D-48): quy tắc đăng ký chuyển sang <see cref="AppDelicare.Services.Ioc.DelicareServicesModule"/> để AppDelicare.Job dùng chung.
    /// Giữ lớp này làm điểm vào cho Startup.ConfigureContainer (đăng ký riêng của web thêm ở đây nếu cần).
    /// </summary>
    public class ComponentRegistrar : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterModule<AppDelicare.Services.Ioc.DelicareServicesModule>();
        }
    }
}
