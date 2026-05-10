using EgresosBot.Api.Application.Models.Expenses;

namespace EgresosBot.Api.Application.Abstractions;

public interface IExpenseService
{
    Task<IReadOnlyCollection<ExpenseResponse>> GetExpensesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<ExpenseResponse> CreateExpenseAsync(int tenantId, int userId, CreateExpenseRequest request, CancellationToken cancellationToken = default);
}
