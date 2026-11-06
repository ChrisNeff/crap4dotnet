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
