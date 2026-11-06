namespace sistemaTNL.DTOs;

public record EstudianteDatos(int? IdTutor);

public record DocenteDatos(
    bool EsLicenciado,
    int AniosExperiencia,
    string? InstitucionesLaborales,
    List<int> IdModalidades);

public record RegistrarPersonaRequest(
    string NombreCompleto,
    DateOnly FechaNacimiento,
    string? Cedula,
    string? Celular,
    int IdBarrio,
    string DireccionExacta,
    bool EsTutor,
    EstudianteDatos? Estudiante,
    DocenteDatos? Docente);

public record PersonaResumen(
    int IdPersona,
    string NombreCompleto,
    DateOnly FechaNacimiento,
    string? Cedula,
    string? Celular,
    string DireccionExacta,
    int IdBarrio,
    int IdMunicipio,
    int IdDepartamento,
    int? IdTutor,
    bool EsTutor,
    bool EsEstudiante,
    bool EsDocente,
    bool Reutilizada);

public record TutorItem(int IdTutor, int IdPersona, string NombreCompleto, string? Cedula);

public record NuevoBarrioRequest(string Nombre, int IdMunicipio);