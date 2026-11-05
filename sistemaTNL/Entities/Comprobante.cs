using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Comprobante
{
    public int IdComprobante { get; set; }

    public int IdPago { get; set; }

    public int Consecutivo { get; set; }

    public string? Numero { get; set; }

    public DateTime FechaHora { get; set; }

    public virtual Pago IdPagoNavigation { get; set; } = null!;
}
