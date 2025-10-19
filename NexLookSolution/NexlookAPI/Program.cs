using Application.Interfaces;
using Application.Services;
using Application.Services.IAService;
using Infra.dbContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;


// Desativa o mapeamento automático de claims (ex: sub → nameidentifier)
AppContext.SetSwitch("Microsoft.AspNetCore.Authentication.JwtBearer.SuppressMapInboundClaims", true);

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddAzureWebAppDiagnostics(); // Add Azure logging

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
     sqlServerOptionsAction: sqlOptions =>
     {
         sqlOptions.EnableRetryOnFailure(
             maxRetryCount: 5,
             maxRetryDelay: TimeSpan.FromSeconds(30),
             errorNumbersToAdd: null);
     }));

var jwtKey = builder.Configuration["jwtSettings:SecretKey"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
        };
    });
builder.Services.AddHttpClient();
builder.Services.AddScoped<IUploadImagemService, UploadImagemService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAIService, IAservice>();
builder.Services.AddScoped<IStorageService, CloudinaryStorageService>();
builder.Services.AddAuthorization();

// Adicione esta configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder
                .WithOrigins("http://localhost:3000",
                "http://localhost:5173", 
                "https://nexlook-app.vercel.app", 
                "https://nexlookapi-bdaqehg2cpehaega.brazilsouth-01.azurewebsites.net",
                "https://nexkontrol-front-fszs.vercel.app"
                ) // URL do seu frontend
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "NexLook API", Version = "v1" });

    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insira o token JWT no campo abaixo.\nExemplo: Bearer {seu_token}",
        Reference = new OpenApiReference
        {
            Id = "Bearer",
            Type = ReferenceType.SecurityScheme
        }
    };

    options.AddSecurityDefinition("Bearer", securityScheme);

    var securityRequirement = new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            securityScheme,
            Array.Empty<string>()
        }
    };


    options.AddSecurityRequirement(securityRequirement);
});
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        return RateLimitPartition.GetFixedWindowLimiter("GlobalLimiter", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100, // Número máximo de requisições permitidas
            Window = TimeSpan.FromMinutes(1), // Janela de tempo
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0 // Sem fila
        });
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429; // Too Many Requests
        await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
    };
});
var app = builder.Build();

// Global exception handler middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception occurred. Request path: {Path}", context.Request.Path);
        throw;
    }
});

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AppDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Verificando pending migrations...");

        // Verifica se existem migrations pendentes
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        var pendingList = pendingMigrations.ToList();

        if (pendingList.Any())
        {
            logger.LogInformation("Encontradas {Count} migrations pendentes: {Migrations}",
                pendingList.Count,
                string.Join(", ", pendingList));

            // Timeout para evitar travar startup
            var migrateTask = dbContext.Database.MigrateAsync();
            if (await Task.WhenAny(migrateTask, Task.Delay(TimeSpan.FromSeconds(30))) == migrateTask)
            {
                logger.LogInformation("Migrations aplicadas com sucesso");
            }
            else
            {
                logger.LogWarning("Migration timeout: processo será tentado novamente no próximo restart");
            }
        }
        else
        {
            logger.LogInformation("Banco de dados está atualizado, nenhuma migration pendente");
        }

        // Verifica conexão com o banco
        var canConnect = await dbContext.Database.CanConnectAsync();
        if (!canConnect)
        {
            logger.LogError("Não foi possível estabelecer conexão com o banco de dados");
        }
        else
        {
            logger.LogInformation("Conexão com o banco de dados estabelecida com sucesso");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro durante verificação/aplicação de migrations");
    }
}


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();

// Add request logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Request {Method} {Path} started at {Time}", 
        context.Request.Method, 
        context.Request.Path, 
        DateTime.UtcNow);

    await next();

    logger.LogInformation("Request {Method} {Path} completed with status {StatusCode} at {Time}",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        DateTime.UtcNow);
});

app.UseRouting();
app.UseCors("AllowSpecificOrigin");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
