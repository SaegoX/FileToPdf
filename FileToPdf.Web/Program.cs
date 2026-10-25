using FileToPdf.Web.Converters;
using FileToPdf.Web.Converters.Csv;
using FileToPdf.Web.Converters.Images;
using FileToPdf.Web.Converters.Text;
using FileToPdf.Web.Converters.Xml;
using FileToPdf.Web.Fonts;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PdfSharp.Fonts;

// В .NET старые кодировки (Windows-1251 и др.) по умолчанию недоступны. Подключаем их для всего приложения:
// они нужны и при чтении TXT/CSV, и XmlReader-у для файлов с encoding="windows-1251".
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Корень приложения — папка с exe, а не текущая папка процесса. Иначе при запуске службой или ярлыком
// (текущая папка, например, C:\Windows\System32) не найдутся wwwroot и appsettings.json.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddProblemDetails();

// Регистрация типов конвертеров.
builder.Services.AddSingleton<IPdfConverter, TextToPdfConverter>();
builder.Services.AddSingleton<IPdfConverter, ImageToPdfConverter>();
builder.Services.AddSingleton<IPdfConverter, CsvToPdfConverter>();
builder.Services.AddSingleton<IPdfConverter, XmlToPdfConverter>();
builder.Services.AddSingleton<ConverterResolver>();

var app = builder.Build();

// PDFsharp хранит поставщика шрифтов в статическом поле, поэтому задаём его один раз при старте.
GlobalFontSettings.FontResolver = new AppFontResolver(Path.Combine(AppContext.BaseDirectory, "Fonts"));

// Непредвиденная ошибка → запись в журнал и ответ 500 без технических подробностей.
app.UseExceptionHandler();

var maxFileSizeMb = app.Configuration.GetValue("Conversion:MaxFileSizeMb", 20);
var maxFileSize = maxFileSizeMb * 1024L * 1024;

// Страница конвертера: wwwroot/index.html отдаётся по адресу "/".
app.UseDefaultFiles();
app.UseStaticFiles();

// Что умеет сервер: странице нужно, чтобы подсказать форматы и проверить размер ещё до загрузки.
app.MapGet("/api/formats", (ConverterResolver resolver) =>
    new { Extensions = resolver.SupportedExtensions.Order(), MaxFileSizeMb = maxFileSizeMb });

// Принимает файл (multipart/form-data, поле "file") и сразу возвращает PDF. На диск ничего не пишется.
app.MapPost("/api/convert", (IFormFile file, ConverterResolver resolver, [FromForm] bool landscape = false) =>
{
    if (file.Length == 0)
        return Results.Problem(detail: "Файл пустой.", statusCode: StatusCodes.Status400BadRequest);

    if (file.Length > maxFileSize)
        return Results.Problem(detail: $"Файл больше {maxFileSizeMb} МБ.", statusCode: StatusCodes.Status413PayloadTooLarge);

    var converter = resolver.FindFor(file.FileName);
    if (converter is null)
    {
        var supported = string.Join(", ", resolver.SupportedExtensions.Order());
        return Results.Problem(
            detail: $"Формат «{Path.GetExtension(file.FileName)}» не поддерживается. Поддерживаются: {supported}.",
            statusCode: StatusCodes.Status415UnsupportedMediaType);
    }

    using var output = new MemoryStream();
    try
    {
        using var input = file.OpenReadStream();
        converter.Convert(input, output, new ConversionOptions(landscape));
    }
    catch (ConversionException ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    return Results.File(output.ToArray(), "application/pdf", Path.ChangeExtension(file.FileName, ".pdf"));
})

// Защита от подделки запросов нужна для форм с авторизацией по cookie; у нас ни того, ни другого.
.DisableAntiforgery()

// Проверка размера файла на стороне сервера: если кто-то обойдёт проверку в браузере, сервер всё равно не примет слишком большой файл.
.WithMetadata(new RequestSizeLimitAttribute(maxFileSize + 1024 * 1024));

app.Run();
