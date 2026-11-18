using System.Security.Claims;

namespace AppDelicare.Services.Interface
{
    public interface IJwtFactoryService
    {
        public Task<string> GenerateEncodedToken(string userName, ClaimsIdentity identity, List<Claim> additionalClaims);
        public ClaimsIdentity GenerateClaimsIdentity(string userName, Guid id);
        /// <summary>BOT-1.2: như trên nhưng hạn riêng (JWT 10′ cấp thay mặt user cho bot).</summary>
        public Task<string> GenerateEncodedToken(string userName, ClaimsIdentity identity, List<Claim> additionalClaims, TimeSpan validFor);
    }
}
