using SecureVoting.Domain;

namespace SecureVoting.Application.Results;

public sealed record CalculatedCandidateResult(
    Guid CandidateId,
    string CandidateName,
    int Votes,
    decimal Percentage,
    int Rank,
    bool IsWinner,
    bool IsTie);

public sealed record CalculatedPositionResult(
    Guid PositionId,
    string PositionTitle,
    int SeatCount,
    int Abstentions,
    IReadOnlyList<CalculatedCandidateResult> Candidates,
    string? TieNote);

public sealed record CalculatedResult(
    int TotalEligible,
    int TotalBallots,
    decimal TurnoutPercentage,
    IReadOnlyList<CalculatedPositionResult> Positions);

public static class ResultCalculator
{
    public static CalculatedResult Calculate(
        Election election,
        IReadOnlyCollection<ElectionPosition> positions,
        IReadOnlyCollection<Candidate> candidates,
        IReadOnlyCollection<Ballot> ballots,
        int eligibleVoters)
    {
        var acceptedBallots = ballots.Where(b => b.ElectionId == election.Id).ToArray();
        var approvedCandidates = candidates
            .Where(c => c.Status == CandidateStatus.Approved)
            .ToArray();
        var results = new List<CalculatedPositionResult>();

        foreach (var position in positions.Where(p => p.IsActive).OrderBy(p => p.DisplayOrder))
        {
            var positionCandidates = approvedCandidates
                .Where(c => c.PositionId == position.Id)
                .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var counts = positionCandidates.ToDictionary(c => c.Id, _ => 0);
            var abstentions = 0;

            foreach (var ballot in acceptedBallots)
            {
                abstentions += ballot.Abstentions.Count(a => a.ElectionPositionId == position.Id);
                foreach (var selection in ballot.Selections.Where(s => s.ElectionPositionId == position.Id))
                {
                    if (counts.ContainsKey(selection.CandidateId))
                    {
                        counts[selection.CandidateId]++;
                    }
                }
            }

            var totalVotes = counts.Values.Sum();
            var ordered = positionCandidates
                .Select(c => new
                {
                    Candidate = c,
                    Votes = counts[c.Id],
                    Percentage = totalVotes == 0
                        ? 0m
                        : Math.Round(counts[c.Id] * 100m / totalVotes, 2, MidpointRounding.AwayFromZero)
                })
                .OrderByDescending(x => x.Votes)
                .ThenBy(x => x.Candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var flags = new Dictionary<Guid, (int Rank, bool Winner, bool Tie)>();
            var winners = 0;
            var index = 0;
            string? tieNote = null;
            while (index < ordered.Length && ordered[index].Votes > 0)
            {
                var count = ordered[index].Votes;
                var group = ordered.Skip(index).TakeWhile(x => x.Votes == count).ToArray();
                var rank = index + 1;
                var groupIsTie = group.Length > 1;
                var remainingSeats = position.SeatCount - winners;

                if (group.Length <= remainingSeats)
                {
                    foreach (var item in group)
                    {
                        flags[item.Candidate.Id] = (rank, true, groupIsTie);
                    }

                    winners += group.Length;
                }
                else if (groupIsTie && election.Rule.TieHandling == TieHandling.SharedOffice)
                {
                    foreach (var item in group)
                    {
                        flags[item.Candidate.Id] = (rank, true, true);
                    }

                    winners += group.Length;
                    tieNote = "A boundary tie was resolved as a shared office under the configured rule.";
                }
                else if (groupIsTie)
                {
                    foreach (var item in group)
                    {
                        flags[item.Candidate.Id] = (rank, false, true);
                    }

                    tieNote = election.Rule.TieHandling == TieHandling.RunoffRequired
                        ? "A tie at the winning boundary requires a runoff."
                        : "A tie at the winning boundary requires manual resolution.";
                    break;
                }
                else
                {
                    foreach (var item in group)
                    {
                        flags[item.Candidate.Id] = (rank, false, false);
                    }

                    break;
                }

                index += group.Length;
            }

            foreach (var item in ordered.Where(x => x.Votes == 0))
            {
                flags[item.Candidate.Id] = (0, false, false);
            }

            if (tieNote is null && flags.Values.Any(x => x.Tie))
            {
                tieNote = "A tie exists in this position. The configured tie rule applies.";
            }

            var candidateResults = ordered
                .Select(x =>
                {
                    var flag = flags[x.Candidate.Id];
                    return new CalculatedCandidateResult(
                        x.Candidate.Id,
                        x.Candidate.DisplayName,
                        x.Votes,
                        x.Percentage,
                        flag.Rank,
                        flag.Winner,
                        flag.Tie);
                })
                .ToArray();

            results.Add(new CalculatedPositionResult(
                position.Id,
                position.Title,
                position.SeatCount,
                abstentions,
                candidateResults,
                tieNote));
        }

        var totalBallots = acceptedBallots.Length;
        var turnout = eligibleVoters == 0
            ? 0m
            : Math.Round(totalBallots * 100m / eligibleVoters, 2, MidpointRounding.AwayFromZero);
        return new CalculatedResult(eligibleVoters, totalBallots, turnout, results);
    }
}
