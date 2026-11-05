using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class VwDesercion
{
    public int IdPeriodo { get; set; }

    public int IdNivel { get; set; }

    public int? EstudiantesDebianContinuar { get; set; }

    public int? EstudiantesSinMatricula { get; set; }

    public decimal? TasaDesercionPct { get; set; }
}
