// @ts-nocheck
// Desplegables en cascada: departamento -> municipio -> barrio.
// Cada función recibe los <select> con los que trabaja.

// Llena un <select> con las opciones y deja seleccionada la indicada
function llenarSelect(select, items, campoId, seleccionado) {
    select.replaceChildren();
    const vacia = document.createElement('option');
    vacia.value = '';
    vacia.textContent = 'Seleccione...';
    select.appendChild(vacia);
    for (const it of items) {
        const o = document.createElement('option');
        o.value = String(it[campoId]);
        o.textContent = it.nombre;
        select.appendChild(o);
    }
    select.value = seleccionado ? String(seleccionado) : '';
}

async function cargarMunicipios(selMun, idDepartamento, seleccionado = null) {
    if (!idDepartamento) { llenarSelect(selMun, [], 'idMunicipio', null); return; }
    const r = await api(`/api/ubicaciones/municipios?idDepartamento=${idDepartamento}`);
    llenarSelect(selMun, r.ok ? r.datos : [], 'idMunicipio', seleccionado);
}

async function cargarBarrios(selBar, idMunicipio, seleccionado = null) {
    if (!idMunicipio) { llenarSelect(selBar, [], 'idBarrio', null); return; }
    const r = await api(`/api/ubicaciones/barrios?idMunicipio=${idMunicipio}`);
    llenarSelect(selBar, r.ok ? r.datos : [], 'idBarrio', seleccionado);
}

// Carga los departamentos y deja conectada la cascada entre los tres desplegables
async function iniciarUbicaciones(selDep, selMun, selBar) {
    const r = await api('/api/ubicaciones/departamentos');
    llenarSelect(selDep, r.ok ? r.datos : [], 'idDepartamento', null);
    llenarSelect(selMun, [], 'idMunicipio', null);
    llenarSelect(selBar, [], 'idBarrio', null);

    selDep.addEventListener('change', async () => {
        await cargarMunicipios(selMun, selDep.value);
        llenarSelect(selBar, [], 'idBarrio', null);
    });
    selMun.addEventListener('change', () => cargarBarrios(selBar, selMun.value));
}