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
}
