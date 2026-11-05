using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class DetalleAsistencium
{
    public int IdDetalle { get; set; }

    public int IdRegistro { get; set; }

    public int IdMatricula { get; set; }

    public string Estado { get; set; } = null!;

    public virtual Matricula IdMatriculaNavigation { get; set; } = null!;

    public virtual RegistroAsistencium IdRegistroNavigation { get; set; } = null!;
}
