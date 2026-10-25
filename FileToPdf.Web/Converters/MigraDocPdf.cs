using System.Text;
using FileToPdf.Web.Fonts;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace FileToPdf.Web.Converters;

/// <summary>
/// Общие шаги для конвертеров на MigraDoc. MigraDoc описывает документ как структуру
/// (разделы, абзацы, таблицы), а раскладку по страницам и переносы делает сам при рендеринге.
/// </summary>
public static class MigraDocPdf
{
    private const double MarginCm = 1.5;
    private const int LongWordChunk = 10;
    private const char ZeroWidthSpace = (char)0x200B;

    /// <summary>Ширина области между полями: нужна, чтобы разделить её между столбцами таблицы.</summary>
    public static Unit ContentWidth(ConversionOptions options) =>
        Unit.FromCentimeter((options.Landscape ? 29.7 : 21.0) - 2 * MarginCm);

    /// <summary>Пустой документ A4 с одним разделом, нашим шрифтом и выбранной ориентацией.</summary>
    public static Document CreateDocument(ConversionOptions options)
    {
        var document = new Document();

        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = AppFontResolver.DefaultFamily;
        normal.Font.Size = 10;

        // Не копируем DefaultPageSetup (так делают старые примеры): в MigraDoc 6 явно заданные
        // ширина и высота страницы важнее Orientation, и альбомная ориентация бы игнорировалась.
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = options.Landscape ? Orientation.Landscape : Orientation.Portrait;
        section.PageSetup.TopMargin = section.PageSetup.BottomMargin = Unit.FromCentimeter(MarginCm);
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromCentimeter(MarginCm);

        return document;
    }

    /// <summary>
    /// MigraDoc переносит строки только по пробелам, поэтому длинная ссылка или код без пробелов
    /// вылезает за край ячейки или страницы. Вставляем в такие слова невидимые места переноса
    /// (пробел нулевой ширины, U+200B): на печати его не видно.
    /// </summary>
    public static string AllowBreaksInLongWords(string text)
    {
        var result = new StringBuilder(text.Length + text.Length / LongWordChunk);
        var wordStart = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && !char.IsWhiteSpace(text[i]))
                continue;

            AppendWord(result, text.AsSpan(wordStart, i - wordStart));
            if (i < text.Length)
                result.Append(text[i]);
            wordStart = i + 1;
        }

        return result.ToString();
    }

    private static void AppendWord(StringBuilder result, ReadOnlySpan<char> word)
    {
        // Короткие слова и числа вроде «12,50» не трогаем: при копировании из PDF они останутся чистыми.
        if (word.Length <= LongWordChunk)
        {
            result.Append(word);
            return;
        }

        var run = 0;
        foreach (var c in word)
        {
            if (run == LongWordChunk)
            {
                result.Append(ZeroWidthSpace);
                run = 0;
            }

            result.Append(c);
            run++;

            // После разделителей списков и путей переносить естественнее всего: «7915;7918;8050», «a/b/c».
            if (c is ',' or ';' or '/')
            {
                result.Append(ZeroWidthSpace);
                run = 0;
            }
        }
    }

    /// <summary>Раскладывает документ по страницам через PDFsharp и пишет PDF в поток.</summary>
    public static void Render(Document document, Stream output)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(output, closeStream: false);
    }
}
