using System;
using System.Collections.Generic;

namespace sistemaTNL.Entities;

public partial class TokenRecuperacion
{
    public int IdToken { get; set; }

    public int IdUsuario { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateTime FechaExpiracion { get; set; }

    public bool Usado { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
