using PdfSharp.Fonts;

namespace FileToPdf.Web.Fonts;

/// <summary>
/// Отдаёт PDFsharp шрифты из папки Fonts рядом с программой,
/// чтобы результат не зависел от шрифтов, установленных на сервере.
/// </summary>
public sealed class AppFontResolver : IFontResolver
{
    public const string DefaultFamily = "Noto Sans";

    private const string RegularFace = "NotoSans-Regular";
    private const string BoldFace = "NotoSans-Bold";

    // Файлы читаются один раз при старте: PDFsharp может запрашивать шрифты из разных потоков.
    private readonly Dictionary<string, byte[]> _faces;

    /// <summary>
    /// Инициализирует новый экземпляр класса AppFontResolver, загружая шрифты из указанного каталога.
    /// </summary>
    /// <param name="fontsDirectory">Путь к каталогу, содержащему файлы шрифтов.</param>
    public AppFontResolver(string fontsDirectory)
    {
        _faces = new Dictionary<string, byte[]>
        {
            [RegularFace] = File.ReadAllBytes(Path.Combine(fontsDirectory, RegularFace + ".ttf")),
            [BoldFace] = File.ReadAllBytes(Path.Combine(fontsDirectory, BoldFace + ".ttf")),
        };
    }

    // Любое семейство (Arial, Courier New, ...) сводим к Noto Sans: MigraDoc по умолчанию
    // запрашивает Arial, а других шрифтов у нас нет(курсив приходится в PDFsharp имитировать наклоном).
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new(isBold ? BoldFace : RegularFace, mustSimulateBold: false, mustSimulateItalic: isItalic);

    public byte[]? GetFont(string faceName) => _faces.GetValueOrDefault(faceName);
}
