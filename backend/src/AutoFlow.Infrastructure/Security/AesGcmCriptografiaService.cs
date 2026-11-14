using System.Security.Cryptography;
using System.Text;
using AutoFlow.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoFlow.Infrastructure.Security;

/// <summary>
/// AES-256-GCM (cifra autenticada). Formato gravado: "v1:" + Base64(nonce[12] | tag[16] | cifra).
/// Cada valor usa um nonce aleatório, então o mesmo texto gera cifras diferentes.
/// </summary>
public sealed class AesGcmCriptografiaService : ICriptografiaService
{
    private const string Prefixo = "v1:";
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;
    private readonly byte[] _chave;

    public AesGcmCriptografiaService(IOptions<CriptografiaOptions> options)
    {
        try
        {
            _chave = Convert.FromBase64String(options.Value.ChaveBase64);
        }
        catch (FormatException)
        {
            _chave = [];
        }
        if (_chave.Length != 32)
            throw new InvalidOperationException(
                "Configure Criptografia:ChaveBase64 com uma chave de 32 bytes em Base64 (openssl rand -base64 32).");
    }

    public string Criptografar(string texto)
    {
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var claro = Encoding.UTF8.GetBytes(texto);
        var cifra = new byte[claro.Length];
        var tag = new byte[TamanhoTag];

        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Encrypt(nonce, claro, cifra, tag);

        var saida = new byte[TamanhoNonce + TamanhoTag + cifra.Length];
        nonce.CopyTo(saida, 0);
        tag.CopyTo(saida, TamanhoNonce);
        cifra.CopyTo(saida, TamanhoNonce + TamanhoTag);
        return Prefixo + Convert.ToBase64String(saida);
    }

    public string Descriptografar(string cifrado)
    {
        if (!cifrado.StartsWith(Prefixo, StringComparison.Ordinal))
            throw new CryptographicException("Valor não está no formato cifrado esperado.");

        var dados = Convert.FromBase64String(cifrado[Prefixo.Length..]);
        if (dados.Length < TamanhoNonce + TamanhoTag)
            throw new CryptographicException("Valor cifrado truncado.");

        var nonce = dados.AsSpan(0, TamanhoNonce);
        var tag = dados.AsSpan(TamanhoNonce, TamanhoTag);
        var cifra = dados.AsSpan(TamanhoNonce + TamanhoTag);
        var claro = new byte[cifra.Length];

        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Decrypt(nonce, cifra, tag, claro);
        return Encoding.UTF8.GetString(claro);
    }
}
