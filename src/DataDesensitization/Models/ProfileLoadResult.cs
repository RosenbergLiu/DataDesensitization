namespace DataDesensitization.Models;

public record ProfileLoadResult(
    List<DesensitizationRule> MatchedRules,
    List<DesensitizationRule> UnmatchedRules);
