using Microsoft.EntityFrameworkCore;

namespace SaidAfricaBackend
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User>               Users               { get; set; }
        public DbSet<Bien>               Biens               { get; set; }
        public DbSet<Reservation>        Reservations        { get; set; }
        public DbSet<Favori>             Favoris             { get; set; }
        public DbSet<DemandeProprietaire> DemandesProprietaire { get; set; }
        public DbSet<Commentaire>        Commentaires        { get; set; }
        public DbSet<Recommandation>      Recommandations     { get; set; }
        public DbSet<Notification>        Notifications       { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .Property(u => u.EstValide)
                .HasDefaultValue(true);

            modelBuilder.Entity<Bien>()
                .Property(b => b.Vues)
                .HasDefaultValue(0);

            modelBuilder.Entity<Bien>()
                .HasOne(b => b.Proprietaire)
                .WithMany(u => u.BiensPossedes)
                .HasForeignKey(b => b.ProprietaireId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DemandeProprietaire>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DemandeProprietaire>()
                .HasOne(d => d.TraiteParAdmin)
                .WithMany()
                .HasForeignKey(d => d.TraiteParAdminId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Commentaire>()
                .HasOne(c => c.Bien)
                .WithMany()
                .HasForeignKey(c => c.BienId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Commentaire>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Recommandation>()
                .HasOne(r => r.Bien)
                .WithMany()
                .HasForeignKey(r => r.BienId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Recommandation>()
                .HasOne(r => r.Expediteur)
                .WithMany()
                .HasForeignKey(r => r.ExpediteurId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Recommandation>()
                .HasOne(r => r.Destinataire)
                .WithMany()
                .HasForeignKey(r => r.DestinataireId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    // ─── UTILISATEUR ──────────────────────────────────────────────────────────
    public class User
    {
        public int    Id       { get; set; }
        public string Nom      { get; set; } = string.Empty;
        public string Prenom   { get; set; } = string.Empty;
        public string Email    { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>Rôles possibles : "Client", "Proprietaire"; "UserIndep", "AdminRegion", "AdminPays", "DirecteurProjet"</summary>
        public string Role     { get; set; } = "Client";

        /// <summary>Validation par un Admin région, requise pour les rôles professionnels (Proprietaire/UserIndep).</summary>
        public bool EstValide { get; set; } = true;

        /// <summary>Région administrative de rattachement (utile pour AdminRegion et le scoping futur des Biens).</summary>
        public string? Region { get; set; }

        // Navigation
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Favori>      Favoris      { get; set; } = new List<Favori>();
        public ICollection<Bien>        BiensPossedes { get; set; } = new List<Bien>();
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

        /// <summary>Nombre de consultations de la fiche détaillée (incrémenté à chaque GET /api/biens/{id}).</summary>
        public int Vues { get; set; } = 0;

        /// <summary>Propriétaire (User avec rôle Proprietaire/UserIndep/Admin). Nullable : biens historiques sans propriétaire assigné.</summary>
        public int? ProprietaireId { get; set; }

        // Navigation
        public User?                    Proprietaire { get; set; }
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

        /// <summary>Statut : "En attente", "Confirmée", "Refusée", "Annulée"</summary>
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

    // ─── DEMANDE DE PASSAGE AU STATUT PROPRIÉTAIRE ───────────────────────────
    public class DemandeProprietaire
    {
        public int    Id     { get; set; }
        public int    UserId { get; set; }
        public string Region { get; set; } = string.Empty;
        public string? Message { get; set; }

        /// <summary>Statut : "En attente", "Validée", "Refusée"</summary>
        public string Statut { get; set; } = "En attente";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int?     TraiteParAdminId { get; set; }
        public DateTime? TraiteLe        { get; set; }

        // Navigation
        public User? User           { get; set; }
        public User? TraiteParAdmin { get; set; }
    }

    // ─── COMMENTAIRE / AVIS SUR UN BIEN ───────────────────────────────────────
    public class Commentaire
    {
        public int    Id     { get; set; }
        public int    BienId { get; set; }
        public int    UserId { get; set; }

        /// <summary>Note de 1 à 5</summary>
        public int    Note  { get; set; }
        public string Texte { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Bien? Bien { get; set; }
        public User? User { get; set; }
    }

    // ─── RECOMMANDATION D'UN BIEN À UN AUTRE UTILISATEUR ──────────────────────
    public class Recommandation
    {
        public int     Id              { get; set; }
        public int     BienId          { get; set; }
        public int     ExpediteurId    { get; set; }
        public int     DestinataireId  { get; set; }
        public string? Message         { get; set; }
        public bool    EstLue          { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Bien? Bien         { get; set; }
        public User? Expediteur   { get; set; }
        public User? Destinataire { get; set; }
    }

    // ─── NOTIFICATION ──────────────────────────────────────────────────────────
    public class Notification
    {
        public int     Id            { get; set; }
        public int     UserId        { get; set; }
        public string  Type          { get; set; } = string.Empty;
        public string  Titre         { get; set; } = string.Empty;
        public string  Message       { get; set; } = string.Empty;

        /// <summary>Id de section sidebar à activer au clic (ex: "reservations")</summary>
        public string? CibleSection  { get; set; }

        public bool     EstLue       { get; set; } = false;
        public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}