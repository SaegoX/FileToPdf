namespace FileToPdf.Web.Converters;

/// <summary>Находит конвертер по расширению файла среди всех зарегистрированных.</summary>
public sealed class ConverterResolver
{
    private readonly Dictionary<string, IPdfConverter> _byExtension = new(StringComparer.OrdinalIgnoreCase);

    public ConverterResolver(IEnumerable<IPdfConverter> converters)
    {
        foreach (var converter in converters)
        {
            foreach (var extension in converter.SupportedExtensions)
            {
                // Add, а не индексатор: два конвертера на одно расширение — ошибка, пусть падает при старте.
                _byExtension.Add(extension, converter);
            }
        }
    }

    public IReadOnlyCollection<string> SupportedExtensions => _byExtension.Keys;

    public IPdfConverter? FindFor(string fileName) =>
        _byExtension.GetValueOrDefault(Path.GetExtension(fileName));
}
