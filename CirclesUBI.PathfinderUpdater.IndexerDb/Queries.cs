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

    // TODO: Exclude trust edges to accounts that aren't signed up yet
    public const string V2TrustEdges = @"
        with ""trustRelations"" as (select ""trustee"" as ""user"",
                                         ""truster"" as ""can_send_to"",
                                         100     as ""limit""
                                  from (
                                           select ""blockNumber"",
                                                  ""timestamp"",
                                                  ""transactionIndex"",
                                                  ""logIndex"",
                                                  ""truster"",
                                                  ""trustee"",
                                                  ""expiryTime"",
                                                  row_number() over (partition by ""truster"", ""trustee"" order by ""blockNumber"" desc, ""transactionIndex"" desc, ""logIndex"" desc) as rn
                                           from ""CrcV2_Trust"") t
                                  where t.rn = 1
                                    and t.""expiryTime"" > (select max(""timestamp"") from ""System_Block"")
                                  order by t.""blockNumber"" desc, t.""transactionIndex"" desc, t.""logIndex"" desc
        )
        select t.""user"",
               t.""can_send_to"",
               t.""limit""
        from ""trustRelations"" t
        join ""V_CrcV2_Avatars"" a on a.avatar = t.""user"";
    ";

    public const string V2BalancesByAccountAndToken = @"
           select account, ""tokenId"" as token_id, ""demurragedTotalBalance""::text as total_balance
           from ""V_CrcV2_BalancesByAccountAndToken""; 
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
}