using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Entities;

namespace sistemaTNL.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Aula> Aulas { get; set; }

    public virtual DbSet<Barrio> Barrios { get; set; }

    public virtual DbSet<CarneSecuencium> CarneSecuencia { get; set; }

    public virtual DbSet<Comprobante> Comprobantes { get; set; }

    public virtual DbSet<ConceptoPago> ConceptoPagos { get; set; }

    public virtual DbSet<Curso> Cursos { get; set; }

    public virtual DbSet<Departamento> Departamentos { get; set; }

    public virtual DbSet<DetalleAsistencium> DetalleAsistencia { get; set; }

    public virtual DbSet<DetallePago> DetallePagos { get; set; }

    public virtual DbSet<Docente> Docentes { get; set; }

    public virtual DbSet<Estudiante> Estudiantes { get; set; }

    public virtual DbSet<Grupo> Grupos { get; set; }

    public virtual DbSet<Matricula> Matriculas { get; set; }

    public virtual DbSet<Modalidad> Modalidads { get; set; }

    public virtual DbSet<Municipio> Municipios { get; set; }

    public virtual DbSet<Nivel> Nivels { get; set; }

    public virtual DbSet<ObligacionPago> ObligacionPagos { get; set; }

    public virtual DbSet<Pago> Pagos { get; set; }

    public virtual DbSet<Parametro> Parametros { get; set; }

    public virtual DbSet<PeriodoInscripcion> PeriodoInscripcions { get; set; }

    public virtual DbSet<Persona> Personas { get; set; }

    public virtual DbSet<RegistroAsistencium> RegistroAsistencia { get; set; }

    public virtual DbSet<RegistroAuditorium> RegistroAuditoria { get; set; }

    public virtual DbSet<TokenRecuperacion> TokenRecuperacions { get; set; }

    public virtual DbSet<Tutor> Tutors { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    public virtual DbSet<VwDesercion> VwDesercions { get; set; }

    public virtual DbSet<VwEstadoCuentum> VwEstadoCuenta { get; set; }

    public virtual DbSet<VwEstudianteActivo> VwEstudianteActivos { get; set; }

    public virtual DbSet<VwIngresosMensuale> VwIngresosMensuales { get; set; }

    public virtual DbSet<VwOfertaAcademica> VwOfertaAcademicas { get; set; }

    public virtual DbSet<VwTotalesFinanciero> VwTotalesFinancieros { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Aula>(entity =>
        {
            entity.HasKey(e => e.IdAula);

            entity.ToTable("aula");

            entity.HasIndex(e => e.Nombre, "UQ_aula_nombre").IsUnique();

            entity.Property(e => e.IdAula).HasColumnName("id_aula");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_aula_estado")
                .HasColumnName("estado");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<Barrio>(entity =>
        {
            entity.HasKey(e => e.IdBarrio);

            entity.ToTable("barrio");

            entity.HasIndex(e => new { e.IdMunicipio, e.Nombre }, "UQ_barrio_municipio_nombre").IsUnique();

            entity.Property(e => e.IdBarrio).HasColumnName("id_barrio");
            entity.Property(e => e.IdMunicipio).HasColumnName("id_municipio");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nombre");

            entity.HasOne(d => d.IdMunicipioNavigation).WithMany(p => p.Barrios)
                .HasForeignKey(d => d.IdMunicipio)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_barrio_municipio");
        });

        modelBuilder.Entity<CarneSecuencium>(entity =>
        {
            entity.HasKey(e => e.Anio);

            entity.ToTable("carne_secuencia");

            entity.Property(e => e.Anio)
                .ValueGeneratedNever()
                .HasColumnName("anio");
            entity.Property(e => e.Siguiente).HasColumnName("siguiente");
        });

        modelBuilder.Entity<Comprobante>(entity =>
        {
            entity.HasKey(e => e.IdComprobante);

            entity.ToTable("comprobante", tb => tb.HasTrigger("trg_comprobante_inmutable"));

            entity.HasIndex(e => e.Consecutivo, "UQ_comprobante_consecutivo").IsUnique();

            entity.HasIndex(e => e.Numero, "UQ_comprobante_numero").IsUnique();

            entity.HasIndex(e => e.IdPago, "UQ_comprobante_pago").IsUnique();

            entity.Property(e => e.IdComprobante).HasColumnName("id_comprobante");
            entity.Property(e => e.Consecutivo)
                .HasDefaultValueSql("(NEXT VALUE FOR [dbo].[seq_comprobante])", "DF_comprobante_consec")
                .HasColumnName("consecutivo");
            entity.Property(e => e.FechaHora)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_comprobante_fecha")
                .HasColumnName("fecha_hora");
            entity.Property(e => e.IdPago).HasColumnName("id_pago");
            entity.Property(e => e.Numero)
                .HasMaxLength(11)
                .IsUnicode(false)
                .HasComputedColumnSql("('CP-'+right('00000000'+CONVERT([varchar](10),[consecutivo]),(8)))", true)
                .HasColumnName("numero");

            entity.HasOne(d => d.IdPagoNavigation).WithOne(p => p.Comprobante)
                .HasForeignKey<Comprobante>(d => d.IdPago)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_comprobante_pago");
        });

        modelBuilder.Entity<ConceptoPago>(entity =>
        {
            entity.HasKey(e => e.IdConcepto);

            entity.ToTable("concepto_pago");

            entity.HasIndex(e => e.Codigo, "UQ_concepto_codigo").IsUnique();

            entity.HasIndex(e => e.Nombre, "UQ_concepto_nombre").IsUnique();

            entity.Property(e => e.IdConcepto).HasColumnName("id_concepto");
            entity.Property(e => e.Codigo)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("codigo");
            entity.Property(e => e.EsObligatorio).HasColumnName("es_obligatorio");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_concepto_estado")
                .HasColumnName("estado");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nombre");
            entity.Property(e => e.PrecioGeneralUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("precio_general_usd");
        });

        modelBuilder.Entity<Curso>(entity =>
        {
            entity.HasKey(e => e.IdCurso);

            entity.ToTable("curso", tb => tb.HasTrigger("trg_curso_estado"));

            entity.HasIndex(e => e.IdNivel, "IX_curso_nivel");

            entity.Property(e => e.IdCurso).HasColumnName("id_curso");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_curso_estado")
                .HasColumnName("estado");
            entity.Property(e => e.IdNivel).HasColumnName("id_nivel");
            entity.Property(e => e.Nombre)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("nombre");

            entity.HasOne(d => d.IdNivelNavigation).WithMany(p => p.Cursos)
                .HasForeignKey(d => d.IdNivel)
                .HasConstraintName("FK_curso_nivel");
        });

        modelBuilder.Entity<Departamento>(entity =>
        {
            entity.HasKey(e => e.IdDepartamento);

            entity.ToTable("departamento");

            entity.HasIndex(e => e.Nombre, "UQ_departamento_nombre").IsUnique();

            entity.Property(e => e.IdDepartamento).HasColumnName("id_departamento");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<DetalleAsistencium>(entity =>
        {
            entity.HasKey(e => e.IdDetalle);

            entity.ToTable("detalle_asistencia", tb =>
                {
                    tb.HasTrigger("trg_detalle_asistencia_inmutable");
                    tb.HasTrigger("trg_detalle_asistencia_insert");
                });

            entity.HasIndex(e => e.IdMatricula, "IX_detasis_matricula");

            entity.HasIndex(e => new { e.IdRegistro, e.IdMatricula }, "UQ_detasis_registro_matricula").IsUnique();

            entity.Property(e => e.IdDetalle).HasColumnName("id_detalle");
            entity.Property(e => e.Estado)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("estado");
            entity.Property(e => e.IdMatricula).HasColumnName("id_matricula");
            entity.Property(e => e.IdRegistro).HasColumnName("id_registro");

            entity.HasOne(d => d.IdMatriculaNavigation).WithMany(p => p.DetalleAsistencia)
                .HasForeignKey(d => d.IdMatricula)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_detasis_matricula");

            entity.HasOne(d => d.IdRegistroNavigation).WithMany(p => p.DetalleAsistencia)
                .HasForeignKey(d => d.IdRegistro)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_detasis_registro");
        });

        modelBuilder.Entity<DetallePago>(entity =>
        {
            entity.HasKey(e => e.IdDetalle);

            entity.ToTable("detalle_pago", tb =>
                {
                    tb.HasTrigger("trg_detalle_pago_inmutable");
                    tb.HasTrigger("trg_detalle_pago_insert");
                });

            entity.HasIndex(e => e.IdObligacion, "IX_detpago_obligacion");

            entity.HasIndex(e => new { e.IdPago, e.IdObligacion }, "UQ_detpago_pago_obligacion").IsUnique();

            entity.Property(e => e.IdDetalle).HasColumnName("id_detalle");
            entity.Property(e => e.IdMatricula).HasColumnName("id_matricula");
            entity.Property(e => e.IdObligacion).HasColumnName("id_obligacion");
            entity.Property(e => e.IdPago).HasColumnName("id_pago");
            entity.Property(e => e.MontoAbonado)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("monto_abonado");
            entity.Property(e => e.MontoEquivalenteUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("monto_equivalente_usd");
            entity.Property(e => e.Observacion)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("observacion");

            entity.HasOne(d => d.ObligacionPago).WithMany(p => p.DetallePagos)
                .HasPrincipalKey(p => new { p.IdObligacion, p.IdMatricula })
                .HasForeignKey(d => new { d.IdObligacion, d.IdMatricula })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_detpago_obligacion");

            entity.HasOne(d => d.Pago).WithMany(p => p.DetallePagos)
                .HasPrincipalKey(p => new { p.IdPago, p.IdMatricula })
                .HasForeignKey(d => new { d.IdPago, d.IdMatricula })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_detpago_pago");
        });

        modelBuilder.Entity<Docente>(entity =>
        {
            entity.HasKey(e => e.IdDocente);

            entity.ToTable("docente", tb => tb.HasTrigger("trg_docente_estado"));

            entity.HasIndex(e => e.IdPersona, "UQ_docente_persona").IsUnique();

            entity.Property(e => e.IdDocente).HasColumnName("id_docente");
            entity.Property(e => e.AniosExperiencia).HasColumnName("anios_experiencia");
            entity.Property(e => e.EsLicenciado).HasColumnName("es_licenciado");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_docente_estado")
                .HasColumnName("estado");
            entity.Property(e => e.IdPersona).HasColumnName("id_persona");
            entity.Property(e => e.InstitucionesLaborales)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("instituciones_laborales");

            entity.HasOne(d => d.IdPersonaNavigation).WithOne(p => p.Docente)
                .HasForeignKey<Docente>(d => d.IdPersona)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_docente_persona");

            entity.HasMany(d => d.IdModalidads).WithMany(p => p.IdDocentes)
                .UsingEntity<Dictionary<string, object>>(
                    "DocenteModalidad",
                    r => r.HasOne<Modalidad>().WithMany()
                        .HasForeignKey("IdModalidad")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_docmod_modalidad"),
                    l => l.HasOne<Docente>().WithMany()
                        .HasForeignKey("IdDocente")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_docmod_docente"),
                    j =>
                    {
                        j.HasKey("IdDocente", "IdModalidad");
                        j.ToTable("docente_modalidad");
                        j.HasIndex(new[] { "IdModalidad" }, "IX_docmod_modalidad");
                        j.IndexerProperty<int>("IdDocente").HasColumnName("id_docente");
                        j.IndexerProperty<int>("IdModalidad").HasColumnName("id_modalidad");
                    });
        });

        modelBuilder.Entity<Estudiante>(entity =>
        {
            entity.HasKey(e => e.IdEstudiante);

            entity.ToTable("estudiante", tb => tb.HasTrigger("trg_estudiante_validar"));

            entity.HasIndex(e => e.IdTutor, "IX_estudiante_tutor");

            entity.HasIndex(e => e.NumeroCarne, "UQ_estudiante_carne")
                .IsUnique()
                .HasFilter("([numero_carne] IS NOT NULL)");

            entity.HasIndex(e => e.IdPersona, "UQ_estudiante_persona").IsUnique();

            entity.Property(e => e.IdEstudiante).HasColumnName("id_estudiante");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_estudiante_estado")
                .HasColumnName("estado");
            entity.Property(e => e.IdPersona).HasColumnName("id_persona");
            entity.Property(e => e.IdTutor).HasColumnName("id_tutor");
            entity.Property(e => e.NumeroCarne)
                .HasMaxLength(7)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("numero_carne");

            entity.HasOne(d => d.IdPersonaNavigation).WithOne(p => p.Estudiante)
                .HasForeignKey<Estudiante>(d => d.IdPersona)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_estudiante_persona");

            entity.HasOne(d => d.IdTutorNavigation).WithMany(p => p.Estudiantes)
                .HasForeignKey(d => d.IdTutor)
                .HasConstraintName("FK_estudiante_tutor");
        });

        modelBuilder.Entity<Grupo>(entity =>
        {
            entity.HasKey(e => e.IdGrupo);

            entity.ToTable("grupo", tb => tb.HasTrigger("trg_grupo_validar"));

            entity.HasIndex(e => e.IdAula, "IX_grupo_aula");

            entity.HasIndex(e => e.IdDocente, "IX_grupo_docente");

            entity.HasIndex(e => e.IdNivel, "IX_grupo_nivel");

            entity.HasIndex(e => new { e.IdGrupo, e.IdPeriodo }, "UQ_grupo_id_periodo").IsUnique();

            entity.HasIndex(e => new { e.IdPeriodo, e.Identificacion }, "UQ_grupo_periodo_ident").IsUnique();

            entity.Property(e => e.IdGrupo).HasColumnName("id_grupo");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_grupo_estado")
                .HasColumnName("estado");
            entity.Property(e => e.Horario)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("horario");
            entity.Property(e => e.IdAula).HasColumnName("id_aula");
            entity.Property(e => e.IdDocente).HasColumnName("id_docente");
            entity.Property(e => e.IdNivel).HasColumnName("id_nivel");
            entity.Property(e => e.IdPeriodo).HasColumnName("id_periodo");
            entity.Property(e => e.Identificacion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("identificacion");

            entity.HasOne(d => d.IdAulaNavigation).WithMany(p => p.Grupos)
                .HasForeignKey(d => d.IdAula)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_grupo_aula");

            entity.HasOne(d => d.IdDocenteNavigation).WithMany(p => p.Grupos)
                .HasForeignKey(d => d.IdDocente)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_grupo_docente");

            entity.HasOne(d => d.IdNivelNavigation).WithMany(p => p.Grupos)
                .HasForeignKey(d => d.IdNivel)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_grupo_nivel");

            entity.HasOne(d => d.IdPeriodoNavigation).WithMany(p => p.Grupos)
                .HasForeignKey(d => d.IdPeriodo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_grupo_periodo");
        });

        modelBuilder.Entity<Matricula>(entity =>
        {
            entity.HasKey(e => e.IdMatricula);

            entity.ToTable("matricula", tb => tb.HasTrigger("trg_matricula_validar"));

            entity.HasIndex(e => e.IdEstudiante, "IX_matricula_estudiante");

            entity.HasIndex(e => new { e.IdGrupo, e.Estado }, "IX_matricula_grupo");

            entity.HasIndex(e => e.IdPeriodo, "IX_matricula_periodo");

            entity.HasIndex(e => new { e.IdEstudiante, e.IdPeriodo }, "UQ_matricula_estudiante_periodo_hab")
                .IsUnique()
                .HasFilter("([estado]=(1))");

            entity.Property(e => e.IdMatricula).HasColumnName("id_matricula");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_matricula_estado")
                .HasColumnName("estado");
            entity.Property(e => e.FechaInhabilitacion).HasColumnName("fecha_inhabilitacion");
            entity.Property(e => e.FechaMatricula)
                .HasDefaultValueSql("(CONVERT([date],sysdatetime()))", "DF_matricula_fecha")
                .HasColumnName("fecha_matricula");
            entity.Property(e => e.IdEstudiante).HasColumnName("id_estudiante");
            entity.Property(e => e.IdGrupo).HasColumnName("id_grupo");
            entity.Property(e => e.IdPeriodo).HasColumnName("id_periodo");
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("observaciones");

            entity.HasOne(d => d.IdEstudianteNavigation).WithMany(p => p.Matriculas)
                .HasForeignKey(d => d.IdEstudiante)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_matricula_estudiante");

            entity.HasOne(d => d.IdPeriodoNavigation).WithMany(p => p.Matriculas)
                .HasForeignKey(d => d.IdPeriodo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_matricula_periodo");

            entity.HasOne(d => d.Grupo).WithMany(p => p.Matriculas)
                .HasPrincipalKey(p => new { p.IdGrupo, p.IdPeriodo })
                .HasForeignKey(d => new { d.IdGrupo, d.IdPeriodo })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_matricula_grupo_periodo");
        });

        modelBuilder.Entity<Modalidad>(entity =>
        {
            entity.HasKey(e => e.IdModalidad);

            entity.ToTable("modalidad", tb => tb.HasTrigger("trg_modalidad_estado"));

            entity.HasIndex(e => e.Nombre, "UQ_modalidad_nombre").IsUnique();

            entity.Property(e => e.IdModalidad).HasColumnName("id_modalidad");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_modalidad_estado")
                .HasColumnName("estado");
            entity.Property(e => e.HorarioPredeterminado)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("horario_predeterminado");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<Municipio>(entity =>
        {
            entity.HasKey(e => e.IdMunicipio);

            entity.ToTable("municipio");

            entity.HasIndex(e => e.IdDepartamento, "IX_municipio_departamento");

            entity.HasIndex(e => new { e.IdDepartamento, e.Nombre }, "UQ_municipio_depto_nombre").IsUnique();

            entity.Property(e => e.IdMunicipio).HasColumnName("id_municipio");
            entity.Property(e => e.IdDepartamento).HasColumnName("id_departamento");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nombre");

            entity.HasOne(d => d.IdDepartamentoNavigation).WithMany(p => p.Municipios)
                .HasForeignKey(d => d.IdDepartamento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_municipio_departamento");
        });

        modelBuilder.Entity<Nivel>(entity =>
        {
            entity.HasKey(e => e.IdNivel);

            entity.ToTable("nivel");

            entity.HasIndex(e => e.Nombre, "UQ_nivel_nombre").IsUnique();

            entity.Property(e => e.IdNivel).HasColumnName("id_nivel");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_nivel_estado")
                .HasColumnName("estado");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre");
            entity.Property(e => e.Orden).HasColumnName("orden");
        });

        modelBuilder.Entity<ObligacionPago>(entity =>
        {
            entity.HasKey(e => e.IdObligacion);

            entity.ToTable("obligacion_pago");

            entity.HasIndex(e => e.IdConcepto, "IX_obligacion_concepto");

            entity.HasIndex(e => e.IdMatricula, "IX_obligacion_matricula");

            entity.HasIndex(e => new { e.IdMatricula, e.IdConcepto, e.MesCorrespondiente }, "UQ_obligacion_concepto_mes")
                .IsUnique()
                .HasFilter("([id_concepto] IS NOT NULL AND [estado_pago]<>'Anulado')");

            entity.HasIndex(e => new { e.IdObligacion, e.IdMatricula }, "UQ_obligacion_id_matricula").IsUnique();

            entity.Property(e => e.IdObligacion).HasColumnName("id_obligacion");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.DescuentoUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("descuento_usd");
            entity.Property(e => e.EstadoPago)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Pendiente", "DF_obligacion_estado")
                .HasColumnName("estado_pago");
            entity.Property(e => e.FechaGeneracion)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_obligacion_fecha")
                .HasColumnName("fecha_generacion");
            entity.Property(e => e.IdConcepto).HasColumnName("id_concepto");
            entity.Property(e => e.IdMatricula).HasColumnName("id_matricula");
            entity.Property(e => e.MesCorrespondiente).HasColumnName("mes_correspondiente");
            entity.Property(e => e.PrecioFinalUsd)
                .HasComputedColumnSql("([precio_original_usd]-[descuento_usd])", true)
                .HasColumnType("decimal(13, 2)")
                .HasColumnName("precio_final_usd");
            entity.Property(e => e.PrecioOriginalUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("precio_original_usd");
            entity.Property(e => e.SaldoPendienteUsd)
                .HasComputedColumnSql("(([precio_original_usd]-[descuento_usd])-[total_abonado_usd])", true)
                .HasColumnType("decimal(14, 2)")
                .HasColumnName("saldo_pendiente_usd");
            entity.Property(e => e.TotalAbonadoUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_abonado_usd");

            entity.HasOne(d => d.IdConceptoNavigation).WithMany(p => p.ObligacionPagos)
                .HasForeignKey(d => d.IdConcepto)
                .HasConstraintName("FK_obligacion_concepto");

            entity.HasOne(d => d.IdMatriculaNavigation).WithMany(p => p.ObligacionPagos)
                .HasForeignKey(d => d.IdMatricula)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_obligacion_matricula");
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.HasKey(e => e.IdPago);

            entity.ToTable("pago", tb =>
                {
                    tb.HasTrigger("trg_pago_insert");
                    tb.HasTrigger("trg_pago_update_delete");
                });

            entity.HasIndex(e => e.FechaPago, "IX_pago_fecha");

            entity.HasIndex(e => e.IdMatricula, "IX_pago_matricula");

            entity.HasIndex(e => e.IdUsuario, "IX_pago_usuario");

            entity.HasIndex(e => new { e.IdPago, e.IdMatricula }, "UQ_pago_id_matricula").IsUnique();

            entity.Property(e => e.IdPago).HasColumnName("id_pago");
            entity.Property(e => e.Estado)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("Registrado", "DF_pago_estado")
                .HasColumnName("estado");
            entity.Property(e => e.FechaAnulacion)
                .HasPrecision(0)
                .HasColumnName("fecha_anulacion");
            entity.Property(e => e.FechaPago)
                .HasDefaultValueSql("(CONVERT([date],sysdatetime()))", "DF_pago_fecha")
                .HasColumnName("fecha_pago");
            entity.Property(e => e.FechaRegistro)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_pago_registro")
                .HasColumnName("fecha_registro");
            entity.Property(e => e.IdMatricula).HasColumnName("id_matricula");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.IdUsuarioAnulacion).HasColumnName("id_usuario_anulacion");
            entity.Property(e => e.MonedaRecibida)
                .HasMaxLength(3)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("moneda_recibida");
            entity.Property(e => e.MotivoAnulacion)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("motivo_anulacion");
            entity.Property(e => e.TipoCambio)
                .HasColumnType("decimal(12, 4)")
                .HasColumnName("tipo_cambio");
            entity.Property(e => e.TotalEquivalenteNio)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_equivalente_nio");
            entity.Property(e => e.TotalEquivalenteUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_equivalente_usd");
            entity.Property(e => e.TotalRecibido)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_recibido");

            entity.HasOne(d => d.IdMatriculaNavigation).WithMany(p => p.Pagos)
                .HasForeignKey(d => d.IdMatricula)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_pago_matricula");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.PagoIdUsuarioNavigations)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_pago_usuario");

            entity.HasOne(d => d.IdUsuarioAnulacionNavigation).WithMany(p => p.PagoIdUsuarioAnulacionNavigations)
                .HasForeignKey(d => d.IdUsuarioAnulacion)
                .HasConstraintName("FK_pago_usuario_anulacion");
        });

        modelBuilder.Entity<Parametro>(entity =>
        {
            entity.HasKey(e => e.Clave);

            entity.ToTable("parametro");

            entity.Property(e => e.Clave)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("clave");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.Valor)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("valor");
        });

        modelBuilder.Entity<PeriodoInscripcion>(entity =>
        {
            entity.HasKey(e => e.IdPeriodo);

            entity.ToTable("periodo_inscripcion", tb => tb.HasTrigger("trg_periodo_validar"));

            entity.HasIndex(e => e.IdCurso, "IX_periodo_curso");

            entity.HasIndex(e => e.IdModalidad, "IX_periodo_modalidad");

            entity.Property(e => e.IdPeriodo).HasColumnName("id_periodo");
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Habilitado", "DF_periodo_estado")
                .HasColumnName("estado");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(CONVERT([date],sysdatetime()))", "DF_periodo_creacion")
                .HasColumnName("fecha_creacion");
            entity.Property(e => e.FechaFinCurso).HasColumnName("fecha_fin_curso");
            entity.Property(e => e.FechaFinInscripcion).HasColumnName("fecha_fin_inscripcion");
            entity.Property(e => e.FechaInicioCurso).HasColumnName("fecha_inicio_curso");
            entity.Property(e => e.FechaInicioInscripcion).HasColumnName("fecha_inicio_inscripcion");
            entity.Property(e => e.IdCurso).HasColumnName("id_curso");
            entity.Property(e => e.IdModalidad).HasColumnName("id_modalidad");

            entity.HasOne(d => d.IdCursoNavigation).WithMany(p => p.PeriodoInscripcions)
                .HasForeignKey(d => d.IdCurso)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_periodo_curso");

            entity.HasOne(d => d.IdModalidadNavigation).WithMany(p => p.PeriodoInscripcions)
                .HasForeignKey(d => d.IdModalidad)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_periodo_modalidad");
        });

        modelBuilder.Entity<Persona>(entity =>
        {
            entity.HasKey(e => e.IdPersona);

            entity.ToTable("persona", tb => tb.HasTrigger("trg_persona_validar"));

            entity.HasIndex(e => e.IdBarrio, "IX_persona_barrio");

            entity.HasIndex(e => e.Cedula, "UQ_persona_cedula")
                .IsUnique()
                .HasFilter("([cedula] IS NOT NULL)");

            entity.Property(e => e.IdPersona).HasColumnName("id_persona");
            entity.Property(e => e.Cedula)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("cedula");
            entity.Property(e => e.Celular)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("celular");
            entity.Property(e => e.DireccionExacta)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("direccion_exacta");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_persona_estado")
                .HasColumnName("estado");
            entity.Property(e => e.FechaNacimiento).HasColumnName("fecha_nacimiento");
            entity.Property(e => e.Fotografia).HasColumnName("fotografia");
            entity.Property(e => e.IdBarrio).HasColumnName("id_barrio");
            entity.Property(e => e.NombreCompleto)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("nombre_completo");

            entity.HasOne(d => d.IdBarrioNavigation).WithMany(p => p.Personas)
                .HasForeignKey(d => d.IdBarrio)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_persona_barrio");
        });

        modelBuilder.Entity<RegistroAsistencium>(entity =>
        {
            entity.HasKey(e => e.IdRegistro);

            entity.ToTable("registro_asistencia", tb =>
                {
                    tb.HasTrigger("trg_registro_asistencia_inmutable");
                    tb.HasTrigger("trg_registro_asistencia_insert");
                });

            entity.HasIndex(e => e.IdUsuario, "IX_regasis_usuario");

            entity.HasIndex(e => new { e.IdGrupo, e.FechaClase }, "UQ_registro_grupo_fecha").IsUnique();

            entity.Property(e => e.IdRegistro).HasColumnName("id_registro");
            entity.Property(e => e.FechaClase).HasColumnName("fecha_clase");
            entity.Property(e => e.FechaRegistro)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_regasis_fecha")
                .HasColumnName("fecha_registro");
            entity.Property(e => e.IdGrupo).HasColumnName("id_grupo");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");

            entity.HasOne(d => d.IdGrupoNavigation).WithMany(p => p.RegistroAsistencia)
                .HasForeignKey(d => d.IdGrupo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_regasis_grupo");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.RegistroAsistencia)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_regasis_usuario");
        });

        modelBuilder.Entity<RegistroAuditorium>(entity =>
        {
            entity.HasKey(e => e.IdAuditoria);

            entity.ToTable("registro_auditoria", tb => tb.HasTrigger("trg_auditoria_inmutable"));

            entity.HasIndex(e => e.FechaHora, "IX_auditoria_fecha");

            entity.HasIndex(e => e.IdUsuario, "IX_auditoria_usuario");

            entity.Property(e => e.IdAuditoria).HasColumnName("id_auditoria");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.DireccionIp)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("direccion_ip");
            entity.Property(e => e.EntidadAfectada)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("entidad_afectada");
            entity.Property(e => e.FechaHora)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_auditoria_fecha")
                .HasColumnName("fecha_hora");
            entity.Property(e => e.IdRegistroAfectado).HasColumnName("id_registro_afectado");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.NombreUsuarioIntento)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre_usuario_intento");
            entity.Property(e => e.Resultado)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValue("Exitoso", "DF_auditoria_resultado")
                .HasColumnName("resultado");
            entity.Property(e => e.TipoAccion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("tipo_accion");
            entity.Property(e => e.ValorAnterior).HasColumnName("valor_anterior");
            entity.Property(e => e.ValorNuevo).HasColumnName("valor_nuevo");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.RegistroAuditoria)
                .HasForeignKey(d => d.IdUsuario)
                .HasConstraintName("FK_auditoria_usuario");
        });

        modelBuilder.Entity<TokenRecuperacion>(entity =>
        {
            entity.HasKey(e => e.IdToken);

            entity.ToTable("token_recuperacion");

            entity.HasIndex(e => e.IdUsuario, "IX_token_usuario");

            entity.Property(e => e.IdToken).HasColumnName("id_token");
            entity.Property(e => e.FechaCreacion)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())", "DF_token_creacion")
                .HasColumnName("fecha_creacion");
            entity.Property(e => e.FechaExpiracion)
                .HasPrecision(0)
                .HasColumnName("fecha_expiracion");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("token_hash");
            entity.Property(e => e.Usado).HasColumnName("usado");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.TokenRecuperacions)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_token_usuario");
        });

        modelBuilder.Entity<Tutor>(entity =>
        {
            entity.HasKey(e => e.IdTutor);

            entity.ToTable("tutor", tb => tb.HasTrigger("trg_tutor_estado"));

            entity.HasIndex(e => e.IdPersona, "UQ_tutor_persona").IsUnique();

            entity.Property(e => e.IdTutor).HasColumnName("id_tutor");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_tutor_estado")
                .HasColumnName("estado");
            entity.Property(e => e.IdPersona).HasColumnName("id_persona");

            entity.HasOne(d => d.IdPersonaNavigation).WithOne(p => p.Tutor)
                .HasForeignKey<Tutor>(d => d.IdPersona)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tutor_persona");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario);

            entity.ToTable("usuario");

            entity.HasIndex(e => e.Correo, "UQ_usuario_correo").IsUnique();

            entity.HasIndex(e => e.NombreUsuario, "UQ_usuario_nombre").IsUnique();

            entity.HasIndex(e => e.IdPersona, "UQ_usuario_persona").IsUnique();

            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.BloqueadoHasta)
                .HasPrecision(0)
                .HasColumnName("bloqueado_hasta");
            entity.Property(e => e.ContrasenaHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("contrasena_hash");
            entity.Property(e => e.Correo)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("correo");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true, "DF_usuario_estado")
                .HasColumnName("estado");
            entity.Property(e => e.IdPersona).HasColumnName("id_persona");
            entity.Property(e => e.IntentosFallidos).HasColumnName("intentos_fallidos");
            entity.Property(e => e.NombreUsuario)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nombre_usuario");
            entity.Property(e => e.Rol)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("rol");

            entity.HasOne(d => d.IdPersonaNavigation).WithOne(p => p.Usuario)
                .HasForeignKey<Usuario>(d => d.IdPersona)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_usuario_persona");
        });

        modelBuilder.Entity<VwDesercion>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_desercion");

            entity.Property(e => e.EstudiantesDebianContinuar).HasColumnName("estudiantes_debian_continuar");
            entity.Property(e => e.EstudiantesSinMatricula).HasColumnName("estudiantes_sin_matricula");
            entity.Property(e => e.IdNivel).HasColumnName("id_nivel");
            entity.Property(e => e.IdPeriodo).HasColumnName("id_periodo");
            entity.Property(e => e.TasaDesercionPct)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("tasa_desercion_pct");
        });

        modelBuilder.Entity<VwEstadoCuentum>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_estado_cuenta");

            entity.Property(e => e.CodigoConcepto)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("codigo_concepto");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("descripcion");
            entity.Property(e => e.DescuentoUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("descuento_usd");
            entity.Property(e => e.EstadoMostrar)
                .HasMaxLength(19)
                .IsUnicode(false)
                .HasColumnName("estado_mostrar");
            entity.Property(e => e.EstadoPago)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("estado_pago");
            entity.Property(e => e.IdEstudiante).HasColumnName("id_estudiante");
            entity.Property(e => e.IdMatricula).HasColumnName("id_matricula");
            entity.Property(e => e.IdObligacion).HasColumnName("id_obligacion");
            entity.Property(e => e.IdPeriodo).HasColumnName("id_periodo");
            entity.Property(e => e.MesCorrespondiente).HasColumnName("mes_correspondiente");
            entity.Property(e => e.NombreCompleto)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("nombre_completo");
            entity.Property(e => e.NumeroCarne)
                .HasMaxLength(7)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("numero_carne");
            entity.Property(e => e.PrecioFinalUsd)
                .HasColumnType("decimal(13, 2)")
                .HasColumnName("precio_final_usd");
            entity.Property(e => e.PrecioOriginalUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("precio_original_usd");
            entity.Property(e => e.SaldoPendienteUsd)
                .HasColumnType("decimal(14, 2)")
                .HasColumnName("saldo_pendiente_usd");
            entity.Property(e => e.TotalAbonadoUsd)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_abonado_usd");
        });

        modelBuilder.Entity<VwEstudianteActivo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_estudiante_activo");

            entity.Property(e => e.IdEstudiante).HasColumnName("id_estudiante");
            entity.Property(e => e.IdGrupo).HasColumnName("id_grupo");
            entity.Property(e => e.IdMatricula).HasColumnName("id_matricula");
            entity.Property(e => e.IdNivel).HasColumnName("id_nivel");
            entity.Property(e => e.IdPeriodo).HasColumnName("id_periodo");
            entity.Property(e => e.NombreCompleto)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("nombre_completo");
            entity.Property(e => e.NumeroCarne)
                .HasMaxLength(7)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("numero_carne");
        });

        modelBuilder.Entity<VwIngresosMensuale>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ingresos_mensuales");

            entity.Property(e => e.Anio).HasColumnName("anio");
            entity.Property(e => e.CantidadPagos).HasColumnName("cantidad_pagos");
            entity.Property(e => e.Mes).HasColumnName("mes");
            entity.Property(e => e.TotalIngresosNio)
                .HasColumnType("decimal(38, 2)")
                .HasColumnName("total_ingresos_nio");
            entity.Property(e => e.TotalIngresosUsd)
                .HasColumnType("decimal(38, 2)")
                .HasColumnName("total_ingresos_usd");
        });

        modelBuilder.Entity<VwOfertaAcademica>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_oferta_academica");

            entity.Property(e => e.Aula)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("aula");
            entity.Property(e => e.Capacidad).HasColumnName("capacidad");
            entity.Property(e => e.CuposDisponibles).HasColumnName("cupos_disponibles");
            entity.Property(e => e.Curso)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("curso");
            entity.Property(e => e.Docente)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("docente");
            entity.Property(e => e.FechaFinCurso).HasColumnName("fecha_fin_curso");
            entity.Property(e => e.FechaInicioCurso).HasColumnName("fecha_inicio_curso");
            entity.Property(e => e.Horario)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("horario");
            entity.Property(e => e.IdCurso).HasColumnName("id_curso");
            entity.Property(e => e.IdGrupo).HasColumnName("id_grupo");
            entity.Property(e => e.IdPeriodo).HasColumnName("id_periodo");
            entity.Property(e => e.Identificacion)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("identificacion");
            entity.Property(e => e.Inscritos).HasColumnName("inscritos");
            entity.Property(e => e.Modalidad)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("modalidad");
            entity.Property(e => e.Nivel)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("nivel");
        });

        modelBuilder.Entity<VwTotalesFinanciero>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_totales_financieros");

            entity.Property(e => e.IdEstudiante).HasColumnName("id_estudiante");
            entity.Property(e => e.IdMatricula).HasColumnName("id_matricula");
            entity.Property(e => e.IdPeriodo).HasColumnName("id_periodo");
            entity.Property(e => e.SaldoPendienteUsd)
                .HasColumnType("decimal(38, 2)")
                .HasColumnName("saldo_pendiente_usd");
            entity.Property(e => e.TotalAPagarUsd)
                .HasColumnType("decimal(38, 2)")
                .HasColumnName("total_a_pagar_usd");
            entity.Property(e => e.TotalDescuentosUsd)
                .HasColumnType("decimal(38, 2)")
                .HasColumnName("total_descuentos_usd");
            entity.Property(e => e.TotalOriginalUsd)
                .HasColumnType("decimal(38, 2)")
                .HasColumnName("total_original_usd");
            entity.Property(e => e.TotalRecibidoUsd)
                .HasColumnType("decimal(38, 2)")
                .HasColumnName("total_recibido_usd");
        });
        modelBuilder.HasSequence<int>("seq_comprobante");

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
