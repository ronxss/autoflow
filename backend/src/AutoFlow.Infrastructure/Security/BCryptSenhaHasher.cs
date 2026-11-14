using AutoFlow.Application.Abstractions;

namespace AutoFlow.Infrastructure.Security;

public sealed class BCryptSenhaHasher : ISenhaHasher
{
    private const int FatorDeCusto = 12;

    public string Hash(string senha) => BCrypt.Net.BCrypt.HashPassword(senha, FatorDeCusto);

    public bool Verificar(string senha, string hash) => BCrypt.Net.BCrypt.Verify(senha, hash);
}
