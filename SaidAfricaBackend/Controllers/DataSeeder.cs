namespace SaidAfricaBackend
{
    public static class DataSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            FixBlankRoles(context);
            PurgeDemoBiens(context);
        }

        // ─── PURGE : supprime les biens démo insérés avec des images Unsplash ────
        private static void PurgeDemoBiens(ApplicationDbContext context)
        {
            var demos = context.Biens
                .Where(b => b.ImageUrl.StartsWith("https://images.unsplash.com"))
                .ToList();

            if (demos.Count == 0) return;

            context.Biens.RemoveRange(demos);
            context.SaveChanges();
            Console.WriteLine($"🗑️  {demos.Count} bien(s) demo supprimes.");
        }

        // ─── CORRECTIF : anciens comptes avec un Role vide en base ───────────────
        private static void FixBlankRoles(ApplicationDbContext context)
        {
            var blancs = context.Users.Where(u => string.IsNullOrEmpty(u.Role)).ToList();
            if (blancs.Count == 0) return;

            foreach (var u in blancs) u.Role = "Client";
            context.SaveChanges();

            Console.WriteLine($"🔧 {blancs.Count} compte(s) avec un rôle vide corrigé(s) en \"Client\".");
        }

        // ─── BIENS DÉMO ───────────────────────────────────────────────────────────
        private static void SeedBiens(ApplicationDbContext context)
        {
            // Ne seeder que si la table est vide
            if (context.Biens.Any()) return;

            var biens = new List<Bien>
            {
                // ─── ELITE ────────────────────────────────────────────────────
                new Bien {
                    Titre = "Palais Atlantique", Type = "villa", Statut = "vente",
                    Prix = "450M FCFA", Chambres = 6, SallesDeBain = 4, Surface = 850,
                    Localisation = "Bonapriso, Douala",
                    Description = "Une demeure d'exception face à l'océan, offrant des volumes hors normes et des finitions en marbre d'Italie.",
                    ImageUrl = "https://images.unsplash.com/photo-1512917774080-9991f1c4c750",
                    GalerieUrls = "https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c|https://images.unsplash.com/photo-1502672260266-1c1ef2d93688",
                    Equipements = "Piscine olympique|Spa|Sécurité 24/7|Jardin paysager|Garage double",
                    Standing = "Elite"
                },
                new Bien {
                    Titre = "Villa Horizon", Type = "villa", Statut = "vente",
                    Prix = "120M FCFA", Chambres = 4, SallesDeBain = 3, Surface = 300,
                    Localisation = "Kribi",
                    Description = "Le refuge parfait pour vos weekends, à deux pas des plages de sable fin.",
                    ImageUrl = "https://images.unsplash.com/photo-1613490493576-7fde63acd811",
                    GalerieUrls = "https://images.unsplash.com/photo-1613490493576-7fde63acd811|https://images.unsplash.com/photo-1600585154340-be6161a56a0c|https://images.unsplash.com/photo-1502672260266-1c1ef2d93688|https://images.unsplash.com/photo-1564013799919-ab600027ffc6",
                    Equipements = "Piscine|Terrasse vue mer|Climatisation",
                    Standing = "Elite"
                },
                new Bien {
                    Titre = "Résidence Bastos", Type = "villa", Statut = "vente",
                    Prix = "300M FCFA", Chambres = 5, SallesDeBain = 3, Surface = 500,
                    Localisation = "Bastos, Yaoundé",
                    Description = "Située dans le quartier diplomatique, cette villa offre une sécurité maximale et un confort absolu.",
                    ImageUrl = "https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    GalerieUrls = "https://images.unsplash.com/photo-1600585154340-be6161a56a0c|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1502672260266-1c1ef2d93688",
                    Equipements = "Sécurité 24/7|Jardin|Générateur|Réseau diplomatique",
                    Standing = "Elite"
                },
                new Bien {
                    Titre = "Manoir du Lac", Type = "villa", Statut = "vente",
                    Prix = "550M FCFA", Chambres = 7, SallesDeBain = 5, Surface = 900,
                    Localisation = "Centre, Yaoundé",
                    Description = "Un joyau architectural entouré d'un jardin paysager de plus de 2000m².",
                    ImageUrl = "https://images.unsplash.com/photo-1518780664697-55e3ad937233",
                    GalerieUrls = "https://images.unsplash.com/photo-1518780664697-55e3ad937233|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1600585154340-be6161a56a0c|https://images.unsplash.com/photo-1564013799919-ab600027ffc6",
                    Equipements = "Piscine|Jardin 2000m²|Spa|Salle de sport|Héliport",
                    Standing = "Elite"
                },
                new Bien {
                    Titre = "Villa Emeraude", Type = "villa", Statut = "vente",
                    Prix = "280M FCFA", Chambres = 4, SallesDeBain = 3, Surface = 450,
                    Localisation = "Limbe",
                    Description = "Moderne et lumineuse, cette villa est équipée d'un système domotique complet.",
                    ImageUrl = "https://images.unsplash.com/photo-1583608205776-bfd35f0d9f83",
                    GalerieUrls = "https://images.unsplash.com/photo-1583608205776-bfd35f0d9f83|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Domotique|Piscine|Climatisation centralisée|Vue sur mer",
                    Standing = "Elite"
                },
                new Bien {
                    Titre = "Royal Beach", Type = "villa", Statut = "vente",
                    Prix = "800M FCFA", Chambres = 8, SallesDeBain = 6, Surface = 1500,
                    Localisation = "Kribi",
                    Description = "La plus prestigieuse propriété de la côte, avec piscine olympique privée.",
                    ImageUrl = "https://images.unsplash.com/photo-1564013799919-ab600027ffc6",
                    GalerieUrls = "https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1600585154340-be6161a56a0c|https://images.unsplash.com/photo-1502672260266-1c1ef2d93688",
                    Equipements = "Piscine olympique|Plage privée|Spa|Sécurité 24/7|Tennis",
                    Standing = "Elite"
                },

                // ─── PENTHOUSE ────────────────────────────────────────────────
                new Bien {
                    Titre = "Sky Loft 360", Type = "appartement", Statut = "vente",
                    Prix = "250M FCFA", Chambres = 3, SallesDeBain = 2, Surface = 280,
                    Localisation = "Akwa, Douala",
                    Description = "Vue panoramique sur toute la ville. Design industriel et prestations haut de gamme.",
                    ImageUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688",
                    GalerieUrls = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Vue panoramique|Terrasse|Concierge|Parking privé",
                    Standing = "Penthouse"
                },
                new Bien {
                    Titre = "The Summit", Type = "appartement", Statut = "vente",
                    Prix = "190M FCFA", Chambres = 2, SallesDeBain = 2, Surface = 220,
                    Localisation = "Golf, Douala",
                    Description = "Un havre de paix au dernier étage, idéal pour les amateurs de calme et de luxe.",
                    ImageUrl = "https://images.unsplash.com/photo-1567496898905-af4139885fe2",
                    GalerieUrls = "https://images.unsplash.com/photo-1567496898905-af4139885fe2|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1600585154340-be6161a56a0c|https://images.unsplash.com/photo-1564013799919-ab600027ffc6",
                    Equipements = "Jacuzzi|Terrasse privée|Cave à vin|Concierge 24/7",
                    Standing = "Penthouse"
                },
                new Bien {
                    Titre = "Cloud Nine", Type = "appartement", Statut = "vente",
                    Prix = "320M FCFA", Chambres = 4, SallesDeBain = 3, Surface = 350,
                    Localisation = "Bonanjo, Douala",
                    Description = "Le summum de l'élégance urbaine avec une terrasse de 100m².",
                    ImageUrl = "https://images.unsplash.com/photo-1523217582562-09d0def993a6",
                    GalerieUrls = "https://images.unsplash.com/photo-1523217582562-09d0def993a6|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Terrasse 100m²|Piscine sur toit|Salle de sport|Parking",
                    Standing = "Penthouse"
                },
                new Bien {
                    Titre = "Blue Terrace", Type = "appartement", Statut = "location",
                    Prix = "175M FCFA", Chambres = 2, SallesDeBain = 1, Surface = 190,
                    Localisation = "Marina, Douala",
                    Description = "Appartement d'architecte offrant une vue imprenable sur le port.",
                    ImageUrl = "https://images.unsplash.com/photo-1493809842364-78817add7ffb",
                    GalerieUrls = "https://images.unsplash.com/photo-1493809842364-78817add7ffb|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Vue sur port|Terrasse|Cuisine équipée|Ascenseur",
                    Standing = "Penthouse"
                },
                new Bien {
                    Titre = "Infinity View", Type = "appartement", Statut = "vente",
                    Prix = "400M FCFA", Chambres = 4, SallesDeBain = 3, Surface = 410,
                    Localisation = "Bali, Douala",
                    Description = "Spacieux Penthouse avec jacuzzi privé en extérieur.",
                    ImageUrl = "https://images.unsplash.com/photo-1515263487990-61b07816b324",
                    GalerieUrls = "https://images.unsplash.com/photo-1515263487990-61b07816b324|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Jacuzzi extérieur|Vue panoramique|Sécurité|Parking VIP",
                    Standing = "Penthouse"
                },
                new Bien {
                    Titre = "Zénith Living", Type = "appartement", Statut = "vente",
                    Prix = "210M FCFA", Chambres = 3, SallesDeBain = 2, Surface = 250,
                    Localisation = "Santa Barbara, Douala",
                    Description = "Décoration contemporaine et cuisine équipée de dernière génération.",
                    ImageUrl = "https://images.unsplash.com/photo-1512915922686-57c11f9ad6b3",
                    GalerieUrls = "https://images.unsplash.com/photo-1512915922686-57c11f9ad6b3|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Cuisine équipée haut de gamme|Domotique|Terrasse|Parking",
                    Standing = "Penthouse"
                },

                // ─── BUSINESS ─────────────────────────────────────────────────
                new Bien {
                    Titre = "Studio Akwa Hub", Type = "appartement", Statut = "location",
                    Prix = "85M FCFA", Chambres = 1, SallesDeBain = 1, Surface = 55,
                    Localisation = "Akwa, Douala",
                    Description = "Espace optimisé pour les professionnels actifs au coeur du quartier des affaires.",
                    ImageUrl = "https://images.unsplash.com/photo-1497366216548-37526070297c",
                    GalerieUrls = "https://images.unsplash.com/photo-1497366216548-37526070297c|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Fibre optique|Sécurité|Salle de réunion|Parking",
                    Standing = "Business"
                },
                new Bien {
                    Titre = "Smart Office", Type = "appartement", Statut = "vente",
                    Prix = "110M FCFA", Chambres = 2, SallesDeBain = 1, Surface = 90,
                    Localisation = "Bonanjo, Douala",
                    Description = "Idéal pour un siège social de prestige ou un cabinet de consultant.",
                    ImageUrl = "https://images.unsplash.com/photo-1556761175-b413da4baf72",
                    GalerieUrls = "https://images.unsplash.com/photo-1556761175-b413da4baf72|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Open space|Salle de conférence|Fibre|Climatisation",
                    Standing = "Business"
                },
                new Bien {
                    Titre = "L'Exécutif", Type = "appartement", Statut = "location",
                    Prix = "65M FCFA", Chambres = 1, SallesDeBain = 1, Surface = 45,
                    Localisation = "Deido, Douala",
                    Description = "Petit mais luxueux, parfait pour un pied-à-terre professionnel.",
                    ImageUrl = "https://images.unsplash.com/photo-1536374432-8d2a3ff0ef80",
                    GalerieUrls = "https://images.unsplash.com/photo-1536374432-8d2a3ff0ef80|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Meublé|Fibre|Sécurité|Cuisine équipée",
                    Standing = "Business"
                },
                new Bien {
                    Titre = "Urban Loft", Type = "appartement", Statut = "vente",
                    Prix = "95M FCFA", Chambres = 2, SallesDeBain = 1, Surface = 70,
                    Localisation = "Yaoundé Ville",
                    Description = "Le style new-yorkais adapté au dynamisme de Yaoundé.",
                    ImageUrl = "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab",
                    GalerieUrls = "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Loft ouvert|Fibre|Sécurité|Parking",
                    Standing = "Business"
                },
                new Bien {
                    Titre = "Corporate Home", Type = "villa", Statut = "location",
                    Prix = "130M FCFA", Chambres = 3, SallesDeBain = 2, Surface = 110,
                    Localisation = "Biyem-Assi, Yaoundé",
                    Description = "Maison de fonction tout confort avec bureau intégré.",
                    ImageUrl = "https://images.unsplash.com/photo-1430285561322-7808604715df",
                    GalerieUrls = "https://images.unsplash.com/photo-1430285561322-7808604715df|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Bureau intégré|Fibre|Jardin|Parking double",
                    Standing = "Business"
                },
                new Bien {
                    Titre = "Work & Rest", Type = "appartement", Statut = "location",
                    Prix = "75M FCFA", Chambres = 1, SallesDeBain = 1, Surface = 60,
                    Localisation = "Omnisport, Yaoundé",
                    Description = "L'équilibre parfait entre espace de travail et confort de vie.",
                    ImageUrl = "https://images.unsplash.com/photo-1554435493-93422e8220c8",
                    GalerieUrls = "https://images.unsplash.com/photo-1554435493-93422e8220c8|https://images.unsplash.com/photo-1512917774080-9991f1c4c750|https://images.unsplash.com/photo-1564013799919-ab600027ffc6|https://images.unsplash.com/photo-1600585154340-be6161a56a0c",
                    Equipements = "Bureau|Fibre|Cuisine équipée|Sécurité",
                    Standing = "Business"
                },
            };

            context.Biens.AddRange(biens);
            context.SaveChanges();

            Console.WriteLine($"✅ {biens.Count} biens insérés en base avec succès.");
        }
    }
}