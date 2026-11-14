using System.Security.Cryptography;
using AutoFlow.Infrastructure;
using AutoFlow.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace AutoFlow.Tests;

public class CriptografiaTests
{
    private static AesGcmCriptografiaService Criar(byte[]? chave = null) => new(
        Options.Create(new CriptografiaOptions
        {
            ChaveBase64 = Convert.ToBase64String(chave ?? RandomNumberGenerator.GetBytes(32))
        }));

    [Fact]
    public void Descriptografa_o_que_criptografou()
    {
        var servico = Criar();
        var cifrado = servico.Criptografar("CPF 123.456.789-09 — ação");

        Assert.StartsWith("v1:", cifrado);
        Assert.DoesNotContain("123.456", cifrado);
        Assert.Equal("CPF 123.456.789-09 — ação", servico.Descriptografar(cifrado));
    }

    [Fact]
    public void O_mesmo_texto_gera_cifras_diferentes()
    {
        var servico = Criar();
        Assert.NotEqual(servico.Criptografar("segredo"), servico.Criptografar("segredo"));
    }

    [Fact]
    public void Rejeita_valor_adulterado()
    {
        var servico = Criar();
        var bytes = Convert.FromBase64String(servico.Criptografar("segredo")[3..]);
        bytes[^1] ^= 0xFF;
        var adulterado = "v1:" + Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => servico.Descriptografar(adulterado));
    }

    [Fact]
    public void Rejeita_chave_diferente()
    {
        var cifrado = Criar().Criptografar("segredo");
        Assert.ThrowsAny<CryptographicException>(() => Criar().Descriptografar(cifrado));
    }

    [Fact]
    public void Exige_chave_de_256_bits()
    {
        Assert.Throws<InvalidOperationException>(() => Criar(new byte[16]));
    }
}
