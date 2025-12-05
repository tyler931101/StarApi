public class LoginResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ErrorMessage { get; set; }
    public LoginErrorType? ErrorType { get; set; }
}

public enum LoginErrorType
{
    InvalidCredentials,
    EmailNotVerified,
    AccountPending,
    AccountInactive,
    AccountLocked,
    AccountDisabled,
    ValidationError,
    ServerError
}