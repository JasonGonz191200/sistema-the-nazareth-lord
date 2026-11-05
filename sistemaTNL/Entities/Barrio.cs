using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Barrio
{
    public int IdBarrio { get; set; }

    public string Nombre { get; set; } = null!;

    public int IdMunicipio { get; set; }

    public virtual Municipio IdMunicipioNavigation { get; set; } = null!;

    public virtual ICollection<Persona> Personas { get; set; } = new List<Persona>();
}
