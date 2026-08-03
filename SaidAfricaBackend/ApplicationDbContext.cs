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
        public DbSet<Message>             Messages            { get; set; }
        public DbSet<Disponibilite>       Disponibilites      { get; set; }
        public DbSet<BienLike>            BienLikes           { get; set; }
        public DbSet<BienVue>             BienVues            { get; set; }
        public DbSet<Payment>             Payments            { get; set; }

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

            modelBuilder.Entity<Bien>()
                .HasOne(b => b.ValideParAdmin)
                .WithMany()
                .HasForeignKey(b => b.ValideParAdminId)
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

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Expediteur)
                .WithMany()
                .HasForeignKey(m => m.ExpediteurId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Destinataire)
                .WithMany()
                .HasForeignKey(m => m.DestinataireId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Bien)
                .WithMany()
                .HasForeignKey(m => m.BienId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Disponibilite>()
                .HasOne(d => d.Bien)
                .WithMany()
                .HasForeignKey(d => d.BienId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BienLike>()
                .HasIndex(l => new { l.BienId, l.UserId }).IsUnique();

            modelBuilder.Entity<BienLike>()
                .HasOne(l => l.Bien)
                .WithMany(b => b.BienLikes)
                .HasForeignKey(l => l.BienId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BienLike>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BienVue>()
                .HasIndex(v => new { v.BienId, v.UserId }).IsUnique();

            modelBuilder.Entity<BienVue>()
                .HasOne(v => v.Bien)
                .WithMany()
                .HasForeignKey(v => v.BienId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BienVue>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Bien)
                .WithMany()
                .HasForeignKey(p => p.BienId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Reservation)
                .WithMany()
                .HasForeignKey(p => p.ReservationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Montant)
                .HasPrecision(12, 2);
        }
    }

    // ─── UTILISATEUR ──────────────────────────────────────────────────────────
    public class User
    {
        public int    Id       { get; set; }
        public string Nom      { get; set; } = string.Empty;
        public string Prenom   { get; set; } = string.Empty;
        public string  Email     { get; set; } = string.Empty;
        public string  Password  { get; set; } = string.Empty;
        public string  Telephone { get; set; } = string.Empty;
        public string? PhotoUrl  { get; set; }

        /// <summary>Rôles possibles : "Client", "Proprietaire"; "UserIndep", "AdminRegion", "AdminPays", "DirecteurProjet"</summary>
        public string Role     { get; set; } = "Client";

        /// <summary>Validation par un Admin région, requise pour les rôles professionnels (Proprietaire/UserIndep).</summary>
        public bool EstValide { get; set; } = true;

        /// <summary>Compte suspendu manuellement par un admin (empêche la connexion).</summary>
        public bool EstBloque { get; set; } = false;

        /// <summary>Région administrative de rattachement (utile pour AdminRegion et le scoping futur des Biens).</summary>
        public string? Region { get; set; }

        // ── Réinitialisation de mot de passe ────────────────────────────────
        public string?   ResetToken       { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // ── KYC (vérification d'identité) ───────────────────────────────────
        /// <summary>NonSoumis | EnAttente | Approuve | Rejete</summary>
        public string   KycStatut       { get; set; } = "NonSoumis";
        /// <summary>CNI | Passeport</summary>
        public string?  KycDocumentType { get; set; }
        /// <summary>Nom de fichier stocké dans Uploads/Kyc/</summary>
        public string?  KycDocumentPath { get; set; }
        public DateTime? KycSoumisAt   { get; set; }
        /// <summary>Motif de refus renseigné par l'admin</summary>
        public string?  KycRemarque    { get; set; }

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

        /// <summary>Coordonnées GPS — définies par le propriétaire via le sélecteur de carte.</summary>
        public double? Latitude  { get; set; }
        public double? Longitude { get; set; }

        /// <summary>Propriétaire (User avec rôle Proprietaire/UserIndep/Admin). Nullable : biens historiques sans propriétaire assigné.</summary>
        public int? ProprietaireId { get; set; }

        // ── Modération par l'AdminRegion ─────────────────────────────────────
        /// <summary>En attente | Validée | Rejetée</summary>
        public string  StatutPublication { get; set; } = "En attente";
        public string? MotifsRejet       { get; set; }
        /// <summary>Titre foncier (PDF/JPG/PNG) stocké dans Uploads/Biens/</summary>
        public string? TitreFoncierPath  { get; set; }
        public int?      ValideParAdminId { get; set; }
        public DateTime? ValideLe         { get; set; }

        // Navigation
        public User?                    Proprietaire   { get; set; }
        public User?                    ValideParAdmin  { get; set; }
        public ICollection<Reservation> Reservations   { get; set; } = new List<Reservation>();
        public ICollection<Favori>      Favoris        { get; set; } = new List<Favori>();
        public ICollection<BienLike>    BienLikes      { get; set; } = new List<BienLike>();
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

        /// <summary>Type de pièce d'identité : "CNI" ou "Passeport"</summary>
        public string?  DocumentType       { get; set; }
        /// <summary>Photo de la pièce d'identité, stockée dans Uploads/Demandes/</summary>
        public string?  DocumentPath       { get; set; }
        /// <summary>Selfie de vérification (photo live du visage), stocké dans Uploads/Demandes/</summary>
        public string?  SelfieDocumentPath { get; set; }

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

    // ─── MESSAGE (MESSAGERIE INTERNE) ──────────────────────────────────────────
    public class Message
    {
        public int    Id              { get; set; }
        public int    ExpediteurId    { get; set; }
        public int    DestinataireId  { get; set; }

        /// <summary>Bien concerné par la conversation (optionnel)</summary>
        public int?   BienId          { get; set; }

        public string Contenu         { get; set; } = string.Empty;
        public bool   EstLu           { get; set; } = false;
        public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? Expediteur   { get; set; }
        public User? Destinataire { get; set; }
        public Bien? Bien         { get; set; }
    }

    // ─── DISPONIBILITE (CALENDRIER PAR BIEN) ──────────────────────────────────
    public class Disponibilite
    {
        public int      Id      { get; set; }
        public int      BienId  { get; set; }

        /// <summary>Date bloquée (partie date uniquement, heure ignorée)</summary>
        public DateTime Date    { get; set; }

        /// <summary>Motif facultatif : "Maintenance", "Déjà loué", etc.</summary>
        public string?  Motif   { get; set; }

        // Navigation
        public Bien? Bien { get; set; }
    }

    // ─── LIKE SUR UN BIEN ─────────────────────────────────────────────────────
    public class BienLike
    {
        public int      Id        { get; set; }
        public int      BienId    { get; set; }
        public int      UserId    { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Bien? Bien { get; set; }
        public User? User { get; set; }
    }

    // ─── VUE UNIQUE SUR UN BIEN (une seule vue comptée par utilisateur) ───────
    public class BienVue
    {
        public int      Id       { get; set; }
        public int      BienId   { get; set; }
        public int      UserId   { get; set; }
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Bien? Bien { get; set; }
        public User? User { get; set; }
    }

    // ─── PAIEMENT MTN MOBILE MONEY ────────────────────────────────────────────
    public class Payment
    {
        public int       Id            { get; set; }

        /// <summary>UUID transmis à MTN MoMo comme identifiant unique de transaction.</summary>
        public string    ReferenceId   { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Reservation | Abonnement | Commission</summary>
        public string    TypePaiement  { get; set; } = string.Empty;

        public decimal   Montant       { get; set; }
        public string    Devise        { get; set; } = "XAF";

        /// <summary>EnAttente | Reussi | Echoue | Expire</summary>
        public string    Statut        { get; set; } = "EnAttente";

        public string    NumeroPayeur  { get; set; } = string.Empty;
        public int       UserId        { get; set; }
        public int?      BienId        { get; set; }
        public int?      ReservationId { get; set; }
        public string?   MotifEchec    { get; set; }
        public DateTime  CreatedAt     { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt     { get; set; }

        // Navigation
        public User?        User        { get; set; }
        public Bien?        Bien        { get; set; }
        public Reservation? Reservation { get; set; }
    }
}