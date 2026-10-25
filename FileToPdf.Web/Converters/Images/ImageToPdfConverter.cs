using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace FileToPdf.Web.Converters.Images;

/// <summary>
/// PNG/JPEG → PDF: одна страница A4, картинка вписана в поля с сохранением пропорций и по центру.
/// MigraDoc здесь не нужен: на странице один объект, его координаты проще посчитать самим.
/// </summary>
public sealed class ImageToPdfConverter : IPdfConverter
{
    // Размеры A4 и поля 1,5 см в пунктах (1 пт = 1/72 дюйма), стандарт взят из MigraDocPdf.
    private const double A4ShortSide = 595.276;
    private const double A4LongSide = 841.89;
    private const double Margin = 42.52;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".png", ".jpg", ".jpeg"];

    /// <summary>
    /// Преобразует изображение из потока PNG или JPEG в PDF с учётом ориентации и параметров конвертации.
    /// </summary>
    /// <remarks>Exif-ориентация JPEG учитывается вручную, так как PDFsharp не поддерживает её
    /// чтение.</remarks>
    /// <param name="input">Входной поток с изображением PNG или JPEG.</param>
    /// <param name="output">Выходной поток для записи PDF-документа.</param>
    /// <param name="options">Параметры конвертации.</param>
    /// <exception cref="ConversionException">Выбрасывается, если входной файл не является изображением PNG или JPEG.</exception>
    public void Convert(Stream input, Stream output, ConversionOptions options)
    {
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);

        // Проверяем тип по первым байтам, а не по расширению: переименованный файл не пройдёт.
        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var isJpeg = bytes.StartsWith(JpegSignature);
        if (!isJpeg && !bytes.StartsWith(PngSignature))
            throw new ConversionException("Файл не является изображением PNG или JPEG.");

        var rotation = isJpeg ? JpegOrientation.GetClockwiseRotation(bytes) : 0;

        buffer.Position = 0;
        using var image = LoadImage(buffer);

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(options.Landscape ? A4LongSide : A4ShortSide);
        page.Height = XUnit.FromPoint(options.Landscape ? A4ShortSide : A4LongSide);

        /// Exif ориентация JPEG задаёт, как картинка должна быть повернута при отображении. PDFsharp не умеет читать Exif, поэтому поворачиваем картинку сами.
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var area = new XRect(Margin, Margin, page.Width.Point - 2 * Margin, page.Height.Point - 2 * Margin);

            // При повороте на 90° или 270° ширина и высота картинки на листе меняются местами.
            var sideways = rotation is 90 or 270;
            var shownWidth = sideways ? image.PointHeight : image.PointWidth;
            var shownHeight = sideways ? image.PointWidth : image.PointHeight;

            // Размер картинки в пунктах PDFsharp считает с учётом её DPI: скан 300 dpi формата A4 займёт ровно лист.
            // Большую картинку уменьшаем, маленькую не растягиваем, чтобы иконка не превратилась в кашу из пикселей.
            var scale = Math.Min(1, Math.Min(area.Width / shownWidth, area.Height / shownHeight));
            var width = image.PointWidth * scale;
            var height = image.PointHeight * scale;

            // Переносим начало координат в центр области, поворачиваем и рисуем картинку вокруг этого центра.
            gfx.TranslateTransform(area.X + area.Width / 2, area.Y + area.Height / 2);
            gfx.RotateTransform(rotation);
            gfx.DrawImage(image, -width / 2, -height / 2, width, height);
        }

        document.Save(output, closeStream: false);
    }

    /// <summary>
    /// Загружает изображение из указанного потока.
    /// </summary>
    /// <param name="stream">Поток, содержащий данные изображения.</param>
    /// <returns>Объект XImage, созданный из потока.</returns>
    /// <exception cref="ConversionException">Выбрасывается, если изображение повреждено или его формат не поддерживается.</exception>
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
