using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Alife.Function.QChat;

/// <summary>
/// 基础的 OneBot v11 客户端，提供多态事件分发。
/// </summary>
public class OneBotClient : IAsyncDisposable
{
    public event Action<OneBotBaseEvent>? OnEventReceived;

    public long BotId => botId;

    public OneBotClient(string url)
    {
        this.url = url;
    }

    public async Task ConnectAsync()
    {
        ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(url), CancellationToken.None);

        // 同步握手：预期第一个报文是 connect 事件
        using CancellationTokenSource cts = new(5000);
        OneBotBaseEvent? ev = await ReceiveEventAsync(cts.Token);

        if (ev is OneBotMetaEvent { MetaEventType: OneBotMetaType.Lifecycle, SubType: "connect" })
        {
            botId = ev.SelfId;
            ReceiveLoop();
        }
        else
        {
            throw new ProtocolViolationException("[OneBotClient] 握手失败：无法识别首个报文。");
        }
    }

    public async Task SendActionAsync(string action, object? @params = null)
    {
        OneBotAction payload = new() { Action = action, Params = @params };
        string json = JsonSerializer.Serialize(payload);
        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (ws.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
        ws.Dispose();
    }

    readonly string url;
    readonly byte[] buffer = new byte[1024 * 64];
    ClientWebSocket ws = new();
    long botId;

    async void ReceiveLoop()
    {
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                OneBotBaseEvent? ev = await ReceiveEventAsync();
                if (ev != null) OnEventReceived?.Invoke(ev);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OneBotClient] 链路异常: {ex.Message}");
        }
    }

    async Task<OneBotBaseEvent?> ReceiveEventAsync(CancellationToken ct = default)
    {
        WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
        if (result.MessageType == WebSocketMessageType.Close) return null;

        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);

        // 手动检测 post_type 以处理 System.Text.Json 的多态限制
        using JsonDocument doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("post_type", out JsonElement typeElem)) return null;

        string type = typeElem.GetString() ?? "";
        return type switch
        {
            "message" => doc.RootElement.Deserialize<OneBotMessageEvent>(),
            "message_sent" => doc.RootElement.Deserialize<OneBotMessageSentEvent>(),
            "meta_event" => doc.RootElement.Deserialize<OneBotMetaEvent>(),
            "notice" => doc.RootElement.Deserialize<OneBotNoticeEvent>(),
            "request" => doc.RootElement.Deserialize<OneBotRequestEvent>(),
            _ => null
        };
    }
}
