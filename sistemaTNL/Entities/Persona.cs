using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Persona
{
    public int IdPersona { get; set; }

    public string NombreCompleto { get; set; } = null!;

    public byte[]? Fotografia { get; set; }

    public DateOnly FechaNacimiento { get; set; }

    public string? Cedula { get; set; }

    public string? Celular { get; set; }

    public int IdBarrio { get; set; }

    public string DireccionExacta { get; set; } = null!;

    public bool Estado { get; set; }

    public virtual Docente? Docente { get; set; }

    public virtual Estudiante? Estudiante { get; set; }

    public virtual Barrio IdBarrioNavigation { get; set; } = null!;

    public virtual Tutor? Tutor { get; set; }

    public virtual Usuario? Usuario { get; set; }
}
