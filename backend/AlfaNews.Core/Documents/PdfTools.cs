using System.Diagnostics;
using System.Globalization;
using AlfaNews.Core.Access;

namespace AlfaNews.Core.Documents;

public sealed class PdfTools(PdfOptions options)
{
    public async Task<int> PageCountAsync(string path, CancellationToken cancellationToken)
    {
        var output = await RunAsync(options.PdfInfoPath, [path], cancellationToken);
        var line = output.Split('\n').SingleOrDefault(line => line.StartsWith("Pages:", StringComparison.Ordinal));
        if (line is null || !int.TryParse(line[6..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            || count is < 1 or > 50)
            throw new AccessException(400, "invalid_input", "PDF должен содержать от 1 до 50 страниц.");
        return count;
    }

    public async Task RenderAsync(string pdfPath, int pageNo, string imagePath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        var temporary = imagePath + "." + Guid.NewGuid().ToString("N");
        try
        {
            await RunAsync(options.PdfToPpmPath,
                ["-f", pageNo.ToString(CultureInfo.InvariantCulture), "-l", pageNo.ToString(CultureInfo.InvariantCulture),
                    "-scale-to", "2200", "-singlefile", "-png", pdfPath, temporary], cancellationToken);
            if (!File.Exists(temporary + ".png") || new FileInfo(temporary + ".png").Length > 16 * 1024 * 1024)
                throw new InvalidOperationException("Некорректное изображение страницы.");
            File.Move(temporary + ".png", imagePath, true);
        }
        finally
        {
            File.Delete(temporary + ".png");
        }
    }

    private async Task<string> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.Environment["LC_ALL"] = "C";
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить Poppler.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.ProcessTimeoutSeconds));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var result = await output;
            await error;
            if (process.ExitCode != 0)
                throw new AccessException(400, "invalid_input", "PDF повреждён, зашифрован или не поддерживается.");
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AccessException(400, "processing_failed", "Превышено время обработки PDF.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}