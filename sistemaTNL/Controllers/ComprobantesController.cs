using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using sistemaTNL.Data;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Genera el comprobante de pago en PDF (solo lectura)
[ApiController]
[Route("api/pagos/{idPago:int}/comprobante")]
public class ComprobantesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    // Licencia gratuita de QuestPDF (uso comunitario). Se asigna una sola vez.
    static ComprobantesController()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ComprobantesController(ApplicationDbContext db) => _db = db;

    // GET api/pagos/{idPago}/comprobante
    [HttpGet]
    public async Task<IActionResult> Obtener(int idPago)
    {
        // Se leen solo los datos necesarios para el comprobante
        var d = await _db.Pagos.AsNoTracking()
            .Where(p => p.IdPago == idPago)
            .Select(p => new
            {
                Numero = p.Comprobante != null ? p.Comprobante.Numero : null,
                FechaEmision = p.Comprobante != null ? (DateTime?)p.Comprobante.FechaHora : null,
                p.FechaPago,
                Estudiante = p.IdMatriculaNavigation.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto,
                Carne = p.IdMatriculaNavigation.IdEstudianteNavigation.NumeroCarne,
                Curso = p.IdMatriculaNavigation.IdPeriodoNavigation.IdCursoNavigation.Nombre,
                Modalidad = p.IdMatriculaNavigation.IdPeriodoNavigation.IdModalidadNavigation.Nombre,
                Grupo = p.IdMatriculaNavigation.Grupo.Identificacion,
                RegistradoPor = p.IdUsuarioNavigation.NombreUsuario,
                p.MonedaRecibida,
                p.TipoCambio,
                p.TotalRecibido,
                p.TotalEquivalenteUsd,
                p.TotalEquivalenteNio,
                p.Estado,
                p.MotivoAnulacion,
                Detalles = p.DetallePagos.Select(x => new
                {
                    x.ObligacionPago.Descripcion,
                    x.MontoAbonado,
                    x.MontoEquivalenteUsd
                }).ToList()
            })
            .FirstOrDefaultAsync()
            ?? throw new NegocioException("El pago no existe.");

        if (d.Numero is null)
            throw new NegocioException("El pago no tiene comprobante asociado.");

        // Símbolo según la moneda en la que se recibió el pago
        var simbolo = d.MonedaRecibida == "NIO" ? "C$" : "$";
        var ci = CultureInfo.InvariantCulture;
        string Monto(decimal v, string s) => $"{s} {v.ToString("N2", ci)}";

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(35);
                page.DefaultTextStyle(t => t.FontSize(10));

                // Encabezado: nombre de la academia y número de comprobante
                page.Header().Column(h =>
                {
                    h.Item().Text("Academia de Inglés The Nazareth Lord").FontSize(16).Bold();
                    h.Item().Text("Comprobante de pago").FontSize(12);
                    h.Item().PaddingTop(4).Text(d.Numero).FontSize(14).Bold();
                    h.Item().PaddingTop(6).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(6);

                    // Marca visible si el pago fue anulado
                    if (d.Estado == "Anulado")
                    {
                        col.Item().Text("PAGO ANULADO").FontSize(16).Bold().FontColor(Colors.Red.Medium);
                        col.Item().Text($"Motivo: {d.MotivoAnulacion}").FontColor(Colors.Red.Medium);
                    }

                    // Datos del pago y del estudiante
                    col.Item().Text($"Fecha de pago: {d.FechaPago:dd/MM/yyyy}");
                    col.Item().Text($"Estudiante: {d.Estudiante}");
                    col.Item().Text($"Carné: {d.Carne}");
                    col.Item().Text($"Curso: {d.Curso} - {d.Modalidad}");
                    col.Item().Text($"Grupo: {d.Grupo}");

                    // Tabla con lo abonado a cada concepto
                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(Encabezado).Text("Concepto");
                            h.Cell().Element(Encabezado).AlignRight().Text($"Abonado ({d.MonedaRecibida})");
                            h.Cell().Element(Encabezado).AlignRight().Text("Equiv. USD");
                        });

                        foreach (var x in d.Detalles)
                        {
                            t.Cell().Element(Celda).Text(x.Descripcion);
                            t.Cell().Element(Celda).AlignRight().Text(Monto(x.MontoAbonado, simbolo));
                            t.Cell().Element(Celda).AlignRight().Text(Monto(x.MontoEquivalenteUsd, "$"));
                        }
                    });

                    // Totales y tipo de cambio utilizado
                    col.Item().PaddingTop(8).AlignRight().Column(tot =>
                    {
                        tot.Item().Text($"Tipo de cambio: C$ {d.TipoCambio.ToString("0.####", ci)} por 1 USD");
                        tot.Item().Text($"Total recibido: {Monto(d.TotalRecibido, simbolo)}").Bold().FontSize(12);
                        tot.Item().Text($"Equivalente en USD: {Monto(d.TotalEquivalenteUsd, "$")}");
                        tot.Item().Text($"Equivalente en córdobas: {Monto(d.TotalEquivalenteNio, "C$")}");
                    });

                    col.Item().PaddingTop(10).Text($"Registrado por: {d.RegistradoPor}");
                });

                // Pie de página
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Emitido el {d.FechaEmision:dd/MM/yyyy HH:mm}   |   Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();

        // "inline" para que Postman o el navegador lo muestren en lugar de forzar la descarga
        Response.Headers.ContentDisposition = $"inline; filename=\"{d.Numero}.pdf\"";
        return File(pdf, "application/pdf");
    }

    // Estilo de la celda de encabezado de la tabla
    private static IContainer Encabezado(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Padding(4).DefaultTextStyle(t => t.Bold());

    // Estilo de las celdas del cuerpo de la tabla
    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);
}