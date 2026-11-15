using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;

namespace AppDelicare.WebApi.Infrastructure
{
    public static class ConfigureService
    {
        /// <summary>
        /// Cấu hình JWT. Khóa ký đọc từ <c>Jwt:SigningKey</c> (user-secrets / biến môi trường), không viết cứng (T01).
        /// Gọi <see cref="StartupSecrets.Validate"/> trước hàm này.
        /// </summary>
        public static void InitAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            SymmetricSecurityKey signingKey = new(StartupSecrets.GetSigningKeyBytes(configuration));
            TimeSpan accessTokenLifetime = StartupSecrets.GetAccessTokenLifetime(configuration);

            IConfigurationSection jwtAppSettingOptions = configuration.GetSection(nameof(JwtIssuerOptions));
            services.Configure<JwtIssuerOptions>(options =>
            {
                options.Issuer = jwtAppSettingOptions[nameof(JwtIssuerOptions.Issuer)] ?? string.Empty;
                options.Audience = jwtAppSettingOptions[nameof(JwtIssuerOptions.Audience)] ?? string.Empty;
                options.ValidFor = accessTokenLifetime;
                options.SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            });

            TokenValidationParameters tokenValidationParameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = jwtAppSettingOptions[nameof(JwtIssuerOptions.Issuer)],
                ValidateAudience = true,
                ValidAudience = jwtAppSettingOptions[nameof(JwtIssuerOptions.Audience)],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

            }).AddJwtBearer(configureOptions =>
            {
                configureOptions.ClaimsIssuer = jwtAppSettingOptions[nameof(JwtIssuerOptions.Issuer)];
                configureOptions.TokenValidationParameters = tokenValidationParameters;
                configureOptions.SaveToken = true;
            });
            // api user claim policy
            services.AddAuthorizationBuilder().AddPolicy("ApiUser", policy => policy.RequireClaim(ConstantHelper.JwtClaimIdentifiers.Rol, ConstantHelper.JwtClaims.ApiAccess));

            IConfigurationSection appSettingOptions = configuration.GetSection(nameof(AppSettingHelper));
            services.Configure<AppSettingHelper>(options =>
            {
                options.DomainFile = appSettingOptions[nameof(AppSettingHelper.DomainFile)] ?? string.Empty;
            });
        }
    }
}
