using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class VwIngresosMensuale
{
    public int? Anio { get; set; }

    public int? Mes { get; set; }

    public decimal? TotalIngresosNio { get; set; }

    public decimal? TotalIngresosUsd { get; set; }

    public int? CantidadPagos { get; set; }
}
