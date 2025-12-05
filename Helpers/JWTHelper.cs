using System;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using StarApi.Models;

namespace StarApi.Helpers
{
    public static class JWTHelper
    {
        public static string GenerateJwtToken(User user, IConfiguration configuration)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(configuration["Jwt:Key"] ?? string.Empty);

            var claims = new[]
            {
                // ADD THIS LINE - "id" claim for backward compatibility
                new Claim("id", user.Id.ToString()),

                // Your existing claims
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                // Optional: Add more user info if needed
                new Claim("avatar", user.AvatarUrl ?? ""),
                new Claim("status", user.Status),
                new Claim("isVerified", user.IsVerified.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(2),
                Issuer = configuration["Jwt:Issuer"],
                Audience = configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public static void DebugTokenClaims(User user, IConfiguration configuration)
        {
            var token = GenerateJwtToken(user, configuration);
            var handler = new JwtSecurityTokenHandler();

            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                Console.WriteLine("=== JWT Token Debug ===");
                Console.WriteLine($"Token: {token.Substring(0, Math.Min(50, token.Length))}...");
                Console.WriteLine("\nClaims in generated token:");
                foreach (var claim in jwtToken.Claims)
                {
                    Console.WriteLine($"  {claim.Type}: {claim.Value}");
                }

                // Verify the "id" claim
                var idClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "id");
                Console.WriteLine($"\n'id' claim exists: {idClaim != null}");
                Console.WriteLine($"'id' claim value: {idClaim?.Value}");
                Console.WriteLine($"Is valid Guid: {Guid.TryParse(idClaim?.Value, out _)}");
            }
        }
    }
}