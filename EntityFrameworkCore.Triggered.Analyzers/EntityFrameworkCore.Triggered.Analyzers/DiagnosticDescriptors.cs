using Microsoft.CodeAnalysis;

namespace EntityFrameworkCore.Triggered.Analyzers;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor EFCT001_OldTriggerSignature = new(
        id: "EFCT001",
        title: "Trigger method has old async signature",
        messageFormat: "Class '{0}' implements '{1}' with the old async method signature. Migrate to '{2}' (async) or update to the new sync signature.",
        category: "Migration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The trigger interface was split into sync and async variants. Methods returning Task with a CancellationToken parameter should use the async interface variant, or be converted to the new sync signature.");
}
