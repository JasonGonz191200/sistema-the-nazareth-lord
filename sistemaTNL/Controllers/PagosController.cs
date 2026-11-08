using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Un concepto incluido en el pago: qué obligación se abona y cuánto
// Monto: en la moneda en que se recibe el pago
// DescuentoUsd: descuento particular opcional, en dólares (solo si el concepto aún no tiene abonos)
public record DetallePagoRequest(int IdObligacion, decimal Monto, decimal? DescuentoUsd, string? Observacion);

// Cuerpo para registrar un pago (RF030)
// Moneda: "USD" o "NIO". TipoCambio: córdobas por 1 dólar (siempre obligatorio).
public record RegistrarPagoRequest(
    int IdMatricula,
    string Moneda,
    decimal TipoCambio,
    DateOnly? FechaPago,
    List<DetallePagoRequest> Detalles);

// Cuerpo para anular un pago registrado por error
public record AnularPagoRequest(string Motivo);

[ApiController]
[Route("api/pagos")]
public class PagosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public PagosController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/pagos/obligaciones?idMatricula=1&tipoCambio=36.60
    // Conceptos de la matrícula con saldo pendiente. Si se envía tipoCambio, se agrega
    // el saldo en córdobas como referencia (la moneda base del sistema es el dólar).
    [HttpGet("obligaciones")]
    public async Task<IActionResult> ObligacionesPendientes(
        [FromQuery] int idMatricula, [FromQuery] decimal? tipoCambio)
    {
        var lista = await _db.ObligacionPagos.AsNoTracking()
            .Where(o => o.IdMatricula == idMatricula && o.EstadoPago != "Anulado" && o.EstadoPago != "Pagado")
            .OrderBy(o => o.MesCorrespondiente).ThenBy(o => o.IdObligacion)
            .ToListAsync();

        // La conversión se hace aquí, en memoria, para no depender de la base
        return Ok(lista.Select(o => new
        {
            o.IdObligacion,
            o.Descripcion,
            Mes = o.MesCorrespondiente,
            o.PrecioOriginalUsd,
            o.DescuentoUsd,
            o.PrecioFinalUsd,
            o.TotalAbonadoUsd,
            o.SaldoPendienteUsd,
            SaldoPendienteNio = tipoCambio is > 0
                ? Math.Round((o.SaldoPendienteUsd ?? 0) * tipoCambio.Value, 2)
                : (decimal?)null,
            Estado = o.EstadoPago
        }));
    }

    // GET api/pagos?idMatricula=1
    // Historial de pagos de una matrícula
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int idMatricula) =>
        Ok(await _db.Pagos.AsNoTracking()
            .Where(p => p.IdMatricula == idMatricula)
            .OrderByDescending(p => p.IdPago)
            .Select(p => new
            {
                p.IdPago,
                Comprobante = p.Comprobante != null ? p.Comprobante.Numero : null,
                p.FechaPago,
                p.MonedaRecibida,
                p.TipoCambio,
                p.TotalRecibido,
                p.TotalEquivalenteUsd,
                p.TotalEquivalenteNio,
                p.Estado
            })
            .ToListAsync());

    // GET api/pagos/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var detalle = await DetalleAsync(id);
        return detalle is null ? NotFound(new { mensaje = "El pago no existe." }) : Ok(detalle);
    }

    // POST api/pagos  (RF030)
    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarPagoRequest req)
    {
        // ---------- 1. VALIDACIONES (antes de escribir nada en la base) ----------
        var moneda = (req.Moneda ?? "").Trim().ToUpperInvariant();
        if (moneda != "USD" && moneda != "NIO")
            throw new NegocioException("La moneda debe ser USD (dólares) o NIO (córdobas).");

        if (req.TipoCambio <= 0)
            throw new NegocioException("El tipo de cambio debe ser mayor que cero.");
        if (Math.Round(req.TipoCambio, 4) != req.TipoCambio)
            throw new NegocioException("El tipo de cambio admite hasta 4 decimales.");

        if (req.Detalles is null || req.Detalles.Count == 0)
            throw new NegocioException("Debe seleccionar al menos un concepto para abonar.");
        if (req.Detalles.Select(d => d.IdObligacion).Distinct().Count() != req.Detalles.Count)
            throw new NegocioException("No puede repetir el mismo concepto en una misma transacción.");

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var fechaPago = req.FechaPago ?? hoy;
        if (fechaPago > hoy)
            throw new NegocioException("La fecha del pago no puede ser futura.");

        // RN026: la matrícula debe existir y estar habilitada
        var matricula = await _db.Matriculas.AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdMatricula == req.IdMatricula)
            ?? throw new NegocioException("La matrícula no existe.");
        if (!matricula.Estado)
            throw new NegocioException("RN026: no se pueden registrar pagos en una matrícula inhabilitada.");

        // Cargamos las obligaciones elegidas (con seguimiento, por si hay que aplicar descuentos)
        var ids = req.Detalles.Select(d => d.IdObligacion).ToList();
        var obligaciones = await _db.ObligacionPagos
            .Where(o => ids.Contains(o.IdObligacion) && o.IdMatricula == req.IdMatricula)
            .ToDictionaryAsync(o => o.IdObligacion);

        var detallesPago = new List<DetallePago>();     // lo que vamos a guardar
        var descuentosAplicar = new List<(ObligacionPago Obligacion, decimal Descuento)>();
        decimal totalRecibido = 0m, totalUsd = 0m;

        foreach (var d in req.Detalles)
        {
            if (!obligaciones.TryGetValue(d.IdObligacion, out var o))
                throw new NegocioException($"El concepto {d.IdObligacion} no pertenece a la matrícula.");
            if (o.EstadoPago == "Anulado")
                throw new NegocioException($"El concepto \"{o.Descripcion}\" está anulado.");

            // Monto válido, con máximo 2 decimales
            if (d.Monto <= 0)
                throw new NegocioException($"El monto de \"{o.Descripcion}\" debe ser mayor que cero.");
            if (Math.Round(d.Monto, 2) != d.Monto)
                throw new NegocioException($"El monto de \"{o.Descripcion}\" admite hasta 2 decimales.");

            // Descuento particular (RN027): solo si cambia y el concepto aún no tiene abonos
            var descuento = o.DescuentoUsd;
            if (d.DescuentoUsd is not null && d.DescuentoUsd != o.DescuentoUsd)
            {
                if (o.TotalAbonadoUsd > 0)
                    throw new NegocioException(
                        $"No se puede cambiar el descuento de \"{o.Descripcion}\" porque ya tiene abonos.");
                if (d.DescuentoUsd < 0 || d.DescuentoUsd > o.PrecioOriginalUsd)
                    throw new NegocioException(
                        $"El descuento de \"{o.Descripcion}\" no puede ser negativo ni superar el precio original.");
                descuento = d.DescuentoUsd.Value;
                descuentosAplicar.Add((o, descuento));
            }

            // Saldo pendiente según el precio final (con el descuento que corresponda)
            var saldo = o.PrecioOriginalUsd - descuento - o.TotalAbonadoUsd;
            if (saldo <= 0)
                throw new NegocioException($"El concepto \"{o.Descripcion}\" no tiene saldo pendiente.");

            // Equivalente en dólares del abono
            var equivalenteUsd = moneda == "USD"
                ? d.Monto
                : Math.Round(d.Monto / req.TipoCambio, 2, MidpointRounding.AwayFromZero);
            if (equivalenteUsd <= 0)
                throw new NegocioException($"El monto de \"{o.Descripcion}\" es demasiado pequeño al convertirlo a dólares.");
            if (equivalenteUsd > saldo)
                throw new NegocioException(
                    $"El abono a \"{o.Descripcion}\" (equivale a {equivalenteUsd:0.00} USD) supera el saldo pendiente ({saldo:0.00} USD).");

            totalRecibido += d.Monto;
            totalUsd += equivalenteUsd;

            detallesPago.Add(new DetallePago
            {
                IdObligacion = o.IdObligacion,
                IdMatricula = req.IdMatricula,
                MontoAbonado = d.Monto,
                MontoEquivalenteUsd = equivalenteUsd,
                Observacion = string.IsNullOrWhiteSpace(d.Observacion) ? null : d.Observacion.Trim()
            });
        }

        // Equivalente en córdobas de toda la transacción
        var totalNio = moneda == "NIO"
            ? totalRecibido
            : Math.Round(totalRecibido * req.TipoCambio, 2, MidpointRounding.AwayFromZero);

        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ---------- 2. GUARDADO (todo junto o nada) ----------
        await using var tx = await _db.Database.BeginTransactionAsync();

        // Primero los descuentos, para que los triggers calculen bien el estado del concepto
        if (descuentosAplicar.Count > 0)
        {
            foreach (var (obligacion, descuento) in descuentosAplicar)
                obligacion.DescuentoUsd = descuento;
            await _db.SaveChangesAsync();
        }

        var pago = new Pago
        {
            IdMatricula = req.IdMatricula,
            IdUsuario = idUsuario,                  // RN025: quién registró el pago
            FechaPago = fechaPago,
            MonedaRecibida = moneda,
            TipoCambio = req.TipoCambio,
            TotalRecibido = totalRecibido,
            TotalEquivalenteUsd = totalUsd,
            TotalEquivalenteNio = totalNio,
            Estado = "Registrado",
            DetallePagos = detallesPago
        };
        _db.Pagos.Add(pago);

        // Los triggers de la base actualizan los saldos y generan el comprobante (RN025)
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var detalle = await DetalleAsync(pago.IdPago);
        await _auditoria.RegistrarAsync(idUsuario, "REGISTRAR_PAGO", "pago", pago.IdPago,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"{totalRecibido:0.00} {moneda} (equivale a {totalUsd:0.00} USD)");

        return Created($"/api/pagos/{pago.IdPago}", detalle);
    }

    // PUT api/pagos/{id}/anular
    // Ejemplo: { "motivo": "Pago registrado por error" }
    // No borra el pago: lo marca como Anulado y recalcula los saldos
    [HttpPut("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id, AnularPagoRequest req)
    {
        var motivo = req.Motivo?.Trim();
        if (string.IsNullOrEmpty(motivo))
            throw new NegocioException("Debe indicar el motivo de la anulación.");

        var pago = await _db.Pagos.AsNoTracking().FirstOrDefaultAsync(p => p.IdPago == id)
            ?? throw new NegocioException("El pago no existe.");
        if (pago.Estado == "Anulado")
            throw new NegocioException("El pago ya está anulado.");

        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Usamos el procedimiento de la base, que anula y recalcula los saldos
        await _db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_anular_pago @id_pago, @id_usuario, @motivo",
            new SqlParameter("@id_pago", id),
            new SqlParameter("@id_usuario", idUsuario),
            new SqlParameter("@motivo", motivo));

        await _auditoria.RegistrarAsync(idUsuario, "ANULAR_PAGO", "pago", id,
            HttpContext.Connection.RemoteIpAddress?.ToString(), motivo);

        return Ok(await DetalleAsync(id));
    }

    // ---------- Apoyo ----------

    // Pago completo: datos generales, comprobante y cada concepto con sus saldos actuales
    private async Task<object?> DetalleAsync(int idPago)
    {
        var pago = await _db.Pagos.AsNoTracking()
            .Where(p => p.IdPago == idPago)
            .Select(p => new
            {
                p.IdPago,
                p.IdMatricula,
                Estudiante = p.IdMatriculaNavigation.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto,
                Comprobante = p.Comprobante != null ? p.Comprobante.Numero : null,
                p.FechaPago,
                p.MonedaRecibida,
                p.TipoCambio,
                p.TotalRecibido,
                p.TotalEquivalenteUsd,
                p.TotalEquivalenteNio,
                p.Estado,
                p.FechaAnulacion,
                p.MotivoAnulacion
            })
            .FirstOrDefaultAsync();
        if (pago is null) return null;

        var detalles = await _db.DetallePagos.AsNoTracking()
            .Where(d => d.IdPago == idPago)
            .Select(d => new
            {
                d.IdObligacion,
                Concepto = d.ObligacionPago.Descripcion,
                d.MontoAbonado,
                d.MontoEquivalenteUsd,
                d.Observacion,
                PrecioOriginalUsd = d.ObligacionPago.PrecioOriginalUsd,
                DescuentoUsd = d.ObligacionPago.DescuentoUsd,
                PrecioFinalUsd = d.ObligacionPago.PrecioFinalUsd,
                TotalAbonadoUsd = d.ObligacionPago.TotalAbonadoUsd,
                SaldoPendienteUsd = d.ObligacionPago.SaldoPendienteUsd,
                EstadoConcepto = d.ObligacionPago.EstadoPago
            })
            .ToListAsync();

        // Saldo en córdobas como referencia, al tipo de cambio de este pago
        var detallesConNio = detalles.Select(d => new
        {
            d.IdObligacion,
            d.Concepto,
            d.MontoAbonado,
            d.MontoEquivalenteUsd,
            d.Observacion,
            d.PrecioOriginalUsd,
            d.DescuentoUsd,
            d.PrecioFinalUsd,
            d.TotalAbonadoUsd,
            d.SaldoPendienteUsd,
            SaldoPendienteNio = Math.Round((d.SaldoPendienteUsd ?? 0) * pago.TipoCambio, 2),
            d.EstadoConcepto
        }).ToList();

        return new { pago, detalles = detallesConNio };
    }
}