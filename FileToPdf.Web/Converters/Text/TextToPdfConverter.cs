namespace FileToPdf.Web.Converters.Text;

/// <summary>
/// Простой текст → PDF: каждая строка файла становится абзацем.
/// Самый короткий пример конвертера на MigraDoc, заодно проверяет весь путь от загрузки до PDF.
/// </summary>
public sealed class TextToPdfConverter : IPdfConverter
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".txt"];

    public void Convert(Stream input, Stream output, ConversionOptions options)
    {
        var text = TextDecoder.ReadAll(input);

        var document = MigraDocPdf.CreateDocument(options);
        var section = document.LastSection;

        // Перенос длинных строк и переход на новую страницу MigraDoc делает сам.
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            section.AddParagraph(line);
        }

        MigraDocPdf.Render(document, output);
    }
}
