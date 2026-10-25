using System.Text;

namespace AlfaNews.Core.Importing;

public sealed class CsvRecordReader(Stream stream) : IDisposable
{
    private readonly StreamReader reader = new(stream, new UTF8Encoding(false, true), false, 65536, true);
    private bool firstLine = true;

    public async Task<string[]?> ReadAsync(CancellationToken cancellationToken)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closed = false;
        var started = false;
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                if (quoted)
                    throw new InvalidDataException("Незакрытая кавычка в CSV.");
                return started ? throw new InvalidDataException("Незавершённая запись CSV.") : null;
            }

            started = true;
            if (firstLine)
            {
                line = line.TrimStart('\uFEFF');
                firstLine = false;
            }

            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (quoted)
                {
                    if (character != '"')
                        field.Append(character);
                    else if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        closed = true;
                    }
                }
                else if (character == ';')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    closed = false;
                }
                else if (character == '"' && field.Length == 0 && !closed)
                    quoted = true;
                else if (character == '"' || closed)
                    throw new InvalidDataException("Некорректное экранирование в CSV.");
                else
                    field.Append(character);
            }

            if (quoted)
                field.Append('\n');
            else
            {
                fields.Add(field.ToString());
                return fields.ToArray();
            }
        }
    }

    public void Dispose() => reader.Dispose();
}