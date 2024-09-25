using CirclesUBI.PathfinderUpdater.Indexer;
using CirclesUBI.PathfinderUpdater.PathfinderRpc;

namespace CirclesUBI.PathfinderUpdater.Updater;

public static class Program
{
    private static readonly Logger Logger = new();

    private static readonly HealthMonitor _blockUpdateHealth =
        new HealthMonitor("Indexer", Config.BlockUpdateHealthThreshold);

    private static readonly HealthMonitor _pathfinderResponseHealth =
        new HealthMonitor("Pathfinder", Config.PathfinderResponseHealthThreshold);

    private static readonly HealthEndpoint HealthEndpoint = new("http://+:8794/", new[]
    {
        _blockUpdateHealth,
        _pathfinderResponseHealth
    });

    private static IndexerSubscription? _indexerSubscription;
    private static RpcEndpoint _pathfinderRpc = null!;
    private static Config _config = null!;
    private static Queries _queries = null!;

    private static long _currentBlock;
    private static long _lastFullUpdate;

    private static int _isWorking;

    public static async Task Main(string[] args)
    {
        _config = Config.Read(args);
        _queries = new Queries(_config.CirclesVersion);
        _pathfinderRpc = new RpcEndpoint(_config.PathfinderUrl);

        _indexerSubscription =
            new IndexerSubscription(_config.IndexerWebsocketUrl, _config.CirclesVersion == "v2" ? 2 : 1);
        _indexerSubscription.SubscriptionEvent += OnIndexerSubscriptionEvent;

        // Start periodic full updates every minute when on v2
        if (_config.CirclesVersion == "v2")
        {
            Task.Run(StartPeriodicUpdate);
        }

        await _indexerSubscription.Run();
    }

    private static void OnIndexerSubscriptionEvent(object? sender, IndexerSubscriptionEventArgs e)
    {
        if (e.Error != null)
        {
            OnFatalError(e.Error);
        }

        Logger.Call("On indexer websocket message", async () =>
            {
                Logger.Log($" _working = {_isWorking}");

                if (Interlocked.CompareExchange(ref _isWorking, 1, 0) != 0)
                {
                    Logger.Log($"Still working. Ignore the incoming message.");
                    return;
                }

                if (e.Message!.TransactionHashes.Contains(Constants.DeadBeefTxHash))
                {
                    OnReorgOccurred();
                }
                else
                {
                    await OnNewBlock(e.Message.TransactionHashes);
                }

                Interlocked.Exchange(ref _isWorking, 0);
            })
            .ContinueWith(result =>
            {
                if (result.Exception != null)
                {
                    OnFatalError(result.Exception);
                }
            });
    }

    private static void OnFatalError(Exception e)
    {
        Logger.Log($"An error occurred:");
        Logger.Log(e.Message);
        Logger.Log(e.StackTrace ?? "");

        Environment.Exit(99);
    }

    private static async Task OnNewBlock(string[] transactionHashes)
    {
        if (transactionHashes.Length == 0)
        {
            Logger.Log("Ignore empty block");
            return;
        }

        await Logger.Call("On new block", async () =>
        {
            _blockUpdateHealth.KeepAlive();

            await UpdateCurrentBlock();

            await UpdatePathfinder();
        });
    }

    private static async Task UpdateCurrentBlock()
    {
        await Logger.Call("Find block number", async () =>
        {
            _currentBlock = await Block.FindLatestBlockNumber(
                _config.IndexerDbConnectionString,
                _queries);

            Logger.Log($"Block No.: {_currentBlock}");
        });
    }

    private static async Task UpdatePathfinder()
    {
        await Logger.Call("Initialize the pathfinder2 with a new capacity graph", async () =>
        {
            await Logger.Call($"Export graph to '{_config.InternalCapacityGraphPath}'", async () =>
            {
                await using var outFileStream =
                    await ExportUtil.Program.ExportToBinaryFile(_config.InternalCapacityGraphPath,
                        _config.IndexerDbConnectionString,
                        _config.CirclesVersion);
            });

            await Logger.Call($"Call 'load_safes_binary' on pathfinder at '{_config.PathfinderUrl}'", async () =>
            {
                var callResult = await _pathfinderRpc.Call(
                    RpcCalls.LoadSafesBinary(_config.ExternalCapacityGraphPath));

                Logger.Log("Response body: ");
                Logger.Log(callResult.resultBody);

                _pathfinderResponseHealth.KeepAlive();
            });

            _lastFullUpdate = _currentBlock;

            Logger.Log($"Pathfinder2 initialized up to block {_lastFullUpdate}");
        });
    }

    private static async Task StartPeriodicUpdate()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(1));

            if (Interlocked.CompareExchange(ref _isWorking, 1, 0) != 0)
            {
                Logger.Log($"Still working. Ignore the periodic update.");
                continue;
            }

            await Logger.Call("Periodic UpdatePathfinder", async () =>
            {
                await UpdateCurrentBlock();
                await UpdatePathfinder();
            });

            Interlocked.Exchange(ref _isWorking, 0);
        }
    }

    private static void OnReorgOccurred()
    {
        Logger.Log("On reorg (the indexer sent the '0xDEADBEEF..' transaction hash)");
    }
}