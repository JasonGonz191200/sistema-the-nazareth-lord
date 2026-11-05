using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class VwEstadoCuentum
{
    public int IdEstudiante { get; set; }

    public string? NumeroCarne { get; set; }

    public string NombreCompleto { get; set; } = null!;

    public int IdMatricula { get; set; }

    public int IdPeriodo { get; set; }

    public int IdObligacion { get; set; }

    public string Descripcion { get; set; } = null!;

    public string? CodigoConcepto { get; set; }

    public DateOnly? MesCorrespondiente { get; set; }

    public decimal PrecioOriginalUsd { get; set; }

    public decimal DescuentoUsd { get; set; }

    public decimal? PrecioFinalUsd { get; set; }

    public decimal TotalAbonadoUsd { get; set; }

    public decimal? SaldoPendienteUsd { get; set; }

    public string EstadoMostrar { get; set; } = null!;

    public string EstadoPago { get; set; } = null!;
}
