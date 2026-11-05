using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class ConceptoPago
{
    public int IdConcepto { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public decimal PrecioGeneralUsd { get; set; }

    public bool EsObligatorio { get; set; }

    public bool Estado { get; set; }

    public virtual ICollection<ObligacionPago> ObligacionPagos { get; set; } = new List<ObligacionPago>();
}
