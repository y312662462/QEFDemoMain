namespace MultiAgentNPC.Services
{
    /// <summary>
    /// Categorizes why a service call failed, so callers can react without
    /// parsing error strings (e.g. show "service unavailable" vs "auth error").
    /// </summary>
    public enum ServiceErrorType
    {
        None = 0,
        Network,
        Timeout,
        Auth,
        Parse,
        Cancelled,
        Unknown
    }

    /// <summary>
    /// Uniform result wrapper for every external service call.
    /// Services never throw to the business layer: any network error, timeout,
    /// 401/403 or parse failure is captured here so Unity cannot be frozen by an
    /// unhandled exception bubbling out of an async call.
    /// </summary>
    public class ServiceResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T Value { get; private set; }
        public ServiceErrorType ErrorType { get; private set; }
        public string ErrorMessage { get; private set; }

        /// <summary>HTTP status code when applicable; 0 when no response was received.</summary>
        public long StatusCode { get; private set; }

        private ServiceResult()
        {
        }

        public static ServiceResult<T> Ok(T value)
        {
            return new ServiceResult<T>
            {
                IsSuccess = true,
                Value = value,
                ErrorType = ServiceErrorType.None
            };
        }

        public static ServiceResult<T> Fail(ServiceErrorType errorType, string message, long statusCode = 0)
        {
            return new ServiceResult<T>
            {
                IsSuccess = false,
                ErrorType = errorType,
                ErrorMessage = message,
                StatusCode = statusCode
            };
        }

        public override string ToString()
        {
            return IsSuccess
                ? $"Ok({typeof(T).Name})"
                : $"Fail({ErrorType}, status={StatusCode}, msg='{ErrorMessage}')";
        }
    }
}
