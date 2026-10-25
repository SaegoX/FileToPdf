namespace FileToPdf.Web.Converters.Csv;

/// <summary>CSV → таблица в PDF. Кодировку и разделитель определяем сами, первая строка — заголовок.</summary>
public sealed class CsvToPdfConverter : IPdfConverter
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".csv"];

    public void Convert(Stream input, Stream output, ConversionOptions options)
    {
        var rows = CsvParser.Parse(TextDecoder.ReadAll(input));
        if (rows.Count == 0)
            throw new ConversionException("В файле нет данных.");

        PdfTable.Render(rows, options, output);
    }
}
