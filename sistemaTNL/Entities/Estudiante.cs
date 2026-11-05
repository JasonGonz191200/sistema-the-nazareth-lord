using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Estudiante
{
    public int IdEstudiante { get; set; }

    public int IdPersona { get; set; }

    public int? IdTutor { get; set; }

    public string? NumeroCarne { get; set; }

    public bool Estado { get; set; }

    public virtual Persona IdPersonaNavigation { get; set; } = null!;

    public virtual Tutor? IdTutorNavigation { get; set; }

    public virtual ICollection<Matricula> Matriculas { get; set; } = new List<Matricula>();
}
