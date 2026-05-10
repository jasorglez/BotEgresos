namespace EgresosBot.Api.Application.Models.Expenses;

public sealed class CreateExpenseRequest
{
    public int? CategoryId { get; set; }
    public int? PayeeId { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal AmountSubtotal { get; set; }
    public decimal IvaAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? BranchName { get; set; }
    public string? CostCenter { get; set; }
    public string? ProjectName { get; set; }
}
