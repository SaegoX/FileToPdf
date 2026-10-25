using FileToPdf.Web.Fonts;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// PDFsharp хранит поставщика шрифтов в статическом поле, поэтому задаём его один раз при старте.
GlobalFontSettings.FontResolver = new AppFontResolver(Path.Combine(AppContext.BaseDirectory, "Fonts"));

app.MapGet("/", () => "FileToPdf работает");

// Временный эндпоинт этапа 1: проверка, что кириллица попадает в PDF. Уберём на этапе 2.
app.MapGet("/api/hello-pdf", () =>
{
    using var document = new PdfDocument();
    var page = document.AddPage();

    using (var gfx = XGraphics.FromPdfPage(page))
    {
        var title = new XFont(AppFontResolver.DefaultFamily, 24, XFontStyleEx.Bold);
        var text = new XFont(AppFontResolver.DefaultFamily, 12);

        gfx.DrawString("Привет, PDF!", title, XBrushes.Black, new XPoint(50, 80));
        gfx.DrawString("Съешь же ещё этих мягких французских булок, да выпей чаю.", text, XBrushes.Black, new XPoint(50, 120));
        gfx.DrawString("Символы: № «кавычки» — тире, € ₽, ЁЁ ёё", text, XBrushes.Black, new XPoint(50, 140));
    }

    using var stream = new MemoryStream();
    document.Save(stream);
    return Results.File(stream.ToArray(), "application/pdf", "hello.pdf");
});

app.Run();
