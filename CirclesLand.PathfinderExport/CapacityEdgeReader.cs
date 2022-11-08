using System.Data;
using System.Diagnostics;
using System.Numerics;
using Npgsql;

namespace CirclesLand.PathfinderExport;

public class CapacityEdgeReader : IDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly string _queryString;

    public CapacityEdgeReader(string connectionString, string queryString)
    {
        _connection = new NpgsqlConnection(connectionString);
        _connection.Open();
        
        _queryString = queryString;
    }

    public async Task<IEnumerable<(
            string senderAddress, 
            string receiverAddress, 
            string tokenOwnerAddress, 
            BigInteger capacity
        )>> ReadCapacityEdges(
        Stopwatch? queryStopWatch = null)
    {
        queryStopWatch?.Start();
        
        var cmd = new NpgsqlCommand(_queryString, _connection); 
        var capacityReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        queryStopWatch?.Stop();
        
        return CreateCapacityEdgeReader(capacityReader);
    }
    
    private IEnumerable<(
            string senderAddress,
            string receiverAddress,
            string tokenOwnerAddress, 
            BigInteger capacity
        )> CreateCapacityEdgeReader(
        IDataReader capacityReader) 
    {
        while (true)
        {
            var end = !capacityReader.Read();
            if (end)
            {
                break;
            }

            var sender = capacityReader.GetString(0).Substring(2);
            var receiver = capacityReader.GetString(1).Substring(2);
            var tokenOwner = capacityReader.GetString(2).Substring(2);
            var capacity = capacityReader.GetString(3);

            // We might get a decimal number. If so, use only the part before the decimal point.
            var decimalPointIndex = capacity.IndexOf(".", StringComparison.Ordinal);
            if (decimalPointIndex > -1)
            {
                capacity = capacity.Substring(0, decimalPointIndex);
            }

            if (!BigInteger.TryParse(capacity, out var capacityBigInteger))
            {
                throw new Exception($"Couldn't parse string {capacity} as BigInteger value.");
            }

            yield return (
                sender,
                receiver,
                tokenOwner,
                capacityBigInteger);
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}