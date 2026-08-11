# TMP-BUG: MemberInjectionGenerator 多 partial 部分时 canonical 选择按路径字符串排序,属性不在 canonical 部分则静默跳过生成

> ✅ **已修复(2026-08,方案 B)**：`MemberInjectionGenerator.Transform.cs` 的 canonical 去重仅在"多个部分都挂以 `Injectable` 结尾的属性"时才按路径执行;正常场景(至多一个部分挂 `[Injectable]`)以属性命中为准,不再依赖 `SyntaxTree.FilePath` 排序。回归测试：`MemberInjectionGeneratorTests.Generate_When_Multiple_Partials_And_Injectable_Part_Sorts_Last_By_Path`(红转绿)+ 两个护栏测试。Everlong.DI agent 侧工作已完成,本文档保留仅因 Nester 侧跟进项(见文末)尚未执行;Nester 恢复 `[Injectable]` 后可删除本文档。
>
> **更新（Nester 侧已绕过）**：Nester 模板已临时改为手写 `Inject()` override（`Template.Shared/Pages/Shell/MainViewModel.cs`,类上 `[Injectable]` 已移除）,四个编译单元全绿、303 测试通过。本文档保留供 Everlong.DI 修复生成器本体;Nester 侧会在修复发版后恢复 `[Injectable]` + `[Inject]` partial 属性并删除手写代码（共享文件内有注释标记）。

## 现象

Nester 模板把 `MainViewModel` 拆成多 partial 部分（共享逻辑在 `Template.Shared/Pages/Shell/MainViewModel.cs`，带 `[Injectable]` + `[Inject]` 属性；平台差异在 `MainViewModel.Avalonia.cs` / `MainViewModel.Wpf.cs`）。之后：

- **Avalonia 桌面 / WPF 模板项目**：编译通过（生成器正常产出 `MainViewModel.Inject.g.cs`）；
- **浏览器项目 / 测试项目**：编译失败 `CS9248: 分布属性 "MainViewModel.Dialogs" 必须具有实现部分`（`Busy` 同错）——生成器对该类型静默跳过了生成，没有任何诊断。

## 根因

`src/Everlong.DI.Generators/Injection/MemberInjectionGenerator.Transform.cs` 第 25–34 行：

```csharp
if (typeSymbol.DeclaringSyntaxReferences.Length > 1)
{
  var canonicalDeclaration = typeSymbol.DeclaringSyntaxReferences
    .OrderBy(static r => r.SyntaxTree.FilePath, StringComparer.Ordinal)
    .ThenBy(static r => r.Span.Start)
    .First();
  if (!ReferenceEquals(classDeclaration.SyntaxTree, canonicalDeclaration.SyntaxTree)
      || classDeclaration.Span != canonicalDeclaration.Span)
    return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());
}
```

多 partial 类型时，生成器把 **`SyntaxTree.FilePath` 字典序最小**的部分当 canonical，只处理该部分上的属性声明，其余部分一律跳过。

问题：`SyntaxTree.FilePath` 是 **csc 收到的路径字符串原样**，其形态取决于 MSBuild 如何把文件传给编译器，**不同项目对同一文件的路径字符串可以不同**：

| 编译单元 | 共享文件路径字符串（带 [Injectable]） | 平台 partial 路径字符串 | 字典序最小者 |
|---|---|---|---|
| Avalonia 桌面 | `..\Template.Shared\Pages\Shell\MainViewModel.cs`（显式 Compile Include，相对路径） | `Pages\Shell\MainViewModel.Avalonia.cs`（项目默认 glob，相对路径） | `..\`(0x2E) < `P`(0x50) → **共享文件** → 生成 ✓ |
| 浏览器 | `..\Template.Shared\Pages\Shell\MainViewModel.cs` | `..\Template.Avalonia\Pages\Shell\MainViewModel.Avalonia.cs`（两者都是显式 Include） | `A` < `S` → **平台 partial（无属性）** → 跳过 ✗ |
| 测试项目 | 同上 | 同上（显式 Include） | 同浏览器 → 跳过 ✗ |

即：**canonical 判定结果取决于项目里文件的 Include 形态（默认 glob vs 显式 Include、相对 vs 绝对），与代码内容无关**。属性所在的共享部分在部分编译单元里"恰好"排第一，在另一些编译单元里排第二，于是生成时有时无。

## 关键洞察

`InjectableAttribute` 是 `[AttributeUsage(AttributeTargets.Class)]`，**AllowMultiple 默认 false**——partial 类型合并时属性并集，同一类型**至多只能有一个部分挂 [Injectable]**（否则 CS0579）。因此：

- `ForAttributeWithMetadataName(InjectableFull, ...)` 对每个类型只会命中至多一个声明；
- 命中那个声明**就是** canonical 目标，路径排序去重纯属多余；
- 只有当"多个部分挂同一显示名的属性"这种病态场景（例如两个程序集都定义了同名 InjectableAttribute）才需要去重。

## 建议修法

**方案 A（最小）**：直接删除第 25–34 行 canonical 块。单部分类型不受影响；多部分类型按属性命中生成，恰好一次。风险：两个程序集各有一个 `Everlong.DI.InjectableAttribute` 时（属性显示名相同、来源不同程序集），`ForAttributeWithMetadataName` 可能命中多次 → 生成两份输出 → 重复成员编译错误。此场景实际由 CS0433 类型冲突兜底，但不算完全免疫。

**方案 B（稳健）**：canonical 改为"**带 [Injectable] 的部分**"中路径最小者，而不是全部部分中路径最小者：

```csharp
if (typeSymbol.DeclaringSyntaxReferences.Length > 1)
{
  // 只有当多个部分都挂 [Injectable] 时才需要去重（如两个程序集各有一个同名
  // InjectableAttribute）。路径排序不是可靠的 canonical 键：csc 收到的路径
  // 形态因项目 Include 方式而异（默认 glob 相对路径 / 显式 Include 相对路径 /
  // 绝对路径），属性部分可能排在普通部分之后。
  bool anotherPartCarriesInjectable = typeSymbol.DeclaringSyntaxReferences
    .Where(r => !ReferenceEquals(r.SyntaxTree, classDeclaration.SyntaxTree)
                || r.Span != classDeclaration.Span)
    .Select(r => r.GetSyntax(token))
    .OfType<TypeDeclarationSyntax>()
    .SelectMany(t => t.AttributeLists)
    .SelectMany(l => l.Attributes)
    .Any(a => a.Name.ToString().EndsWith("Injectable", StringComparison.Ordinal));

  if (anotherPartCarriesInjectable)
  {
    var canonicalDeclaration = typeSymbol.DeclaringSyntaxReferences
      .OrderBy(static r => r.SyntaxTree.FilePath, StringComparer.Ordinal)
      .ThenBy(static r => r.Span.Start)
      .First();
    if (!ReferenceEquals(classDeclaration.SyntaxTree, canonicalDeclaration.SyntaxTree)
        || classDeclaration.Span != canonicalDeclaration.Span)
      return new Result<InjectionInfo?>(null, diagnostics.ToImmutable());
  }
}
```

`a.Name.ToString()` 对 `Injectable` / `InjectableAttribute` / `Everlong.DI.Injectable` / `global::Everlong.DI.Injectable` 均以 "Injectable" 结尾，语法级检查足够（误报需"另一部分挂了同名异类属性"，最坏结果 = 退化到旧跳过行为，仍会以 CS9248 响亮暴露）。

## 验证

Nester 侧（`D:\Projects\cs\Everlong.Nester`）修好后按此验证：

1. `dotnet build examples/Template.Avalonia.Browser/AvaloniaTemplate.Browser.csproj` → 0 错误（现报 CS9248 ×2）
2. `dotnet build tests/Everlong.Nester.Tests/Everlong.Nester.Tests.csproj` → 0 错误（现报 CS9248 ×2）
3. `dotnet build examples/Template.Avalonia/AvaloniaTemplate.csproj` 与 WPF 项目保持 0 错误（回归）
4. 生成物检查：两个项目 obj 下 `Everlong.DI.Generators/...MemberInjectionGenerator/NesterApp.Pages.Shell.MainViewModel.Inject.g.cs` 应存在且含 `Dialogs`/`Busy` 实现
5. 全量 `dotnet test tests\Everlong.Nester.Tests\Everlong.Nester.Tests.csproj`

## 版本流（Nester 侧跟进项）

Everlong.DI 修好发版后，Nester 需要：

- `src/Everlong.Nester.Abstractions/Everlong.Nester.Abstractions.csproj` 中 `<PackageReference Include="Everlong.DI" Version="0.1.0" />` 升到新版本
- 新包推入本地 feed `D:\Projects\LocalNuget`（Nuke `CopyToLocalFeed` 或手动 push）
- 模板/测试项目 restore 后重编译

## 复现最小样例

两个文件同项目编译即可复现：

```csharp
// A.cs —— 路径字典序大于 B.cs
[Injectable]
public partial class Foo
{
  [Inject] private partial IService Bar { get; }
}

// B.cs
public partial class Foo { }
```

当 `B.cs` 的路径字符串排序在 `A.cs` 之前时（例如 B 由显式 Include 以 `..\` 开头引入、A 是项目内默认 glob），生成器跳过 A → CS9248。Nester 场景里同一类型两个部分在四个编译单元中排序不一致，所以部分项目过、部分项目炸。
