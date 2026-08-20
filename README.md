# crap4dotnet

A .NET 10 command-line tool that computes the **CRAP** (Change Risk Anti-Patterns)
score for every method in a C# codebase and reports the worst offenders — a
faithful, idiomatic port of Robert C. Martin's
[crap4j](https://github.com/unclebob/crap4java).

CRAP flags methods that are both **complex** and **under-tested** — the code most
likely to break when you change it.

```
CRAP(method) = CC² × (1 − coverage)³ + CC
```

where `CC` is the method's cyclomatic complexity and `coverage` is its test
coverage ratio. The cubic `(1 − coverage)³` term punishes complex code that lacks
tests; the linear `+ CC` keeps even well-tested complex code on the radar. The
default "too crappy" threshold is **8.0**.

Both complexity and coverage are read from the same
[Coverlet](https://github.com/coverlet-coverage/coverlet)-produced Cobertura XML,
so the two numbers can never be attributed to different methods.

## Status

🚧 Under construction — see [`docs/superpowers/specs`](docs/superpowers/specs)
for the design and [`docs/superpowers/plans`](docs/superpowers/plans) for the
implementation plan.

## Install

Ships as a .NET global tool:

```bash
# from a local build
just install                      # pack + install from ./artifacts/nupkg

# once published (future)
dotnet tool install -g crap4dotnet
```

## Usage

```bash
crap4dotnet                       # run tests with coverage, score all methods
crap4dotnet ./src/Foo.cs ./src/   # score only methods in the given files/dirs
crap4dotnet --changed             # score only git-modified files
crap4dotnet --coverage cov.xml    # score an existing Cobertura file (skip tests)
crap4dotnet --help
```

**Exit codes:** `0` clean · `1` error · `2` at least one method exceeds the 8.0 threshold.

## Development

Common tasks run through [`just`](https://github.com/casey/just):

```bash
just            # list recipes
just build
just test
just ci         # build + test + pack (what CI runs)
just dogfood    # run crap4dotnet on its own coverage
```

## Credits

crap4dotnet implements the **CRAP metric** popularized by
[crap4j](https://github.com/unclebob/crap4java) (Robert C. Martin); the metric
originates with Alberto Savoia and Bob Evans. This is an independent
implementation of the published algorithm — it contains no crap4j source. See
[`NOTICE`](NOTICE).

## License

[MIT](LICENSE)
