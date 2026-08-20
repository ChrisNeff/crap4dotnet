# crap4dotnet — Design Spec

**Date:** 2026-08-20
**Status:** Approved design, pre-implementation
**Author:** Christopher Neff (with Claude)

---

## 1. Summary

`crap4dotnet` is a .NET 10 command-line tool that computes the **CRAP**
(Change Risk Anti-Patterns) score for every method in a C# codebase and reports
the worst offenders. It is a faithful, idiomatic .NET port of Robert C. Martin's
[`crap4java`](https://github.com/unclebob/crap4java) — same metric, same default
threshold (8.0), same run-or-consume-coverage behavior, same 0/1/2 exit-code
contract.

It ships as a **.NET global tool** (`dotnet tool install -g crap4dotnet`,
invoked as `crap4dotnet`), the direct equivalent of crap4java's runnable jar.

### The CRAP metric

For a method *m*:

```
CRAP(m) = CC² × (1 − coverage)³ + CC
```

- **CC** — cyclomatic complexity of the method.
- **coverage** — the method's test-coverage ratio in `[0, 1]`.

CRAP punishes methods that are both complex and under-tested (the cubic
`(1 − coverage)³` term), while the linear `+ CC` keeps even well-tested complex
methods on the radar. The default "too crappy" threshold is **8.0**.

Both **CC and coverage are read from the same Coverlet-produced Cobertura XML
node** (`<method complexity="…" line-rate="…">`), which eliminates the
method-matching problem entirely: the complexity number and the coverage number
live on the same element, so they can never be attributed to different methods.

---

## 2. Goals & Non-Goals

### Goals (v1 — full parity with crap4j)

- Compute CRAP per method from Coverlet Cobertura coverage.
- CLI surface identical to crap4java:
  - no args → analyze all methods,
  - positional file/directory targets → analyze only those,
  - `--changed` → analyze only git-modified files,
  - `--help`.
- Fixed 8.0 threshold; report sorted worst-first; exit `2` when exceeded.
- Orchestrate `dotnet test` by default; consume an existing file via
  `--coverage <path>` (additive convenience, does not alter default behavior).
- Ship as a packable global tool; installable locally today.
- Public repo, MIT-licensed, with attribution to crap4j and the CRAP metric.
- CI (GitHub Actions) that restores, builds, tests, and packs on every push.
- A `justfile` as the standard task-runner entry point; CI and README invoke it.

### Non-Goals (explicitly NOT in v1 — not present in crap4j)

These are excluded to keep the port faithful ("not expanded"). Each is a
potential future spec, not part of this one:

- Configurable threshold (`--threshold`) — crap4j's 8.0 is fixed.
- Alternate output formats (JSON / HTML / SARIF).
- Rich console rendering (e.g. Spectre.Console) — v1 is plain aligned text.
- MSBuild task / analyzer integration.
- Publishing to NuGet.org (a later "add release workflow" step).
- Branch-coverage CRAP variant.

---

## 3. Architecture

Two production projects plus tests. The **Core library performs no console or
process I/O** — it turns parsed coverage into scores and a report model. All
non-deterministic, hard-to-test work (spawning `dotnet test`, locating the
output XML, reading git, writing to the console) lives in the thin **Cli**
shell. This separation is what lets the tool score *itself* well — a code-quality
tool must be a good code-quality citizen.

### 3.1 Solution layout

```
crap4dotnet/                         repo root (C:\Dev\ChrisNeff\crap4dotnet, branch main)
├─ crap4dotnet.sln
├─ justfile                          standard task runner (see §9)
├─ Directory.Build.props             net10.0 · nullable enable · implicit usings · analyzers · shared metadata
├─ LICENSE                           MIT
├─ NOTICE                            attribution: crap4j (Robert C. Martin) + CRAP metric origin
├─ README.md
├─ .gitignore                        (dotnet + ./artifacts)
├─ .github/workflows/ci.yml          restore → build → test → pack  (calls `just ci`)
├─ src/
│  ├─ Crap4Dotnet.Core/              class library — pure, no console/process I/O
│  │   Crap4Dotnet.Core.csproj
│  │   MethodCoverage.cs
│  │   CoberturaParser.cs
│  │   CrapCalculator.cs
│  │   MethodCrap.cs
│  │   CoverageMerger.cs
│  │   SourceScopeFilter.cs
│  │   CrapReport.cs
│  └─ Crap4Dotnet.Cli/               exe · PackAsTool · ToolCommandName=crap4dotnet
│      Crap4Dotnet.Cli.csproj
│      Program.cs
│      CliOptions.cs
│      DotnetTestRunner.cs
│      CoberturaLocator.cs
│      GitChangedFilesProvider.cs
│      TextReportRenderer.cs
│      ExitCodes.cs
└─ tests/
   └─ Crap4Dotnet.Tests/             xUnit over Core
       Crap4Dotnet.Tests.csproj
       CrapCalculatorTests.cs
       CoberturaParserTests.cs
       CoverageMergerTests.cs
       SourceScopeFilterTests.cs
       CrapReportTests.cs
       fixtures/sample.cobertura.xml
```

### 3.2 Data flow

```
args ─▶ CliOptions (parse: --help | --coverage <f> | --changed | positional targets)
          │  no --coverage?                              --coverage <file>?
          ▼                                                     │
   DotnetTestRunner                                             │
     "dotnet test --collect:\"XPlat Code Coverage\"             │
        --results-directory <temp we own>"                      │
          │  exit code + temp dir                               │
          ▼                                                     │
   CoberturaLocator (glob <temp>/**/coverage.cobertura.xml) ────┤
          ▼                                                     ▼
   CoberturaParser  ─▶ IReadOnlyList<MethodCoverage>   (per file)
          ▼
   CoverageMerger   ─▶ union by (Type, Method, Signature); keep best coverage
          ▼
   SourceScopeFilter ─▶ filter by targets ∩ changed-files (if any)
          ▼
   CrapCalculator   ─▶ MethodCrap per method
          ▼
   CrapReport.Build(methods, threshold: 8.0) ─▶ ordered worst-first + counts
          ▼
   TextReportRenderer ─▶ console       ExitCodes ─▶ process exit (0 | 1 | 2)
```

---

## 4. Core components

All types live in `Crap4Dotnet.Core`, are pure, and are fully unit-tested.

### `MethodCoverage`
Immutable record of one parsed `<method>` node:
```
record MethodCoverage(
    string TypeName,       // <class name="...">
    string MethodName,     // <method name="...">
    string Signature,      // <method signature="...">
    string FileName,       // <class filename="...">
    double? LineRate,      // <method line-rate="..."> → coverage in [0,1], null if absent
    int?    Complexity);   // <method complexity="..."> → CC, null if absent
```

### `CoberturaParser`
`IReadOnlyList<MethodCoverage> Parse(Stream xml)` — walks
`coverage/packages/package/classes/class/methods/method`, tolerant of missing
attributes (missing `line-rate` or `complexity` → `null`, surfaced as `N/A`
downstream). Uses `System.Xml.Linq`. No exceptions for well-formed-but-sparse
files; throws only on malformed XML (caught by the CLI → exit 1).

### `CrapCalculator`
The pure formula. The most heavily tested unit in the codebase.
```
double Crap(int cc, double coverage);          // coverage clamped to [0,1]
MethodCrap Score(MethodCoverage m);            // null CC or coverage → Crap = null
```
Reference values (used as tests): `Crap(1, 1.0) = 1`; `Crap(10, 0.0) = 110`;
`Crap(5, 0.5) = 8.125` (just over threshold); `Crap(2, 1.0) = 2`.

### `MethodCrap`
```
record MethodCrap(
    string TypeName, string MethodName, string Signature, string FileName,
    int?    Cc,
    double? Coverage,
    double? Crap,          // null when Cc or Coverage unavailable
    bool    IsCrappy);     // Crap > threshold; false when Crap is null
```
`Crap == null` methods render as `N/A` and are **excluded** from the
over-threshold count (mirrors crap4j's N/A handling).

### `CoverageMerger`
When multiple test projects produce multiple Cobertura files, the same method
may appear more than once. Merge by `(TypeName, MethodName, Signature)`, keeping
the **highest** `LineRate` (best evidence the method is exercised); complexity is
expected to be identical across files, take the first non-null.

### `SourceScopeFilter`
Pure filter over `MethodCoverage` by source file:
```
IReadOnlyList<MethodCoverage> Apply(
    IReadOnlyList<MethodCoverage> methods,
    IReadOnlyCollection<string> targetPaths,     // files (exact) or dirs (prefix); empty = all
    IReadOnlyCollection<string>? changedFiles);  // null = ignore; non-null = intersect
```
- No targets and `changedFiles == null` → return all.
- Targets only → keep methods whose `FileName` equals a target file or is under a
  target directory.
- `--changed` only → keep methods whose `FileName` is in `changedFiles`.
- Both → **intersection** (changed files that also fall under the targets).

**Filename normalization** (the one genuinely fiddly parity detail): Cobertura
`filename` may be absolute or project-relative depending on Coverlet config; git
and CLI paths are repo-relative. The filter normalizes all paths to full,
`Path.GetFullPath`-canonical, case-insensitive-on-Windows form, and falls back to
a path-suffix match when a full-path match fails. This behavior gets its own
focused test class (`SourceScopeFilterTests`) with both absolute and relative
Cobertura fixtures.

### `CrapReport`
```
record CrapReport(
    IReadOnlyList<MethodCrap> Ordered,   // CRAP descending, N/A sorted last
    double Threshold,                    // 8.0
    int    Total,                        // methods scored
    int    OverThreshold);               // count with IsCrappy == true
static CrapReport Build(IReadOnlyList<MethodCrap> methods, double threshold);
```

---

## 5. CLI components (`Crap4Dotnet.Cli`)

Kept deliberately thin. Argument parsing is hand-rolled (a handful of flags — no
dependency on the still-churning `System.CommandLine`).

- **`CliOptions`** — parses argv into `{ bool Help, string? CoverageFile,
  bool Changed, IReadOnlyList<string> Targets }`; validation errors → exit 1.
- **`DotnetTestRunner`** — wraps `System.Diagnostics.Process` to run
  `dotnet test --collect:"XPlat Code Coverage" --results-directory <temp>` in the
  current directory. Owns a temp results dir it creates, so output lands under a
  **known root**. Returns `{ int ExitCode, string ResultsDir, string StdErr }`.
- **`CoberturaLocator`** — globs `<ResultsDir>/**/coverage.cobertura.xml`,
  returns all matches (one per test project).
- **`GitChangedFilesProvider`** — runs `git` to compute the changed set for
  `--changed`. "Changed" = files differing from `HEAD`, **including staged and
  working-tree modifications and untracked files** (`git diff --name-only HEAD`
  ∪ `git ls-files --others --exclude-standard`). Repo-relative paths, handed to
  `SourceScopeFilter`.
- **`TextReportRenderer`** — formats a `CrapReport` as an aligned plain-text
  table: `Method | CC | Coverage | CRAP`, worst-first, offenders marked with a
  leading `*`, `N/A` for unavailable cells, footer
  `N methods · M over 8.0`. Zero extra dependencies; the format string is
  unit-testable via the report model.
- **`Program`** — wires the pipeline, maps outcomes to exit codes.
- **`ExitCodes`** — `Ok = 0`, `Error = 1`, `ThresholdExceeded = 2`.

---

## 6. CLI contract

| Invocation | Behavior |
|---|---|
| `crap4dotnet` | Run `dotnet test` w/ coverage, score **all** methods |
| `crap4dotnet ./src/Foo.cs ./src/Bar/` | Score only methods whose source file matches those files/dirs |
| `crap4dotnet --changed` | Score only methods in git-modified files |
| `crap4dotnet --coverage <file> [targets…] [--changed]` | Same selection logic, but skip the test run and score `<file>` |
| `crap4dotnet --help` | Usage and exit 0 |

### Exit codes — strict crap4j parity (0/1/2 only)

- **0** — ran to completion, nothing over threshold.
- **2** — reserved *exclusively* for "ran fine, but at least one method exceeds
  8.0."
- **1** — every error path: invalid usage, `dotnet test` failed to run, no
  coverage file produced/found, malformed or unreadable Cobertura XML.

A distinct code (e.g. `3`) for execution errors is deliberately **not**
introduced, to keep the observable contract byte-for-byte identical to crap4j.
Error detail is written to stderr; the exit code stays within crap4j's scheme.

---

## 7. Orchestration & error handling

- `DotnetTestRunner` passes an explicit `--results-directory <temp>` we own, so
  Coverlet's GUID subfolder lands under a known root — no fragile hunting through
  the default `TestResults/<guid>/` tree.
- `dotnet test` non-zero exit, or zero coverage files located → **exit 1**, with
  captured stderr surfaced to the user.
- Malformed/unreadable XML → **exit 1**, clear message.
- Zero methods after filtering → empty report, **exit 0** (with a one-line note
  that nothing matched).
- A method missing `complexity` or `line-rate` → listed with `N/A`, excluded
  from the over-threshold count.

---

## 8. Testing strategy

- **Core is exhaustively unit-tested** (the deterministic heart):
  - `CrapCalculatorTests` — reference values above, clamping, `N/A` propagation.
  - `CoberturaParserTests` — parse `fixtures/sample.cobertura.xml`; sparse
    attributes; malformed input.
  - `CoverageMergerTests` — duplicate methods across files; best-coverage wins.
  - `SourceScopeFilterTests` — file vs dir targets; `--changed`; intersection;
    absolute-vs-relative filename normalization.
  - `CrapReportTests` — ordering (worst-first, N/A last), threshold counts.
- **CLI**: an end-to-end test drives `Program` via `--coverage <fixture>` (no
  process spawn required), asserting the rendered report and exit code.
  `DotnetTestRunner` and `GitChangedFilesProvider` are thin process wrappers,
  validated by one integration test each against a committed sample.
- **Dogfooding**: CI runs `just dogfood` — `crap4dotnet` scoring its own
  coverage — as a bonus gate and living example.

---

## 9. `justfile` (standard task runner)

```
default:          # list recipes
    @just --list

build:
    dotnet build crap4dotnet.sln

test:
    dotnet test crap4dotnet.sln --collect:"XPlat Code Coverage" --results-directory ./artifacts/test

coverage: test    # produce a single cobertura at ./artifacts/coverage.cobertura.xml

run *ARGS:
    dotnet run --project src/Crap4Dotnet.Cli -- {{ARGS}}

pack:
    dotnet pack src/Crap4Dotnet.Cli -c Release -o ./artifacts/nupkg

install: pack
    dotnet tool install -g --add-source ./artifacts/nupkg crap4dotnet

uninstall:
    -dotnet tool uninstall -g crap4dotnet

dogfood:
    dotnet run --project src/Crap4Dotnet.Cli -- --coverage ./artifacts/coverage.cobertura.xml

ci: build test pack

clean:
    -rm -rf ./artifacts
    dotnet clean crap4dotnet.sln
```

README and `.github/workflows/ci.yml` invoke these recipes so local and CI runs
are identical (`just ci`).

> Note: `dotnet test` writes `coverage.cobertura.xml` into a GUID subfolder under
> the results directory. The `coverage` recipe is responsible for resolving that
> single produced file to the stable path `./artifacts/coverage.cobertura.xml`
> (locate-newest + copy) so `dogfood` has a deterministic input. The exact glue
> is finalized during implementation; the recipe contract above is the intent.

---

## 10. Packaging, licensing, CI

- **Global tool**: `Crap4Dotnet.Cli.csproj` sets `PackAsTool=true`,
  `ToolCommandName=crap4dotnet`, `PackageId=crap4dotnet`, plus authors, license,
  repo URL, and README for the package.
- **Local install** (today): `just install` (pack → install from `./artifacts/nupkg`).
- **License**: MIT. `NOTICE` credits crap4j (Robert C. Martin) and the origin of
  the CRAP metric (Alberg & Savoia; popularized by Bob Evans' crap4j). We
  implement the published *algorithm*, not crap4j's source.
- **CI**: GitHub Actions on push/PR — `actions/setup-dotnet` (10.x) → `just ci`
  (restore, build, test, pack). **No NuGet.org publish** in v1; adding a release
  workflow later is additive.

---

## 11. Open questions / risks

- **Coverlet complexity semantics**: Coverlet's `complexity` attribute is
  cyclomatic complexity but its exact counting may differ slightly from crap4j's
  JaCoCo-derived CC. Acceptable for a faithful *metric* port; noted so scores
  aren't expected to match crap4j's to the decimal on equivalent code.
- **Filename normalization** across Coverlet configurations is the highest-risk
  area; mitigated by dedicated tests with both absolute and relative fixtures.
- **`--changed` base**: defined as "differs from `HEAD` (staged + working tree +
  untracked)." If a branch-vs-merge-base definition is later wanted, that's an
  additive change, not a rework.
