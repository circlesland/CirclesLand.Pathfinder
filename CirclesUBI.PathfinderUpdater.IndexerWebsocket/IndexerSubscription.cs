using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CirclesUBI.PathfinderUpdater.Indexer;

public class IndexerSubscription(string indexerUrl, int version = 1) : IDisposable
{
    private readonly ClientWebSocket _clientWebSocket = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public event EventHandler<IndexerSubscriptionEventArgs>? SubscriptionEvent;

    public async Task Stop()
    {
        await _cancellationTokenSource.CancelAsync();
        await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", CancellationToken.None);
    }

    public async Task Run()
    {
        try
        {
            await _clientWebSocket.ConnectAsync(new Uri(indexerUrl), _cancellationTokenSource.Token);

            if (version == 2)
            {
                await SendAsync(
                    @"{ ""jsonrpc"": ""2.0"", ""method"": ""eth_subscribe"", ""params"": [""circles"", {}], ""id"": 1}");
            }

            while (_clientWebSocket.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested)
            {
                var blockUpdateMessage = await ReceiveWsMessage();

                if (version == 1)
                {
                    // Parse the message sent by the blockchain-indexer
                    // (https://github.com/CirclesUBI/blockchain-indexer)
                    var transactionHashesInLastBlock = JsonConvert.DeserializeObject<string[]>(blockUpdateMessage);
                    if (transactionHashesInLastBlock == null)
                    {
                        throw new Exception($"Received an invalid block update via websocket: {blockUpdateMessage}");
                    }

                    SubscriptionEvent?.Invoke(this, new IndexerSubscriptionEventArgs(
                        new NewBlockMessage(transactionHashesInLastBlock)));
                }
                else if (version == 2)
                {
                    // Parse the message sent by the circles-nethermind-plugin
                    // (https://github.com/CirclesUBI/circles-nethermind-plugin)
                    var newBlockMessage = JsonConvert.DeserializeObject<JsonRpcEnvelope>(blockUpdateMessage);
                    if (newBlockMessage == null)
                    {
                        continue;
                    }

                    if (newBlockMessage.Error != null)
                    {
                        throw new Exception($"Received an invalid block update via websocket: {blockUpdateMessage}");
                    }

                    // get the single events:
                    var paramsJObject = newBlockMessage.Params as JObject;
                    var resultJArray = paramsJObject?["result"] as JArray;
                    if (resultJArray == null)
                    {
                        continue;
                    }

                    var transactionHashesInLastBlock = new HashSet<string>();
                    foreach (var circlesEvent in resultJArray)
                    {
                        var transactionHash = circlesEvent["values"]?["transactionHash"]?.ToString();
                        if (transactionHash != null)
                        {
                            transactionHashesInLastBlock.Add(transactionHash);
                        }
                    }

                    SubscriptionEvent?.Invoke(this, new IndexerSubscriptionEventArgs(
                        new NewBlockMessage(transactionHashesInLastBlock.ToArray())));
                }
                else
                {
                    throw new ArgumentException($"Invalid indexer version: {version}");
                }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
            SubscriptionEvent?.Invoke(this, new IndexerSubscriptionEventArgs(exception));
            throw;
        }
    }

    private async Task<string> ReceiveWsMessage()
    {
        var receiving = true;
        var buffer = new byte[4096];
        var mem = new Memory<byte>(buffer);
        var fullMessageBuffer = new List<byte[]>();

        while (receiving)
        {
            var result = await _clientWebSocket.ReceiveAsync(mem, _cancellationTokenSource.Token);
            var data = new ArraySegment<byte>(buffer, 0, result.Count).ToArray();
            fullMessageBuffer.Add(data);
            receiving = !result.EndOfMessage;
        }

        var fullLength = fullMessageBuffer.Sum(o => o.Length);
        var fullMessageBytes = new byte[fullLength];
        var currentIdx = 0;

        foreach (var part in fullMessageBuffer)
        {
            Array.Copy(part, 0, fullMessageBytes, currentIdx, part.Length);
            currentIdx += part.Length;
        }

        fullMessageBuffer.Clear();

        var fullMessageString = Encoding.UTF8.GetString(fullMessageBytes);
        return fullMessageString;
    }

    private async Task SendAsync(string message)
    {
        var buffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);
        await _clientWebSocket.SendAsync(segment, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _clientWebSocket.Dispose();
    }
}