using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Cuerpo para agregar un cobro a una matrícula.
// - Concepto del catálogo: enviar IdConcepto (PrecioUsd y Descripcion son opcionales).
// - "Otro concepto": IdConcepto = null, y Descripcion y PrecioUsd obligatorios.
public record AgregarObligacionRequest(
    int IdMatricula,
    int? IdConcepto,
    string? Descripcion,
    decimal? PrecioUsd,
    decimal? DescuentoUsd);

[ApiController]
[Route("api/obligaciones")]
public class ObligacionesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ObligacionesController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/obligaciones/conceptos
    // Conceptos opcionales del catálogo que se pueden agregar a una matrícula
    [HttpGet("conceptos")]
    public async Task<IActionResult> ConceptosOpcionales() =>
        Ok(await _db.ConceptoPagos.AsNoTracking()
            .Where(c => c.Estado && !c.EsObligatorio)
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.IdConcepto, c.Codigo, c.Nombre, c.PrecioGeneralUsd })
            .ToListAsync());

    // POST api/obligaciones
    [HttpPost]
    public async Task<IActionResult> Agregar(AgregarObligacionRequest req)
    {
        // RN026: la matrícula debe existir y estar habilitada
        var matricula = await _db.Matriculas.AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdMatricula == req.IdMatricula)
            ?? throw new NegocioException("La matrícula no existe.");
        if (!matricula.Estado)
            throw new NegocioException("RN026: no se pueden agregar cobros a una matrícula inhabilitada.");

        var descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        if (descripcion is not null && descripcion.Length > 250)
            throw new NegocioException("La descripción no puede superar los 250 caracteres.");

        decimal precio;
        if (req.IdConcepto is null)
        {
            // "Otro concepto" (RN027): descripción y monto mayor que cero
            if (descripcion is null || req.PrecioUsd is null || req.PrecioUsd <= 0)
                throw new NegocioException(
                    "RN027: \"Otro concepto\" requiere una descripción y un monto mayor que cero.");
            precio = req.PrecioUsd.Value;
        }
        else
        {
            var concepto = await _db.ConceptoPagos.AsNoTracking()
                .FirstOrDefaultAsync(c => c.IdConcepto == req.IdConcepto)
                ?? throw new NegocioException("El concepto seleccionado no existe.");
            if (!concepto.Estado)
                throw new NegocioException("El concepto seleccionado está inhabilitado.");

            // Matrícula, Certificado y Mensualidad se generan solos al matricular
            if (concepto.EsObligatorio)
                throw new NegocioException(
                    $"El concepto \"{concepto.Nombre}\" se genera automáticamente al matricular.");

            // Un mismo concepto del catálogo solo puede estar una vez por matrícula
            if (await _db.ObligacionPagos.AnyAsync(o =>
                    o.IdMatricula == req.IdMatricula && o.IdConcepto == req.IdConcepto && o.EstadoPago != "Anulado"))
                throw new NegocioException($"El concepto \"{concepto.Nombre}\" ya fue agregado a esta matrícula.");

            precio = req.PrecioUsd ?? concepto.PrecioGeneralUsd;
            if (precio <= 0)
                throw new NegocioException("El precio debe ser mayor que cero.");
        }

        if (Math.Round(precio, 2) != precio)
            throw new NegocioException("El precio admite hasta 2 decimales.");

        // Descuento particular: entre 0 y el precio original
        var descuento = req.DescuentoUsd ?? 0m;
        if (descuento < 0 || descuento > precio)
            throw new NegocioException("El descuento no puede ser negativo ni superar el precio.");
        if (Math.Round(descuento, 2) != descuento)
            throw new NegocioException("El descuento admite hasta 2 decimales.");

        // Llamamos al procedimiento de la base; devuelve el id por un parámetro de salida
        var pId = new SqlParameter("@id_obligacion", SqlDbType.Int)
        { Direction = ParameterDirection.Output };

        await _db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_agregar_obligacion @id_matricula, @id_concepto, @descripcion, " +
            "@precio_usd, @descuento_usd, @id_obligacion OUTPUT",
            new SqlParameter("@id_matricula", req.IdMatricula),
            new SqlParameter("@id_concepto", (object?)req.IdConcepto ?? DBNull.Value),
            new SqlParameter("@descripcion", (object?)descripcion ?? DBNull.Value),
            new SqlParameter("@precio_usd", precio),
            new SqlParameter("@descuento_usd", descuento),
            pId);

        var idObligacion = (int)pId.Value;

        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario, "AGREGAR_OBLIGACION", "obligacion_pago", idObligacion,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Matrícula {req.IdMatricula}: {descripcion ?? "concepto del catálogo"} ({precio:0.00} USD)");

        // Devolvemos el cobro creado
        var creada = await _db.ObligacionPagos.AsNoTracking()
            .Where(o => o.IdObligacion == idObligacion)
            .Select(o => new
            {
                o.IdObligacion,
                o.IdMatricula,
                o.Descripcion,
                o.PrecioOriginalUsd,
                o.DescuentoUsd,
                o.PrecioFinalUsd,
                o.TotalAbonadoUsd,
                o.SaldoPendienteUsd,
                Estado = o.EstadoPago
            })
            .FirstAsync();

        return Created($"/api/pagos/obligaciones?idMatricula={req.IdMatricula}", creada);
    }
}