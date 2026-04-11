namespace AgentNotes.Core;

/// <summary>Значения по умолчанию для hot-context: бюджеты, списки секций, блоклист тяжёлых L1.
/// Переопределение через <c>memory-architecture-v1</c> JSON (см. <see cref="MemoryArchitectureManifestData"/>).</summary>
internal static class HotContextDefaults
{
    /// <summary>Порог предупреждения <c>memory_health</c> (сумма символов hot-секций).</summary>
    public const int HotContextBudgetWarningChars = 6000;

    /// <summary>Порог critical <c>memory_health</c>.</summary>
    public const int HotContextBudgetCriticalChars = 12000;

    public static readonly string[] DefaultL0Ids =
    [
        "baseline-integrity-epistemic-v1",
        "epistemic-default-distrust-v1",
        "active-scope",
        "current-task",
        "core-software-context",
        "language-style-ru",
        "personal-workstyle-v1",
        "execution-gate-v1",
        "response-finalizer-v1",
        "hot-context-writing-contract",
        "ontology-router-v1"
    ];

    public static readonly string[] DefaultCompactOrderSuffix =
    [
        "workspace-scope-map-v1",
        "scope-door-to-singularity",
        "scope-portal",
        "scope-mixed",
        "memory-architecture-v1",
        "memory-load-policy-v1",
        "memory-compaction-loop-v1",
        "archive-index-v1"
    ];

    public static readonly string[] RequiredCoreSectionIds =
    [
        "active-scope",
        "current-task"
    ];

    /// <summary>L1 / тяжёлые секции: не включать в hot, даже если перечислены в L0 манифеста.
    /// Дополнительные id — в JSON <c>hot_context_section_exclusions</c>.</summary>
    public static bool IsBuiltInHotExclusion(string sectionId)
    {
        return sectionId.StartsWith("hpmor-", StringComparison.OrdinalIgnoreCase)
            || sectionId.Equals("it-source-mini-index-v1", StringComparison.Ordinal)
            || sectionId.Equals("knowledge-index-v1", StringComparison.Ordinal)
            || sectionId.Equals("imc-ui-ux-vision-v1", StringComparison.Ordinal)
            || sectionId.Equals("psychology-gender-studies-subdomain-v1", StringComparison.Ordinal)
            || sectionId.Equals("world-human-system-v1", StringComparison.Ordinal)
            || sectionId.Equals("world-human-system-playbook-v1", StringComparison.Ordinal);
    }
}

/// <summary>Данные из JSON, на который указывает <c>l0_manifest:</c> в секции memory-architecture-v1.</summary>
internal sealed record MemoryArchitectureManifestData(
    IReadOnlyList<string> L0,
    IReadOnlyList<string>? CompactOrderSuffix,
    int? HotBudgetWarningChars,
    int? HotBudgetCriticalChars,
    IReadOnlyList<string>? HotContextSectionExclusions);
