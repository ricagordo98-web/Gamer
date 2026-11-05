using System.Globalization;

namespace Gamer.Ui.Games;

public static class DealFormat
{
    public static string Money(decimal amount, string currency)
    {
        string number = amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
        return currency switch
        {
            "EUR" => $"{number} €",
            "USD" => $"${number}",
            "GBP" => $"£{number}",
            _ => $"{number} {currency}",
        };
    }

    public static string Playtime(int minutes) =>
        minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h";

    public static string? TimeLeft(DateTimeOffset? end, DateTimeOffset now)
    {
        if (end is null || end <= now)
        {
            return null;
        }

        TimeSpan left = end.Value - now;
        return left switch
        {
            { TotalHours: < 1 } => $"Acaba em {(int)left.TotalMinutes} min",
            { TotalDays: < 1 } => $"Acaba em {(int)left.TotalHours} h",
            _ => $"Acaba em {(int)left.TotalDays} d {left.Hours} h",
        };
    }

    public static string Ago(DateTimeOffset moment, DateTimeOffset now)
    {
        TimeSpan elapsed = now - moment;
        return elapsed switch
        {
            { TotalMinutes: < 1 } => "agora mesmo",
            { TotalHours: < 1 } => $"há {(int)elapsed.TotalMinutes} min",
            { TotalDays: < 1 } => $"há {(int)elapsed.TotalHours} h",
            _ => $"há {(int)elapsed.TotalDays} d",
        };
    }
}
