using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.Expenses;
using EgresosBot.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgresosBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/expenses")]
public sealed class ExpensesController(IExpenseService expenseService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ExpenseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var tenantId = User.GetRequiredTenantId();
        var expenses = await expenseService.GetExpensesAsync(tenantId, cancellationToken);
        return Ok(expenses);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Post([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var tenantId = User.GetRequiredTenantId();
        var userId = User.GetRequiredUserId();
        var expense = await expenseService.CreateExpenseAsync(tenantId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = expense.Id }, expense);
    }
}
