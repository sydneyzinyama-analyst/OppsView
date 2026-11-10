using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OppsView.Api.Domain;

namespace OppsView.Api.Infrastructure;

/// <summary>
/// Turns a <see cref="DomainException"/> thrown by a controller action into
/// the same 400 ValidationProblemDetails shape that [ApiController] model
/// validation produces, so the client handles every validation error the
/// same way.
///
/// An MVC exception filter rather than global exception middleware: a broken
/// business rule is an expected outcome, not a crash, so it shouldn't be
/// logged as an unhandled error. Anything else still reaches
/// UseExceptionHandler and becomes a logged 500.
/// </summary>
public sealed class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DomainException domain) return;

        var field = JsonNamingPolicy.CamelCase.ConvertName(domain.Field);
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [domain.Message] })
        {
            Status = StatusCodes.Status400BadRequest,
        };
        context.Result = new BadRequestObjectResult(problem);
        context.ExceptionHandled = true;
    }
}
