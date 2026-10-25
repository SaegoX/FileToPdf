using FileToPdf.Web.Converters;
using FileToPdf.Web.Converters.Csv;
using FileToPdf.Web.Converters.Images;
using FileToPdf.Web.Converters.Text;
using FileToPdf.Web.Fonts;
using Microsoft.AspNetCore.Mvc;
using PdfSharp.Fonts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

// Конвертеры: чтобы добавить формат, достаточно зарегистрировать здесь новый класс.
builder.Services.AddSingleton<IPdfConverter, TextToPdfConverter>();
builder.Services.AddSingleton<IPdfConverter, ImageToPdfConverter>();
builder.Services.AddSingleton<IPdfConverter, CsvToPdfConverter>();
builder.Services.AddSingleton<ConverterResolver>();

var app = builder.Build();

// PDFsharp хранит поставщика шрифтов в статическом поле, поэтому задаём его один раз при старте.
GlobalFontSettings.FontResolver = new AppFontResolver(Path.Combine(AppContext.BaseDirectory, "Fonts"));

// Непредвиденная ошибка → запись в журнал и ответ 500 без технических подробностей.
app.UseExceptionHandler();

var maxFileSizeMb = app.Configuration.GetValue("Conversion:MaxFileSizeMb", 20);
var maxFileSize = maxFileSizeMb * 1024L * 1024;

app.MapGet("/", () => "FileToPdf работает");

app.MapGet("/api/formats", (ConverterResolver resolver) => resolver.SupportedExtensions.Order());

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
// Сервер отклонит слишком большой запрос, не читая его целиком. +1 МБ на служебную часть формы.
.WithMetadata(new RequestSizeLimitAttribute(maxFileSize + 1024 * 1024));

app.Run();
