using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class ObligacionPago
{
    public int IdObligacion { get; set; }

    public int IdMatricula { get; set; }

    public int? IdConcepto { get; set; }

    public string Descripcion { get; set; } = null!;

    public DateOnly? MesCorrespondiente { get; set; }

    public decimal PrecioOriginalUsd { get; set; }

    public decimal DescuentoUsd { get; set; }

    public decimal? PrecioFinalUsd { get; set; }

    public decimal TotalAbonadoUsd { get; set; }

    public decimal? SaldoPendienteUsd { get; set; }

    public string EstadoPago { get; set; } = null!;

    public DateTime FechaGeneracion { get; set; }

    public virtual ICollection<DetallePago> DetallePagos { get; set; } = new List<DetallePago>();

    public virtual ConceptoPago? IdConceptoNavigation { get; set; }

    public virtual Matricula IdMatriculaNavigation { get; set; } = null!;
}
