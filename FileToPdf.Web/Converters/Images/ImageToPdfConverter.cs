using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace FileToPdf.Web.Converters.Images;

/// <summary>
/// PNG/JPEG → PDF: одна страница A4, картинка вписана в поля с сохранением пропорций и по центру.
/// MigraDoc здесь не нужен: на странице один объект, его координаты проще посчитать самим.
/// </summary>
public sealed class ImageToPdfConverter : IPdfConverter
{
    // Размеры A4 и поля 1,5 см в пунктах (1 пт = 1/72 дюйма), как в MigraDocPdf.
    private const double A4ShortSide = 595.276;
    private const double A4LongSide = 841.89;
    private const double Margin = 42.52;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".png", ".jpg", ".jpeg"];

    public void Convert(Stream input, Stream output, ConversionOptions options)
    {
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);

        // Проверяем тип по первым байтам, а не по расширению: переименованный файл не пройдёт.
        var header = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        if (!header.StartsWith(PngSignature) && !header.StartsWith(JpegSignature))
            throw new ConversionException("Файл не является изображением PNG или JPEG.");

        buffer.Position = 0;
        using var image = LoadImage(buffer);

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(options.Landscape ? A4LongSide : A4ShortSide);
        page.Height = XUnit.FromPoint(options.Landscape ? A4ShortSide : A4LongSide);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var area = new XRect(Margin, Margin, page.Width.Point - 2 * Margin, page.Height.Point - 2 * Margin);

            // Размер картинки в пунктах PDFsharp считает с учётом её DPI: скан 300 dpi формата A4 займёт ровно лист.
            // Большую картинку уменьшаем, маленькую не растягиваем, чтобы иконка не превратилась в кашу из пикселей.
            var scale = Math.Min(1, Math.Min(area.Width / image.PointWidth, area.Height / image.PointHeight));
            var width = image.PointWidth * scale;
            var height = image.PointHeight * scale;

            gfx.DrawImage(image, area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height);
        }

        document.Save(output, closeStream: false);
    }

    private static XImage LoadImage(Stream stream)
    {
        try
        {
            return XImage.FromStream(stream);
        }
        catch (Exception ex)
        {
            // Подпись файла верная, но внутри мусор или вариант формата, который PDFsharp не умеет читать.
            throw new ConversionException("Не удалось прочитать изображение: файл повреждён или его вариант формата не поддерживается.", ex);
        }
    }
}
