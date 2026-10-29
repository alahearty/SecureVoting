using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SecureVoting.Domain;

namespace SecureVoting.Application.Results;

public static class ResultIntegrity
{
    public static string Compute(ElectionResult result) =>
        Compute(
            result.ElectionId,
            result.Version,
            result.TotalEligible,
            result.TotalBallots,
            result.TurnoutPercentage,
            result.Lines);

    public static string Compute(
        Guid electionId,
        int version,
        int totalEligible,
        int totalBallots,
        decimal turnoutPercentage,
        IEnumerable<ElectionResultLine> lines)
    {
        var canonical = string.Join(
            '\n',
            lines
                .OrderBy(x => x.PositionId)
                .ThenBy(x => x.CandidateId)
                .Select(x => string.Join(
                    '|',
                    x.PositionId.ToString("N"),
                    x.CandidateId.ToString("N"),
                    x.VoteCount.ToString(CultureInfo.InvariantCulture),
                    x.PositionSeatCount.ToString(CultureInfo.InvariantCulture),
                    x.PositionAbstentions.ToString(CultureInfo.InvariantCulture),
                    x.Percentage.ToString("0.####", CultureInfo.InvariantCulture),
                    x.Rank.ToString(CultureInfo.InvariantCulture),
                    x.IsWinner ? "1" : "0",
                    x.IsTie ? "1" : "0")));
        var payload = string.Join(
            '|',
            electionId.ToString("N"),
            version.ToString(CultureInfo.InvariantCulture),
            totalEligible.ToString(CultureInfo.InvariantCulture),
            totalBallots.ToString(CultureInfo.InvariantCulture),
            turnoutPercentage.ToString("0.####", CultureInfo.InvariantCulture),
            canonical);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool Verify(ElectionResult result) =>
        string.Equals(result.IntegrityHash, Compute(result), StringComparison.Ordinal);
}
