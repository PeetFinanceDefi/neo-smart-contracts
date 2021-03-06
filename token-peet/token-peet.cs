using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;


[assembly: ContractTitle("Peet Defi")]
[assembly: ContractDescription("Token for Peet Defi")]
[assembly: ContractVersion("1.0.0")]
[assembly: ContractAuthor("Peet")]
[assembly: ContractEmail("dev@peetdecentralized.finance")]
[assembly: Features(ContractPropertyState.HasStorage)]
public class TokenPeet : SmartContract
{
    [DisplayName("transfer")]
    public static event Action<byte[], byte[], BigInteger> Transferred;

    private static readonly byte[] Oracle = "ASjSPpmAzGYjAivqvPAyNG8cGEPRWgjFr6".ToScriptHash(); //Oracle Address

    private static readonly BigInteger MaxTotalSupplyValue = 100000;

    // Starting with 10k PET supply since we're coming from ETH base
    private static readonly BigInteger StartSupplyValue = 10000;

    private const ulong factor = 100000000; //decided by Decimals()

    public static object Main(string method, object[] args)
    {
        if (Runtime.Trigger == TriggerType.Verification)
        {
            return Runtime.CheckWitness(Oracle);
        }
        else if (Runtime.Trigger == TriggerType.Application)
        {
            var callscript = ExecutionEngine.CallingScriptHash;

            if (method == "balanceOf") return BalanceOf((byte[])args[0]);

            if (method == "decimals") return Decimals();

            if (method == "deploy") return Deploy();

            if (method == "mint") return Mint((BigInteger)args[0]);

            if (method == "name") return Name();

            if (method == "symbol") return Symbol();

            if (method == "supportedStandards") return SupportedStandards();

            if (method == "totalSupply") return TotalSupply();

            if (method == "transfer") return Transfer((byte[])args[0], (byte[])args[1], (BigInteger)args[2], callscript);
        }
        return false;
    }

    [DisplayName("balanceOf")]
    public static BigInteger BalanceOf(byte[] account)
    {
        if (account.Length != 20)
            throw new InvalidOperationException("The parameter account SHOULD be 20-byte addresses.");
        StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
        return asset.Get(account).AsBigInteger();
    }
    [DisplayName("decimals")]
    public static byte Decimals() => 8;

    private static bool IsPayable(byte[] to)
    {
        var c = Blockchain.GetContract(to);
        return c == null || c.IsPayable;
    }

    [DisplayName("mint")]
    public static bool Mint(BigInteger amount)
    {
        if (!Runtime.CheckWitness(Oracle)) return false;
        StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
        StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));

        amount = BigInteger.Multiply(amount, factor);
        BigInteger currentSupply = TotalSupply();
        BigInteger newSupply = BigInteger.Add(currentSupply, amount);
        if (newSupply > BigInteger.Multiply(MaxTotalSupplyValue, factor))
            throw new InvalidOperationException("Max supply for Peet is reached with this new supply.");

        var fromAmount = asset.Get(Oracle).AsBigInteger();
        var newAmount = BigInteger.Add(fromAmount, amount);

        asset.Put(Oracle, newAmount);
        contract.Put("totalSupply", newSupply);
        return true;
    }

    [DisplayName("deploy")]
    public static bool Deploy()
    {
        if (TotalSupply() != 0) return false;
        BigInteger factorized = BigInteger.Multiply(StartSupplyValue, factor);

        StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
        contract.Put("totalSupply", factorized);
        StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
        asset.Put(Oracle, factorized);
        Transferred(null, Oracle, factorized);
        return true;
    }

    [DisplayName("name")]
    public static string Name() => "Peet"; //name of the token

    [DisplayName("symbol")]
    public static string Symbol() => "PTE"; //symbol of the token

    [DisplayName("supportedStandards")]
    public static string[] SupportedStandards() => new string[] { "NEP-5", "NEP-7", "NEP-10" };

    [DisplayName("totalSupply")]
    public static BigInteger TotalSupply()
    {
        StorageMap contract = Storage.CurrentContext.CreateMap(nameof(contract));
        return contract.Get("totalSupply").AsBigInteger();
    }
#if DEBUG
    [DisplayName("transfer")] //Only for ABI file
    public static bool Transfer(byte[] from, byte[] to, BigInteger amount) => true;
#endif
    //Methods of actual execution
    private static bool Transfer(byte[] from, byte[] to, BigInteger amount, byte[] callscript)
    {
        //Check parameters
        if (from.Length != 20 || to.Length != 20)
            throw new InvalidOperationException("The parameters from and to SHOULD be 20-byte addresses.");
        if (amount <= 0)
            throw new InvalidOperationException("The parameter amount MUST be greater than 0.");
        if (!IsPayable(to))
            return false;
        if (!Runtime.CheckWitness(from) && from.AsBigInteger() != callscript.AsBigInteger())
            return false;
        StorageMap asset = Storage.CurrentContext.CreateMap(nameof(asset));
        var fromAmount = asset.Get(from).AsBigInteger();
        if (fromAmount < amount)
            return false;
        if (from == to)
            return true;

        //Reduce payer balances
        if (fromAmount == amount)
            asset.Delete(from);
        else
            asset.Put(from, fromAmount - amount);

        //Increase the payee balance
        var toAmount = asset.Get(to).AsBigInteger();
        asset.Put(to, toAmount + amount);

        Transferred(from, to, amount);
        return true;
    }
}