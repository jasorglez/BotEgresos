using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.Expenses;
using EgresosBot.Api.Domain.Entities;
using EgresosBot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgresosBot.Api.Infrastructure.Services;

public sealed class ExpenseService(EgresosBotDbContext dbContext) : IExpenseService
{
    public async Task<IReadOnlyCollection<ExpenseResponse>> GetExpensesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Expenses
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Payee)
            .Include(x => x.CreatedByUser)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new ExpenseResponse
            {
                Id = x.Id,
                PublicId = x.PublicId,
                Status = x.Status,
                ExpenseDate = x.ExpenseDate,
                Description = x.Description,
                AmountSubtotal = x.AmountSubtotal,
                IvaAmount = x.IvaAmount,
                AmountTotal = x.AmountTotal,
                CurrencyCode = x.CurrencyCode,
                PaymentMethod = x.PaymentMethod,
                CategoryName = x.Category != null ? x.Category.Name : null,
                PayeeName = x.Payee != null ? x.Payee.Name : null,
                CreatedByEmail = x.CreatedByUser.Email,
                CreatedAtUtc = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpenseResponse> CreateExpenseAsync(int tenantId, int userId, CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Expense
        {
            PublicId = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedByUserId = userId,
            CategoryId = request.CategoryId,
            PayeeId = request.PayeeId,
            Status = "SUBMITTED",
            ExpenseDate = request.ExpenseDate,
            Description = request.Description.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            AmountSubtotal = request.AmountSubtotal,
            IvaAmount = request.IvaAmount,
            AmountTotal = request.AmountSubtotal + request.IvaAmount,
            CurrencyCode = "MXN",
            PaymentMethod = request.PaymentMethod,
            BranchName = request.BranchName,
            CostCenter = request.CostCenter,
            ProjectName = request.ProjectName,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Expenses.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await dbContext.Expenses
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Payee)
            .Include(x => x.CreatedByUser)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return new ExpenseResponse
        {
            Id = created.Id,
            PublicId = created.PublicId,
            Status = created.Status,
            ExpenseDate = created.ExpenseDate,
            Description = created.Description,
            AmountSubtotal = created.AmountSubtotal,
            IvaAmount = created.IvaAmount,
            AmountTotal = created.AmountTotal,
            CurrencyCode = created.CurrencyCode,
            PaymentMethod = created.PaymentMethod,
            CategoryName = created.Category?.Name,
            PayeeName = created.Payee?.Name,
            CreatedByEmail = created.CreatedByUser.Email,
            CreatedAtUtc = created.CreatedAt
        };
    }
}
