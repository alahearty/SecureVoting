namespace SecureVoting.Domain;

public static class ElectionStateMachine
{
    private static readonly Dictionary<ElectionStatus, ElectionStatus[]> Allowed = new()
    {
        [ElectionStatus.Draft] = [ElectionStatus.Scheduled, ElectionStatus.Cancelled],
        [ElectionStatus.Scheduled] =
        [
            ElectionStatus.Open,
            ElectionStatus.Suspended,
            ElectionStatus.Cancelled
        ],
        [ElectionStatus.Open] =
        [
            ElectionStatus.Closed,
            ElectionStatus.Suspended,
            ElectionStatus.Cancelled
        ],
        [ElectionStatus.Suspended] =
        [
            ElectionStatus.Scheduled,
            ElectionStatus.Open,
            ElectionStatus.Closed,
            ElectionStatus.Cancelled
        ],
        [ElectionStatus.Closed] = [ElectionStatus.UnderReview],
        [ElectionStatus.UnderReview] = [ElectionStatus.Published],
        [ElectionStatus.Published] = [ElectionStatus.UnderReview],
        [ElectionStatus.Cancelled] = []
    };

    public static IReadOnlyCollection<ElectionStatus> AllowedNext(ElectionStatus status) =>
        Allowed.TryGetValue(status, out var next) ? next : [];

    public static bool CanTransition(ElectionStatus from, ElectionStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static void EnsureTransition(ElectionStatus from, ElectionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new ConflictException($"Election cannot move from {from} to {to}.");
        }
    }

    public static bool IsTerminal(ElectionStatus status) =>
        status is ElectionStatus.Published or ElectionStatus.Cancelled;

    public static bool AcceptsBallots(ElectionStatus status) => status == ElectionStatus.Open;

    public static bool ConfigurationIsEditable(ElectionStatus status) =>
        status is ElectionStatus.Draft or ElectionStatus.Scheduled;
}
