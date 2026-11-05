using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class VwTotalesFinanciero
{
    public int IdEstudiante { get; set; }

    public int IdMatricula { get; set; }

    public int IdPeriodo { get; set; }

    public decimal? TotalOriginalUsd { get; set; }

    public decimal? TotalDescuentosUsd { get; set; }

    public decimal? TotalAPagarUsd { get; set; }

    public decimal? TotalRecibidoUsd { get; set; }

    public decimal? SaldoPendienteUsd { get; set; }
}
