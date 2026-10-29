namespace SecureVoting.Domain;

public class DomainException(string message) : Exception(message);

public sealed class ValidationException(string message) : DomainException(message);

public sealed class NotFoundException(string message) : DomainException(message);

public sealed class ConflictException(string message) : DomainException(message);

public sealed class ForbiddenOperationException(string message) : DomainException(message);
