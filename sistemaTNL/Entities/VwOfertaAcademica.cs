using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class VwOfertaAcademica
{
    public int IdCurso { get; set; }

    public string Curso { get; set; } = null!;

    public string Nivel { get; set; } = null!;

    public int IdPeriodo { get; set; }

    public string Modalidad { get; set; } = null!;

    public int IdGrupo { get; set; }

    public string Identificacion { get; set; } = null!;

    public string? Horario { get; set; }

    public string Aula { get; set; } = null!;

    public string Docente { get; set; } = null!;

    public DateOnly FechaInicioCurso { get; set; }

    public DateOnly FechaFinCurso { get; set; }

    public int? Inscritos { get; set; }

    public int? Capacidad { get; set; }

    public int? CuposDisponibles { get; set; }
}
