using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arc4u.OAuth2.DataProtection;
public static class CacheStoreExtension
{
    public static IDataProtectionBuilder PersistKeysToCache(this IDataProtectionBuilder builder, Action<CacheStoreOption> option)
    {
        var validate = new CacheStoreOption();
        option(validate);

        ArgumentNullException.ThrowIfNull(validate.CacheKey, nameof(validate.CacheKey));

        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
        {
            var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            return new ConfigureOptions<KeyManagementOptions>(options =>
            {
                options.XmlRepository = new CacheStore(services, loggerFactory, validate.CacheKey, validate.CacheName);
            });
        });

        return builder;
    }

    public static IDataProtectionBuilder PersistKeysToCache(this IDataProtectionBuilder builder, [DisallowNull] IConfiguration configuration, [DisallowNull] string configSectionName = "DataProtectionStore")
    {
        ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

        var section = configuration.GetSection(configSectionName);
        if (!section.Exists())
        {
            throw new KeyNotFoundException($"A section with name {configuration} doesn't exist.");
        }

        var storeInfo = section.Get<CacheStoreOption>();
        if (storeInfo.CacheKey is null && storeInfo.CacheName is null)
        {
            throw new InvalidCastException($"Retrieving the cache data protection store info from section {configSectionName} is impossible.");
        }

        void optionFiller(CacheStoreOption option)
        {
            option.CacheKey = storeInfo.CacheKey;
            option.CacheName = storeInfo.CacheName;
        }

        return PersistKeysToCache(builder, optionFiller);
    }
}
