using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Parametro
{
    public string Clave { get; set; } = null!;

    public string Valor { get; set; } = null!;

    public string? Descripcion { get; set; }
}
