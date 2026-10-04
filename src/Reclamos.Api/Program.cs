using System.Text.Json.Serialization;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Reclamos.Api.Infra;
using Reclamos.Api.Soap;
using Reclamos.Datos;
using Reclamos.Dominio.Servicios;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Datos: un solo NpgsqlDataSource (pool de conexiones) para toda la aplicación
var cadena = config.GetConnectionString("Reclamos") ?? throw new InvalidOperationException("Falta ConnectionStrings:Reclamos.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(cadena));
builder.Services.AddSingleton<IReclamoRepositorio, ReclamoRepositorio>();
builder.Services.AddSingleton<IClienteRepositorio, ClienteRepositorio>();
builder.Services.AddSingleton<IUsuarioRepositorio, UsuarioRepositorio>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ReclamoServicio>();
builder.Services.AddSingleton<Tokens>();

// Seguridad: JWT para la API REST, roles Operador y Supervisor
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = Tokens.Emisor,
        ValidAudience = Tokens.Emisor,
        IssuerSigningKey = Tokens.Clave(config),
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
    };
});
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejoDeErrores>();
builder.Services.AddOpenApi();

// SOAP con CoreWCF
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<ReclamosSoap>();

var app = builder.Build();

await Semilla.PrepararAsync(app.Services, config);

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseMiddleware<ClaveSoap>();
app.UseServiceModel(soap =>
{
    soap.AddService<ReclamosSoap>(o => o.DebugBehavior.IncludeExceptionDetailInFaults = false);
    soap.AddServiceEndpoint<ReclamosSoap, IReclamosSoap>(new BasicHttpBinding(), "/soap/reclamos");
});
app.Services.GetRequiredService<ServiceMetadataBehavior>().HttpGetEnabled = true;

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// La interfaz Angular compilada vive en wwwroot: cualquier ruta que no sea de la API devuelve index.html
app.MapFallbackToFile("index.html");

app.Run();

// Para las pruebas de integración (WebApplicationFactory)
public partial class Program;
