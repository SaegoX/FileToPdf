using System.Xml;
using System.Xml.Linq;

namespace FileToPdf.Web.Converters.Xml;

/// <summary>
/// XML → PDF. Если файл — список однотипных записей, выводим таблицу
/// (столбцы — атрибуты и простые вложенные элементы), иначе — дерево с отступами.
/// </summary>
public sealed class XmlToPdfConverter : IPdfConverter
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = [".xml"];

    public void Convert(Stream input, Stream output, ConversionOptions options)
    {
        var root = Load(input);

        var records = XmlRecords.Find(root);
        if (records is not null)
            PdfTable.Render(XmlRecords.ToRows(records), options, output);
        else
            XmlTreeRenderer.Render(root, options, output);
    }

    private static XElement Load(Stream input)
    {
        var settings = new XmlReaderSettings
        {
            // DTD запрещаем: иначе маленький файл с вложенными сущностями («XML-бомба»)
            // разворачивается в гигабайты, а внешние ссылки могут читать файлы с сервера.
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        try
        {
            // Кодировку XmlReader определяет сам: по BOM или по объявлению <?xml encoding="..."?>.
            using var reader = XmlReader.Create(input, settings);
            return XDocument.Load(reader).Root!;
        }
        catch (XmlException ex)
        {
            var where = ex.LineNumber > 0 ? $" (строка {ex.LineNumber}, позиция {ex.LinePosition})" : "";
            throw new ConversionException(
                $"Не удалось прочитать XML{where}: файл повреждён или содержит запрещённые конструкции, например DTD.", ex);
        }
    }
}
