using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace AlfaNews.Core.Ingest;

public sealed class FetchException(string code, bool transient, string message) : Exception(message)
{
    public string Code { get; } = code;
    public bool Transient { get; } = transient;
}

public sealed record FetchedPage(string Body, Uri FinalUrl, bool Truncated);

public sealed partial class SafeFetcher : IDisposable
{
    static SafeFetcher() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public const int MaxBytes = 5 * 1024 * 1024;
    private const int MaxRedirects = 5;

    private readonly HttpClient client;
    private readonly bool allowPrivateAddresses;
    private readonly TimeSpan domainInterval;
    private readonly TimeSpan requestTimeout;
    private readonly Dictionary<string, DateTimeOffset> nextRequestAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim gate = new(1, 1);

    public SafeFetcher(bool allowPrivateAddresses = false, TimeSpan? domainInterval = null, TimeSpan? requestTimeout = null)
    {
        this.allowPrivateAddresses = allowPrivateAddresses;
        this.domainInterval = domainInterval ?? TimeSpan.FromSeconds(1);
        this.requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(15);
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = ConnectAsync
        };
        client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AlfaNewsBot/0.1 (+hackathon research)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml,application/rss+xml;q=0.9,*/*;q=0.5");
    }

    public async Task<FetchedPage> GetAsync(string url, IReadOnlyCollection<string> allowedHosts,
        CancellationToken cancellationToken)
    {
        var current = ParseAllowed(url, allowedHosts);
        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            await WaitForDomainAsync(current.Host, cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(requestTimeout);
            try
            {
                using var response = await client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    current = ParseAllowed(new Uri(current, location).ToString(), allowedHosts);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw new FetchException($"http_{(int)response.StatusCode}", (int)response.StatusCode >= 500 || (int)response.StatusCode == 429,
                        $"Ответ {(int)response.StatusCode}.");
                var (body, truncated) = await ReadLimitedAsync(response, timeout.Token, cancellationToken);
                return new FetchedPage(body, current, truncated);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new FetchException("timeout", true, "Превышено время ожидания.");
            }
            catch (HttpRequestException exception) when (exception.InnerException is FetchException inner)
            {
                throw inner;
            }
            catch (HttpRequestException exception)
            {
                throw new FetchException("network", true, exception.InnerException?.Message ?? exception.Message);
            }
        }
        throw new FetchException("too_many_redirects", false, "Слишком много перенаправлений.");
    }

    public void Dispose()
    {
        client.Dispose();
        gate.Dispose();
    }

    private static Uri ParseAllowed(string url, IReadOnlyCollection<string> allowedHosts)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new FetchException("invalid_url", false, "Некорректный адрес.");
        if (!allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new FetchException("host_not_allowed", false, $"Хост {uri.Host} не входит в список разрешённых.");
        return uri;
    }

    private async Task WaitForDomainAsync(string host, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var at = nextRequestAt.TryGetValue(host, out var scheduled) && scheduled > now ? scheduled : now;
            nextRequestAt[host] = at + domainInterval;
            if (at > now)
                await Task.Delay(at - now, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<(string Body, bool Truncated)> ReadLimitedAsync(HttpResponseMessage response,
        CancellationToken cancellationToken, CancellationToken callerToken)
    {
        if (response.Content.Headers.ContentLength > MaxBytes)
            throw new FetchException("too_large", false, "Ответ превышает допустимый размер.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        var truncated = false;
        try
        {
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes)
                    throw new FetchException("too_large", false, "Ответ превышает допустимый размер.");
            }
        }
        catch (Exception exception) when (buffer.Length > 0 && !callerToken.IsCancellationRequested
            && exception is IOException or OperationCanceledException)
        {
            truncated = true;
        }
        return (Decode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), response.Content.Headers.ContentType?.CharSet), truncated);
    }

    private static string Decode(ReadOnlySpan<byte> bytes, string? headerCharset)
    {
        var name = headerCharset?.Trim('"');
        if (string.IsNullOrWhiteSpace(name))
            name = MetaCharsetRegex().Match(Encoding.Latin1.GetString(bytes[..Math.Min(bytes.Length, 4096)])).Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                return Encoding.GetEncoding(name).GetString(bytes);
            }
            catch (ArgumentException)
            {
            }
        }
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1251).GetString(bytes);
        }
    }

    [GeneratedRegex(@"charset\s*=\s*[""']?([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MetaCharsetRegex();

    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var allowed = addresses.Where(address => allowPrivateAddresses || IsPublic(address)).ToArray();
        if (allowed.Length == 0)
            throw new FetchException("private_address", false, "Адрес источника не является публичным.");
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (address.GetAddressBytes()[0] & 0xFE) == 0xFC);
        var bytes = address.GetAddressBytes();
        return !(bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            || bytes[0] >= 224);
    }
}