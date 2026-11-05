using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class PeriodoInscripcion
{
    public int IdPeriodo { get; set; }

    public int IdCurso { get; set; }

    public int IdModalidad { get; set; }

    public DateOnly FechaCreacion { get; set; }

    public DateOnly FechaInicioInscripcion { get; set; }

    public DateOnly FechaFinInscripcion { get; set; }

    public DateOnly FechaInicioCurso { get; set; }

    public DateOnly FechaFinCurso { get; set; }

    public string Estado { get; set; } = null!;

    public virtual ICollection<Grupo> Grupos { get; set; } = new List<Grupo>();

    public virtual Curso IdCursoNavigation { get; set; } = null!;

    public virtual Modalidad IdModalidadNavigation { get; set; } = null!;

    public virtual ICollection<Matricula> Matriculas { get; set; } = new List<Matricula>();
}
