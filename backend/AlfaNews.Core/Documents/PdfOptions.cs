namespace AlfaNews.Core.Documents;

public sealed class PdfOptions
{
    private readonly string baseDirectory = FindBaseDirectory();
    public string RootPath { get; set; } = "data/cache/research";
    public string InputPath { get; set; } = "data";
    public string PdfInfoPath { get; set; } = "pdfinfo";
    public string PdfToPpmPath { get; set; } = "pdftoppm";
    public int ProcessTimeoutSeconds { get; set; } = 120;
    public const long MaxFileBytes = 50L * 1024 * 1024;
    // Каждая страница — отдельный платный запрос распознавания; стартовые обзоры длиннее приходят готовыми из миграции.
    public const int MaxPages = 10;
    public string InputDirectory => Path.GetFullPath(InputPath, baseDirectory);

    public string Resolve(string relativePath)
    {
        var root = Path.GetFullPath(RootPath, baseDirectory);
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Недопустимый путь документа.");
        return resolved;
    }

    private static string FindBaseDirectory()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Alfa-2026.slnx")))
                return directory.FullName;
        return Directory.GetCurrentDirectory();
    }
}