using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Municipio
{
    public int IdMunicipio { get; set; }

    public string Nombre { get; set; } = null!;

    public int IdDepartamento { get; set; }

    public virtual ICollection<Barrio> Barrios { get; set; } = new List<Barrio>();

    public virtual Departamento IdDepartamentoNavigation { get; set; } = null!;
}
