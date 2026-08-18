using System.Text.RegularExpressions;
using Asistente.Domain.Dtos;
using Asistente.Domain.Services.Interfaces;
using Asistente.Persistence.Configuration;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;

namespace Asistente.Persistence.Services;

/// <summary>
/// Consulta de tickets contra la base corporativa Oracle, con Dapper.
///
/// Se usa Dapper y no EF Core porque acá no hay entidades ni seguimiento de cambios: son
/// lecturas puntuales sobre una vista ajena que no controlamos, y modelarla en EF nos
/// ataría a un esquema que puede cambiar sin aviso.
///
/// Todo lo que el usuario puede influir viaja como parámetro enlazado. Los nombres de
/// vista y columnas vienen de configuración y se validan como identificadores antes de
/// interpolarse: es configuración del operador y no entrada de usuario, pero un valor mal
/// escrito rompería la consulta de una forma difícil de diagnosticar.
/// </summary>
public sealed partial class TicketQueryServiceOracle : ITicketQueryService
{
    private readonly TicketsDbSettings _config;
    private readonly ILogger<TicketQueryServiceOracle> _logger;

    public TicketQueryServiceOracle(
        IOptions<DatabaseSettings> opciones,
        ILogger<TicketQueryServiceOracle> logger)
    {
        _config = opciones.Value.Tickets;
        _logger = logger;

        ValidarMapeo(_config.Mapeo);
    }

    public async Task<IReadOnlyList<ClienteDto>> BuscarClientesAsync(
        string usuario, string? termino, int maximo, CancellationToken ct)
    {
        var m = _config.Mapeo;

        // Los clientes salen de los propios tickets del usuario: no hace falta una vista
        // aparte, y así no se listan clientes con los que esta persona no trabaja.
        var sql =
            $"SELECT DISTINCT {m.ColumnaClienteId} AS Id, {m.ColumnaClienteNombre} AS Nombre " +
            $"FROM {m.Vista} " +
            $"WHERE UPPER({m.ColumnaAsignadoA}) = UPPER(:usuario) " +
            $"AND (:termino IS NULL OR UPPER({m.ColumnaClienteNombre}) LIKE UPPER(:patron)) " +
            "ORDER BY Nombre " +
            "FETCH FIRST :maximo ROWS ONLY";

        var limpio = Vacio(termino);

        var filas = await ConsultarAsync<ClienteFila>(
            sql,
            new
            {
                usuario,
                termino = limpio,
                patron = limpio is null ? null : "%" + limpio + "%",
                maximo = Math.Clamp(maximo, 1, _config.MaxFilas),
            },
            ct);

        return filas.Select(f => new ClienteDto(f.Id, f.Nombre, f.Id)).ToList();
    }

    public async Task<PaginaDto<TicketDto>> BuscarTicketsAsync(
        string usuario, ConsultaTickets consulta, CancellationToken ct)
    {
        var m = _config.Mapeo;
        var tamano = Math.Clamp(consulta.Tamano, 1, _config.MaxFilas);
        var pagina = Math.Max(1, consulta.Pagina);
        var texto = Vacio(consulta.Texto);

        var filtro =
            $"WHERE UPPER({m.ColumnaAsignadoA}) = UPPER(:usuario) " +
            $"AND (:clienteId IS NULL OR {m.ColumnaClienteId} = :clienteId) " +
            "AND (:texto IS NULL " +
            $"     OR UPPER({m.ColumnaTicketId}) LIKE UPPER(:patron) " +
            $"     OR UPPER({m.ColumnaTitulo}) LIKE UPPER(:patron)) ";

        var parametros = new
        {
            usuario,
            clienteId = Vacio(consulta.ClienteId),
            texto,
            patron = texto is null ? null : "%" + texto + "%",
            saltar = (pagina - 1) * tamano,
            tomar = tamano,
        };

        var total = (await ConsultarAsync<int>(
            $"SELECT COUNT(*) FROM {m.Vista} " + filtro, parametros, ct)).FirstOrDefault();

        // Orden descendente por fecha de creación: FR-011 / AC-10.
        var sql =
            Proyeccion(m) +
            $"FROM {m.Vista} " +
            filtro +
            $"ORDER BY {m.ColumnaFechaCreacion} DESC " +
            "OFFSET :saltar ROWS FETCH NEXT :tomar ROWS ONLY";

        var items = await ConsultarAsync<TicketFila>(sql, parametros, ct);
        return new PaginaDto<TicketDto>(items.Select(Mapear).ToList(), total, pagina, tamano);
    }

    public async Task<TicketDto?> ObtenerPorIdAsync(
        string usuario, string ticketId, CancellationToken ct)
    {
        var m = _config.Mapeo;

        // El filtro por usuario va también acá: sin él, cualquiera podría traer el ticket
        // de otra persona conociendo su identificador.
        var sql =
            Proyeccion(m) +
            $"FROM {m.Vista} " +
            $"WHERE {m.ColumnaTicketId} = :ticketId " +
            $"AND UPPER({m.ColumnaAsignadoA}) = UPPER(:usuario) " +
            "FETCH FIRST 1 ROWS ONLY";

        var filas = await ConsultarAsync<TicketFila>(sql, new { usuario, ticketId }, ct);
        return filas.Count == 0 ? null : Mapear(filas[0]);
    }

    // ---------- Apoyo ----------

    private static string Proyeccion(MapeoTickets m) =>
        $"SELECT {m.ColumnaTicketId} AS TicketId, " +
        $"{m.ColumnaClienteId} AS ClienteId, " +
        $"{m.ColumnaClienteNombre} AS ClienteNombre, " +
        $"{m.ColumnaTitulo} AS Titulo, " +
        $"{m.ColumnaEstado} AS Estado, " +
        $"{m.ColumnaPrioridad} AS Prioridad, " +
        $"{m.ColumnaFechaCreacion} AS CreadoEn, " +
        $"{m.ColumnaAsignadoA} AS AsignadoA ";

    private sealed record ClienteFila(string Id, string Nombre);

    private sealed record TicketFila(
        string TicketId,
        string ClienteId,
        string ClienteNombre,
        string Titulo,
        string Estado,
        string Prioridad,
        DateTime CreadoEn,
        string AsignadoA);

    private static TicketDto Mapear(TicketFila f) =>
        new(f.TicketId, f.ClienteId, f.ClienteNombre, f.Titulo, f.Estado, f.Prioridad,
            DateTime.SpecifyKind(f.CreadoEn, DateTimeKind.Utc), f.AsignadoA);

    private async Task<IReadOnlyList<T>> ConsultarAsync<T>(
        string sql, object parametros, CancellationToken ct)
    {
        await using var conexion = new OracleConnection(_config.AdoDataSource);

        // BindByName es imprescindible: sin él Oracle asocia los parámetros por posición,
        // y una consulta que repite :usuario o :patron recibiría los valores corridos.
        conexion.BindByName = true;

        var comando = new CommandDefinition(
            sql, parametros, commandTimeout: _config.CommandTimeoutSeconds, cancellationToken: ct);

        try
        {
            var filas = await conexion.QueryAsync<T>(comando);
            return filas.ToList();
        }
        catch (OracleException ex)
        {
            // Se registra el número de error y no el mensaje crudo: ese mensaje suele
            // incluir host, usuario y fragmentos del SQL (NFR-011, AC-16).
            _logger.LogError(
                "Fallo al consultar la base de tickets. Error Oracle {Numero}.", ex.Number);
            throw;
        }
    }

    private static string? Vacio(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// Acepta solo identificadores simples o calificados por esquema. Cualquier otra cosa
    /// aborta el arranque, que es preferible a descubrirlo con una consulta rota.
    /// </summary>
    private static void ValidarMapeo(MapeoTickets mapeo)
    {
        var valores = new (string Nombre, string Valor)[]
        {
            (nameof(mapeo.Vista), mapeo.Vista),
            (nameof(mapeo.ColumnaTicketId), mapeo.ColumnaTicketId),
            (nameof(mapeo.ColumnaClienteId), mapeo.ColumnaClienteId),
            (nameof(mapeo.ColumnaClienteNombre), mapeo.ColumnaClienteNombre),
            (nameof(mapeo.ColumnaTitulo), mapeo.ColumnaTitulo),
            (nameof(mapeo.ColumnaEstado), mapeo.ColumnaEstado),
            (nameof(mapeo.ColumnaPrioridad), mapeo.ColumnaPrioridad),
            (nameof(mapeo.ColumnaFechaCreacion), mapeo.ColumnaFechaCreacion),
            (nameof(mapeo.ColumnaAsignadoA), mapeo.ColumnaAsignadoA),
        };

        foreach (var (nombre, valor) in valores)
        {
            if (!Identificador().IsMatch(valor))
            {
                throw new InvalidOperationException(
                    $"El mapeo de tickets tiene un identificador inválido en '{nombre}': '{valor}'. "
                    + "Solo se admiten letras, dígitos, guion bajo y un punto de esquema.");
            }
        }
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$")]
    private static partial Regex Identificador();
}
