namespace CirclesUBI.PathfinderUpdater;

public static class Queries
{
    public const string V2TrustEdges = @"
    -- Get a snapshot of all active (v2) trust relations:
    --
    -- Rules:
    -- * Only the latest event (""blockNumber"" desc, ""transactionIndex"" desc, ""logIndex"" desc) per 'truster', 'trustee' pair is relevant
    -- * If the 'expiryTime' of the latest event for a pair is '""expiryTime"" > (select max(""timestamp"") from ""System_Block"")' then
    --   the trust relation must be omitted from the result
    select trustee as ""user"",
           truster as ""can_send_to"",
           100 as ""limit""
    from (
             select ""blockNumber"",
                    timestamp,
                    ""transactionIndex"",
                    ""logIndex"",
                    truster,
                    trustee,
                    ""expiryTime"",
                    row_number() over (partition by truster, trustee order by ""blockNumber"" desc, ""transactionIndex"" desc, ""logIndex"" desc) as rn
             from ""CrcV2_Trust""
         ) t
    where rn = 1
    and ""expiryTime"" > (select max(""timestamp"") from ""System_Block"")
    order by ""blockNumber"" desc, ""transactionIndex"" desc, ""logIndex"" desc;
    ";

    public const string V2BalancesByAccountAndToken = @"
    with ""transfers"" as (select ""blockNumber"",
                      timestamp,
                      ""transactionIndex"",
                      ""logIndex"",
                      0 as ""batchIndex"",
                      ""transactionHash"",
                      operator,
                      ""from"",
                      ""to"",
                      id,
                      value
               from ""CrcV2_TransferSingle""
               union all
               select ""blockNumber"",
                      timestamp,
                      ""transactionIndex"",
                      ""logIndex"",
                      ""batchIndex"",
                      ""transactionHash"",
                      ""operatorAddress"",
                      ""fromAddress"",
                      ""toAddress"",
                      id,
                      value
               from ""CrcV2_TransferBatch""),
    ""orderedTransfers"" as (
        select ""blockNumber"",
               timestamp,
               ""transactionIndex"",
               ""logIndex"",
               ""batchIndex"",
               ""transactionHash"",
               operator,
               ""from"",
               ""to"",
               id,
               value
        from transfers
        order by ""blockNumber"", ""transactionIndex"", ""logIndex"", ""batchIndex""   
    )
    select ""to"" as account, id as token_id, sum(value) as total_balance
    from ""orderedTransfers""
    group by ""to"", id;
    ";


    public const string V1BalancesByAccountAndToken = @"
        select safe_address, token_owner, balance::text
        from cache_crc_balances_by_safe_and_token
        where safe_address != '0x0000000000000000000000000000000000000000'
        and balance > 0;
    ";

    public const string V1TrustEdges = @"
        select ""user"", ""can_send_to"", ""limit""
        from cache_crc_current_trust;
    ";

    public const string V1Accounts = @"
        select ""user"", ""token""
        from crc_all_signups;
    ";

    public const string V2Accounts = @"
    with all_signups as (select avatar ""user"", avatar ""token""
                     from ""CrcV2_RegisterHuman""
                     union all
                     select invited ""user"", invited ""token""
                     from ""CrcV2_InviteHuman""
                     union all
                     select organization as ""user"", null as ""token""
                     from ""CrcV2_RegisterOrganization""
                     union all
                     select ""group"" as ""user"", ""group"" as ""token""
                     from ""CrcV2_RegisterGroup"")
    select ""user"", ""token""
    from all_signups;
    ";

    public static string V1BlockByTransactionHash(string txHash)
    {
        return $"select block_number from transaction_2 where hash = '{txHash}';";
    }
}