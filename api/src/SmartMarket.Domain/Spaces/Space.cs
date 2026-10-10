namespace SmartMarket.Domain.Spaces;

public sealed class Space
{
    private readonly List<Lock> _locks = [];

    public Guid Id { get; private set; }
    public Guid DistrictId { get; private set; }
    public string Name { get; private set; }
    public SpaceType Type { get; private set; }
    public string[] ProductTypes { get; private set; }
    public string OpenHours { get; private set; }
    public string RulesRef { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyList<Lock> Locks => _locks;

    /// <summary>Product types that make a space a food space (hygiene training required).</summary>
    public static readonly string[] FoodProductTypes = ["อาหาร", "เครื่องดื่ม"];

    public bool SellsFood => ProductTypes.Any(FoodProductTypes.Contains);

    public Space(
        Guid id, Guid districtId, string name, SpaceType type,
        string[] productTypes, string openHours, string rulesRef, bool isActive = true)
    {
        Id = id;
        DistrictId = districtId;
        Name = name;
        Type = type;
        ProductTypes = productTypes;
        OpenHours = openHours;
        RulesRef = rulesRef;
        IsActive = isActive;
    }
}
