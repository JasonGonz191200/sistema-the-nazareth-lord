using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Docente
{
    public int IdDocente { get; set; }

    public int IdPersona { get; set; }

    public bool EsLicenciado { get; set; }

    public int AniosExperiencia { get; set; }

    public string? InstitucionesLaborales { get; set; }

    public bool Estado { get; set; }

    public virtual ICollection<Grupo> Grupos { get; set; } = new List<Grupo>();

    public virtual Persona IdPersonaNavigation { get; set; } = null!;

    public virtual ICollection<Modalidad> IdModalidads { get; set; } = new List<Modalidad>();
}
