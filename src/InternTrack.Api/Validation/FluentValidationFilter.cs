using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InternTrack.Api.Validation;

/// <summary>
/// Valide automatiquement chaque argument d'action pour lequel un <see cref="IValidator{T}"/> est enregistré.
/// En cas d'erreur, la requête est rejetée en 400 (ValidationProblemDetails) avant d'atteindre la logique métier
/// et la base de données.
/// </summary>
public sealed class FluentValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = string.IsNullOrEmpty(failure.PropertyName) ? "request" : failure.PropertyName;
                if (!errors.TryGetValue(key, out var messages))
                    errors[key] = messages = new List<string>();
                messages.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            var problem = new ValidationProblemDetails(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()))
            {
                Title = "Données invalides",
                Status = StatusCodes.Status400BadRequest,
                // Le front affiche « detail » : on y regroupe les messages lisibles.
                Detail = string.Join(" ", errors.SelectMany(e => e.Value).Distinct())
            };

            context.Result = new BadRequestObjectResult(problem);
            return;
        }

        await next();
    }
}
