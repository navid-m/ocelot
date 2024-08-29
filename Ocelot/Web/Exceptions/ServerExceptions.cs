namespace Ocelot.Web.Exceptions;

public class AddressInUseException : Exception
{
    public AddressInUseException()
        : base("Address is in use.") { }

    public AddressInUseException(string message)
        : base(message) { }
}

public class InvalidResponseException(string message) : Exception(message) { }

public class ResponseGenerationException(string message) : Exception(message) { }
