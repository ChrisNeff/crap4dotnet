namespace Crap4Dotnet.Core;

public sealed record MethodCoverage(
    string TypeName,
    string MethodName,
    string Signature,
    string FileName,
    double? LineRate,
    int? Complexity);
