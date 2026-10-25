using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace FileToPdf.Web.Converters;

/// <summary>
/// PDF с одной таблицей, первая строка — заголовок. Общий для CSV и табличного режима XML.
/// MigraDoc сам переносит текст в ячейках, разбивает таблицу по страницам и повторяет заголовок.
/// </summary>
public static class PdfTable
{
    /// <summary>Около 10 секунд на раскладку в худшем случае; больше — уже неудобно ждать.</summary>
    public const int MaxRows = 10000;

    // Ширину столбцов прикидываем по первым строкам: этого достаточно и не тратит время на огромных файлах.
    private const int WidthSampleRows = 200;
    // Столько символов любой столбец получает гарантированно, чтобы короткие значения и заголовки не обрезались.
    private const int ShortTextChars = 8;
    // Длиннее этого текст всё равно переносится, лишняя ширина ему не нужна.
    private const int MaxColumnChars = 40;
    // Средняя ширина символа Noto Sans в долях кегля, с запасом на жирный заголовок.
    private const double AverageCharWidth = 0.6;
    private const double CellPaddingPt = 3;

    private const char NoBreakSpace = (char)0xA0;

    private static readonly Color HeaderShading = new(230, 230, 230);
    private static readonly Color StripeShading = new(246, 246, 246);
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    /// Генерирует PDF-документ с таблицей на основе предоставленных строк и записывает результат в поток.
    /// </summary>
    /// <param name="rows">Строки таблицы, где первая строка считается заголовком.</param>
    /// <param name="options">Параметры конвертации и оформления документа.</param>
    /// <param name="output">Поток для вывода сгенерированного PDF-документа.</param>
    /// <exception cref="ConversionException">Выбрасывается, если количество строк превышает максимально допустимое значение.</exception>
    public static void Render(IReadOnlyList<string[]> rows, ConversionOptions options, Stream output)
    {
        if (rows.Count - 1 > MaxRows)
            throw new ConversionException($"Слишком большая таблица: {rows.Count - 1} строк, максимум {MaxRows}. Разбейте файл на части.");

        var columnCount = rows.Max(r => r.Length);
        var fontSize = columnCount switch { <= 6 => 9, <= 12 => 8, _ => 7 };
        var document = MigraDocPdf.CreateDocument(options);

        var table = document.LastSection.AddTable();
        table.Format.Font.Size = fontSize;
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.LeftPadding = table.RightPadding = Unit.FromPoint(CellPaddingPt);
        table.TopPadding = table.BottomPadding = Unit.FromPoint(1.5);

        var widths = ColumnWidths(rows, columnCount, MigraDocPdf.ContentWidth(options).Point, fontSize);
        for (var i = 0; i < columnCount; i++)
        {
            var column = table.AddColumn(Unit.FromPoint(widths[i]));
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

    /// <summary>
    /// Заполняет ячейки строки значениями из массива строк, добавляя переносы строк при необходимости.
    /// </summary>
    /// <remarks>Если массив короче количества ячеек, оставшиеся ячейки остаются пустыми.</remarks>
    /// <param name="row">Строка таблицы для заполнения.</param>
    /// <param name="values">Массив значений для заполнения ячеек строки.</param>
    private static void FillRow(Row row, string[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            var paragraph = row.Cells[i].AddParagraph();
            var lines = values[i].ReplaceLineEndings("\n").Split('\n');
            for (var l = 0; l < lines.Length; l++)
            {
                if (l > 0)
                    paragraph.AddLineBreak();
                if (lines[l].Length > 0)
                    paragraph.AddText(MigraDocPdf.AllowBreaksInLongWords(lines[l]));
            }
        }
    }

    /// <summary>
    /// Каждый столбец сначала получает место под короткий текст (до <see cref="ShortTextChars"/> символов),
    /// чтобы узкие столбцы вроде «день» или «пара» не обрезались. Остаток ширины делится
    /// между длинными столбцами пропорционально длине их текста.
    /// </summary>
    private static double[] ColumnWidths(IReadOnlyList<string[]> rows, int columnCount, double contentWidth, double fontSize)
    {
        var lengths = new int[columnCount];
        foreach (var row in rows.Take(WidthSampleRows))
        {
            for (var i = 0; i < row.Length; i++)
            {
                var longestLine = row[i].ReplaceLineEndings("\n").Split('\n').Max(l => l.Length);
                lengths[i] = Math.Max(lengths[i], longestLine);
            }
        }

        var charWidth = fontSize * AverageCharWidth;
        var baseWidths = lengths.Select(l => 2 * CellPaddingPt + Math.Clamp(l, 1, ShortTextChars) * charWidth).ToArray();
        var extraWeights = lengths.Select(l => (double)Math.Clamp(l, ShortTextChars, MaxColumnChars) - ShortTextChars).ToArray();

        var free = contentWidth - baseWidths.Sum();
        if (free <= 0 || extraWeights.Sum() == 0)
        {
            // Либо все столбцы короткие, либо их так много, что и минимум не влезает: делим пропорционально.
            return baseWidths.Select(w => contentWidth * w / baseWidths.Sum()).ToArray();
        }

        return baseWidths.Select((w, i) => w + free * extraWeights[i] / extraWeights.Sum()).ToArray();
    }

    /// <summary>Столбец числовой, если все непустые значения под заголовком — числа (1 234,5 или 1234.5).</summary>
    private static bool IsNumericColumn(IReadOnlyList<string[]> rows, int column)
    {
        var hasValues = false;
        for (var r = 1; r < rows.Count; r++)
        {
            if (column >= rows[r].Length || rows[r][column].Length == 0)
                continue;

            var value = rows[r][column].Replace(NoBreakSpace, ' ');
            if (!decimal.TryParse(value, NumberStyles.Number, Russian, out _) &&
                !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                return false;

            hasValues = true;
        }

        return hasValues;
    }
}
