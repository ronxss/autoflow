using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AutoFlow.Api;
using AutoFlow.Api.Hubs;
using AutoFlow.Application;
using AutoFlow.Application.Abstractions;
using AutoFlow.Infrastructure;
using AutoFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
builder.Services.AddScoped<INotificadorTempoReal, SignalRNotificador>();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorExcecoes>();

// ---------- Autenticação (JWT emitido pela própria API) ----------
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Chave.Length < 32)
    throw new InvalidOperationException("Configure Jwt:Chave com pelo menos 32 caracteres.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emissor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Chave)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        o.Events = new JwtBearerEvents
        {
            // WebSockets não enviam o cabeçalho Authorization: o SignalR manda o token na query string.
            OnMessageReceived = contexto =>
            {
                var token = contexto.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && contexto.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    contexto.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ---------- CORS (painel administrativo) e limite de tentativas de login ----------
var origens = config.GetSection("Cors:Origens").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod()));

var tentativasPorMinuto = config.GetValue("Seguranca:TentativasDeLoginPorMinuto", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = tentativasPorMinuto,
            Window = TimeSpan.FromMinutes(1)
        }));
});

var app = builder.Build();

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (config.GetValue<bool>("Banco:InicializarAoSubir"))
    await InicializadorBanco.InicializarAsync(app.Services);

app.Run();
