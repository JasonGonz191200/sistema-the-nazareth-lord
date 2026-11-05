using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Pago
{
    public int IdPago { get; set; }

    public int IdMatricula { get; set; }

    public int IdUsuario { get; set; }

    public DateOnly FechaPago { get; set; }

    public DateTime FechaRegistro { get; set; }

    public string MonedaRecibida { get; set; } = null!;

    public decimal TipoCambio { get; set; }

    public decimal TotalRecibido { get; set; }

    public decimal TotalEquivalenteUsd { get; set; }

    public decimal TotalEquivalenteNio { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime? FechaAnulacion { get; set; }

    public int? IdUsuarioAnulacion { get; set; }

    public string? MotivoAnulacion { get; set; }

    public virtual Comprobante? Comprobante { get; set; }

    public virtual ICollection<DetallePago> DetallePagos { get; set; } = new List<DetallePago>();

    public virtual Matricula IdMatriculaNavigation { get; set; } = null!;

    public virtual Usuario? IdUsuarioAnulacionNavigation { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
