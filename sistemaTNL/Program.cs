using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Middleware;
using sistemaTNL.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IPersonaService, PersonaService>();
// Tarea automática que finaliza los períodos vencidos (RN012)
builder.Services.AddHostedService<FinalizarPeriodosService>();
// Servicio de envío de correos (SMTP)
builder.Services.AddScoped<ICorreoService, CorreoService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.HttpOnly = true;
        // En desarrollo con el perfil http la cookie "Secure" no se enviaría
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    });

builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());


// CORS: permite que el frontend (que corre en otro origen) llame a la API
// enviando la cookie de sesión. Solo se aceptan los orígenes de la lista.
const string PoliticaCors = "Frontend";
builder.Services.AddCors(o => o.AddPolicy(PoliticaCors, p =>
{
    // En producción la lista sale de la configuración ("Cors:Origenes" en appsettings o variables de entorno)
    var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>();

    // En desarrollo, si no se configuró nada, se aceptan los puertos habituales de los frontends
    if (origenes is null || origenes.Length == 0)
        origenes = builder.Environment.IsDevelopment()
            ? new[] { "http://localhost:5173", "http://localhost:3000", "http://localhost:4200" }
            : Array.Empty<string>();

    p.WithOrigins(origenes)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials();   // necesario para que el navegador envíe la cookie de sesión
}));

var app = builder.Build();

app.UseMiddleware<ManejadorErroresMiddleware>();
app.UseHttpsRedirection();
app.UseCors(PoliticaCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();



app.Run();