using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Modalidad
{
    public int IdModalidad { get; set; }

    public string Nombre { get; set; } = null!;

    public string? HorarioPredeterminado { get; set; }

    public bool Estado { get; set; }

    public virtual ICollection<PeriodoInscripcion> PeriodoInscripcions { get; set; } = new List<PeriodoInscripcion>();

    public virtual ICollection<Docente> IdDocentes { get; set; } = new List<Docente>();
}
