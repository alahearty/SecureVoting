using SecureVoting.Application.Contracts;
using SecureVoting.Domain;

namespace SecureVoting.Application.Elections;

public sealed record ValidatedBallotPosition(Guid PositionId, IReadOnlyList<Guid> CandidateIds, bool IsAbstention);

public sealed record ValidatedBallot(IReadOnlyList<ValidatedBallotPosition> Positions);

public static class BallotValidator
{
    public static ValidatedBallot Validate(
        Election election,
        IEnumerable<ElectionPosition> positions,
        IEnumerable<Candidate> candidates,
        IEnumerable<PositionChoiceRequest> choices,
        bool isEligible,
        DateTime nowUtc)
    {
        if (!isEligible)
        {
            throw new ForbiddenOperationException("The authenticated student is not eligible for this election.");
        }

        if (election.Status != ElectionStatus.Open)
        {
            throw new ConflictException("This election is not open for voting.");
        }

        if (nowUtc < election.OpensAtUtc || nowUtc >= election.ClosesAtUtc)
        {
            throw new ConflictException("The trusted server clock is outside the authorised voting period.");
        }

        var activePositions = positions
            .Where(p => p.IsActive && p.ElectionId == election.Id)
            .OrderBy(p => p.DisplayOrder)
            .ToArray();
        if (activePositions.Length == 0)
        {
            throw new ConflictException("This election has no active positions.");
        }

        var approvedCandidates = candidates
            .Where(c => c.ElectionId == election.Id && c.Status == CandidateStatus.Approved)
            .ToArray();
        var choiceList = choices.ToArray();
        if (choiceList.GroupBy(c => c.PositionId).Any(g => g.Count() > 1))
        {
            throw new ValidationException("A position may appear only once in a ballot.");
        }

        var byPosition = choiceList.ToDictionary(c => c.PositionId);
        var knownPositionIds = activePositions.Select(p => p.Id).ToHashSet();
        if (choiceList.Any(c => !knownPositionIds.Contains(c.PositionId)))
        {
            throw new ValidationException("The ballot contains a position that does not belong to this election.");
        }

        if (activePositions.Any(p => !byPosition.ContainsKey(p.Id)))
        {
            throw new ValidationException("Every active position must be reviewed, including an explicit abstention where allowed.");
        }

        var validated = new List<ValidatedBallotPosition>();
        foreach (var position in activePositions)
        {
            var choice = byPosition[position.Id];
            var ids = choice.CandidateIds.ToArray();
            if (ids.Distinct().Count() != ids.Length)
            {
                throw new ValidationException($"Duplicate candidate selection in position '{position.Title}'.");
            }

            if (ids.Length == 0)
            {
                if (!election.Rule.AllowAbstention)
                {
                    throw new ValidationException($"Abstention is not allowed for position '{position.Title}'.");
                }

                validated.Add(new ValidatedBallotPosition(position.Id, [], true));
                continue;
            }

            if (ids.Length > position.MaxSelections)
            {
                throw new ValidationException(
                    $"Position '{position.Title}' permits at most {position.MaxSelections} selection(s).");
            }

            var positionCandidates = approvedCandidates
                .Where(c => c.PositionId == position.Id && c.ElectionId == election.Id)
                .ToDictionary(c => c.Id);
            if (ids.Any(id => !positionCandidates.ContainsKey(id)))
            {
                throw new ValidationException(
                    $"A selected candidate is not approved for position '{position.Title}'.");
            }

            validated.Add(new ValidatedBallotPosition(position.Id, ids, false));
        }

        return new ValidatedBallot(validated);
    }
}
