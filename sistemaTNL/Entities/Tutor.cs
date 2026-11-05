using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Tutor
{
    public int IdTutor { get; set; }

    public int IdPersona { get; set; }

    public bool Estado { get; set; }

    public virtual ICollection<Estudiante> Estudiantes { get; set; } = new List<Estudiante>();

    public virtual Persona IdPersonaNavigation { get; set; } = null!;
}
