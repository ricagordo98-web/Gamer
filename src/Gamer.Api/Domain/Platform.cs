using Gamer.Contracts.Games;

namespace Gamer.Api.Domain;

public class Platform
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Family { get; set; }
    public PlatformFormat Format { get; set; }
}
