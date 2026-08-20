default:
    @just --list

build:
    dotnet build

test:
    dotnet test

coverage:
    dotnet test --collect:"XPlat Code Coverage" --results-directory ./artifacts/test
    # resolve the newest produced cobertura to a stable path for dogfooding
    powershell -NoProfile -Command "New-Item -ItemType Directory -Force ./artifacts | Out-Null; \
      Get-ChildItem -Recurse ./artifacts/test -Filter coverage.cobertura.xml | \
      Sort-Object LastWriteTime -Descending | Select-Object -First 1 | \
      ForEach-Object { Copy-Item \$_.FullName ./artifacts/coverage.cobertura.xml -Force }"

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
    dotnet clean
