using System.Globalization;

namespace Gamer.Ui.Games;

public record BarItem(string Label, int Value, string Text);

public static class StatsFormat
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-PT");

    public static string Hours(int minutes) =>
        minutes switch
        {
            0 => "0 h",
            < 60 => $"{minutes} min",
            _ => $"{(minutes / 60.0).ToString(minutes >= 6000 ? "N0" : "0.#", Portuguese)} h",
        };

    public static string Players(int? players) =>
        players switch
        {
            null => string.Empty,
            >= 1000 => $"{(players.Value / 1000.0).ToString("0.#", Portuguese)} mil agora",
            _ => $"{players} agora",
        };

    public static string PerHour(decimal value, int minutes, string currency) =>
        minutes < 60 ? "—" : DealFormat.Money(value / (minutes / 60m), currency);

    public static string LastPlayed(DateTimeOffset? at, DateTimeOffset now) =>
        at is null ? "Sem registo" : DealFormat.Ago(at.Value, now);
}
