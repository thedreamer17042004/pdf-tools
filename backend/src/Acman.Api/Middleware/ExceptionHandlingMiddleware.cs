using System.Net;
using System.Text.Json;
using Acman.BuildingBlocks.Errors;
using Acman.Pdf.Abstractions.Exceptions;
using FluentValidation;

namespace Acman.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            ValidationException fv => (
                (int)HttpStatusCode.BadRequest,
                "validation_error",
                string.Join("; ", fv.Errors.Select(e => e.ErrorMessage))),
            ValidationAppException va => (va.StatusCode, va.Code, va.Message),
            NotFoundException nf => (nf.StatusCode, nf.Code, nf.Message),
            AppException app => (app.StatusCode, app.Code, app.Message),
            PdfPasswordRequiredException => (401, "pdf_password_required", exception.Message),
            PdfPageOutOfRangeException page => (400, page.Code, page.Message),
            PdfOpenException open => (400, open.Code, open.Message),
            PdfRenderException render => (500, render.Code, render.Message),
            PdfException pdf => (400, pdf.Code, pdf.Message),
            _ => (500, "internal_error", "An unexpected error occurred.")
        };

        if (status >= 500)
        {
            _logger.LogError(exception, "Unhandled server error");
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with {Code}", code);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error = new { code, message, traceId = context.TraceIdentifier }
        }, JsonOptions));
    }
}
