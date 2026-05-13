namespace PredictdotfunNet.Exceptions;

public class PredictApiException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public PredictApiException(string message, int statusCode = 0, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
