using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Asistente.Api.Security;

/// <summary>
/// Hace que el endpoint solo exista fuera de producción.
///
/// Devuelve 404 en lugar de 403 a propósito: un 403 confirmaría que el endpoint existe,
/// y estas utilidades pueden borrar jornadas. Fuera de desarrollo conviene que ni siquiera
/// se sepa que están.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SoloDesarrolloAttribute : Attribute, IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var entorno = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();

        if (!entorno.IsDevelopment())
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
