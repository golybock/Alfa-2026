using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AlfaNews.Tests;

public sealed record LocalResponse(int Status, string Body, string ContentType = "text/html; charset=utf-8", string? Location = null,
    int StallAfterBodyMs = 0, bool DeclareLongerBody = false, Encoding? BodyEncoding = null);

public sealed class LocalHttpServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Func<string, LocalResponse> handler;
    private readonly Task loop;

    public LocalHttpServer(Func<string, LocalResponse> handler)
    {
        this.handler = handler;
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        BaseUrl = $"http://127.0.0.1:{port}";
        listener.Prefixes.Add(BaseUrl + "/");
        listener.Start();
        loop = Task.Run(RunAsync);
    }

    public string BaseUrl { get; }

    public List<string> Requests { get; } = [];

    public void Dispose()
    {
        listener.Close();
        try
        {
            loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
    }

    private async Task RunAsync()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                return;
            }
            var path = context.Request.Url!.PathAndQuery;
            lock (Requests)
                Requests.Add(path);
            var response = handler(path);
            context.Response.StatusCode = response.Status;
            context.Response.ContentType = response.ContentType;
            if (response.Location is not null)
                context.Response.Headers["Location"] = response.Location;
            try
            {
                var bytes = (response.BodyEncoding ?? Encoding.UTF8).GetBytes(response.Body);
                context.Response.ContentLength64 = bytes.Length + (response.DeclareLongerBody ? 5000 : 0);
                await context.Response.OutputStream.WriteAsync(bytes);
                await context.Response.OutputStream.FlushAsync();
                if (response.StallAfterBodyMs > 0)
                    await Task.Delay(response.StallAfterBodyMs);
                if (response.DeclareLongerBody)
                    context.Response.Abort();
                else
                    context.Response.Close();
            }
            catch (Exception exception) when (exception is HttpListenerException or IOException or ObjectDisposedException)
            {
                context.Response.Abort();
            }
        }
    }
}