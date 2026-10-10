namespace SmartMarket.Domain.Districts;

public sealed class District
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }

    public District(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}
