using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Arc4u.Configuration;
using Arc4u.Dependency;
using Arc4u.Dependency.Attribute;
using Arc4u.Diagnostics;
using Arc4u.OAuth2.Token;
using Arc4u.Security.Principal;
using Arc4u.ServiceModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arc4u.OAuth2.Security.Principal;

[Export(typeof(IAppPrincipalFactory))]
public class AppServicePrincipalFactory : IAppPrincipalFactory
{
    public const string ProviderKey = "ProviderId";

    public static readonly string tokenExpirationClaimType = "exp";
    public static readonly string[] ClaimsToExclude = { "exp", "aud", "iss", "iat", "nbf", "acr", "aio", "appidacr", "ipaddr", "scp", "sub", "tid", "uti", "unique_name", "apptype", "appid", "ver", "http://schemas.microsoft.com/ws/2008/06/identity/claims/authenticationinstant", "http://schemas.microsoft.com/identity/claims/scope" };

    private readonly IContainerResolve _container;
    private readonly ILogger<AppServicePrincipalFactory> _logger;
    private readonly IOptionsMonitor<SimpleKeyValueSettings> _settings;
    private readonly IClaimsTransformation _claimsTransformation;
    private readonly ActivitySource? _activitySource;


    public Task<AppPrincipal> CreatePrincipal(Messages messages, object parameter = null)
    {
        throw new NotImplementedException();
    }

    public AppServicePrincipalFactory(IContainerResolve container, ILogger<AppServicePrincipalFactory> logger, IOptionsMonitor<SimpleKeyValueSettings> settings, IClaimsTransformation claimsTransformation, IActivitySourceFactory activitySourceFactory)
    {
        _container = container;
        _logger = logger;
        _settings = settings;
        _claimsTransformation = claimsTransformation;
        _activitySource = activitySourceFactory.GetArc4u();
    }

    public async Task<AppPrincipal> CreatePrincipal(string settingsResolveName, Messages messages, object parameter = null)
    {
        var settings = _settings.Get(settingsResolveName);
        return await CreatePrincipal(settings, messages, parameter).ConfigureAwait(false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="settings">The settings used to create the token.</param>
    /// <param name="messages">Information about the steps to create a token.</param>
    /// <param name="parameter">unused.</param>
    /// <returns></returns>
    /// <exception cref="AppPrincipalException">Thrown when a principal cannot be created.</exception>
    public async Task<AppPrincipal> CreatePrincipal(IKeyValueSettings settings, Messages messages, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(messages);

        using var activity = _activitySource?.StartActivity("Prepare the creation of the Arc4u Principal", ActivityKind.Producer);

        var identity = new ClaimsIdentity("OAuth2Bearer", "upn", ClaimsIdentity.DefaultRoleClaimType);

        await BuildTheIdentity(identity, settings, messages, parameter).ConfigureAwait(false);

        var principal = await _claimsTransformation.TransformAsync(new ClaimsPrincipal(identity)).ConfigureAwait(false);

        if (principal is AppPrincipal appPrincipal)
        {
            appPrincipal.ActivityID = Activity.Current?.Id ?? Guid.NewGuid().ToString();

            activity?.SetTag(LoggingConstants.ActivityId, appPrincipal.ActivityID);

            return appPrincipal;
        }

        throw new AppPrincipalException("No principal can be created.");
        
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="identity"></param>
    /// <param name="settings"></param>
    /// <param name="messages"></param>
    /// <param name="parameter"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">When the provider doesn't exists.</exception>
    private async Task BuildTheIdentity(ClaimsIdentity identity, IKeyValueSettings settings, Messages messages, object? parameter = null)
    {
        // Check if we have a provider registered.
        if (!_container.TryResolve(settings.Values[ProviderKey], out ITokenProvider provider))
        {
            throw new NotSupportedException($"The principal cannot be created. We are missing an account provider: {settings.Values[ProviderKey]}");
        }

        // Check the settings contains the service url.
        TokenInfo? token = null;
        try
        {
            token = await provider.GetTokenAsync(settings, parameter).ConfigureAwait(true);
            identity.BootstrapContext = token.Token;
            var jwtToken = new JwtSecurityToken(token.Token);
            identity.AddClaims(jwtToken.Claims.Where(c => !ClaimsToExclude.Any(arg => arg.Equals(c.Type))).Select(c => new Claim(c.Type, c.Value)));
        }
        catch (Exception ex)
        {
            _logger.Technical().LogException(ex);
        }
    }


    private async ValueTask RemoveCacheFromUserAsync()
    {
        if (_container.TryResolve<IApplicationContext>(out var appContext))
        {
            if (appContext.Principal is not null && appContext.Principal.Identity is not null && appContext.Principal.Identity is ClaimsIdentity claimsIdentity)
            {
                var cacheHelper = _container.Resolve<ICacheHelper>();
                var cacheKeyGenerator = _container.Resolve<ICacheKeyGenerator>();

                await cacheHelper.GetCache().RemoveAsync(cacheKeyGenerator.GetClaimsKey(claimsIdentity), CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                _logger.Technical().LogError("No principal exists on the current context.");
            }
        }
    }
    public async ValueTask SignOutUserAsync(CancellationToken cancellationToken)
    {
        await RemoveCacheFromUserAsync().ConfigureAwait(false);
    }

    public async ValueTask SignOutUserAsync(IKeyValueSettings settings, CancellationToken cancellationToken)
    {
        await RemoveCacheFromUserAsync().ConfigureAwait(false);
    }
}

