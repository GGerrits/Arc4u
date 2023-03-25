using AutoFixture.AutoMoq;
using AutoFixture;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System;
using Arc4u.Caching;
using Arc4u.Dependency;
using Arc4u.Serializer;
using FluentAssertions;
using Arc4u.Caching.Memory;
using Arc4u.Dependency.ComponentModel;
using Arc4u.OAuth2.DataProtection;
using Microsoft.Extensions.Logging;
using System.Xml.Linq;
using System.Linq;
using Moq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;

namespace Arc4u.Standard.UnitTest.Caching;

[Trait("Category", "CI")]
public class CacheDataProtectionStoreTests
{
    public CacheDataProtectionStoreTests()
    {
        _fixture = new Fixture();
        _fixture.Customize(new AutoMoqCustomization());
    }

    private readonly Fixture _fixture;

    [Fact]
    public void CheckMemoryStoreShould()
    {
        // arrange
        var (services, configuration) = BuiltContainer();

        var container = new ComponentModelContainer(services);

        container.Register<ICache, MemoryCache>(CacheContext.Memory);

        container.CreateContainer();

        // act
        var sut = container.GetRequiredService<ICacheContext>();

        var cacheInstance = sut["Volatile"];

        cacheInstance.Put("key", "value");

        // assert
        cacheInstance.Get<string>("key").Should().Be("value");
    }

    [Fact]
    public void StoreXElementShould()
    {
        // arrange
        var (services, configuration) = BuiltContainer();

        var container = new ComponentModelContainer(services);

        container.Register<ICache, MemoryCache>(CacheContext.Memory);

        container.CreateContainer();

        var loggerFactory = container.GetRequiredService<ILoggerFactory>();

        // act
        var sut = new CacheStore(container, loggerFactory, "DataProtection", "Volatile");

        var element = new XElement("Data", new XAttribute("CreationDate", DateTime.UtcNow),
                            new XElement("Cert", "Begin Certficate"));

        sut.StoreElement(element, "");

        var result = sut.GetAllElements();

        result.Count.Should().Be(1);
        result.First().Name.LocalName.Should().Be("Data");

    }

    [Fact]
    public void StoreXElementWithNoCacheNameShould()
    {
        // arrange
        var (services, configuration) = BuiltContainer();

        var container = new ComponentModelContainer(services);

        container.Register<ICache, MemoryCache>(CacheContext.Memory);

        container.CreateContainer();

        var loggerFactory = container.GetRequiredService<ILoggerFactory>();

        // act
        var sut = new CacheStore(container, loggerFactory, "DataProtection");

        var element = new XElement("Data", new XAttribute("CreationDate", DateTime.UtcNow),
                            new XElement("Cert", "Begin Certficate"));

        sut.StoreElement(element, "");

        var result = sut.GetAllElements();

        result.Count.Should().Be(1);
        result.First().Name.LocalName.Should().Be("Data");

    }

    [Fact]
    public void CacheStoreKeyShould()
    {
        // arrange
        var (services, configuration) = BuiltContainer();

        var container = new ComponentModelContainer(services);

        container.Register<ICache, MemoryCache>(CacheContext.Memory);

        container.CreateContainer();

        var loggerFactory = container.GetRequiredService<ILoggerFactory>();

        // act
        var exception = Record.Exception(() => new CacheStore(container, loggerFactory, null));

        // assert
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentNullException>();
    }

    /// <summary>
    /// End to end test!
    /// Use the complete process to store key in the CacheStore, via a MemoryCache
    /// </summary>
    [Fact]
    public void CacheExtensionFromConfigShould()
    {
        // arrange
        var (services, configuration) = BuiltContainer();

        var mockBuilder = _fixture.Freeze<Mock<IDataProtectionBuilder>>();
        mockBuilder.Setup(p => p.Services).Returns(services);

        mockBuilder.Object.PersistKeysToCache(configuration);

        var container = new ComponentModelContainer(services);

        container.Register<ICache, MemoryCache>(CacheContext.Memory);

        container.CreateContainer();

        var sut = container.GetService<IConfigureOptions<KeyManagementOptions>>();

        sut.Should().NotBeNull();

        var options = new KeyManagementOptions();

        sut!.Configure(options);

        options.XmlRepository.Should().NotBeNull();

        var element = new XElement("Data", new XAttribute("CreationDate", DateTime.UtcNow),
                            new XElement("Cert", "Begin Certficate"));

        options.XmlRepository!.StoreElement(element, "");

        var result = options.XmlRepository.GetAllElements();

        result.Count.Should().Be(1);
        result.First().Name.LocalName.Should().Be("Data");

    }

    [Fact]
    public void SectionNameNotDefinedFromConfigShould()
    {
        IServiceCollection services = new ServiceCollection();

        var config = new ConfigurationBuilder()
        .AddInMemoryCollection(
         new Dictionary<string, string?>
         {
             ["DataProtectionStore:CacheKey"] = "Key-",
             ["DataProtectionStore:CacheName"] = "Volatile"

         }).Build();

        IConfiguration configuration = new ConfigurationRoot(new List<IConfigurationProvider>(config.Providers));

        var mockBuilder = _fixture.Freeze<Mock<IDataProtectionBuilder>>();
        mockBuilder.Setup(p => p.Services).Returns(services);

        var exception = Record.Exception(() => mockBuilder.Object.PersistKeysToCache(configuration, "NotDefined"));

        exception.Should().NotBeNull();
        exception.Should().BeOfType<KeyNotFoundException>();
    }

    [Fact]
    public void BadValuesFromConfigShould()
    {
        IServiceCollection services = new ServiceCollection();

        var config = new ConfigurationBuilder()
        .AddInMemoryCollection(
         new Dictionary<string, string?>
         {
             ["DataProtectionStore:Key"] = "Key-",
             ["DataProtectionStore:Name"] = "Volatile"

         }).Build();

        IConfiguration configuration = new ConfigurationRoot(new List<IConfigurationProvider>(config.Providers));

        var mockBuilder = _fixture.Freeze<Mock<IDataProtectionBuilder>>();
        mockBuilder.Setup(p => p.Services).Returns(services);

        var exception = Record.Exception(() => mockBuilder.Object.PersistKeysToCache(configuration));

        exception.Should().NotBeNull();
        exception.Should().BeOfType<InvalidCastException>();
    }

    [Fact]
    public void CacheKeyNotDefinedFromConfigShould()
    {
        IServiceCollection services = new ServiceCollection();

        var config = new ConfigurationBuilder()
        .AddInMemoryCollection(
         new Dictionary<string, string?>
         {
             ["DataProtectionStore:CacheName"] = "Volatile"

         }).Build();

        IConfiguration configuration = new ConfigurationRoot(new List<IConfigurationProvider>(config.Providers));

        var mockBuilder = _fixture.Freeze<Mock<IDataProtectionBuilder>>();
        mockBuilder.Setup(p => p.Services).Returns(services);

        var exception = Record.Exception(() => mockBuilder.Object.PersistKeysToCache(configuration));

        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentNullException>();
    }

    private static (IServiceCollection, IConfiguration) BuiltContainer()
    {
        IServiceCollection services = new ServiceCollection();

        var config = new ConfigurationBuilder()
        .AddInMemoryCollection(
         new Dictionary<string, string?>
         {
             ["Caching:Default"] = "Volatile",
             ["Caching:Principal:CacheName"] = "Volatile",
             ["Caching:Principal:Duration"] = TimeSpan.FromSeconds(10).ToString(),
             ["Caching:Principal:IsEnabled"] = "True",

             ["Caching:Caches:0:Name"] = "Volatile",
             ["Caching:Caches:0:Kind"] = CacheContext.Memory,
             ["Caching:Caches:0:IsAutoStart"] = "True",
             ["Caching:Caches:0:Settings:SizeLimit"] = "10",

             ["DataProtectionStore:CacheKey"] = "Key-",
             ["DataProtectionStore:CacheName"] = "Volatile"

         }).Build();

        IConfiguration configuration = new ConfigurationRoot(new List<IConfigurationProvider>(config.Providers));

        services.AddLogging();
        services.AddCacheContext(configuration);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTransient<IObjectSerialization, JsonSerialization>();

        return (services, configuration);
    }
}
