using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using SaidAfricaBackend;
using SaidAfricaBackend.Services;
using System.Text;

// ─── Chargement du fichier .env (développement local uniquement) ──────────────
var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('#') || !trimmed.Contains('=')) continue;
        var idx = trimmed.IndexOf('=');
        var key = trimmed[..idx].Trim();
        var val = trimmed[(idx + 1)..].Trim();
        Environment.SetEnvironmentVariable(key, val);
    }
}

var builder = WebApplication.CreateBuilder(args);

// Limite Kestrel globale : 50 Mo (évite fermeture connexion pendant upload)
builder.WebHost.ConfigureKestrel(k =>
    k.Limits.MaxRequestBodySize = 50L * 1024 * 1024);

// 1. Connexion MySQL — supporte Railway (MYSQLHOST) et local (localhost)
string connectionString;
var mysqlHost = Environment.GetEnvironmentVariable("MYSQLHOST");
if (!string.IsNullOrEmpty(mysqlHost))
{
    var mysqlPort     = Environment.GetEnvironmentVariable("MYSQLPORT")     ?? "3306";
    var mysqlUser     = Environment.GetEnvironmentVariable("MYSQLUSER")     ?? "root";
    var mysqlPassword = Environment.GetEnvironmentVariable("MYSQLPASSWORD") ?? "";
    var mysqlDatabase = Environment.GetEnvironmentVariable("MYSQLDATABASE") ?? "said_africa";
    connectionString  = $"server={mysqlHost};port={mysqlPort};database={mysqlDatabase};user={mysqlUser};password={mysqlPassword}";
}
else
{
    connectionString = "server=localhost;port=3306;database=said_africa;user=root;password=";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

// 2. CORS — développement local + production (levetimmo.com)
builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontend", policy => {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000",
                "http://localhost:3001",
                "http://localhost:4173",
                "https://levetimmo.com",
                "https://www.levetimmo.com"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Authentification JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException(
        "JWT Key manquant. Définissez la variable d'environnement Jwt__Key dans Railway.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICamPayService, CamPayService>();
builder.Services.AddSingleton<ISmsService, BrevoSmsService>();

builder.Services.AddControllers();

var app = builder.Build();

// --- CONFIGURATION DU PIPELINE ---
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode  = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync("{\"success\":false,\"message\":\"Erreur interne du serveur. Réessayez.\"}");
}));

app.UseRouting();
app.UseCors("AllowFrontend");

// Frontend statique depuis wwwroot/ (généré par Vite dans le Dockerfile)
app.UseDefaultFiles(); // / → index.html
app.UseStaticFiles();  // wwwroot/ servi automatiquement

// Servir les images uploadées depuis Uploads/
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "Uploads");
Directory.CreateDirectory(uploadsPath);
var provider = new FileExtensionContentTypeProvider();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider        = new PhysicalFileProvider(uploadsPath),
    RequestPath         = "/uploads",
    ContentTypeProvider = provider,
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ─── Migrations + Seed ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Ajout idempotent des colonnes Biens via ADO.NET brut.
    // Bypass les migrations pour garantir que les colonnes existent même si la migration
    // échoue (IF NOT EXISTS non supporté sur MySQL pur, CREATE PROCEDURE = commit implicite).
    try
    {
        var colDefs = new (string col, string def)[]
        {
            ("CertificatPropriete",   "LONGTEXT CHARACTER SET utf8mb4 NULL"),
            ("StatutCivil",            "LONGTEXT CHARACTER SET utf8mb4 NULL"),
            ("RegimeMatrimonial",      "LONGTEXT CHARACTER SET utf8mb4 NULL"),
            ("ADesEnfants",            "TINYINT(1) NULL"),
            ("DossierTechnique",       "LONGTEXT CHARACTER SET utf8mb4 NULL"),
            ("PermisBatir",            "LONGTEXT CHARACTER SET utf8mb4 NULL"),
            ("PlanBatiment",           "LONGTEXT CHARACTER SET utf8mb4 NULL"),
            ("DossierCalculTechnique", "LONGTEXT CHARACTER SET utf8mb4 NULL"),
            ("DocumentsVerifies",      "LONGTEXT CHARACTER SET utf8mb4 NULL"),
            ("FraisVisite",            "INT NULL"),
        };
        foreach (var (col, def) in colDefs)
        {
            try { db.Database.ExecuteSqlRaw($"ALTER TABLE `Biens` ADD COLUMN `{col}` {def}"); }
            catch { } // 1060 = colonne existante → ok
        }
    }
    catch { } // table Biens pas encore créée → Migrate() la crée juste après

    try
    {
        db.Database.Migrate();
        DataSeeder.Seed(db);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erreur lors de la migration MySQL au démarrage.");
    }
}

// ─── Écouter sur le port Railway (PORT) ou 8080 par défaut ───────────────────
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");
