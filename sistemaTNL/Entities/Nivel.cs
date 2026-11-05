using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Nivel
{
    public int IdNivel { get; set; }

    public string Nombre { get; set; } = null!;

    public int? Orden { get; set; }

    public bool Estado { get; set; }

    public virtual ICollection<Curso> Cursos { get; set; } = new List<Curso>();

    public virtual ICollection<Grupo> Grupos { get; set; } = new List<Grupo>();
}
