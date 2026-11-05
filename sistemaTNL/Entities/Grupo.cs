using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Grupo
{
    public int IdGrupo { get; set; }

    public int IdPeriodo { get; set; }

    public int IdDocente { get; set; }

    public int IdAula { get; set; }

    public int IdNivel { get; set; }

    public string Identificacion { get; set; } = null!;

    public string? Horario { get; set; }

    public bool Estado { get; set; }

    public virtual Aula IdAulaNavigation { get; set; } = null!;

    public virtual Docente IdDocenteNavigation { get; set; } = null!;

    public virtual Nivel IdNivelNavigation { get; set; } = null!;

    public virtual PeriodoInscripcion IdPeriodoNavigation { get; set; } = null!;

    public virtual ICollection<Matricula> Matriculas { get; set; } = new List<Matricula>();

    public virtual ICollection<RegistroAsistencium> RegistroAsistencia { get; set; } = new List<RegistroAsistencium>();
}
