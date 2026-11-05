using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Aula
{
    public int IdAula { get; set; }

    public string Nombre { get; set; } = null!;

    public bool Estado { get; set; }

    public virtual ICollection<Grupo> Grupos { get; set; } = new List<Grupo>();
}
