using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Curso
{
    public int IdCurso { get; set; }

    public string Nombre { get; set; } = null!;

    public int? IdNivel { get; set; }

    public bool Estado { get; set; }

    public virtual Nivel? IdNivelNavigation { get; set; }

    public virtual ICollection<PeriodoInscripcion> PeriodoInscripcions { get; set; } = new List<PeriodoInscripcion>();
}
