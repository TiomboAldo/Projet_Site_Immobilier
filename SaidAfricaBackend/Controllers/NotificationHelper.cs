namespace SaidAfricaBackend.Controllers
{
    /// <summary>
    /// Ajoute une notification au contexte sans appeler SaveChangesAsync : elle est
    /// persistée avec le SaveChangesAsync de l'opération appelante (même transaction logique).
    /// </summary>
    public static class NotificationHelper
    {
        public static void Creer(
            ApplicationDbContext context,
            int userId,
            string type,
            string titre,
            string message,
            string? cibleSection = null)
        {
            context.Notifications.Add(new Notification
            {
                UserId       = userId,
                Type         = type,
                Titre        = titre,
                Message      = message,
                CibleSection = cibleSection,
            });
        }
    }
}
