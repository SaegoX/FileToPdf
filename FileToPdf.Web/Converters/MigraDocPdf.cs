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
        section.PageSetup.TopMargin = section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);

        return document;
    }

    /// <summary>Раскладывает документ по страницам через PDFsharp и пишет PDF в поток.</summary>
    public static void Render(Document document, Stream output)
    {
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(output, closeStream: false);
    }
}
