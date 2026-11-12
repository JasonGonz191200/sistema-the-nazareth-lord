// Función común para llamar a la API (mismo origen, así la cookie de sesión viaja sola).
// Devuelve siempre { ok, estado, datos }.
async function api(ruta, opciones = {}) {
    const config = { method: 'GET', headers: {}, credentials: 'same-origin', ...opciones };

    // Si se envía un cuerpo (objeto), se convierte a JSON; los archivos (FormData) van tal cual
    if (config.body && !(config.body instanceof FormData)) {
        config.headers['Content-Type'] = 'application/json';
        config.body = JSON.stringify(config.body);
    }

    let respuesta;
    try {
        respuesta = await fetch(ruta, config);
    } catch {
        return { ok: false, estado: 0, datos: { mensaje: 'No se pudo conectar con el servidor.' } };
    }

    // 401 fuera del login = la sesión venció: se vuelve a la pantalla de ingreso
    if (respuesta.status === 401 && !config.sinRedireccion) {
        window.location.href = '/index.html';
        return { ok: false, estado: 401, datos: null };
    }

    // Si la respuesta trae JSON, se lee
    let datos = null;
    const tipo = respuesta.headers.get('content-type') || '';
    if (tipo.includes('application/json')) {
        datos = await respuesta.json();
    }
    return { ok: respuesta.ok, estado: respuesta.status, datos };
}

// Texto de error listo para mostrar al usuario
function mensajeError(resultado, textoPorDefecto = 'No se pudo completar la operación.') {
    return resultado?.datos?.mensaje || textoPorDefecto;
}
// Devuelve el elemento de la página con ese id. Si no existe, avisa con un error claro
// (así el resto del código no tiene que comprobar a cada rato si es null).
function el(id) {
    const e = document.getElementById(id);
    if (!e) throw new Error('Falta el elemento con id "' + id + '" en la página.');
    return e;
}