namespace SmartMarket.Domain.Spaces;

public sealed class Lock
{
    public Guid Id { get; private set; }
    public Guid SpaceId { get; private set; }
    public string Code { get; private set; }

    public Lock(Guid id, Guid spaceId, string code)
    {
        Id = id;
        SpaceId = spaceId;
        Code = code;
    }
}
