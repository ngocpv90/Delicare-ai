using AppDelicare.Application.Common;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Application
{
    /// <summary>Đăng ký tầng Application: dispatcher, handler, validator, handler sự kiện (quét assembly), IClock, BusinessRules.</summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<BusinessRulesOptions>(configuration.GetSection(BusinessRulesOptions.Section));
            services.AddSingleton<IClock, SystemClock>();
            services.AddScoped<Common.Crud.ICrudService, Common.Crud.CrudService>();
            services.AddScoped<MasterData.IUomService, MasterData.UomService>();
            services.AddScoped<Dispatcher>();
            services.AddScoped<IDispatcher>(sp => sp.GetRequiredService<Dispatcher>());
            services.AddScoped<IDomainEventCollector>(sp => sp.GetRequiredService<Dispatcher>());

            var assembly = typeof(DependencyInjection).Assembly;
            foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType))
                {
                    var definition = contract.GetGenericTypeDefinition();
                    if (definition == typeof(IRequestHandler<,>) || definition == typeof(IDomainEventHandler<>))
                    {
                        services.AddScoped(contract, type);
                    }
                }
            }
            services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);
            return services;
        }
    }
}
