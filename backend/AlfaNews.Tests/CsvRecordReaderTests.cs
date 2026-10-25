using System.Text;
using AlfaNews.Core.Importing;

namespace AlfaNews.Tests;

public sealed class CsvRecordReaderTests
{
    [Fact]
    public async Task ReadsBomQuotedDelimitersEscapedQuotesAndMultilineFields()
    {
        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes("\uFEFF\"id\";value\r\na;\"текст; с \"\"кавычкой\"\"\nи переносом\"\r\nb;"));
        using var reader = new CsvRecordReader(stream);
        Assert.Equal(["id", "value"], Assert.IsType<string[]>(await reader.ReadAsync(CancellationToken.None)));
        Assert.Equal(["a", "текст; с \"кавычкой\"\nи переносом"],
            Assert.IsType<string[]>(await reader.ReadAsync(CancellationToken.None)));
        Assert.Equal(["b", ""], Assert.IsType<string[]>(await reader.ReadAsync(CancellationToken.None)));
        Assert.Null(await reader.ReadAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("a;\"unterminated")]
    [InlineData("a;b\"c")]
    [InlineData("a;\"b\"c")]
    public async Task RejectsMalformedQuoting(string csv)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        using var reader = new CsvRecordReader(stream);
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RejectsInvalidUtf8()
    {
        await using var stream = new MemoryStream([0xff, 0xfe, 0x61]);
        using var reader = new CsvRecordReader(stream);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => reader.ReadAsync(CancellationToken.None));
    }
}