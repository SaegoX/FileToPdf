using System.Text;

namespace FileToPdf.Web.Converters;

/// <summary>
/// Читает текстовый файл, сам определяя кодировку: UTF-8, UTF-16 (по BOM) или Windows-1251,
/// в которой до сих пор сохраняют многие русские программы, включая Excel.
/// </summary>
public static class TextDecoder
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding Windows1251;

    static TextDecoder()
    {
        // В .NET старые кодировки вроде 1251 по умолчанию недоступны, их нужно подключить.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1251 = Encoding.GetEncoding(1251);
    }

    public static string ReadAll(Stream input)
    {
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        ReadOnlySpan<byte> bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);

        if (bytes.StartsWith(Encoding.UTF8.Preamble))
            return Encoding.UTF8.GetString(bytes[Encoding.UTF8.Preamble.Length..]);
        if (bytes.StartsWith(Encoding.Unicode.Preamble))
            return Encoding.Unicode.GetString(bytes[Encoding.Unicode.Preamble.Length..]);

        // Без BOM: русский текст в 1251 почти никогда не бывает корректным UTF-8,
        // поэтому пробуем строгий UTF-8 и при ошибке читаем как 1251.
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Windows1251.GetString(bytes);
        }
    }
}
