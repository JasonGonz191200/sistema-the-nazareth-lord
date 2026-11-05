using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class DetallePago
{
    public int IdDetalle { get; set; }

    public int IdPago { get; set; }

    public int IdObligacion { get; set; }

    public int IdMatricula { get; set; }

    public decimal MontoAbonado { get; set; }

    public decimal MontoEquivalenteUsd { get; set; }

    public string? Observacion { get; set; }

    public virtual ObligacionPago ObligacionPago { get; set; } = null!;

    public virtual Pago Pago { get; set; } = null!;
}
