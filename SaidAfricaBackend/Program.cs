using Microsoft.EntityFrameworkCore;
using SaidAfricaBackend;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration de la connexion MySQL
var connectionString = "server=localhost;port=3306;database=said_africa;user=root;password=";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
    
// 2. Définition de la politique CORS (ports Vite 5173 ET 3000)
builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontend", policy => {
        policy.WithOrigins(
                "http://localhost:5173",  // Vite (défaut)
                "http://localhost:3000",  // Vite (alternatif)
                "http://localhost:4173"   // Vite preview
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// --- CONFIGURATION DU PIPELINE (L'ordre est vital) ---

// On NE met PAS app.UseHttpsRedirection(); ici pour éviter l'erreur de certificat

app.UseRouting();

app.UseCors("AllowFrontend"); 

app.UseAuthorization();

app.MapControllers();

// ─── SEED : pré-remplir la base au démarrage si vide ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated(); // Crée les tables si elles n'existent pas
    DataSeeder.Seed(db);
}

app.Run();