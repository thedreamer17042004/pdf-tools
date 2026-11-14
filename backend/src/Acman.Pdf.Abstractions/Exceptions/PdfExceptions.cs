namespace Acman.Pdf.Abstractions.Exceptions;

public class PdfException : Exception
{
    public PdfException(string message, string code = "pdf_error")
        : base(message)
    {
        Code = code;
    }

    public PdfException(string message, Exception innerException, string code = "pdf_error")
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class PdfOpenException : PdfException
{
    public PdfOpenException(string message)
        : base(message, "pdf_open_failed")
    {
    }

    public PdfOpenException(string message, Exception innerException)
        : base(message, innerException, "pdf_open_failed")
    {
    }
}

public sealed class PdfPasswordRequiredException : PdfException
{
    public PdfPasswordRequiredException(string message = "PDF password is required.")
        : base(message, "pdf_password_required")
    {
    }
}

public sealed class PdfRenderException : PdfException
{
    public PdfRenderException(string message)
        : base(message, "pdf_render_failed")
    {
    }

    public PdfRenderException(string message, Exception innerException)
        : base(message, innerException, "pdf_render_failed")
    {
    }
}

public sealed class PdfPageOutOfRangeException : PdfException
{
    public PdfPageOutOfRangeException(int pageIndex, int pageCount)
        : base($"Page index {pageIndex} is out of range for document with {pageCount} page(s).", "pdf_page_out_of_range")
    {
        PageIndex = pageIndex;
        PageCount = pageCount;
    }

    public int PageIndex { get; }
    public int PageCount { get; }
}
