
/// <summary>
/// Exception that is used to propagate a user-information to the client.
/// </summary>
public class UserReportedHttpException : Exception
{
    /// <summary>
    /// The HTTP status code to return to the client.
    /// </summary>
    public System.Net.HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="UserReportedHttpException"/> class with a specified error message and status code.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="statusCode">The HTTP status code to return to the client. Default is 400 (Bad Request).</param>
    public UserReportedHttpException(string message, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="UserReportedHttpException"/> class with a specified error message, inner exception, and status code.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    /// <param name="statusCode">The HTTP status code to return to the client. Default is 400 (Bad Request).</param>
    public UserReportedHttpException(string message, Exception innerException, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.BadRequest)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}