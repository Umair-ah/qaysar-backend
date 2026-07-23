using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Models;

namespace Qaysar.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.AdminUsers.AnyAsync())
        {
            var username = Environment.GetEnvironmentVariable("ADMIN_USERNAME") ?? "admin";
            var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "admin123";
            db.AdminUsers.Add(new AdminUser
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });
            await db.SaveChangesAsync();
        }
    }
}
