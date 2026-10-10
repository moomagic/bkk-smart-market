namespace SmartMarket.Domain.Common;

/// <summary>
/// Marks a property as personal data (PDPA). Its value is masked in audit entries
/// and must never be written to the application log.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PersonalDataAttribute : Attribute;
