using System.Security.Claims;
using Asistente.Common;
using Asistente.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Asistente.Api.Security;

/// <summary>
/// Exige que la sesión tenga un permiso concreto.
///
/// Consulta la base en cada petición en lugar de leer un claim de la cookie. Un claim se
/// emite al iniciar sesión y queda congelado hasta que la sesión vence: quien perdiera una
/// atribución la conservaría durante horas. El costo es una consulta por operación
/// reservada, que son pocas y esporádicas.
///
/// Responde 403 y no 404: acá el endpoint no es un secreto —la interfaz lo muestra a quien
/// corresponde—, y decirle a alguien que no le alcanzan los permisos es más útil que
/// fingir que la dirección no existe.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExigePermisoAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permiso;

    public ExigePermisoAttribute(string permiso) => _permiso = permiso;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var usuario = context.HttpContext.User;

        if (usuario.Identity?.IsAuthenticated != true
            || !long.TryParse(usuario.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var repositorio = context.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>();
        var permisos = await repositorio.ListarPermisosAsync(userId, context.HttpContext.RequestAborted);

        if (!permisos.Contains(_permiso, StringComparer.Ordinal))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Title = "No tenés permiso para esta operación.",
                Status = StatusCodes.Status403Forbidden,
                Type = CodigosError.PermisoDenegado,
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };

            return;
        }

        await next();
    }
}
