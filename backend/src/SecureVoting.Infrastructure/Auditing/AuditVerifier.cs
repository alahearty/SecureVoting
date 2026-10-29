using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SecureVoting.Application.Contracts;
using SecureVoting.Application.Security;
using SecureVoting.Domain;
using SecureVoting.Infrastructure.Persistence;

namespace SecureVoting.Infrastructure.Auditing;

public sealed class AuditVerifier(AppDbContext db)
{
    public async Task<AuditVerificationDto> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var entries = await db.AuditLogs
            .AsNoTracking()
            .OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);
        var expectedSequence = 1L;
        var previousHash = new string('0', 64);
        foreach (var entry in entries)
        {
            if (entry.Sequence != expectedSequence)
            {
                return new AuditVerificationDto(false, expectedSequence - 1, entry.Sequence,
                    "Audit sequence is not contiguous.", previousHash);
            }

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(entry.PreviousHash),
                    Encoding.ASCII.GetBytes(previousHash)))
            {
                return new AuditVerificationDto(false, expectedSequence - 1, entry.Sequence,
                    "Audit previous-hash link is broken.", previousHash);
            }

            var computed = AuditHash.Compute(entry);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(computed),
                    Encoding.ASCII.GetBytes(entry.Hash)))
            {
                return new AuditVerificationDto(false, expectedSequence - 1, entry.Sequence,
                    "Audit entry hash does not match its content.", previousHash);
            }

            previousHash = entry.Hash;
            expectedSequence++;
        }

        var state = await db.AuditChainStates.AsNoTracking().SingleAsync(cancellationToken);
        if (state.LastSequence != expectedSequence - 1 ||
            !string.Equals(state.LastHash, previousHash, StringComparison.Ordinal))
        {
            return new AuditVerificationDto(false, expectedSequence - 1, null,
                "Audit chain-state checkpoint does not match the log.", previousHash);
        }

        return new AuditVerificationDto(true, entries.Count, null,
            "Audit chain is internally consistent.", previousHash);
    }
}
