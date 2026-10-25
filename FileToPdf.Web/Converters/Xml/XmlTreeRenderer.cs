using System.Xml.Linq;
using MigraDoc.DocumentObjectModel;

namespace FileToPdf.Web.Converters.Xml;

/// <summary>
/// XML деревом: каждый элемент — абзац с отступом по глубине вложенности,
/// имя элемента выделено цветом, атрибуты и текст идут следом.
/// </summary>
public static class XmlTreeRenderer
{
    /// <summary>Защита от огромных файлов: десятки тысяч абзацев раскладываются очень долго.</summary>
    public const int MaxElements = 20000;

    private const double IndentCm = 0.5;
    private const int MaxIndentLevel = 15; // ограничение, чтобы текст не уехал за край страницы

    private static readonly Color ElementColor = new(0, 84, 147);
    private static readonly Color AttributeColor = new(150, 75, 0);

    /// <summary>
    /// Преобразует XML-элемент в PDF-документ и записывает результат в указанный поток.
    /// </summary>
    /// <param name="root">Корневой XML-элемент для преобразования.</param>
    /// <param name="options">Параметры преобразования в PDF.</param>
    /// <param name="output">Поток для записи сгенерированного PDF-документа.</param>
    /// <exception cref="ConversionException">Выбрасывается, если количество элементов XML превышает максимально допустимое значение.</exception>
    public static void Render(XElement root, ConversionOptions options, Stream output)
    {
        var elementCount = root.DescendantsAndSelf().Count();
        if (elementCount > MaxElements)
            throw new ConversionException($"Слишком большой XML: {elementCount} элементов, максимум {MaxElements}.");

        var document = MigraDocPdf.CreateDocument(options);
        var section = document.LastSection;

        // Обход стеком, а не рекурсией: очень глубокий XML не обрушит процесс переполнением стека.
        var stack = new Stack<(XElement Element, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (element, depth) = stack.Pop();
            AddElement(section, element, depth);

            foreach (var child in element.Elements().Reverse())
                stack.Push((child, depth + 1));
        }

        MigraDocPdf.Render(document, output);
    }

    /// <summary>
    /// Добавляет элемент XML в раздел документа с форматированием, учитывающим уровень вложенности и атрибуты.
    /// </summary>
    /// <param name="section">Раздел MigraDoc, в который добавляется элемент.</param>
    /// <param name="element">XML-элемент, который необходимо отобразить.</param>
    /// <param name="depth">Текущий уровень вложенности для определения отступа.</param>
    private static void AddElement(Section section, XElement element, int depth)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.LeftIndent = Unit.FromCentimeter(Math.Min(depth, MaxIndentLevel) * IndentCm);
        paragraph.Format.SpaceAfter = Unit.FromPoint(1);

        var name = paragraph.AddFormattedText(element.Name.LocalName, TextFormat.Bold);
        name.Color = ElementColor;

        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
        {
            paragraph.AddText("   ");
            paragraph.AddFormattedText(attribute.Name.LocalName + ": ").Color = AttributeColor;
            paragraph.AddText(MigraDocPdf.AllowBreaksInLongWords(attribute.Value));
        }

        // Собственный текст элемента (без текста вложенных элементов — он будет в их абзацах).
        var text = string.Join(' ', element.Nodes().OfType<XText>().Select(t => t.Value.Trim()).Where(t => t.Length > 0));
        if (text.Length > 0)
        {
            paragraph.AddText(" — ");
            paragraph.AddText(MigraDocPdf.AllowBreaksInLongWords(text));
        }
    }
}
