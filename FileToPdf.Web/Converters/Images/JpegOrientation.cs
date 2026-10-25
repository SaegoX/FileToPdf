using System.Buffers.Binary;

namespace FileToPdf.Web.Converters.Images;

/// <summary>
/// Читает из EXIF-метаданных JPEG, на сколько градусов нужно повернуть снимок.
/// Телефоны часто сохраняют кадр как есть и только помечают «поверни на 90°». Просмотрщики эту пометку
/// учитывают, а PDFsharp вкладывает картинку как есть, поэтому поворачиваем при рисовании сами.
/// </summary>
public static class JpegOrientation
{
    private const ushort OrientationTag = 0x0112;

    /// <returns>Поворот по часовой стрелке: 0, 90, 180 или 270 градусов.</returns>
    public static int GetClockwiseRotation(ReadOnlySpan<byte> jpeg)
    {
        // JPEG — цепочка сегментов «FF, тип, длина (2 байта, включая себя), данные». EXIF лежит в сегменте APP1 (FF E1).
        var pos = 2; // пропускаем начало файла FF D8
        while (pos + 4 <= jpeg.Length && jpeg[pos] == 0xFF)
        {
            var marker = jpeg[pos + 1];
            var length = BinaryPrimitives.ReadUInt16BigEndian(jpeg[(pos + 2)..]);

            // SOS (FF DA): дальше идут сами пиксели, метаданных уже не будет.
            if (marker == 0xDA || length < 2 || pos + 2 + length > jpeg.Length)
                break;

            if (marker == 0xE1)
            {
                // Значения 2, 4, 5, 7 означают зеркальное отражение; на практике почти не встречаются, их не учитываем.
                switch (ReadExifOrientation(jpeg.Slice(pos + 4, length - 2)))
                {
                    case 3: return 180;
                    case 6: return 90;
                    case 8: return 270;
                }
            }

            pos += 2 + length;
        }

        return 0;
    }

    private static int ReadExifOrientation(ReadOnlySpan<byte> app1)
    {
        // "Exif\0\0", затем TIFF-заголовок: порядок байт ("II" — младший первым, "MM" — старший), число 42,
        // смещение первого каталога тегов. Все смещения считаются от начала TIFF-заголовка.
        if (app1.Length < 14 || !app1.StartsWith("Exif\0\0"u8))
            return 0;

        var tiff = app1[6..];
        var littleEndian = tiff[0] == (byte)'I';
        var directory = ReadUInt32(tiff[4..], littleEndian);
        if (directory < 8 || directory + 2 > (uint)tiff.Length)
            return 0;

        var entries = tiff[(int)directory..];
        var count = ReadUInt16(entries, littleEndian);

        // Каждая запись — 12 байт: тег (2), тип (2), количество (4), значение (4).
        for (var offset = 2; offset + 12 <= entries.Length && count-- > 0; offset += 12)
        {
            if (ReadUInt16(entries[offset..], littleEndian) == OrientationTag)
                return ReadUInt16(entries[(offset + 8)..], littleEndian);
        }

        return 0;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, bool littleEndian) =>
        littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(data) : BinaryPrimitives.ReadUInt16BigEndian(data);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, bool littleEndian) =>
        littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(data) : BinaryPrimitives.ReadUInt32BigEndian(data);
}
