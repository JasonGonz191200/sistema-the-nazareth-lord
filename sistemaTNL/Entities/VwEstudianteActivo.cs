using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class VwEstudianteActivo
{
    public int IdEstudiante { get; set; }

    public string? NumeroCarne { get; set; }

    public string NombreCompleto { get; set; } = null!;

    public int IdMatricula { get; set; }

    public int IdPeriodo { get; set; }

    public int IdGrupo { get; set; }

    public int IdNivel { get; set; }
}
