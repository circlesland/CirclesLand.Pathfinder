using System.Diagnostics;
using CirclesUBI.Pathfinder.Models;

namespace CirclesUBI.PathfinderUpdater;

public static class CapacityGraph
{
    public static async Task<(
        IEnumerable<IncrementalExportRow> result,
        TimeSpan queryDuration,
        TimeSpan downloadDuration,
        TimeSpan totalDuration
        )> SinceBlock(string connectionString, long sinceBlockNo)
    {
        using var capacityEdgeReader = new CapacityEdgeReader(connectionString, Queries.GetChanges(sinceBlockNo));

        var queryStopWatch = new Stopwatch();
        var totalStopWatch = new Stopwatch();
        totalStopWatch.Start();

        var edgeIterator = await capacityEdgeReader.ReadCapacityEdges(
            queryStopWatch);

        var rows = new List<IncrementalExportRow>();

        foreach (var edge in edgeIterator)
        {
            rows.Add(new IncrementalExportRow
            (
                from: edge.senderAddress,
                to: edge.receiverAddress,
                tokenOwner: edge.tokenOwnerAddress,
                capacity: edge.capacity.ToString()
            ));
        }

        totalStopWatch.Stop();
        
        return (rows, queryStopWatch.Elapsed, totalStopWatch.Elapsed - queryStopWatch.Elapsed, totalStopWatch.Elapsed);
    }
}