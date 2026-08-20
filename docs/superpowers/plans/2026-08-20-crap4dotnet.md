# crap4dotnet Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 10 global tool `crap4dotnet` that computes the CRAP score per method from Coverlet Cobertura coverage and reports the worst offenders, at full CLI parity with crap4j.

**Architecture:** A pure `Crap4Dotnet.Core` library (parsing, scoring, filtering, report model — zero console/process I/O) plus a thin `Crap4Dotnet.Cli` shell (arg parsing, `dotnet test` orchestration, XML location, git, rendering, exit codes). All deterministic logic lives in Core and is exhaustively unit-tested; the Cli wires it together.

**Tech Stack:** .NET 10 / C# latest, xUnit, coverlet.collector, `System.Xml.Linq`, `just` task runner, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-20-crap4dotnet-design.md`

## Global Constraints

- Target framework: `net10.0`; `Nullable=enable`; `ImplicitUsings=enable`; `LangVersion=latest`; `TreatWarningsAsErrors=true`.
- CRAP formula: `CC² × (1 − coverage)³ + CC`; coverage clamped to `[0,1]`.
- Threshold: fixed `8.0` (no `--threshold` flag — crap4j parity).
- Exit codes, strict crap4j parity: `0` clean · `2` reserved solely for "ran fine, ≥1 method over threshold" · `1` for every error path.
- CC and coverage both come from the same Cobertura `<method>` node (`complexity` and `line-rate`). No independent complexity computation.
- No third-party CLI-parsing dependency (hand-rolled). Output is plain aligned text.
- Tool packaging: `PackAsTool=true`, `ToolCommandName=crap4dotnet`, `PackageId=crap4dotnet`.
- License MIT; `NOTICE` credits crap4j (Robert C. Martin) and the CRAP metric origin.
- No NuGet.org publish in v1. CI restores/builds/tests/packs only.
- All culture-sensitive parsing/formatting uses `CultureInfo.InvariantCulture`.

---

### Task 1: Solution & project scaffolding

**Files:**
- Create: `crap4dotnet.sln`
- Create: `Directory.Build.props`
- Create: `.gitignore`, `LICENSE`, `NOTICE`
- Create: `src/Crap4Dotnet.Core/Crap4Dotnet.Core.csproj`
- Create: `tests/Crap4Dotnet.Tests/Crap4Dotnet.Tests.csproj`
- Test: `tests/Crap4Dotnet.Tests/ScaffoldingTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: a buildable solution with a green test run; `Crap4Dotnet.Core` namespace root; test project referencing Core with `coverlet.collector`.

- [ ] **Step 1: Create solution and projects**

```bash
cd /c/Dev/ChrisNeff/crap4dotnet
dotnet new sln -n crap4dotnet
dotnet new classlib -n Crap4Dotnet.Core -o src/Crap4Dotnet.Core
dotnet new xunit -n Crap4Dotnet.Tests -o tests/Crap4Dotnet.Tests
rm src/Crap4Dotnet.Core/Class1.cs tests/Crap4Dotnet.Tests/UnitTest1.cs
dotnet sln add src/Crap4Dotnet.Core tests/Crap4Dotnet.Tests
dotnet add tests/Crap4Dotnet.Tests reference src/Crap4Dotnet.Core
dotnet add tests/Crap4Dotnet.Tests package coverlet.collector
```

- [ ] **Step 2: Write `Directory.Build.props`** (centralizes settings; remove per-project `<TargetFramework>` lines from both csproj files so this wins)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <InvariantGlobalization>true</InvariantGlobalization>
    <Authors>Christopher Neff</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/ChrisNeff/crap4dotnet</RepositoryUrl>
  </PropertyGroup>
</Project>
```

Then edit `src/Crap4Dotnet.Core/Crap4Dotnet.Core.csproj` and `tests/Crap4Dotnet.Tests/Crap4Dotnet.Tests.csproj` to delete their `<TargetFramework>net...</TargetFramework>` lines.

- [ ] **Step 3: Write `.gitignore`, `LICENSE`, `NOTICE`**

`.gitignore`:
```gitignore
bin/
obj/
artifacts/
*.user
.vs/
```

`LICENSE` — standard MIT text, copyright `2026 Christopher Neff`.

`NOTICE`:
```
crap4dotnet is a .NET port of the CRAP metric popularized by crap4j
(https://github.com/unclebob/crap4java) by Robert C. Martin.

The CRAP (Change Risk Anti-Patterns) metric originates with Alberto Savoia
and Bob Evans. crap4dotnet implements the published algorithm independently;
it contains no code from crap4j.
```

- [ ] **Step 4: Write the scaffolding test**

```csharp
// tests/Crap4Dotnet.Tests/ScaffoldingTests.cs
namespace Crap4Dotnet.Tests;

public class ScaffoldingTests
{
    [Fact]
    public void Solution_builds_and_tests_run()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 5: Run tests to verify green**

Run: `dotnet test`
Expected: PASS (1 test).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution, core + test projects, licensing"
```

---

### Task 2: CRAP formula (`CrapCalculator.Crap`)

**Files:**
- Create: `src/Crap4Dotnet.Core/CrapCalculator.cs`
- Test: `tests/Crap4Dotnet.Tests/CrapCalculatorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public static double Crap4Dotnet.Core.CrapCalculator.Crap(int complexity, double coverage)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Crap4Dotnet.Tests/CrapCalculatorTests.cs
using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class CrapCalculatorTests
{
    [Theory]
    [InlineData(1, 1.0, 1.0)]      // fully covered, trivial
    [InlineData(10, 0.0, 110.0)]   // 100*1 + 10
    [InlineData(5, 0.5, 8.125)]    // 25*0.125 + 5, just over 8.0
    [InlineData(2, 1.0, 2.0)]
    public void Crap_matches_reference_values(int cc, double cov, double expected)
    {
        Assert.Equal(expected, CrapCalculator.Crap(cc, cov), precision: 3);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]  // clamped low -> treated as 0 coverage
    [InlineData(1.5, 1.0)]   // clamped high -> treated as full coverage
    public void Crap_clamps_coverage(double cov, double clampedCov)
    {
        Assert.Equal(CrapCalculator.Crap(4, clampedCov), CrapCalculator.Crap(4, cov), precision: 6);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter CrapCalculatorTests`
Expected: FAIL — `CrapCalculator` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Crap4Dotnet.Core/CrapCalculator.cs
namespace Crap4Dotnet.Core;

public static class CrapCalculator
{
    public static double Crap(int complexity, double coverage)
    {
        double c = Math.Clamp(coverage, 0.0, 1.0);
        double cc = complexity;
        return cc * cc * Math.Pow(1.0 - c, 3.0) + cc;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter CrapCalculatorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): CRAP formula"
```

---

### Task 3: Method records + `CrapCalculator.Score`

**Files:**
- Create: `src/Crap4Dotnet.Core/MethodCoverage.cs`
- Create: `src/Crap4Dotnet.Core/MethodCrap.cs`
- Modify: `src/Crap4Dotnet.Core/CrapCalculator.cs` (add `Score`)
- Test: `tests/Crap4Dotnet.Tests/CrapCalculatorTests.cs` (add cases)

**Interfaces:**
- Consumes: `CrapCalculator.Crap`.
- Produces:
  - `record MethodCoverage(string TypeName, string MethodName, string Signature, string FileName, double? LineRate, int? Complexity)`
  - `record MethodCrap(string TypeName, string MethodName, string Signature, string FileName, int? Cc, double? Coverage, double? Crap, bool IsCrappy)`
  - `public static MethodCrap CrapCalculator.Score(MethodCoverage m, double threshold)`

- [ ] **Step 1: Write the failing test**

```csharp
// append to CrapCalculatorTests.cs
[Fact]
public void Score_computes_crap_and_flags_over_threshold()
{
    var m = new MethodCoverage("Ns.Foo", "Bar", "(System.Int32)", "src/Foo.cs", 0.5, 5);
    var s = CrapCalculator.Score(m, threshold: 8.0);
    Assert.Equal(8.125, s.Crap!.Value, precision: 3);
    Assert.True(s.IsCrappy);
    Assert.Equal(5, s.Cc);
    Assert.Equal(0.5, s.Coverage);
}

[Fact]
public void Score_returns_na_when_complexity_or_coverage_missing()
{
    var noCc = CrapCalculator.Score(new MethodCoverage("T", "M", "()", "f.cs", 0.5, null), 8.0);
    var noCov = CrapCalculator.Score(new MethodCoverage("T", "M", "()", "f.cs", null, 5), 8.0);
    Assert.Null(noCc.Crap);
    Assert.False(noCc.IsCrappy);
    Assert.Null(noCov.Crap);
    Assert.False(noCov.IsCrappy);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter CrapCalculatorTests`
Expected: FAIL — `MethodCoverage`/`MethodCrap`/`Score` undefined.

- [ ] **Step 3: Write the records and `Score`**

```csharp
// src/Crap4Dotnet.Core/MethodCoverage.cs
namespace Crap4Dotnet.Core;

public sealed record MethodCoverage(
    string TypeName,
    string MethodName,
    string Signature,
    string FileName,
    double? LineRate,
    int? Complexity);
```

```csharp
// src/Crap4Dotnet.Core/MethodCrap.cs
namespace Crap4Dotnet.Core;

public sealed record MethodCrap(
    string TypeName,
    string MethodName,
    string Signature,
    string FileName,
    int? Cc,
    double? Coverage,
    double? Crap,
    bool IsCrappy);
```

```csharp
// add to CrapCalculator.cs
public static MethodCrap Score(MethodCoverage m, double threshold)
{
    if (m.Complexity is int cc && m.LineRate is double cov)
    {
        double crap = Crap(cc, cov);
        return new MethodCrap(m.TypeName, m.MethodName, m.Signature, m.FileName,
            cc, cov, crap, crap > threshold);
    }
    return new MethodCrap(m.TypeName, m.MethodName, m.Signature, m.FileName,
        m.Complexity, m.LineRate, null, false);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter CrapCalculatorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): method records and Score with N/A handling"
```

---

### Task 4: Cobertura parser

**Files:**
- Create: `src/Crap4Dotnet.Core/CoberturaParser.cs`
- Test: `tests/Crap4Dotnet.Tests/CoberturaParserTests.cs`
- Test fixture: `tests/Crap4Dotnet.Tests/fixtures/sample.cobertura.xml` (set `CopyToOutputDirectory`)

**Interfaces:**
- Consumes: `MethodCoverage`.
- Produces: `public static IReadOnlyList<MethodCoverage> CoberturaParser.Parse(Stream xml)`.

- [ ] **Step 1: Add the fixture and ensure it copies to output**

Create `tests/Crap4Dotnet.Tests/fixtures/sample.cobertura.xml`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<coverage line-rate="0.5" branch-rate="0.5" version="1.9" complexity="12">
  <sources><source>/repo/</source></sources>
  <packages>
    <package name="MyAssembly" line-rate="0.5" branch-rate="0.5" complexity="12">
      <classes>
        <class name="MyAssembly.Foo" filename="src/Foo.cs" line-rate="0.4" branch-rate="0.5" complexity="7">
          <methods>
            <method name="Bar" signature="(System.Int32)" line-rate="0.5" branch-rate="0.5" complexity="5">
              <lines><line number="10" hits="0" branch="false" /></lines>
            </method>
            <method name="NoComplexity" signature="()" line-rate="1.0" branch-rate="1.0">
              <lines><line number="20" hits="1" branch="false" /></lines>
            </method>
          </methods>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
```

Add to `Crap4Dotnet.Tests.csproj`:
```xml
<ItemGroup>
  <None Include="fixtures/**/*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing test**

```csharp
// tests/Crap4Dotnet.Tests/CoberturaParserTests.cs
using System.Xml;
using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class CoberturaParserTests
{
    private static Stream Fixture() =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.cobertura.xml"));

    [Fact]
    public void Parse_reads_methods_with_class_context()
    {
        var methods = CoberturaParser.Parse(Fixture());
        var bar = methods.Single(m => m.MethodName == "Bar");
        Assert.Equal("MyAssembly.Foo", bar.TypeName);
        Assert.Equal("(System.Int32)", bar.Signature);
        Assert.Equal("src/Foo.cs", bar.FileName);
        Assert.Equal(0.5, bar.LineRate);
        Assert.Equal(5, bar.Complexity);
    }

    [Fact]
    public void Parse_returns_null_for_missing_attributes()
    {
        var m = CoberturaParser.Parse(Fixture()).Single(x => x.MethodName == "NoComplexity");
        Assert.Null(m.Complexity);
        Assert.Equal(1.0, m.LineRate);
    }

    [Fact]
    public void Parse_throws_on_malformed_xml()
    {
        using var bad = new MemoryStream("<coverage><oops"u8.ToArray());
        Assert.Throws<XmlException>(() => CoberturaParser.Parse(bad));
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter CoberturaParserTests`
Expected: FAIL — `CoberturaParser` undefined.

- [ ] **Step 4: Write the implementation**

```csharp
// src/Crap4Dotnet.Core/CoberturaParser.cs
using System.Globalization;
using System.Xml.Linq;

namespace Crap4Dotnet.Core;

public static class CoberturaParser
{
    public static IReadOnlyList<MethodCoverage> Parse(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var methods = new List<MethodCoverage>();
        foreach (var cls in doc.Descendants("class"))
        {
            string typeName = (string?)cls.Attribute("name") ?? "";
            string fileName = (string?)cls.Attribute("filename") ?? "";
            foreach (var m in cls.Descendants("method"))
            {
                methods.Add(new MethodCoverage(
                    typeName,
                    (string?)m.Attribute("name") ?? "",
                    (string?)m.Attribute("signature") ?? "",
                    fileName,
                    ParseDouble(m.Attribute("line-rate")),
                    ParseInt(m.Attribute("complexity"))));
            }
        }
        return methods;
    }

    private static double? ParseDouble(XAttribute? a) =>
        a is not null && double.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : null;

    private static int? ParseInt(XAttribute? a) =>
        a is not null && int.TryParse(a.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v : null;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter CoberturaParserTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): Cobertura parser with sparse-attribute tolerance"
```

---

### Task 5: Coverage merger

**Files:**
- Create: `src/Crap4Dotnet.Core/CoverageMerger.cs`
- Test: `tests/Crap4Dotnet.Tests/CoverageMergerTests.cs`

**Interfaces:**
- Consumes: `MethodCoverage`.
- Produces: `public static IReadOnlyList<MethodCoverage> CoverageMerger.Merge(IEnumerable<MethodCoverage> methods)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Crap4Dotnet.Tests/CoverageMergerTests.cs
using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class CoverageMergerTests
{
    [Fact]
    public void Merge_keeps_highest_coverage_for_duplicate_method()
    {
        var input = new[]
        {
            new MethodCoverage("T", "M", "()", "f.cs", 0.2, 5),
            new MethodCoverage("T", "M", "()", "f.cs", 0.9, 5),
        };
        var merged = CoverageMerger.Merge(input);
        var m = Assert.Single(merged);
        Assert.Equal(0.9, m.LineRate);
        Assert.Equal(5, m.Complexity);
    }

    [Fact]
    public void Merge_treats_different_signatures_as_distinct()
    {
        var input = new[]
        {
            new MethodCoverage("T", "M", "(System.Int32)", "f.cs", 0.5, 3),
            new MethodCoverage("T", "M", "(System.String)", "f.cs", 0.5, 4),
        };
        Assert.Equal(2, CoverageMerger.Merge(input).Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter CoverageMergerTests`
Expected: FAIL — `CoverageMerger` undefined.

- [ ] **Step 3: Write the implementation**

```csharp
// src/Crap4Dotnet.Core/CoverageMerger.cs
namespace Crap4Dotnet.Core;

public static class CoverageMerger
{
    public static IReadOnlyList<MethodCoverage> Merge(IEnumerable<MethodCoverage> methods) =>
        methods
            .GroupBy(m => (m.TypeName, m.MethodName, m.Signature))
            .Select(g => new MethodCoverage(
                g.Key.TypeName,
                g.Key.MethodName,
                g.Key.Signature,
                g.First().FileName,
                g.Max(x => x.LineRate),
                g.Select(x => x.Complexity).FirstOrDefault(x => x is not null) ?? g.First().Complexity))
            .ToList();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter CoverageMergerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): merge duplicate methods across coverage files"
```

---

### Task 6: Source scope filter (targets + `--changed`)

**Files:**
- Create: `src/Crap4Dotnet.Core/SourceScopeFilter.cs`
- Test: `tests/Crap4Dotnet.Tests/SourceScopeFilterTests.cs`

**Interfaces:**
- Consumes: `MethodCoverage`.
- Produces: `public static IReadOnlyList<MethodCoverage> SourceScopeFilter.Apply(IReadOnlyList<MethodCoverage> methods, IReadOnlyCollection<string> targetPaths, IReadOnlyCollection<string>? changedFiles)`.

**Matching rule (deterministic):** normalize each path to lowercase forward-slash segments (dropping `.` and empty segments); a method matches a target/changed path when the path's segment sequence is a **contiguous sub-sequence** of the method's file segments. This handles file targets (`src/Foo.cs`), directory targets (`src/Bar`), absolute-vs-relative Cobertura filenames, and repo-relative git paths uniformly.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Crap4Dotnet.Tests/SourceScopeFilterTests.cs
using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class SourceScopeFilterTests
{
    private static MethodCoverage M(string file) => new("T", "M", "()", file, 0.5, 3);
    private static readonly IReadOnlyList<MethodCoverage> Methods =
    [
        M("/repo/src/Foo.cs"),
        M("src/Bar/Baz.cs"),
        M(@"C:\repo\src\Qux.cs"),
    ];

    [Fact]
    public void No_targets_no_changed_returns_all()
    {
        Assert.Equal(3, SourceScopeFilter.Apply(Methods, [], null).Count);
    }

    [Fact]
    public void File_target_matches_regardless_of_absolute_or_relative()
    {
        var r = SourceScopeFilter.Apply(Methods, ["src/Foo.cs"], null);
        Assert.Single(r);
        Assert.Equal("/repo/src/Foo.cs", r[0].FileName);
    }

    [Fact]
    public void Directory_target_matches_files_under_it()
    {
        var r = SourceScopeFilter.Apply(Methods, ["src/Bar"], null);
        Assert.Single(r);
        Assert.Equal("src/Bar/Baz.cs", r[0].FileName);
    }

    [Fact]
    public void Backslash_paths_normalize()
    {
        var r = SourceScopeFilter.Apply(Methods, ["src/Qux.cs"], null);
        Assert.Single(r);
    }

    [Fact]
    public void Changed_and_targets_intersect()
    {
        var r = SourceScopeFilter.Apply(
            Methods,
            targetPaths: ["src"],
            changedFiles: ["src/Foo.cs"]);
        Assert.Single(r);
        Assert.Equal("/repo/src/Foo.cs", r[0].FileName);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter SourceScopeFilterTests`
Expected: FAIL — `SourceScopeFilter` undefined.

- [ ] **Step 3: Write the implementation**

```csharp
// src/Crap4Dotnet.Core/SourceScopeFilter.cs
namespace Crap4Dotnet.Core;

public static class SourceScopeFilter
{
    public static IReadOnlyList<MethodCoverage> Apply(
        IReadOnlyList<MethodCoverage> methods,
        IReadOnlyCollection<string> targetPaths,
        IReadOnlyCollection<string>? changedFiles)
    {
        IEnumerable<MethodCoverage> result = methods;

        if (targetPaths.Count > 0)
        {
            var targets = targetPaths.Select(Segments).ToList();
            result = result.Where(m => targets.Any(t => IsContiguousSubsequence(Segments(m.FileName), t)));
        }

        if (changedFiles is not null)
        {
            var changed = changedFiles.Select(Segments).ToList();
            result = result.Where(m => changed.Any(c => IsContiguousSubsequence(Segments(m.FileName), c)));
        }

        return result.ToList();
    }

    private static string[] Segments(string p) =>
        p.Replace('\\', '/')
         .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Where(s => s != ".")
         .Select(s => s.ToLowerInvariant())
         .ToArray();

    private static bool IsContiguousSubsequence(string[] file, string[] target)
    {
        if (target.Length == 0 || target.Length > file.Length) return false;
        for (int i = 0; i + target.Length <= file.Length; i++)
        {
            bool all = true;
            for (int j = 0; j < target.Length; j++)
                if (file[i + j] != target[j]) { all = false; break; }
            if (all) return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter SourceScopeFilterTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): source scope filter for path targets and --changed"
```

---

### Task 7: Crap report (ordering + counts)

**Files:**
- Create: `src/Crap4Dotnet.Core/CrapReport.cs`
- Test: `tests/Crap4Dotnet.Tests/CrapReportTests.cs`

**Interfaces:**
- Consumes: `MethodCoverage`, `MethodCrap`, `CrapCalculator.Score`.
- Produces: `record CrapReport(IReadOnlyList<MethodCrap> Ordered, double Threshold, int Total, int OverThreshold)` and `public static CrapReport CrapReport.Build(IReadOnlyList<MethodCoverage> methods, double threshold)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Crap4Dotnet.Tests/CrapReportTests.cs
using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class CrapReportTests
{
    [Fact]
    public void Build_orders_worst_first_with_na_last_and_counts_breaches()
    {
        var methods = new[]
        {
            new MethodCoverage("T", "Low", "()", "f.cs", 1.0, 1),   // crap 1
            new MethodCoverage("T", "High", "()", "f.cs", 0.0, 10),  // crap 110, breach
            new MethodCoverage("T", "Na", "()", "f.cs", null, 3),    // N/A
        };
        var report = CrapReport.Build(methods, threshold: 8.0);

        Assert.Equal("High", report.Ordered[0].MethodName);
        Assert.Equal("Low", report.Ordered[1].MethodName);
        Assert.Equal("Na", report.Ordered[2].MethodName);   // N/A sorts last
        Assert.Equal(3, report.Total);
        Assert.Equal(1, report.OverThreshold);
        Assert.Equal(8.0, report.Threshold);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter CrapReportTests`
Expected: FAIL — `CrapReport` undefined.

- [ ] **Step 3: Write the implementation**

```csharp
// src/Crap4Dotnet.Core/CrapReport.cs
namespace Crap4Dotnet.Core;

public sealed record CrapReport(
    IReadOnlyList<MethodCrap> Ordered,
    double Threshold,
    int Total,
    int OverThreshold)
{
    public static CrapReport Build(IReadOnlyList<MethodCoverage> methods, double threshold)
    {
        var scored = methods.Select(m => CrapCalculator.Score(m, threshold)).ToList();
        var ordered = scored
            .OrderByDescending(x => x.Crap.HasValue)
            .ThenByDescending(x => x.Crap ?? 0.0)
            .ToList();
        return new CrapReport(ordered, threshold, scored.Count, scored.Count(x => x.IsCrappy));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter CrapReportTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): crap report ordering and breach counts"
```

---

### Task 8: CLI project + options parsing + exit codes

**Files:**
- Create: `src/Crap4Dotnet.Cli/Crap4Dotnet.Cli.csproj`
- Create: `src/Crap4Dotnet.Cli/ExitCodes.cs`
- Create: `src/Crap4Dotnet.Cli/CliOptions.cs`
- Modify: `tests/Crap4Dotnet.Tests/Crap4Dotnet.Tests.csproj` (reference Cli)
- Test: `tests/Crap4Dotnet.Tests/CliOptionsTests.cs`

**Interfaces:**
- Consumes: nothing from Core yet.
- Produces:
  - `static class ExitCodes { const int Ok = 0, Error = 1, ThresholdExceeded = 2; }`
  - `record CliOptions(bool Help, string? CoverageFile, bool Changed, IReadOnlyList<string> Targets, string? Error)` with `public static CliOptions Parse(string[] args)`.

- [ ] **Step 1: Create the CLI project and wire references**

```bash
cd /c/Dev/ChrisNeff/crap4dotnet
dotnet new console -n Crap4Dotnet.Cli -o src/Crap4Dotnet.Cli
rm src/Crap4Dotnet.Cli/Program.cs   # replaced in Task 13
dotnet sln add src/Crap4Dotnet.Cli
dotnet add src/Crap4Dotnet.Cli reference src/Crap4Dotnet.Core
dotnet add tests/Crap4Dotnet.Tests reference src/Crap4Dotnet.Cli
```

Edit `src/Crap4Dotnet.Cli/Crap4Dotnet.Cli.csproj`: remove `<TargetFramework>` line; add tool packaging:
```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>crap4dotnet</ToolCommandName>
  <PackageId>crap4dotnet</PackageId>
  <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>
<ItemGroup>
  <None Include="../../README.md" Pack="true" PackagePath="/" Condition="Exists('../../README.md')" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing test**

```csharp
// tests/Crap4Dotnet.Tests/CliOptionsTests.cs
using Crap4Dotnet.Cli;
namespace Crap4Dotnet.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Parse_help_flag()
    {
        Assert.True(CliOptions.Parse(["--help"]).Help);
    }

    [Fact]
    public void Parse_coverage_and_targets_and_changed()
    {
        var o = CliOptions.Parse(["--coverage", "cov.xml", "src/Foo.cs", "--changed", "src/Bar"]);
        Assert.Equal("cov.xml", o.CoverageFile);
        Assert.True(o.Changed);
        Assert.Equal(new[] { "src/Foo.cs", "src/Bar" }, o.Targets);
        Assert.Null(o.Error);
    }

    [Fact]
    public void Parse_coverage_without_value_is_error()
    {
        Assert.NotNull(CliOptions.Parse(["--coverage"]).Error);
    }

    [Fact]
    public void Parse_unknown_flag_is_error()
    {
        Assert.NotNull(CliOptions.Parse(["--nope"]).Error);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test --filter CliOptionsTests`
Expected: FAIL — `ExitCodes`/`CliOptions` undefined.

- [ ] **Step 4: Write the implementations**

```csharp
// src/Crap4Dotnet.Cli/ExitCodes.cs
namespace Crap4Dotnet.Cli;

public static class ExitCodes
{
    public const int Ok = 0;
    public const int Error = 1;
    public const int ThresholdExceeded = 2;
}
```

```csharp
// src/Crap4Dotnet.Cli/CliOptions.cs
namespace Crap4Dotnet.Cli;

public sealed record CliOptions(
    bool Help,
    string? CoverageFile,
    bool Changed,
    IReadOnlyList<string> Targets,
    string? Error)
{
    public static CliOptions Parse(string[] args)
    {
        bool help = false, changed = false;
        string? coverage = null;
        var targets = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    help = true;
                    break;
                case "--changed":
                    changed = true;
                    break;
                case "--coverage":
                    if (i + 1 >= args.Length)
                        return new CliOptions(false, null, false, [], "--coverage requires a file path");
                    coverage = args[++i];
                    break;
                default:
                    if (args[i].StartsWith('-'))
                        return new CliOptions(false, null, false, [], $"unknown option: {args[i]}");
                    targets.Add(args[i]);
                    break;
            }
        }

        return new CliOptions(help, coverage, changed, targets, null);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test --filter CliOptionsTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(cli): project, exit codes, argument parsing"
```

---

### Task 9: Text report renderer

**Files:**
- Create: `src/Crap4Dotnet.Cli/TextReportRenderer.cs`
- Test: `tests/Crap4Dotnet.Tests/TextReportRendererTests.cs`

**Interfaces:**
- Consumes: `CrapReport`, `MethodCrap`.
- Produces: `public static string TextReportRenderer.Render(CrapReport report)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Crap4Dotnet.Tests/TextReportRendererTests.cs
using Crap4Dotnet.Cli;
using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class TextReportRendererTests
{
    [Fact]
    public void Render_marks_offenders_shows_na_and_footer()
    {
        var report = CrapReport.Build(new[]
        {
            new MethodCoverage("T", "High", "()", "f.cs", 0.0, 10),
            new MethodCoverage("T", "Na", "()", "f.cs", null, 3),
        }, 8.0);

        var text = TextReportRenderer.Render(report);

        Assert.Contains("* T.High()", text);       // offender flagged
        Assert.Contains("110.00", text);            // crap value
        Assert.Contains("N/A", text);               // N/A method
        Assert.Contains("2 methods", text);
        Assert.Contains("1 over 8.0", text);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter TextReportRendererTests`
Expected: FAIL — `TextReportRenderer` undefined.

- [ ] **Step 3: Write the implementation**

```csharp
// src/Crap4Dotnet.Cli/TextReportRenderer.cs
using System.Globalization;
using System.Text;
using Crap4Dotnet.Core;

namespace Crap4Dotnet.Cli;

public static class TextReportRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Render(CrapReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  {"Method",-50} {"CC",4} {"Coverage",9} {"CRAP",8}");

        foreach (var m in report.Ordered)
        {
            string flag = m.IsCrappy ? "*" : " ";
            string name = $"{m.TypeName}.{m.MethodName}{m.Signature}";
            string cc = m.Cc?.ToString(Inv) ?? "N/A";
            string cov = m.Coverage is double c ? string.Format(Inv, "{0:0.0}%", c * 100) : "N/A";
            string crap = m.Crap is double cr ? cr.ToString("0.00", Inv) : "N/A";
            sb.AppendLine($"{flag} {Truncate(name, 50),-50} {cc,4} {cov,9} {crap,8}");
        }

        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0} methods · {1} over {2:0.0}",
            report.Total, report.OverThreshold, report.Threshold));
        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : "…" + s[^(max - 1)..];
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter TextReportRendererTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(cli): plain-text report renderer"
```

---

### Task 10: Cobertura locator

**Files:**
- Create: `src/Crap4Dotnet.Cli/CoberturaLocator.cs`
- Test: `tests/Crap4Dotnet.Tests/CoberturaLocatorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public static IReadOnlyList<string> CoberturaLocator.Locate(string resultsDir)`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Crap4Dotnet.Tests/CoberturaLocatorTests.cs
using Crap4Dotnet.Cli;
namespace Crap4Dotnet.Tests;

public class CoberturaLocatorTests
{
    [Fact]
    public void Locate_finds_files_in_guid_subfolders()
    {
        string root = Path.Combine(Path.GetTempPath(), "crap4dotnet-test-" + Guid.NewGuid().ToString("N"));
        string sub = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "coverage.cobertura.xml"), "<coverage/>");
        try
        {
            var found = CoberturaLocator.Locate(root);
            Assert.Single(found);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Locate_returns_empty_for_missing_dir()
    {
        Assert.Empty(CoberturaLocator.Locate(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid())));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter CoberturaLocatorTests`
Expected: FAIL — `CoberturaLocator` undefined.

- [ ] **Step 3: Write the implementation**

```csharp
// src/Crap4Dotnet.Cli/CoberturaLocator.cs
namespace Crap4Dotnet.Cli;

public static class CoberturaLocator
{
    public static IReadOnlyList<string> Locate(string resultsDir) =>
        Directory.Exists(resultsDir)
            ? Directory.GetFiles(resultsDir, "coverage.cobertura.xml", SearchOption.AllDirectories)
            : [];
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter CoberturaLocatorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(cli): locate cobertura files under results dir"
```

---

### Task 11: Process wrappers — git changed files + dotnet test runner

**Files:**
- Create: `src/Crap4Dotnet.Cli/ProcessRunner.cs` (shared helper)
- Create: `src/Crap4Dotnet.Cli/GitChangedFilesProvider.cs`
- Create: `src/Crap4Dotnet.Cli/DotnetTestRunner.cs`
- Test: `tests/Crap4Dotnet.Tests/GitChangedFilesProviderTests.cs`

**Interfaces:**
- Consumes: nothing from Core.
- Produces:
  - `record ProcessResult(int ExitCode, string StdOut, string StdErr)` and `public static ProcessResult ProcessRunner.Run(string file, string args, string workingDir)`.
  - `public static IReadOnlyList<string> GitChangedFilesProvider.GetChangedFiles(string workingDir)`.
  - `record TestRunResult(int ExitCode, string ResultsDir, string StdErr)` and `public static TestRunResult DotnetTestRunner.Run(string workingDir)`.

**Note on testing scope:** `DotnetTestRunner` is a thin process wrapper validated by the end-to-end path in Task 13 (via `--coverage`) and manual `just` runs; it is not unit-tested in isolation (spawning `dotnet test` in unit tests is slow and environment-dependent). `GitChangedFilesProvider` gets one integration test against a real temp git repo.

- [ ] **Step 1: Write the failing test (git provider, real temp repo)**

```csharp
// tests/Crap4Dotnet.Tests/GitChangedFilesProviderTests.cs
using Crap4Dotnet.Cli;
namespace Crap4Dotnet.Tests;

public class GitChangedFilesProviderTests
{
    [Fact]
    public void GetChangedFiles_includes_untracked_and_modified()
    {
        string dir = Path.Combine(Path.GetTempPath(), "crap4dotnet-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ProcessRunner.Run("git", "init -q", dir);
            ProcessRunner.Run("git", "config user.email t@t.t", dir);
            ProcessRunner.Run("git", "config user.name t", dir);
            File.WriteAllText(Path.Combine(dir, "committed.cs"), "// v1");
            ProcessRunner.Run("git", "add -A", dir);
            ProcessRunner.Run("git", "commit -q -m init", dir);

            File.WriteAllText(Path.Combine(dir, "committed.cs"), "// v2");   // modified
            File.WriteAllText(Path.Combine(dir, "untracked.cs"), "// new"); // untracked

            var changed = GitChangedFilesProvider.GetChangedFiles(dir);
            Assert.Contains("committed.cs", changed);
            Assert.Contains("untracked.cs", changed);
        }
        finally { ForceDelete(dir); }
    }

    private static void ForceDelete(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, recursive: true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter GitChangedFilesProviderTests`
Expected: FAIL — `ProcessRunner`/`GitChangedFilesProvider` undefined.

- [ ] **Step 3: Write `ProcessRunner`**

```csharp
// src/Crap4Dotnet.Cli/ProcessRunner.cs
using System.Diagnostics;

namespace Crap4Dotnet.Cli;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

public static class ProcessRunner
{
    public static ProcessResult Run(string file, string args, string workingDir)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return new ProcessResult(p.ExitCode, stdout, stderr);
    }
}
```

- [ ] **Step 4: Write `GitChangedFilesProvider`**

```csharp
// src/Crap4Dotnet.Cli/GitChangedFilesProvider.cs
namespace Crap4Dotnet.Cli;

public static class GitChangedFilesProvider
{
    public static IReadOnlyList<string> GetChangedFiles(string workingDir)
    {
        var modified = Lines(ProcessRunner.Run("git", "diff --name-only HEAD", workingDir).StdOut);
        var untracked = Lines(ProcessRunner.Run("git", "ls-files --others --exclude-standard", workingDir).StdOut);
        return modified.Concat(untracked).Distinct().ToList();
    }

    private static IEnumerable<string> Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
```

- [ ] **Step 5: Write `DotnetTestRunner`**

```csharp
// src/Crap4Dotnet.Cli/DotnetTestRunner.cs
namespace Crap4Dotnet.Cli;

public sealed record TestRunResult(int ExitCode, string ResultsDir, string StdErr);

public static class DotnetTestRunner
{
    public static TestRunResult Run(string workingDir)
    {
        string resultsDir = Path.Combine(Path.GetTempPath(), "crap4dotnet", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(resultsDir);
        var r = ProcessRunner.Run(
            "dotnet",
            $"test --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDir}\"",
            workingDir);
        return new TestRunResult(r.ExitCode, resultsDir, r.StdErr);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test --filter GitChangedFilesProviderTests`
Expected: PASS (requires `git` on PATH).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(cli): process runner, git changed files, dotnet test runner"
```

---

### Task 12: Program wiring + end-to-end (`--coverage`)

**Files:**
- Create: `src/Crap4Dotnet.Cli/Program.cs`
- Test: `tests/Crap4Dotnet.Tests/ProgramEndToEndTests.cs`

**Interfaces:**
- Consumes: `CliOptions`, `ExitCodes`, `CoberturaParser`, `CoverageMerger`, `SourceScopeFilter`, `GitChangedFilesProvider`, `CrapReport`, `TextReportRenderer`, `DotnetTestRunner`, `CoberturaLocator`.
- Produces: `public static int Program.Run(string[] args, TextWriter outw, TextWriter errw)` and `public static int Program.Main(string[] args)`.

- [ ] **Step 1: Write the failing end-to-end test**

```csharp
// tests/Crap4Dotnet.Tests/ProgramEndToEndTests.cs
using Crap4Dotnet.Cli;
namespace Crap4Dotnet.Tests;

public class ProgramEndToEndTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.cobertura.xml");

    [Fact]
    public void Coverage_path_scores_fixture_and_exits_two_when_over_threshold()
    {
        var outw = new StringWriter();
        var errw = new StringWriter();
        // sample fixture: Bar cc5/cov0.5 -> 8.125 (>8). So expect ThresholdExceeded.
        int code = Program.Run(["--coverage", Fixture], outw, errw);

        Assert.Equal(ExitCodes.ThresholdExceeded, code);
        Assert.Contains("MyAssembly.Foo.Bar", outw.ToString());
        Assert.Contains("over 8.0", outw.ToString());
    }

    [Fact]
    public void Missing_coverage_file_is_error()
    {
        int code = Program.Run(["--coverage", "nope.xml"], new StringWriter(), new StringWriter());
        Assert.Equal(ExitCodes.Error, code);
    }

    [Fact]
    public void Help_exits_zero()
    {
        Assert.Equal(ExitCodes.Ok, Program.Run(["--help"], new StringWriter(), new StringWriter()));
    }

    [Fact]
    public void Target_filter_limits_scope()
    {
        var outw = new StringWriter();
        int code = Program.Run(["--coverage", Fixture, "does/not/match.cs"], outw, new StringWriter());
        Assert.Equal(ExitCodes.Ok, code);           // nothing matched -> no breaches
        Assert.Contains("0 methods", outw.ToString());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter ProgramEndToEndTests`
Expected: FAIL — `Program` undefined.

- [ ] **Step 3: Write `Program`**

```csharp
// src/Crap4Dotnet.Cli/Program.cs
using Crap4Dotnet.Core;

namespace Crap4Dotnet.Cli;

public static class Program
{
    private const double Threshold = 8.0;

    private const string HelpText =
        """
        crap4dotnet — CRAP score per method (CC² × (1 − coverage)³ + CC)

        Usage:
          crap4dotnet [targets...]         run tests, score all (or matching) methods
          crap4dotnet --changed            score only git-modified files
          crap4dotnet --coverage <file>    score an existing Cobertura XML (skip tests)
          crap4dotnet --help               show this help

        Exit codes: 0 clean · 1 error · 2 threshold (8.0) exceeded
        """;

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter outw, TextWriter errw)
    {
        var opts = CliOptions.Parse(args);
        if (opts.Error is not null) { errw.WriteLine(opts.Error); return ExitCodes.Error; }
        if (opts.Help) { outw.WriteLine(HelpText); return ExitCodes.Ok; }

        string cwd = Directory.GetCurrentDirectory();

        IReadOnlyList<string> files;
        if (opts.CoverageFile is not null)
        {
            if (!File.Exists(opts.CoverageFile))
            {
                errw.WriteLine($"coverage file not found: {opts.CoverageFile}");
                return ExitCodes.Error;
            }
            files = [opts.CoverageFile];
        }
        else
        {
            var run = DotnetTestRunner.Run(cwd);
            if (run.ExitCode != 0)
            {
                errw.WriteLine(run.StdErr.Length > 0 ? run.StdErr : "dotnet test failed");
                return ExitCodes.Error;
            }
            files = CoberturaLocator.Locate(run.ResultsDir);
            if (files.Count == 0) { errw.WriteLine("no coverage produced"); return ExitCodes.Error; }
        }

        var parsed = new List<MethodCoverage>();
        foreach (var f in files)
        {
            try
            {
                using var s = File.OpenRead(f);
                parsed.AddRange(CoberturaParser.Parse(s));
            }
            catch (Exception ex)
            {
                errw.WriteLine($"failed to parse {f}: {ex.Message}");
                return ExitCodes.Error;
            }
        }

        var merged = CoverageMerger.Merge(parsed);
        var changed = opts.Changed ? GitChangedFilesProvider.GetChangedFiles(cwd) : null;
        var scoped = SourceScopeFilter.Apply(merged, opts.Targets, changed);
        var report = CrapReport.Build(scoped, Threshold);

        outw.Write(TextReportRenderer.Render(report));
        return report.OverThreshold > 0 ? ExitCodes.ThresholdExceeded : ExitCodes.Ok;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter ProgramEndToEndTests`
Expected: PASS.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test`
Expected: PASS (all tests).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(cli): wire pipeline and end-to-end --coverage path"
```

---

### Task 13: justfile + README + dogfood

**Files:**
- Create: `justfile`
- Create: `README.md`
- Test: manual `just` runs (no unit test; deliverable is working tooling)

**Interfaces:**
- Consumes: the built solution and the global-tool package.
- Produces: `just` recipes per the spec §9; a README documenting install/usage.

- [ ] **Step 1: Write `justfile`**

```make
default:
    @just --list

build:
    dotnet build crap4dotnet.sln

test:
    dotnet test crap4dotnet.sln

coverage:
    dotnet test crap4dotnet.sln --collect:"XPlat Code Coverage" --results-directory ./artifacts/test
    # resolve the newest produced cobertura to a stable path for dogfooding
    powershell -NoProfile -Command "New-Item -ItemType Directory -Force ./artifacts | Out-Null; \
      Get-ChildItem -Recurse ./artifacts/test -Filter coverage.cobertura.xml | \
      Sort-Object LastWriteTime -Descending | Select-Object -First 1 | \
      ForEach-Object { Copy-Item $_.FullName ./artifacts/coverage.cobertura.xml -Force }"

run *ARGS:
    dotnet run --project src/Crap4Dotnet.Cli -- {{ARGS}}

pack:
    dotnet pack src/Crap4Dotnet.Cli -c Release -o ./artifacts/nupkg

install: pack
    dotnet tool install -g --add-source ./artifacts/nupkg crap4dotnet

uninstall:
    -dotnet tool uninstall -g crap4dotnet

dogfood: coverage
    dotnet run --project src/Crap4Dotnet.Cli -- --coverage ./artifacts/coverage.cobertura.xml

ci: build test pack

clean:
    -rm -rf ./artifacts
    dotnet clean crap4dotnet.sln
```

- [ ] **Step 2: Write `README.md`** (CRAP explanation, install via `just install`, usage table mirroring `--help`, exit codes, attribution to crap4j).

- [ ] **Step 3: Verify recipes**

Run: `just ci`
Expected: build + test + pack all succeed; `./artifacts/nupkg/crap4dotnet.*.nupkg` exists.

Run: `just dogfood`
Expected: prints a CRAP report for crap4dotnet's own code.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: justfile task runner and README"
```

---

### Task 14: GitHub Actions CI

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `just ci`.
- Produces: a push/PR workflow that installs .NET 10 + just and runs `just ci`.

- [ ] **Step 1: Write the workflow**

```yaml
# .github/workflows/ci.yml
name: ci
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - uses: extractions/setup-just@v2
      - run: just ci
```

- [ ] **Step 2: Validate locally**

Run: `just ci`
Expected: PASS (same command CI runs).

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "ci: build, test, pack via just on push and PR"
```

---

## Self-Review

**1. Spec coverage:**
- §1 CRAP formula → Task 2. §3 Core/Cli split → Tasks 2–12. §4 all Core types → Tasks 2–7. §5 all Cli components → Tasks 8–12. §6 CLI contract + exit codes → Tasks 8, 12. §7 orchestration/errors → Tasks 11, 12. §8 testing → every task (TDD) + Task 12 e2e + Task 13 dogfood. §9 justfile → Task 13. §10 packaging/license/CI → Tasks 1, 8, 13, 14. §11 filename normalization risk → Task 6 (dedicated tests). No gaps.

**2. Placeholder scan:** No TBD/TODO; every code and test step is concrete. README body (Task 13 Step 2) is described by required content, not code — acceptable for a docs deliverable.

**3. Type consistency:** `MethodCoverage`, `MethodCrap`, `CrapReport`, `CliOptions`, `ProcessResult`, `TestRunResult` signatures are identical across defining task and all consumers. `CrapCalculator.Score(MethodCoverage, double)` and `CrapReport.Build(IReadOnlyList<MethodCoverage>, double)` used consistently in Tasks 7, 9, 12. `ExitCodes.{Ok,Error,ThresholdExceeded}` used consistently in Tasks 8, 12. `Program.Run(string[], TextWriter, TextWriter)` defined and consumed in Task 12.

**Known environmental dependencies:** tests in Tasks 11 require `git` on PATH; `just`/CI require .NET 10 SDK. Both are documented Global Constraints / prerequisites.
