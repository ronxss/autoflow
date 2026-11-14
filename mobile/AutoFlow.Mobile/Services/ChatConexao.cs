using System.Text.Json.Serialization;
using AutoFlow.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AutoFlow.Mobile.Services;

/// <summary>Conexão em tempo real com o hub SignalR da API (/hubs/chat).</summary>
public class ChatConexao(SessaoService sessao)
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private HubConnection? _hub;

    public event Action<MensagemDto>? MensagemRecebida;
    public event Action<Guid, Guid>? MensagensLidas;
    public event Action<SolicitacaoDto>? SolicitacaoAtualizada;

    public async Task ConectarAsync()
    {
        await _trava.WaitAsync();
        try
        {
            _hub ??= Criar();
            if (_hub.State == HubConnectionState.Disconnected)
                await _hub.StartAsync();
        }
        finally
        {
            _trava.Release();
        }
    }

    public async Task<MensagemDto> EnviarAsync(Guid solicitacaoId, string texto)
    {
        await ConectarAsync();
        return await _hub!.InvokeAsync<MensagemDto>("EnviarMensagem", new EnviarMensagemRequest(solicitacaoId, texto));
    }

    public async Task MarcarComoLidasAsync(Guid solicitacaoId)
    {
        await ConectarAsync();
        await _hub!.InvokeAsync<bool>("MarcarComoLidas", solicitacaoId);
    }

    public async Task DesconectarAsync()
    {
        await _trava.WaitAsync();
        try
        {
            if (_hub is not null)
            {
                await _hub.DisposeAsync();
                _hub = null;
            }
        }
        finally
        {
            _trava.Release();
        }
    }

    private HubConnection Criar()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl($"{Configuracao.ApiBaseUrl}/hubs/chat", opcoes =>
            {
                opcoes.AccessTokenProvider = async () =>
                {
                    await sessao.GarantirTokenValidoAsync();
                    return sessao.AccessToken;
                };
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(opcoes => opcoes.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        hub.On<MensagemDto>("MensagemRecebida", mensagem => MensagemRecebida?.Invoke(mensagem));
        hub.On<Guid, Guid>("MensagensLidas", (solicitacaoId, leitorId) => MensagensLidas?.Invoke(solicitacaoId, leitorId));
        hub.On<SolicitacaoDto>("SolicitacaoAtualizada", solicitacao => SolicitacaoAtualizada?.Invoke(solicitacao));
        return hub;
    }
}
