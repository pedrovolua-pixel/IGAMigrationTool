using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Azure;
using Azure.Core;
using Azure.Core.Cryptography;
using Azure.Core.Pipeline;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal static class Program
{
    public static int Main()
    {
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        try
        {
            Dispatch.Run();
            return 0;
        }
        catch (Exception error)
        {
            // Never emit SDK exception messages or protected XML/crypto values.
            Console.WriteLine("DISPATCH FAILED type=" + error.GetType().Name);
            var frame = new System.Diagnostics.StackTrace(error, true).GetFrame(0);
            Console.WriteLine("FAILURE fixtureFrame=" + (frame?.GetMethod()?.DeclaringType?.Assembly == typeof(Program).Assembly) +
                              " sourceLine=" + frame?.GetFileLineNumber());
            return 1;
        }
    }
}

internal static class Dispatch
{
    private static int checks;

    internal static void Require(bool value, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (!value)
        {
            Console.WriteLine("ASSERT FAILED line=" + line);
            throw new InvalidOperationException("Fixture assertion failed");
        }
    }

    private static void Case(string label, Action action)
    {
        Console.WriteLine(label + " START");
        action();
        checks++;
        Console.WriteLine(label + " PASS");
    }

    internal static void Denies<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("Expected denial");
    }

    internal static IXmlRepository Repository(BlobClient client, bool factory = false)
    {
        var services = new ServiceCollection();
        var builder = services.AddDataProtection();
        if (factory)
        {
            builder.PersistKeysToAzureBlobStorage(_ => client);
        }
        else
        {
            builder.PersistKeysToAzureBlobStorage(client);
        }
        using var provider = services.BuildServiceProvider();
        var repository = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository!;
        Require(repository.GetType().FullName == "Azure.Extensions.AspNetCore.DataProtection.Blobs.AzureBlobXmlRepository");
        return repository;
    }

    internal static void Run()
    {
        foreach (var factory in new[] { false, true })
        {
            Case("DISP02-legacy-" + (factory ? "factory" : "instance"), () =>
            {
                var blob = new FixtureBlob();
                var repository = Repository(blob, factory);
                Require(repository.GetAllElements().Single().Attribute("id")!.Value == "old");
                repository.StoreElement(new XElement("key", new XAttribute("id", "new")), "fixture");
                Require(blob.Reads == 1 && blob.Writes == 1 && blob.Trap.Calls == 0);
                Require(blob.LastIfMatch == new FixtureResponse(200).Headers.ETag);
                Require(XElement.Parse(blob.Body).Elements().Count() == 2);
                Console.WriteLine("DISP07 counters reads=1 writes=1 transport=0 credential=0");
            });
        }
        foreach (var write in new[] { false, true })
        {
            Case("DISP02-modern-async-only-" + (write ? "write" : "read"), () =>
            {
                var client = new ModernOnlyBlob();
                var repository = Repository(client);
                Denies<Exception>(() =>
                {
                    if (write)
                    {
                        repository.StoreElement(new XElement("key"), "fixture");
                    }
                    else
                    {
                        repository.GetAllElements();
                    }
                });
                Require(client.Trap.Calls > 0 && client.ModernCalls == 0 && client.AsyncCalls == 0);
                Console.WriteLine("DISP07 control transport=" + client.Trap.Calls + " modern=0 async=0");
            });
        }
        foreach (var mode in new[] { "missing", "empty", "malformed", "inventory", "304" })
        {
            Case("DISP03-read-denies-" + mode, () =>
            {
                var client = new FixtureBlob { ReadMode = mode };
                Denies<GuardDenied>(() => Repository(client).GetAllElements());
                Require(client.Reads == 1 && client.Writes == 0 && client.Trap.Calls == 0);
            });
        }
        Case("DISP03-validated-304", () =>
        {
            var client = new FixtureBlob();
            var repository = Repository(client);
            repository.GetAllElements();
            client.ReadMode = "validated304";
            Require(repository.GetAllElements().Single().Attribute("id")!.Value == "old");
            Require(client.Reads == 2 && client.Trap.Calls == 0);
        });
        Case("DISP04-no-read-create-denied", () =>
        {
            var client = new FixtureBlob();
            Denies<GuardDenied>(() => Repository(client).StoreElement(new XElement("key"), "fixture"));
            Require(client.Writes == 1 && client.AcceptedWrites == 0 && client.Trap.Calls == 0);
        });
        foreach (var condition in new[] { new BlobRequestConditions(), new BlobRequestConditions { IfMatch = new ETag("") }, new BlobRequestConditions { IfMatch = ETag.All },
                     new BlobRequestConditions { IfNoneMatch = ETag.All } })
        {
            Case("DISP04-unsafe-condition-" + checks, () =>
            {
                var client = new FixtureBlob();
                using var input = new MemoryStream(Encoding.UTF8.GetBytes(client.Body));
                Denies<GuardDenied>(() => client.Upload(input, conditions: condition));
                Require(client.AcceptedWrites == 0 && client.Trap.Calls == 0);
            });
        }
        Case("DISP04-lost-fixture-inventory", () =>
        {
            var client = new FixtureBlob();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("<repository><key id='replacement'/></repository>"));
            Denies<GuardDenied>(() => client.Upload(input, conditions: new BlobRequestConditions { IfMatch = new ETag("\"fixture-v1\"") }));
            Require(client.AcceptedWrites == 0 && client.Trap.Calls == 0);
        });
        Case("DISP05-conflict-then-missing", () =>
        {
            var client = new FixtureBlob { WriteMode = "conflictThenMissing" };
            var repository = Repository(client);
            repository.GetAllElements();
            Denies<GuardDenied>(() => repository.StoreElement(new XElement("key"), "fixture"));
            Require(client.Reads == 2 && client.Writes == 1 && client.AcceptedWrites == 0 && client.Trap.Calls == 0);
        });
        Case("DISP05-repeated-conflicts", () =>
        {
            var client = new FixtureBlob { WriteMode = "conflict" };
            var repository = Repository(client);
            repository.GetAllElements();
            Denies<RequestFailedException>(() => repository.StoreElement(new XElement("key"), "fixture"));
            Require(client.Writes == 5 && client.AcceptedWrites == 0 && client.Trap.Calls == 0);
            Console.WriteLine("DISP07 conflict reads=" + client.Reads + " writes=" + client.Writes + " transport=0");
        });
        Case("DISP05-terminal-error", () =>
        {
            var client = new FixtureBlob { WriteMode = "terminal" };
            var repository = Repository(client);
            repository.GetAllElements();
            Denies<RequestFailedException>(() => repository.StoreElement(new XElement("key"), "fixture"));
            Require(client.Writes == 1 && client.AcceptedWrites == 0 && client.Trap.Calls == 0);
        });
        Case("DISP05-unguarded-missing-create-control", () =>
        {
            var client = new FixtureBlob { Guarded = false, ReadMode = "missing" };
            var repository = Repository(client);
            Require(repository.GetAllElements().Count == 0);
            repository.StoreElement(new XElement("key"), "fixture");
            Require(client.LastIfNoneMatch == ETag.All && client.AcceptedWrites == 1 && client.Trap.Calls == 0);
        });
        KeyCases();
        ResolverPipelineCases.Run();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name is
                     "Azure.Extensions.AspNetCore.DataProtection.Blobs" or "Azure.Extensions.AspNetCore.DataProtection.Keys" or
                     "Azure.Storage.Blobs" or "Azure.Core" or "Azure.Security.KeyVault.Keys" or "Microsoft.AspNetCore.DataProtection"))
        {
            Console.WriteLine("DISP01 loaded=" + assembly.GetName().Name + " version=" + assembly.GetName().Version + " sha256=" +
                              Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant());
        }
        Require(Environment.Version.ToString() == "10.0.12");
        Console.WriteLine("DISPATCH PASS checks=" + checks + " runtime=" + Environment.Version);
    }

    private static void KeyCases()
    {
        using var resolver = new FixtureResolver();
        var services = new ServiceCollection();
        services.AddDataProtection().ProtectKeysWithAzureKeyVault(new Uri(FixtureResolver.Path), _ => resolver);
        using var provider = services.BuildServiceProvider();
        var encryptor = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor!;
        Require(encryptor.GetType().FullName == "Azure.Extensions.AspNetCore.DataProtection.Keys.AzureKeyVaultXmlEncryptor");
        EncryptedXmlInfo? old = null;
        Case("DISP06-versionless-new-wrap", () =>
        {
            old = encryptor.Encrypt(new XElement("fixture", "synthetic"));
            Require(old.EncryptedElement.Element("kid")!.Value == FixtureResolver.Path + "/v1");
            Require(resolver.Resolved.SequenceEqual(new[] { FixtureResolver.Path }) && resolver.Wraps == 1);
        });
        Case("DISP06-new-version-and-historical-unwrap", () =>
        {
            resolver.CurrentVersion = "v2";
            var current = encryptor.Encrypt(new XElement("fixture", "synthetic"));
            Require(current.EncryptedElement.Element("kid")!.Value.EndsWith("/v2", StringComparison.Ordinal));
            var decryptor = (IXmlDecryptor)ActivatorUtilities.CreateInstance(provider, old!.DecryptorType);
            Require(GuardDecrypt(decryptor, old.EncryptedElement).Value == "synthetic");
            Require(resolver.Resolved.Last() == FixtureResolver.Path + "/v1" && resolver.Unwraps == 1);
            Require(resolver.AsyncCalls == 0);
        });
        foreach (var id in new[] { FixtureResolver.Path, "https://foreign.invalid/keys/wrapping/v1", FixtureResolver.Path + "/v3" })
        {
            Case("DISP06-invalid-historical-" + checks, () =>
            {
                var decryptor = (IXmlDecryptor)ActivatorUtilities.CreateInstance(provider, old!.DecryptorType);
                var value = new XElement(old.EncryptedElement);
                value.Element("kid")!.Value = id;
                var count = resolver.Resolved.Count;
                Denies<GuardDenied>(() => GuardDecrypt(decryptor, value));
                Require(resolver.Resolved.Count == count && resolver.Unwraps == 1);
            });
        }
        foreach (var id in new[] { FixtureResolver.Path, "https://foreign.invalid/keys/wrapping/v2" })
        {
            Case("DISP06-invalid-resolved-" + checks, () =>
            {
                resolver.ReturnedId = id;
                Denies<GuardDenied>(() => encryptor.Encrypt(new XElement("fixture", "synthetic")));
                Require(resolver.Wraps == 2);
            });
        }
        Console.WriteLine("DISP07 key counters resolve=" + resolver.Resolved.Count + " wrap=" + resolver.Wraps +
                          " unwrap=" + resolver.Unwraps + " async=" + resolver.AsyncCalls + " transport=0 credential=0");
    }

    private static XElement GuardDecrypt(IXmlDecryptor decryptor, XElement encrypted)
    {
        // Closed test-only binding, not the future production URI/parser contract.
        var id = encrypted.Element("kid")?.Value;
        if (id != FixtureResolver.Path + "/v1" && id != FixtureResolver.Path + "/v2")
        {
            throw new GuardDenied();
        }
        return decryptor.Decrypt(encrypted);
    }
}

internal sealed class GuardDenied : Exception;

internal sealed class TrapTransport : HttpPipelineTransport
{
    internal int Calls { get; private set; }
    public override Request CreateRequest() => HttpClientTransport.Shared.CreateRequest();
    public override void Process(HttpMessage message)
    {
        Calls++;
        throw new GuardDenied();
    }
    public override ValueTask ProcessAsync(HttpMessage message)
    {
        Process(message);
        return ValueTask.CompletedTask;
    }
}

internal class FixtureBlob : BlobClient
{
    internal TrapTransport Trap { get; }
    internal int Reads { get; private set; }
    internal int Writes { get; private set; }
    internal int AcceptedWrites { get; private set; }
    internal bool Guarded { get; init; } = true;
    internal string ReadMode { get; set; } = "valid";
    internal string WriteMode { get; init; } = "valid";
    internal string Body { get; private set; } = "<repository><key id='old'/></repository>";
    internal ETag? LastIfMatch { get; private set; }
    internal ETag? LastIfNoneMatch { get; private set; }
    private bool observed;

    internal FixtureBlob() : this(new TrapTransport()) { }
    private FixtureBlob(TrapTransport trap) : base(new Uri("https://synthetic.invalid/ring/object"),
        new BlobClientOptions { Transport = trap, Retry = { MaxRetries = 0 } }) => Trap = trap;

    public override Response DownloadTo(Stream destination, BlobRequestConditions conditions = default!,
        StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        Reads++;
        if (ReadMode == "missing")
        {
            if (Guarded)
            {
                throw new GuardDenied();
            }
            throw new RequestFailedException(404, "Synthetic missing");
        }
        if (ReadMode == "304")
        {
            throw new GuardDenied();
        }
        if (ReadMode == "validated304")
        {
            Dispatch.Require(observed && conditions?.IfNoneMatch == new FixtureResponse(200).Headers.ETag);
            return new FixtureResponse(304);
        }
        var downloaded = ReadMode switch
        {
            "empty" => "",
            "malformed" => "<repository>",
            "inventory" => "<repository><key id='replacement'/></repository>",
            _ => Body
        };
        ValidateInventory(downloaded);
        destination.Write(Encoding.UTF8.GetBytes(downloaded));
        observed = true;
        return new FixtureResponse(200);
    }

    public override Response<BlobContentInfo> Upload(Stream content, BlobHttpHeaders httpHeaders = default!,
        IDictionary<string, string> metadata = default!, BlobRequestConditions conditions = default!,
        IProgress<long> progressHandler = default!, AccessTier? accessTier = default,
        StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        Writes++;
        LastIfMatch = conditions?.IfMatch;
        LastIfNoneMatch = conditions?.IfNoneMatch;
        if (Guarded && (conditions?.IfMatch is null || string.IsNullOrEmpty(conditions.IfMatch.Value.ToString()) ||
                       conditions.IfMatch == ETag.All || conditions.IfNoneMatch is not null))
        {
            throw new GuardDenied();
        }
        using var reader = new StreamReader(content, leaveOpen: true);
        var value = reader.ReadToEnd();
        if (Guarded)
        {
            ValidateInventory(value);
        }
        if (WriteMode == "conflictThenMissing")
        {
            ReadMode = "missing";
            throw new RequestFailedException(412, "Synthetic conflict");
        }
        if (WriteMode == "conflict")
        {
            throw new RequestFailedException(412, "Synthetic conflict");
        }
        if (WriteMode == "terminal")
        {
            throw new RequestFailedException(503, "Synthetic terminal");
        }
        AcceptedWrites++;
        Body = value;
        return Response.FromValue(BlobsModelFactory.BlobContentInfo(new ETag("\"fixture-v2\""),
            DateTimeOffset.UnixEpoch, [], "fixture", "fixture", "fixture", 0), new FixtureResponse(201));
    }

    private static void ValidateInventory(string value)
    {
        try
        {
            if (!XElement.Parse(value).Elements("key").Any(e => e.Attribute("id")?.Value == "old"))
            {
                throw new GuardDenied();
            }
        }
        catch (System.Xml.XmlException)
        {
            throw new GuardDenied();
        }
    }
}

internal sealed class ModernOnlyBlob : BlobClient
{
    internal TrapTransport Trap { get; }
    internal int ModernCalls { get; private set; }
    internal int AsyncCalls { get; private set; }
    internal ModernOnlyBlob() : this(new TrapTransport()) { }
    private ModernOnlyBlob(TrapTransport trap) : base(new Uri("https://synthetic.invalid/ring/object"),
        new BlobClientOptions { Transport = trap, Retry = { MaxRetries = 0 } }) => Trap = trap;
    public override Response DownloadTo(Stream destination, BlobDownloadToOptions options, CancellationToken cancellationToken = default)
    {
        ModernCalls++;
        throw new GuardDenied();
    }
    public override Task<Response> DownloadToAsync(Stream destination, BlobDownloadToOptions options, CancellationToken cancellationToken = default)
    {
        AsyncCalls++;
        throw new GuardDenied();
    }
    public override Response<BlobContentInfo> Upload(Stream content, BlobUploadOptions options, CancellationToken cancellationToken = default)
    {
        ModernCalls++;
        throw new GuardDenied();
    }
    public override Task<Response<BlobContentInfo>> UploadAsync(Stream content, BlobUploadOptions options, CancellationToken cancellationToken = default)
    {
        AsyncCalls++;
        throw new GuardDenied();
    }
}

internal sealed class FixtureResponse(int status) : Response
{
    public override int Status => status;
    public override string ReasonPhrase => "Synthetic";
    public override Stream? ContentStream { get; set; }
    public override string ClientRequestId { get; set; } = "fixture";
    public override void Dispose() { }
    protected override bool ContainsHeader(string name) => name.Equals("ETag", StringComparison.OrdinalIgnoreCase);
    protected override bool TryGetHeader(string name, out string value)
    {
        value = "\"fixture-v1\"";
        return ContainsHeader(name);
    }
    protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
    {
        values = ["\"fixture-v1\""];
        return ContainsHeader(name);
    }
    protected override IEnumerable<HttpHeader> EnumerateHeaders() => [new HttpHeader("ETag", "\"fixture-v1\"")];
}

internal sealed class FixtureResolver : IKeyEncryptionKeyResolver, IDisposable
{
    internal const string Path = "https://synthetic-vault.invalid/keys/wrapping";
    // Different test-only key material ensures historical unwrap cannot substitute the current version.
    private readonly Dictionary<string, RSA> keys = new() { ["v1"] = RSA.Create(2048), ["v2"] = RSA.Create(2048) };
    internal string CurrentVersion { get; set; } = "v1";
    internal string? ReturnedId { get; set; }
    internal List<string> Resolved { get; } = [];
    internal int Wraps { get; set; }
    internal int Unwraps { get; set; }
    internal int AsyncCalls { get; set; }
    public IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default)
    {
        Resolved.Add(keyId);
        var id = keyId == Path ? ReturnedId ?? Path + "/" + CurrentVersion : keyId;
        if (id != Path + "/v1" && id != Path + "/v2")
        {
            throw new GuardDenied();
        }
        return new FixtureKey(id, keys[id == Path + "/v1" ? "v1" : "v2"], this);
    }
    public Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default)
    {
        AsyncCalls++;
        throw new GuardDenied();
    }
    public void Dispose()
    {
        foreach (var key in keys.Values)
        {
            key.Dispose();
        }
    }
}

internal sealed class FixtureKey(string id, RSA rsa, FixtureResolver owner) : IKeyEncryptionKey
{
    public string KeyId => id;
    public byte[] WrapKey(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
    {
        Dispatch.Require(algorithm == "RSA-OAEP");
        owner.Wraps++;
        return rsa.Encrypt(key.Span, RSAEncryptionPadding.OaepSHA1);
    }
    public byte[] UnwrapKey(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default)
    {
        Dispatch.Require(algorithm == "RSA-OAEP");
        owner.Unwraps++;
        return rsa.Decrypt(encryptedKey.Span, RSAEncryptionPadding.OaepSHA1);
    }
    public Task<byte[]> WrapKeyAsync(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
    {
        owner.AsyncCalls++;
        throw new GuardDenied();
    }
    public Task<byte[]> UnwrapKeyAsync(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default)
    {
        owner.AsyncCalls++;
        throw new GuardDenied();
    }
}
