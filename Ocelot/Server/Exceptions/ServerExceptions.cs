namespace Ocelot.Server.Exceptions;

public class AddressInUseException(string message) : Exception(message) { }

public class InvalidResponseException(string message) : Exception(message) { }

public class ResponseGenerationException(string message) : Exception(message) { }
