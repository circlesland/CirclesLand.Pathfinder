namespace CirclesUBI.PathfinderUpdater.Indexer;

// {"jsonrpc":"2.0","error":{"code":-32700,"message":"Incorrect message"},"id":null}
public record JsonRpcEnvelope(string? Method, JsonRpcError? Error, object? Params);
public record JsonRpcError(int Code, string Message);