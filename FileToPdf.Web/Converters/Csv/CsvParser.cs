using System.Text;

namespace FileToPdf.Web.Converters.Csv;

/// <summary>
/// Разбор CSV по RFC 4180: поле в кавычках может содержать разделитель, перевод строки
/// и саму кавычку (записывается двумя подряд: "").
/// </summary>
public static class CsvParser
{
    // Порядок важен при равенстве: русский Excel сохраняет через «;», поэтому он первый.
    private static readonly char[] CandidateDelimiters = [';', ',', '\t', '|'];

    public static List<string[]> Parse(string text)
    {
        // Excel понимает служебную первую строку вида "sep=;" — уважаем её и не показываем в таблице.
        char delimiter;
        if (text.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && text.Length > 4)
        {
            delimiter = text[4];
            var lineEnd = text.IndexOf('\n');
            text = lineEnd < 0 ? "" : text[(lineEnd + 1)..];
        }
        else
        {
            delimiter = DetectDelimiter(text);
        }

        var rows = Parse(text, delimiter, out var unclosedQuoteLine);
        if (unclosedQuoteLine > 0)
            throw new ConversionException($"Не закрыта кавычка, открытая в строке {unclosedQuoteLine}. Проверьте файл.");

        return rows;
    }

    /// <summary>
    /// Пробует каждый разделитель на первых строках и выбирает тот, при котором больше всего строк
    /// имеют столько же столбцов, сколько заголовок (и столбцов больше одного).
    /// </summary>
    private static char DetectDelimiter(string text)
    {
        var sample = string.Join('\n', text.Split('\n', 21).Take(20));

        var best = CandidateDelimiters[0];
        var bestScore = (Rows: 0, Columns: 0);
        foreach (var delimiter in CandidateDelimiters)
        {
            var rows = Parse(sample, delimiter, out _);
            if (rows.Count == 0 || rows[0].Length < 2)
                continue;

            var score = (Rows: rows.Count(r => r.Length == rows[0].Length), Columns: rows[0].Length);
            if (score.Rows > bestScore.Rows || (score.Rows == bestScore.Rows && score.Columns > bestScore.Columns))
            {
                best = delimiter;
                bestScore = score;
            }
        }

        return best;
    }

    /// <param name="unclosedQuoteLine">Номер строки с незакрытой кавычкой или 0, если всё в порядке.</param>
    private static List<string[]> Parse(string text, char delimiter, out int unclosedQuoteLine)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var quoteLine = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
                line++;

            if (inQuotes)
            {
                if (c != '"')
                    field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"')
                    field.Append(text[++i]); // "" внутри кавычек — это одна кавычка
                else
                    inQuotes = false;
            }
            else if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
                quoteLine = line;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                    line++;
                }

                row.Add(field.ToString());
                field.Clear();
                AddRow(rows, row);
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            AddRow(rows, row);
        }

        unclosedQuoteLine = inQuotes ? quoteLine : 0;
        return rows;
    }

    private static void AddRow(List<string[]> rows, List<string> row)
    {
        // Пустые строки файла (часто в конце) пропускаем.
        if (row.Count > 1 || row[0].Length > 0)
            rows.Add(row.ToArray());
        row.Clear();
    }
}
