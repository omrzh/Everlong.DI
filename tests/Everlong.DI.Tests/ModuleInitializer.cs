using System.Runtime.CompilerServices;
using DiffEngine;
using VerifyTests;

public static class ModuleInitializer
{
  [ModuleInitializer]
  public static void Init()
  {
    VerifySourceGenerators.Initialize();

    DiffTools.UseOrder(
        DiffTool.Rider,
        DiffTool.VisualStudioCode,
        DiffTool.WinMerge,
        DiffTool.BeyondCompare
    );
  }
}
