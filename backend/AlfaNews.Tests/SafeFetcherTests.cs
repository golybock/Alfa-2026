using System.Diagnostics;
using AlfaNews.Core.Ingest;

namespace AlfaNews.Tests;

public sealed class SafeFetcherTests
{
    private static readonly string[] Loopback = ["127.0.0.1"];

    [Fact]
    public async Task FetchesAllowedHostAndRejectsOtherHostsAndPrivateAddresses()
    {
        using var server = new LocalHttpServer(path => new LocalResponse(200, "<html>ok</html>"));
        using var fetcher = new SafeFetcher(allowPrivateAddresses: true, TimeSpan.Zero);

        var page = await fetcher.GetAsync(server.BaseUrl + "/a", Loopback, CancellationToken.None);
        Assert.Equal("<html>ok</html>", page.Body);

        var foreign = await Assert.ThrowsAsync<FetchException>(() =>
            fetcher.GetAsync(server.BaseUrl.Replace("127.0.0.1", "localhost") + "/a", Loopback,
                CancellationToken.None));
        Assert.Equal("host_not_allowed", foreign.Code);

        using var strict = new SafeFetcher(allowPrivateAddresses: false, TimeSpan.Zero);
        var privateAddress = await Assert.ThrowsAsync<FetchException>(() =>
            strict.GetAsync(server.BaseUrl + "/a", Loopback, CancellationToken.None));
        Assert.Equal("private_address", privateAddress.Code);
        Assert.False(privateAddress.Transient);
    }

    [Fact]
    public async Task RedirectsAreCheckedAgainstAllowedHosts()
    {
        using var server = new LocalHttpServer(path => path switch
        {
            "/ok" => new LocalResponse(302, string.Empty, Location: "/final"),
            "/final" => new LocalResponse(200, "final"),
            _ => new LocalResponse(302, string.Empty, Location: "http://localhost:1/stolen")
        });
        using var fetcher = new SafeFetcher(allowPrivateAddresses: true, TimeSpan.Zero);

        Assert.Equal("final", (await fetcher.GetAsync(server.BaseUrl + "/ok", Loopback, CancellationToken.None)).Body);
        var blocked = await Assert.ThrowsAsync<FetchException>(() =>
            fetcher.GetAsync(server.BaseUrl + "/away", Loopback, CancellationToken.None));
        Assert.Equal("host_not_allowed", blocked.Code);
    }

    [Fact]
    public async Task OversizedResponseAndHttpErrorsAreClassified()
    {
        using var server = new LocalHttpServer(path => path switch
        {
            "/big" => new LocalResponse(200, new string('x', SafeFetcher.MaxBytes + 10)),
            "/missing" => new LocalResponse(404, "no"),
            _ => new LocalResponse(503, "busy")
        });
        using var fetcher = new SafeFetcher(allowPrivateAddresses: true, TimeSpan.Zero);

        var big = await Assert.ThrowsAsync<FetchException>(() =>
            fetcher.GetAsync(server.BaseUrl + "/big", Loopback, CancellationToken.None));
        Assert.Equal("too_large", big.Code);
        var missing = await Assert.ThrowsAsync<FetchException>(() =>
            fetcher.GetAsync(server.BaseUrl + "/missing", Loopback, CancellationToken.None));
        Assert.Equal("http_404", missing.Code);
        Assert.False(missing.Transient);
        var busy = await Assert.ThrowsAsync<FetchException>(() =>
            fetcher.GetAsync(server.BaseUrl + "/busy", Loopback, CancellationToken.None));
        Assert.True(busy.Transient);
    }

    [Fact]
    public async Task DecodesWindows1251PagesByMetaTagAndByUtf8Fallback()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var cyrillic = System.Text.Encoding.GetEncoding(1251);
        using var server = new LocalHttpServer(path => path == "/meta"
            ? new LocalResponse(200,
                "<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=windows-1251\"></head><body>Привет, мир</body></html>",
                "text/html", BodyEncoding: cyrillic)
            : new LocalResponse(200, "<html><body>Привет, мир</body></html>", "text/html", BodyEncoding: cyrillic));
        using var fetcher = new SafeFetcher(allowPrivateAddresses: true, TimeSpan.Zero);

        Assert.Contains("Привет, мир",
            (await fetcher.GetAsync(server.BaseUrl + "/meta", Loopback, CancellationToken.None)).Body);
        Assert.Contains("Привет, мир",
            (await fetcher.GetAsync(server.BaseUrl + "/plain", Loopback, CancellationToken.None)).Body);
    }

    [Fact]
    public async Task StalledOrCutBodyReturnsReceivedPartWhenSomethingArrived()
    {
        const string partial = "<rss><channel><item><title>A</title><link>http://127.0.0.1/a</link></item><item><ti";
        using var server = new LocalHttpServer(path => path == "/cut"
            ? new LocalResponse(200, partial, StallAfterBodyMs: 3000, DeclareLongerBody: true)
            : new LocalResponse(200, string.Empty, StallAfterBodyMs: 3000, DeclareLongerBody: true));
        using var fetcher = new SafeFetcher(allowPrivateAddresses: true, TimeSpan.Zero, TimeSpan.FromMilliseconds(800));

        var page = await fetcher.GetAsync(server.BaseUrl + "/cut", Loopback, CancellationToken.None);
        Assert.True(page.Truncated);
        Assert.Equal(partial, page.Body);
        Assert.Single(RssReader.Parse(page.Body));

        var empty = await Assert.ThrowsAsync<FetchException>(() =>
            fetcher.GetAsync(server.BaseUrl + "/none", Loopback, CancellationToken.None));
        Assert.True(empty.Transient);
    }

    [Fact]
    public async Task RequestsToOneDomainAreSpacedByTheConfiguredInterval()
    {
        using var server = new LocalHttpServer(path => new LocalResponse(200, "ok"));
        using var fetcher = new SafeFetcher(allowPrivateAddresses: true, TimeSpan.FromMilliseconds(400));
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 3; i++)
            await fetcher.GetAsync(server.BaseUrl + "/" + i, Loopback, CancellationToken.None);
        Assert.True(watch.ElapsedMilliseconds >= 750, $"Прошло {watch.ElapsedMilliseconds} мс.");
    }
}