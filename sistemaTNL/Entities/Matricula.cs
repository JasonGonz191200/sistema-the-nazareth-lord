using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Matricula
{
    public int IdMatricula { get; set; }

    public int IdEstudiante { get; set; }

    public int IdPeriodo { get; set; }

    public int IdGrupo { get; set; }

    public DateOnly FechaMatricula { get; set; }

    public string? Observaciones { get; set; }

    public bool Estado { get; set; }

    public DateOnly? FechaInhabilitacion { get; set; }

    public virtual ICollection<DetalleAsistencium> DetalleAsistencia { get; set; } = new List<DetalleAsistencium>();

    public virtual Grupo Grupo { get; set; } = null!;

    public virtual Estudiante IdEstudianteNavigation { get; set; } = null!;

    public virtual PeriodoInscripcion IdPeriodoNavigation { get; set; } = null!;

    public virtual ICollection<ObligacionPago> ObligacionPagos { get; set; } = new List<ObligacionPago>();

    public virtual ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
