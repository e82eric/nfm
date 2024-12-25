namespace nfm.menu;

public class Result
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static Result Ok()
    {
        return new Result { Success = true };
    }

    public static Result Error(string message)
    {
        return new Result { Success = false, ErrorMessage = message };
    }
}