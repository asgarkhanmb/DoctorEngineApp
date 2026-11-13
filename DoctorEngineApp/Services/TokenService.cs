using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DoctorEngine.Contracts;
using DoctorEngineApp.Data;
using DoctorEngineApp.Helpers;
using DoctorEngineApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DoctorEngineApp.Services
{
    /// <summary>Tətbiqlər üçün access token (JWT) və refresh token yaradılması / yenilənməsi / ləğvi.</summary>
    public class TokenService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _users;
        private readonly JwtOptions _opt;

        public TokenService(AppDbContext db, UserManager<AppUser> users, IOptions<JwtOptions> opt)
        {
            _db = db;
            _users = users;
            _opt = opt.Value;
        }

        public static SymmetricSecurityKey SigningKey(JwtOptions opt) =>
            new(Encoding.UTF8.GetBytes(opt.Key!));

        /// <summary>Yeni giriş: yeni token zənciri.</summary>
        public Task<AuthResponse> IssueAsync(AppUser user, string? deviceName) =>
            IssueAsync(user, deviceName, NewId());

        private async Task<AuthResponse> IssueAsync(AppUser user, string? deviceName, string familyId)
        {
            var roles = (await _users.GetRolesAsync(user)).ToList();
            var now = DateTime.UtcNow;
            var accessExpires = now.AddMinutes(_opt.AccessTokenMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, NewId()),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new(AppClaimsPrincipalFactory.FullNameClaim, user.FullName ?? string.Empty),
                // Rol / şifrə dəyişəndə köhnə tokenlərin aşkarlanması üçün
                new("sstamp", user.SecurityStamp ?? string.Empty)
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var jwt = new JwtSecurityToken(
                issuer: _opt.Issuer,
                audience: _opt.Audience,
                claims: claims,
                notBefore: now,
                expires: accessExpires,
                signingCredentials: new SigningCredentials(SigningKey(_opt), SecurityAlgorithms.HmacSha256));

            var refreshPlain = NewSecret();
            var refreshExpires = now.AddDays(_opt.RefreshTokenDays);
            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = Hash(refreshPlain),
                FamilyId = familyId,
                DeviceName = Trim(deviceName, 100),
                CreatedAtUtc = now,
                ExpiresAtUtc = refreshExpires
            });
            await _db.SaveChangesAsync();

            return new AuthResponse(
                new JwtSecurityTokenHandler().WriteToken(jwt),
                new DateTimeOffset(accessExpires, TimeSpan.Zero),
                refreshPlain,
                new DateTimeOffset(refreshExpires, TimeSpan.Zero),
                ToDto(user, roles));
        }

        /// <summary>
        /// Refresh token → yeni cüt (köhnə ləğv olunur). Ləğv olunmuş token yenidən gəlsə —
        /// oğurluq ehtimalı: bütün zəncir ləğv edilir və null qaytarılır.
        /// </summary>
        public async Task<AuthResponse?> RefreshAsync(string refreshPlain, string? deviceName)
        {
            if (string.IsNullOrWhiteSpace(refreshPlain)) return null;
            var hash = Hash(refreshPlain);
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (token == null) return null;

            if (token.RevokedAtUtc != null)
            {
                // Təkrar istifadə: zəncirin bütün aktiv tokenlərini ləğv et
                await RevokeFamilyAsync(token.FamilyId);
                return null;
            }
            if (DateTime.UtcNow >= token.ExpiresAtUtc) return null;

            var user = await _users.FindByIdAsync(token.UserId);
            if (user == null || await _users.IsLockedOutAsync(user)) return null;

            var response = await IssueAsync(user, deviceName ?? token.DeviceName, token.FamilyId);
            token.RevokedAtUtc = DateTime.UtcNow;
            token.ReplacedByHash = Hash(response.RefreshToken);
            await _db.SaveChangesAsync();
            return response;
        }

        /// <summary>Çıxış: bu cihazın tokenini (zəncirini) ləğv edir.</summary>
        public async Task RevokeAsync(string refreshPlain)
        {
            if (string.IsNullOrWhiteSpace(refreshPlain)) return;
            var hash = Hash(refreshPlain);
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (token != null) await RevokeFamilyAsync(token.FamilyId);
        }

        /// <summary>Şifrə dəyişəndə və s.: istifadəçinin bütün cihazlarından çıxış.</summary>
        public async Task RevokeAllForUserAsync(string userId)
        {
            var now = DateTime.UtcNow;
            await _db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now));
        }

        private async Task RevokeFamilyAsync(string familyId)
        {
            var now = DateTime.UtcNow;
            await _db.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now));
        }

        public static UserDto ToDto(AppUser user, IEnumerable<string> roles) => new(
            user.Id,
            user.UserName ?? string.Empty,
            user.FullName ?? string.Empty,
            user.Email ?? string.Empty,
            string.IsNullOrEmpty(user.ProfileImage) ? null : "/uploads/avatars/" + user.ProfileImage,
            roles.ToList());

        // 256-bit təsadüfi sirr (URL-safe)
        private static string NewSecret() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string NewId() => Guid.NewGuid().ToString("N");

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        private static string? Trim(string? s, int max) =>
            string.IsNullOrWhiteSpace(s) ? null : (s.Length > max ? s[..max] : s.Trim());
    }
}
