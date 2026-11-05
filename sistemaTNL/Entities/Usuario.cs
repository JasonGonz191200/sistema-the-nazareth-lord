using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public int IdPersona { get; set; }

    public string NombreUsuario { get; set; } = null!;

    public string ContrasenaHash { get; set; } = null!;

    public string Correo { get; set; } = null!;

    public string Rol { get; set; } = null!;

    public bool Estado { get; set; }

    public int IntentosFallidos { get; set; }

    public DateTime? BloqueadoHasta { get; set; }

    public virtual Persona IdPersonaNavigation { get; set; } = null!;

    public virtual ICollection<Pago> PagoIdUsuarioAnulacionNavigations { get; set; } = new List<Pago>();

    public virtual ICollection<Pago> PagoIdUsuarioNavigations { get; set; } = new List<Pago>();

    public virtual ICollection<RegistroAsistencium> RegistroAsistencia { get; set; } = new List<RegistroAsistencium>();

    public virtual ICollection<RegistroAuditorium> RegistroAuditoria { get; set; } = new List<RegistroAuditorium>();

    public virtual ICollection<TokenRecuperacion> TokenRecuperacions { get; set; } = new List<TokenRecuperacion>();
}
