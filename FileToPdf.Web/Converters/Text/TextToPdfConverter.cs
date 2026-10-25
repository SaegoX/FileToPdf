namespace FileToPdf.Web.Converters.Text;

/// <summary>
/// Простой текст → PDF: каждая строка файла становится абзацем.
/// Просто пример использования MigraDoc, без сложной верстки. Перенос длинных строк и переход на новую страницу MigraDoc делает сам.
/// </summary>
public sealed class TextToPdfConverter : IPdfConverter
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".txt"];

    /// <summary>
    /// Преобразует текстовый поток во входном формате в PDF-документ с использованием указанных параметров
    /// преобразования.
    /// </summary>
    /// <param name="input">Входной поток с исходными текстовыми данными.</param>
    /// <param name="output">Выходной поток для записи PDF-документа.</param>
    /// <param name="options">Параметры преобразования для настройки процесса конвертации.</param>
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
