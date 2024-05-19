namespace CirclesUBI.PathfinderUpdater.Indexer;

public sealed class NewBlockMessage(string[] transactionHashes)
{
    public string[] TransactionHashes { get; } = transactionHashes;
}