namespace Gamer.Api.Features.Deals;

public class ScrapeException(string message, Exception? inner = null) : Exception(message, inner);

public class BlockedException(string message) : ScrapeException(message);

public class ParseException(string message, Exception? inner = null) : ScrapeException(message, inner);

public class BrowserUnavailableException(string message, Exception? inner = null) : ScrapeException(message, inner);
