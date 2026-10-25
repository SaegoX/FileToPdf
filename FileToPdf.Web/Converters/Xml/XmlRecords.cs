using System.Xml.Linq;

namespace FileToPdf.Web.Converters.Xml;

/// <summary>Поиск в XML списка однотипных записей и превращение его в строки таблицы.</summary>
public static class XmlRecords
{
    // Записи — это когда почти все дочерние элементы контейнера называются одинаково.
    private const double SameNameShare = 0.8;

    /// <returns>Записи или null, если XML не похож на список и его лучше показать деревом.</returns>
    public static List<XElement>? Find(XElement root)
    {
        // Спускаемся через обёртки вида <root><items><item/>...</items></root>.
        var container = root;
        while (container.Elements().Count() == 1 && container.Elements().First().HasElements)
            container = container.Elements().First();

        var children = container.Elements().ToList();
        if (children.Count < 2)
            return null;

        var records = children.GroupBy(e => e.Name).MaxBy(g => g.Count())!.ToList();
        if (records.Count < children.Count * SameNameShare)
            return null;

        // Если записи сами глубоко вложены (у полей есть свои поля), таблица потеряет структуру — нужно дерево.
        var nestedRecords = records.Count(r => r.Elements().Any(field => field.HasElements));
        return nestedRecords > records.Count / 2 ? null : records;
    }

    /// <summary>Первая строка — заголовок: имена атрибутов и полей в порядке первого появления.</summary>
    public static List<string[]> ToRows(List<XElement> records)
    {
        var columns = new List<string>();
        var values = new List<Dictionary<string, string>>(records.Count);

        foreach (var record in records)
        {
            var fields = new Dictionary<string, string>();

            foreach (var attribute in record.Attributes().Where(a => !a.IsNamespaceDeclaration))
                AddField(fields, columns, attribute.Name.LocalName, attribute.Value);

            // Повторяющиеся поля (<phone>…</phone><phone>…</phone>) объединяем в одну ячейку.
            foreach (var group in record.Elements().GroupBy(e => e.Name.LocalName))
                AddField(fields, columns, group.Key, string.Join("; ", group.Select(e => Normalize(e.Value))));

            // Запись без атрибутов и полей, только с текстом: <item>текст</item>.
            if (!record.HasAttributes && !record.HasElements)
                AddField(fields, columns, record.Name.LocalName, Normalize(record.Value));

            values.Add(fields);
        }

        var rows = new List<string[]>(values.Count + 1) { columns.ToArray() };
        rows.AddRange(values.Select(fields => columns.Select(c => fields.GetValueOrDefault(c, "")).ToArray()));
        return rows;
    }

    private static void AddField(Dictionary<string, string> fields, List<string> columns, string name, string value)
    {
        if (!columns.Contains(name))
            columns.Add(name);

        fields[name] = fields.TryGetValue(name, out var existing) ? $"{existing}; {value}" : value;
    }

    // Отступы и переводы строк из разметки в ячейке не нужны.
    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
