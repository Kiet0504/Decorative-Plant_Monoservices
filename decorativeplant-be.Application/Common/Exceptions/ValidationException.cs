namespace decorativeplant_be.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public List<string> Errors { get; }

    public ValidationException(List<string> errors) : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string error) : base("Validation error occurred.")
    {
        Errors = new List<string> { error };
    }
}
