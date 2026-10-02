using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IgaMigration.BffFoundation;

// The supported OIDC handler relaxes RequireSignedTokens in code flow. This
// supported validation extension restores the accepted signature requirement;
// all issuer/audience/lifetime/key validation remains in IdentityModel.
internal sealed class SignedJsonWebTokenHandler : JsonWebTokenHandler
{
    public override Task<TokenValidationResult> ValidateTokenAsync(string token, TokenValidationParameters validationParameters)
    {
        var strict = validationParameters.Clone();
        strict.RequireSignedTokens = true;
        return base.ValidateTokenAsync(token, strict);
    }
}
