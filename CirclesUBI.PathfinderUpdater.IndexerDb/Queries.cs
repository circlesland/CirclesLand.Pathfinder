namespace CirclesUBI.PathfinderUpdater;

public class Queries
{
    private readonly string _version;

    public Queries(string version)
    {
        if (version != "v1" && version != "v2")
        {
            throw new ArgumentException("The circles version must be 'v1' or 'v2'.");
        }

        _version = version;
    }

    public string BalancesByAccountAndToken =>
        _version == "v1" ? V1BalancesByAccountAndToken : V2BalancesByAccountAndToken;

    public const string V1BalancesByAccountAndToken = @"
        select safe_address, token_owner, balance::text
        from cache_crc_balances_by_safe_and_token
        where safe_address != '0x0000000000000000000000000000000000000000'
        and balance > 0;
    ";

    public string TrustEdges => _version == "v1" ? V1TrustEdges : V2TrustEdges;

    public const string V1TrustEdges = @"
        select ""user"", ""can_send_to"", ""limit""
        from cache_crc_current_trust;
    ";

    public string Accounts => _version == "v1" ? V1Accounts : V2Accounts;

    public const string V1Accounts = @"
        select ""user"", ""token""
        from crc_all_signups;
    ";

    public string LatestBlockNumber => _version == "v1" ? V1LatestBlockNumber : V2LatestBlockNumber;
    
    public const string V1LatestBlockNumber = "select max(block_number) from transaction_2;";
    
    public const string V2LatestBlockNumber = "select max(\"blockNumber\") from \"System_Block\";";

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
    select ""to"" as account, id::text as token_id, sum(value)::text as total_balance
    from ""orderedTransfers""
       where ""to"" != '0x0000000000000000000000000000000000000000'
    group by ""to"", id
    having sum(value) > 0;
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


    public static string V2BlockByTransactionHash(string txHash)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(txHash, "0x[0-9a-fA-F]{64}"))
        {
            throw new ArgumentException("Invalid transaction hash format.");
        }

        return $@"
        with all_indexed_txs as (
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_RegisterHuman""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_InviteHuman""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_RegisterOrganization""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_RegisterGroup""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_TransferSingle""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_TransferBatch""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_Trust""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_ApprovalForAll""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_DiscountCost""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_PersonalMint""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_RegisterShortName""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_Stopped""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_UpdateMetadataDigest""
               union all
               select ""blockNumber"",
                      ""transactionHash""
               from ""CrcV2_URI""
           )
           select distinct *
           from all_indexed_txs
           where ""transactionHash"" = '{txHash}';
           ";
    }
}