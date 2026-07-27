using Finmy.Ledger.Domain.Transactions;

namespace Finmy.Ledger.Application.Abstractions;

public interface ITransactionRepository
{
    void Add(Transaction transaction);
}
