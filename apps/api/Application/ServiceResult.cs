namespace JobPilot.Api.Application;

public enum ServiceResultStatus
{
    Ok,
    Created,
    Accepted,
    NotFound,
    NoContent,
    Conflict,
    Invalid
}

public sealed record ServiceResult<T>(ServiceResultStatus Status, T? Value = default, string? Error = null)
{
    public static ServiceResult<T> Ok(T value) => new(ServiceResultStatus.Ok, value);
    public static ServiceResult<T> Created(T value) => new(ServiceResultStatus.Created, value);
    public static ServiceResult<T> Accepted(T value) => new(ServiceResultStatus.Accepted, value);
    public static ServiceResult<T> NotFound() => new(ServiceResultStatus.NotFound);
    public static ServiceResult<T> NoContent() => new(ServiceResultStatus.NoContent);
    public static ServiceResult<T> Invalid(string error, T? value = default) => new(ServiceResultStatus.Invalid, value, error);

    public ServiceResult<TResult> Map<TResult>(Func<T, TResult> map) =>
        new(Status, Value is null ? default : map(Value), Error);
}
