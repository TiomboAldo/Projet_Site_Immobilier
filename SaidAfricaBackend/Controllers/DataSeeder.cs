namespace SaidAfricaBackend
{
    public static class DataSeeder
    {
        public static void Seed(ApplicationDbContext context)
        {
            FixBlankRoles(context);
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
