namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(
  AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue,
  AllowMultiple = true)]
public sealed class NotNullAttribute : Attribute;

[AttributeUsage(
  AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue,
  AllowMultiple = true)]
public sealed class MaybeNullAttribute : Attribute;

[AttributeUsage(
  AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue,
  AllowMultiple = true)]
public sealed class AllowNullAttribute : Attribute;

[AttributeUsage(
  AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue,
  AllowMultiple = true)]
public sealed class DisallowNullAttribute : Attribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class NotNullWhenAttribute(bool returnValue) : Attribute
{
  public bool ReturnValue { get; } = returnValue;
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class MaybeNullWhenAttribute(bool returnValue) : Attribute
{
  public bool ReturnValue { get; } = returnValue;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class MemberNotNullAttribute(params string[] members) : Attribute
{
  public string[] Members { get; } = members;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class MemberNotNullWhenAttribute(bool returnValue, params string[] members) : Attribute
{
  public bool ReturnValue { get; } = returnValue;
  public string[] Members { get; } = members;
}
