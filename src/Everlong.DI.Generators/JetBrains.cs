/* MIT License

Copyright (c) 2025 JetBrains http://www.jetbrains.com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */

// Sourced from JetBrains.Annotations (https://github.com/JetBrains/JetBrains.Annotations).
// Subset inlined as internal so Rider can resolve the annotations from this assembly without
// requiring consumers to reference JetBrains.Annotations directly.

#pragma warning disable CS1591

using System;

namespace JetBrains.Annotations
{
  [AttributeUsage(AttributeTargets.All)]
  internal sealed class UsedImplicitlyAttribute : Attribute
  {
    public UsedImplicitlyAttribute()
      : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }

    public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags)
      : this(useKindFlags, ImplicitUseTargetFlags.Default) { }

    public UsedImplicitlyAttribute(ImplicitUseTargetFlags targetFlags)
      : this(ImplicitUseKindFlags.Default, targetFlags) { }

    public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
    {
      UseKindFlags = useKindFlags;
      TargetFlags  = targetFlags;
    }

    public ImplicitUseKindFlags   UseKindFlags { get; }
    public ImplicitUseTargetFlags TargetFlags  { get; }
  }

  [AttributeUsage(AttributeTargets.Class | AttributeTargets.GenericParameter | AttributeTargets.Parameter)]
  internal sealed class MeansImplicitUseAttribute : Attribute
  {
    public MeansImplicitUseAttribute()
      : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }

    public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags)
      : this(useKindFlags, ImplicitUseTargetFlags.Default) { }

    public MeansImplicitUseAttribute(ImplicitUseTargetFlags targetFlags)
      : this(ImplicitUseKindFlags.Default, targetFlags) { }

    public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags, ImplicitUseTargetFlags targetFlags)
    {
      UseKindFlags = useKindFlags;
      TargetFlags  = targetFlags;
    }

    [UsedImplicitly] public ImplicitUseKindFlags   UseKindFlags { get; }
    [UsedImplicitly] public ImplicitUseTargetFlags TargetFlags  { get; }
  }

  [Flags]
  internal enum ImplicitUseKindFlags
  {
    Default = Access | Assign | InstantiatedWithFixedConstructorSignature,
    Access = 1,
    Assign = 2,
    InstantiatedWithFixedConstructorSignature = 4,
    InstantiatedNoFixedConstructorSignature = 8,
  }

  [Flags]
  internal enum ImplicitUseTargetFlags
  {
    Default = Itself,
    Itself = 1,
    Members = 2,
    WithInheritors = 4,
    WithMembers = Itself | Members,
  }

  [MeansImplicitUse(ImplicitUseTargetFlags.WithMembers)]
  [AttributeUsage(AttributeTargets.All, Inherited = false)]
  internal sealed class PublicAPIAttribute : Attribute
  {
    public PublicAPIAttribute() { }
    public PublicAPIAttribute(string comment) { Comment = comment; }
    public string? Comment { get; }
  }

  [AttributeUsage(AttributeTargets.Method)]
  internal sealed class PureAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Method)]
  internal sealed class MustUseReturnValueAttribute : Attribute
  {
    public MustUseReturnValueAttribute() { }
    public MustUseReturnValueAttribute(string justification) { Justification = justification; }
    public string? Justification { get; }
    public bool IsFluentBuilderMethod { get; set; }
  }

  [AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor
    | AttributeTargets.Method | AttributeTargets.Parameter)]
  internal sealed class MustDisposeResourceAttribute : Attribute
  {
    public MustDisposeResourceAttribute() { Value = true; }
    public MustDisposeResourceAttribute(bool value) { Value = value; }
    public bool Value { get; }
  }
}
