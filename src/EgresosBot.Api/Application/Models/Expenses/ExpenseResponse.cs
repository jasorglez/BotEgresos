namespace EgresosBot.Api.Application.Models.Expenses;

public sealed class ExpenseResponse
{
    public int Id { get; set; }
    public Guid PublicId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly ExpenseDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal AmountSubtotal { get; set; }
    public decimal IvaAmount { get; set; }
    public decimal AmountTotal { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? CategoryName { get; set; }
    public string? PayeeName { get; set; }
    public string CreatedByEmail { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
