namespace SaidAfricaBackend
{
    public static class DataSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            FixBlankRoles(context);
            FixAdminEmail(context, "jean.fotso.admin@saidafrica.cm", "tiombomaxence@gmail.com");
        }

        private static void FixAdminEmail(ApplicationDbContext context, string oldEmail, string newEmail)
        {
            var user = context.Users.FirstOrDefault(u => u.Email.ToLower() == oldEmail.ToLower());
            if (user == null) return;
            user.Email = newEmail;
            context.SaveChanges();
            Console.WriteLine($"🔧 Email admin mis à jour : {oldEmail} → {newEmail}");
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
    }
}
