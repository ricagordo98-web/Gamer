namespace Gamer.Ui.Services;

public class ApiException(string message, Exception? innerException = null) : Exception(message, innerException);
