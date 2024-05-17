namespace CirclesUBI.PathfinderUpdater;

public static class Queries
{
    public const string BalancesBySafeAndToken = @"
        select safe_address, token_owner, balance::text
        from cache_crc_balances_by_safe_and_token
        where safe_address != '0x0000000000000000000000000000000000000000'
        and balance > 0;
    ";

    public const string TrustEdges = @"
        select ""user"", ""can_send_to"", ""limit""
        from cache_crc_current_trust;
    ";

    public const string Users = @"
        select ""user"", ""token""
        from crc_all_signups;
    ";

    public static string BlockByTransactionHash(string txHash)
    {
        return $"select block_number from transaction_2 where hash = '{txHash}';";
    }

    public static string GetChanges(long sinceBlock)
    {
        return $"select token_holder, can_send_to, token_owner, capacity::text from get_capacity_changes_since_block_2({sinceBlock});";
    }
}