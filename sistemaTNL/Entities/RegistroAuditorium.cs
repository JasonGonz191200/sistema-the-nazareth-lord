using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class RegistroAuditorium
{
    public long IdAuditoria { get; set; }

    public int? IdUsuario { get; set; }

    public string? NombreUsuarioIntento { get; set; }

    public DateTimeOffset FechaHora { get; set; }

    public string TipoAccion { get; set; } = null!;

    public string EntidadAfectada { get; set; } = null!;

    public int? IdRegistroAfectado { get; set; }

    public string Resultado { get; set; } = null!;

    public string? DireccionIp { get; set; }

    public string? ValorAnterior { get; set; }

    public string? ValorNuevo { get; set; }

    public string? Descripcion { get; set; }

    public virtual Usuario? IdUsuarioNavigation { get; set; }
}
