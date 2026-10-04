using Microsoft.IdentityModel.JsonWebTokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace IgaMigration.BffFoundation;

// The supported OIDC handler relaxes RequireSignedTokens in code flow. This
// supported validation extension restores the accepted signature requirement;
// all issuer/audience/lifetime/key validation remains in IdentityModel.
internal sealed class SignedJsonWebTokenHandler : JsonWebTokenHandler
{
    public override async Task<TokenValidationResult> ValidateTokenAsync(string token, TokenValidationParameters validationParameters)
    {
        var strict = validationParameters.Clone();
        strict.RequireSignedTokens = true;
        var validated = await base.ValidateTokenAsync(token, strict);
        if (!validated.IsValid) return validated;
        // Framework conversion to JwtSecurityToken rebuilds projected claims
        // and can flatten repeated array values. Inspect the original already
        // validated signed payload through the supported token reader.
        var payload = new JwtSecurityTokenHandler().ReadJwtToken(token).Payload;
        if (!payload.TryGetValue("auth_time", out var raw) || raw is not (long or int))
            return new TokenValidationResult { IsValid = false, Exception = new SecurityTokenValidationException("Scalar integer authentication time required.") };
        return validated;
    }
}
