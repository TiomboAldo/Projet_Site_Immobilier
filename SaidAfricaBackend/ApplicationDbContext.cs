using Microsoft.EntityFrameworkCore;

namespace SaidAfricaBackend
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User>        Users        { get; set; }
        public DbSet<Bien>        Biens        { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Favori>      Favoris      { get; set; }
    }

    // ─── UTILISATEUR ──────────────────────────────────────────────────────────
    public class User
    {
        public int    Id       { get; set; }
        public string Nom      { get; set; } = string.Empty;
        public string Prenom   { get; set; } = string.Empty;
        public string Email    { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>Rôles possibles : "Client", "Agent", "Admin"</summary>
        public string Role     { get; set; } = "Client";

        // Navigation
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Favori>      Favoris      { get; set; } = new List<Favori>();
    }

    // ─── BIEN IMMOBILIER ──────────────────────────────────────────────────────
    public class Bien
    {
        public int    Id           { get; set; }
        public string Titre        { get; set; } = string.Empty;

        /// <summary>Type : "villa", "appartement", "terrain"</summary>
        public string Type         { get; set; } = string.Empty;

        /// <summary>Statut : "vente", "location"</summary>
        public string Statut       { get; set; } = string.Empty;

        public string Prix         { get; set; } = string.Empty;
        public int    Chambres     { get; set; }
        public int    SallesDeBain { get; set; }
        public int    Surface      { get; set; }   // en m²
        public string Localisation { get; set; } = string.Empty;
        public string Description  { get; set; } = string.Empty;

        /// <summary>URL de l'image principale</summary>
        public string ImageUrl     { get; set; } = string.Empty;

        /// <summary>URLs des images galerie séparées par "|"</summary>
        public string GalerieUrls  { get; set; } = string.Empty;

        /// <summary>Équipements séparés par "|" ex: "Piscine|Jardin|Climatisation"</summary>
        public string Equipements  { get; set; } = string.Empty;

        /// <summary>Standing : "Elite", "Penthouse", "Business"</summary>
        public string Standing     { get; set; } = string.Empty;

        public bool     EstDisponible { get; set; } = true;
        public DateTime DateAjout    { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Favori>      Favoris      { get; set; } = new List<Favori>();
    }

    // ─── RÉSERVATION / DEMANDE DE VISITE ─────────────────────────────────────
    public class Reservation
    {
        public int      Id         { get; set; }
        public int      UserId     { get; set; }
        public int      BienId     { get; set; }

        public string   Prenom     { get; set; } = string.Empty;
        public string   Nom        { get; set; } = string.Empty;
        public string   Email      { get; set; } = string.Empty;
        public string   Telephone  { get; set; } = string.Empty;
        public string   Lieu       { get; set; } = string.Empty;
        public DateTime DateVisite { get; set; }
        public string   Message    { get; set; } = string.Empty;

        /// <summary>Statut : "En attente", "Confirmée", "Annulée"</summary>
        public string   Statut     { get; set; } = "En attente";

        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public Bien? Bien { get; set; }
    }

    // ─── FAVORI ───────────────────────────────────────────────────────────────
    public class Favori
    {
        public int      Id        { get; set; }
        public int      UserId    { get; set; }
        public int      BienId    { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public Bien? Bien { get; set; }
    }
}