// Dibuja el menú lateral común a todas las páginas, carga los datos del usuario
// y maneja el cierre de sesión. Cada página lo usa así:  montarLayout('inicio.html');

// Opciones del menú. Cuando una página nueva esté lista, se cambia su "activo" a true.
const MENU = [
    { texto: 'Inicio', href: 'inicio.html', activo: true },

    { seccion: 'Personas' },
    { texto: 'Personas y roles', href: 'personas.html', activo: true },

    { seccion: 'Académico' },
    { texto: 'Cursos y períodos', href: 'cursos.html', activo: true },
    { texto: 'Grupos', href: 'grupos.html', activo: false },
    { texto: 'Asistencia', href: 'asistencia.html', activo: false },

    { seccion: 'Finanzas' },
    { texto: 'Matrículas', href: 'matriculas.html', activo: false },
    { texto: 'Pagos', href: 'pagos.html', activo: false },

    { seccion: 'Consultas' },
    { texto: 'Reportes', href: 'reportes.html', activo: false },
    { texto: 'Auditoría', href: 'auditoria.html', activo: false },

    { seccion: 'Administración' },
    { texto: 'Usuarios', href: 'usuarios.html', activo: false },
];

// Construye el menú dentro de <div id="menu"></div>.
// "paginaActual" es el nombre del archivo de la página (para resaltar su opción).
async function montarLayout(paginaActual) {
    const contenedor = document.getElementById('menu');
    if (!contenedor) return null;

    // Lista de opciones
    const items = MENU.map((m) => {
        if (m.seccion) return `<li class="seccion">${m.seccion}</li>`;
        if (!m.activo) return `<li><a class="nav-link disabled" href="#">${m.texto}</a></li>`;
        const clase = m.href === paginaActual ? 'nav-link active' : 'nav-link';
        return `<li><a class="${clase}" href="${m.href}">${m.texto}</a></li>`;
    }).join('');

    contenedor.innerHTML = `
    <nav class="barra-lateral">
      <div class="marca d-flex justify-content-between align-items-center">
        <div>
          <div class="fw-bold">The Nazareth Lord</div>
          <small class="text-white-50">Matrícula y pagos</small>
        </div>
        <!-- Botón del menú: solo se ve en pantallas pequeñas -->
        <button class="btn btn-sm btn-outline-light d-md-none" type="button"
                data-bs-toggle="collapse" data-bs-target="#listaMenu" aria-label="Menú">☰</button>
      </div>

      <div id="listaMenu" class="collapse d-md-flex flex-column flex-grow-1">
        <ul class="nav flex-column mt-2">${items}</ul>
        <div class="mt-auto p-3 border-top border-white border-opacity-25">
          <div id="nombreUsuario" class="fw-semibold">...</div>
          <small id="rolUsuario" class="text-white-50"></small>
          <button id="btnSalir" class="btn btn-sm btn-outline-light w-100 mt-2">Cerrar sesión</button>
        </div>
      </div>
    </nav>`;

    // Cerrar sesión
    const btnSalir = document.getElementById('btnSalir');
    if (btnSalir) {
        btnSalir.addEventListener('click', async () => {
            await api('/api/auth/logout', { method: 'POST', sinRedireccion: true });
            window.location.href = '/index.html';
        });
    }

    // Datos del usuario con sesión. Si la sesión venció, api() redirige al login.
    const r = await api('/api/auth/yo');
    if (!r.ok) return null;
    const elNombre = document.getElementById('nombreUsuario');
    const elRol = document.getElementById('rolUsuario');
    if (elNombre) elNombre.textContent = r.datos?.nombreUsuario ?? '';
    if (elRol) elRol.textContent = r.datos?.rol ?? '';
    return r.datos;
}