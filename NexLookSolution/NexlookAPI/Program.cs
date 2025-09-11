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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
        };
    });
builder.Services.AddHttpClient();
builder.Services.AddScoped<IUploadImagemService, UploadImagemService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAIService, IAservice>();
builder.Services.AddAuthorization();

// Adicione esta configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder =>
        {
            builder
                .WithOrigins("http://localhost:3000","http://localhost:5173", "https://nexlook-app.vercel.app") // URL do seu frontend
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
    try
    {
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Attempting to apply database migrations");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");
        
        // Log connection string (masked)
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (connectionString != null)
        {
            var maskedConnectionString = connectionString.Contains(";") 
                ? string.Join(";", connectionString.Split(';').Select(part => 
                    part.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) 
                        ? "Password=*****" 
                        : part))
                : "Connection string is in unexpected format";
            logger.LogInformation("Using connection string: {ConnectionString}", maskedConnectionString);
        }
        else
        {
            logger.LogWarning("Connection string is null!");
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while applying migrations or initializing the database");
        throw; // Rethrow to ensure the application doesn't start with an invalid database state
    }
}

//app.UseCors("AllowSpecificOrigin");
app.UseSwagger();
app.UseSwaggerUI();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
