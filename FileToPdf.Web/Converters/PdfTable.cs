using System.Globalization;
using System.Text;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace FileToPdf.Web.Converters;

/// <summary>
/// PDF с одной таблицей, первая строка — заголовок. Общий для CSV и табличного режима XML.
/// MigraDoc сам переносит текст в ячейках, разбивает таблицу по страницам и повторяет заголовок.
/// </summary>
public static class PdfTable
{
    /// <summary>Больше строк MigraDoc раскладывает слишком долго, а такой PDF всё равно никто не читает.</summary>
    public const int MaxRows = 5000;

    // Ширину столбцов прикидываем по первым строкам: этого достаточно и не тратит время на огромных файлах.
    private const int WidthSampleRows = 200;
    private const int MinColumnWeight = 4;
    private const int MaxColumnWeight = 40;

    private static readonly Color HeaderShading = new(230, 230, 230);
    private static readonly Color StripeShading = new(246, 246, 246);
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private const char ZeroWidthSpace = '​';

    public static void Render(IReadOnlyList<string[]> rows, ConversionOptions options, Stream output)
    {
        if (rows.Count - 1 > MaxRows)
            throw new ConversionException($"Слишком большая таблица: {rows.Count - 1} строк, максимум {MaxRows}. Разбейте файл на части.");

        var columnCount = rows.Max(r => r.Length);
        var document = MigraDocPdf.CreateDocument(options);

        var table = document.LastSection.AddTable();
        table.Format.Font.Size = columnCount switch { <= 6 => 9, <= 12 => 8, _ => 7 };
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.LeftPadding = table.RightPadding = Unit.FromPoint(3);
        table.TopPadding = table.BottomPadding = Unit.FromPoint(1.5);

        // Ширина между полями делится между столбцами пропорционально длине их содержимого.
        var weights = ColumnWeights(rows, columnCount);
        var contentWidth = MigraDocPdf.ContentWidth(options).Point;
        for (var i = 0; i < columnCount; i++)
        {
            var column = table.AddColumn(Unit.FromPoint(contentWidth * weights[i] / weights.Sum()));
            if (IsNumericColumn(rows, i))
                column.Format.Alignment = ParagraphAlignment.Right;
        }

        var header = table.AddRow();
        header.HeadingFormat = true; // повторять эту строку вверху каждой страницы
        header.Format.Font.Bold = true;
        header.Shading.Color = HeaderShading;
        FillRow(header, rows[0]);

        for (var r = 1; r < rows.Count; r++)
        {
            var row = table.AddRow();
            if (r % 2 == 0)
                row.Shading.Color = StripeShading;
            FillRow(row, rows[r]);
        }

        MigraDocPdf.Render(document, output);
    }

    private static void FillRow(Row row, string[] values)
    {
        // Строка может быть короче заголовка: недостающие ячейки остаются пустыми.
        for (var i = 0; i < values.Length; i++)
        {
            var paragraph = row.Cells[i].AddParagraph();
            var lines = values[i].ReplaceLineEndings("\n").Split('\n');
            for (var l = 0; l < lines.Length; l++)
            {
                if (l > 0)
                    paragraph.AddLineBreak();
                if (lines[l].Length > 0)
                    paragraph.AddText(AllowBreaksInLongWords(lines[l]));
            }
        }
    }

    /// <summary>
    /// MigraDoc переносит строки только по пробелам, поэтому длинная ссылка или код без пробелов
    /// вылезает в соседнюю ячейку. Вставляем в такие слова невидимые места переноса.
    /// </summary>
    private static string AllowBreaksInLongWords(string text)
    {
        const int chunk = 10;
        var result = new StringBuilder(text.Length + text.Length / chunk);
        var run = 0;
        foreach (var c in text)
        {
            run = char.IsWhiteSpace(c) ? 0 : run + 1;
            if (run > chunk)
            {
                result.Append(ZeroWidthSpace);
                run = 1;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    private static int[] ColumnWeights(IReadOnlyList<string[]> rows, int columnCount)
    {
        var weights = new int[columnCount];
        foreach (var row in rows.Take(WidthSampleRows))
        {
            for (var i = 0; i < row.Length; i++)
            {
                var longestLine = row[i].ReplaceLineEndings("\n").Split('\n').Max(l => l.Length);
                weights[i] = Math.Max(weights[i], longestLine);
            }
        }

        return weights.Select(w => Math.Clamp(w, MinColumnWeight, MaxColumnWeight)).ToArray();
    }

    /// <summary>Столбец числовой, если все непустые значения под заголовком — числа (1 234,5 или 1234.5).</summary>
    private static bool IsNumericColumn(IReadOnlyList<string[]> rows, int column)
    {
        var hasValues = false;
        for (var r = 1; r < rows.Count; r++)
        {
            if (column >= rows[r].Length || rows[r][column].Length == 0)
                continue;

            var value = rows[r][column].Replace(' ', ' ');
            if (!decimal.TryParse(value, NumberStyles.Number, Russian, out _) &&
                !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                return false;

            hasValues = true;
        }

        return hasValues;
    }
}
