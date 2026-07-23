using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Qaysar.Api.Configuration;
using Qaysar.Api.Data;
using Qaysar.Api.DTOs;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _jwt;

    public AuthService(AppDbContext db, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _jwt = jwt.Value;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null) return null;
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;

        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwt.Key);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, "Admin"),
            }),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            // No Expires -> token effectively never expires (validated with ValidateLifetime=false)
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = handler.CreateToken(descriptor);
        return new LoginResponse(handler.WriteToken(token), user.Username);
    }
}
