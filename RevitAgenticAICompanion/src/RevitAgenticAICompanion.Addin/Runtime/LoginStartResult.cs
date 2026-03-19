namespace RevitAgenticAICompanion.Runtime
{
    public sealed class LoginStartResult
    {
        public LoginStartResult(bool isStarted, string authUrl, string detail)
        {
            IsStarted = isStarted;
            AuthUrl = authUrl ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public bool IsStarted { get; }
        public string AuthUrl { get; }
        public string Detail { get; }
    }
}
