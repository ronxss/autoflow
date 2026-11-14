using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoFlow.Application.Abstractions;
using AutoFlow.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AutoFlow.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider relogio) : ITokenService
{
    private readonly JwtOptions _opcoes = options.Value;

    public (string Token, DateTime ExpiraEm) GerarAccessToken(Usuario usuario)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var expiraEm = agora.AddMinutes(_opcoes.MinutosAccessToken);
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Chave));

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = _opcoes.Emissor,
            Audience = _opcoes.Audiencia,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", usuario.Id.ToString()),
                new Claim("email", usuario.Email),
                new Claim("name", usuario.Nome),
                new Claim("role", usuario.Tipo.ToString())
            }),
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm,
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descritor), expiraEm);
    }

    public string GerarRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>O banco guarda só o SHA-256 do refresh token, nunca o valor original.</summary>
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
