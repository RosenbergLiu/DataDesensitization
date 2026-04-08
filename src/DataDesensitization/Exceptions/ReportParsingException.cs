namespace DataDesensitization.Exceptions;

public class ReportParsingException : Exception
{
    public ReportParsingException(string message)
        : base(message) { }

    public ReportParsingException(string message, Exception innerException)
        : base(message, innerException) { }
}
