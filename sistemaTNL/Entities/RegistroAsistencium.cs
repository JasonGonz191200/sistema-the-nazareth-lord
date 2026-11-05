using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class RegistroAsistencium
{
    public int IdRegistro { get; set; }

    public int IdGrupo { get; set; }

    public int IdUsuario { get; set; }

    public DateOnly FechaClase { get; set; }

    public DateTime FechaRegistro { get; set; }

    public virtual ICollection<DetalleAsistencium> DetalleAsistencia { get; set; } = new List<DetalleAsistencium>();

    public virtual Grupo IdGrupoNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
