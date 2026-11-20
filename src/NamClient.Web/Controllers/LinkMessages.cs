using NamClient.Web.Services;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Controllers;

internal static class LinkMessages
{
    public static StatusMessage Describe(LinkOutcome outcome, string noun, string nounPlural)
    {
        string Count(int n) => n == 1 ? $"1 {noun}" : $"{n:N0} {nounPlural}";

        var parts = new List<string>();
        if (outcome.Linked > 0)
        {
            parts.Add($"{Count(outcome.Linked)} linked.");
        }

        if (outcome.AlreadyLinked > 0)
        {
            parts.Add($"{Count(outcome.AlreadyLinked)} already linked.");
        }

        if (outcome.Missing > 0)
        {
            parts.Add($"{Count(outcome.Missing)} no longer exist.");
        }

        var text = parts.Count > 0 ? string.Join(' ', parts) : "Nothing to link.";
        return outcome.Linked > 0 ? StatusMessage.Success(text) : StatusMessage.Error(text);
    }

    public static StatusMessage DescribeUnlink(int removed, string noun, string nounPlural) =>
        removed switch
        {
            0 => StatusMessage.Error($"Nothing was unlinked; the {nounPlural} may already have been removed."),
            1 => StatusMessage.Success($"1 {noun} unlinked."),
            _ => StatusMessage.Success($"{removed:N0} {nounPlural} unlinked."),
        };
}
