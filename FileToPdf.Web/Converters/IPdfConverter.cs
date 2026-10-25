namespace FileToPdf.Web.Converters;

/// <summary>
/// Конвертер одного вида файлов в PDF. Новый формат = новый класс с этим интерфейсом,
/// зарегистрированный в Program.cs; остальной код менять не нужно (паттерн «Стратегия»).
/// </summary>
public interface IPdfConverter
{
    /// <summary>Расширения файлов с точкой, например ".csv".</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>Читает исходный файл из <paramref name="input"/> и пишет готовый PDF в <paramref name="output"/>.</summary>
    /// <exception cref="ConversionException">Файл повреждён или его нельзя сконвертировать.</exception>
    void Convert(Stream input, Stream output, ConversionOptions options);
}

/// <summary>Настройки, которые пользователь выбирает на странице.</summary>
public sealed record ConversionOptions(bool Landscape = false);

/// <summary>
/// Ошибка во входном файле. Её текст показывается пользователю,
/// поэтому он должен быть понятным и без технических подробностей.
/// </summary>
public sealed class ConversionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
