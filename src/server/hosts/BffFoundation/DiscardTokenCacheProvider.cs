using Microsoft.Identity.Client;
using Microsoft.Identity.Web.TokenCacheProviders;

namespace IgaMigration.BffFoundation;

// Microsoft.Identity.Web's supported MSAL code-redemption path needs a cache
// provider. This provider persists nothing and clears the in-process token cache
// after each access. There is no downstream API acquisition in this foundation.
public sealed class DiscardTokenCacheProvider : IMsalTokenCacheProvider
{
    public void Initialize(ITokenCache tokenCache)
    {
        tokenCache.SetBeforeAccess(notification => notification.TokenCache.DeserializeMsalV3(null, true));
        tokenCache.SetAfterAccess(notification => notification.TokenCache.DeserializeMsalV3(null, true));
    }

    public Task InitializeAsync(ITokenCache tokenCache)
    {
        Initialize(tokenCache);
        return Task.CompletedTask;
    }

    public Task ClearAsync(string homeAccountId) => Task.CompletedTask;
}
