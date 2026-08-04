using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Finmy.Ledger.Application.Transactions.Dtos;

namespace Finmy.Ledger.Application.Transactions;

public static class RequestFingerprint
{
    public static string Compute(RecordTransactionRequest request)
    {
        var spaceId = request.SpaceId.ToString();
        var envelopeId = request.EnvelopeId.ToString();
        var amount = request.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        var direction = request.Direction.ToString();
        var occurredOn = request.OccurredOn.ToUniversalTime().ToString("O");
        var description = request.Description ?? string.Empty;
        char separator = '\u001f';

        var canonical = $"{spaceId}{separator}{envelopeId}{separator}{amount}{separator}{direction}{separator}{occurredOn}{separator}{description}";
        var byteCanonical = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(byteCanonical);
        var hexHash = Convert.ToHexStringLower(hash);

        return hexHash;
    }
}
