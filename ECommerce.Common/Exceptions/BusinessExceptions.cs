namespace ECommerce.Common.Exceptions
{
    public class BusinessException : Exception
    {
        public int StatusCode { get; set; }

        public BusinessException(string message, int statusCode = 400) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public class NotFoundException : BusinessException
    {
        public NotFoundException(string entity, object key)
            : base($"{entity} with id {key} was not found", 404)
        {
        }
    }

    public class ValidationException : BusinessException
    {
        public List<string> Errors { get; set; }

        public ValidationException(List<string> errors)
            : base("Validation failed", 400)
        {
            Errors = errors;
        }
    }

    public class ConflictException : BusinessException
    {
        public ConflictException(string message) : base(message, 409)
        {
        }
    }

    public class UnauthorizedException : BusinessException
    {
        public UnauthorizedException(string message = "Unauthorized") : base(message, 401)
        {
        }
    }
}
