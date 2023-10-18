using Eluvio;
using NethereumSample.BaseContent;
using Nethereum.Contracts;
using Newtonsoft.Json.Linq;
using McMaster.Extensions.CommandLineUtils;
using Elv.NET.Contracts.BaseContentSpace;
using Elv.NET.Contracts.BaseContentSpace.ContractDefinition;

namespace SignSample;

class Program
{
    /// This example performs a number of operations in order to create content on the blockchain
    /// and to mirror those changes to the content fabric
    /// The sequence of steps are as follows
    /// Given the following input:
    ///   - private key 
    ///   - Main Net endpoint 
    ///   - contract Address
    ///   - content Type Address, 
    ///   - Library Address
    ///  1) Instantiate a BaseContentSpaceService (baseContentSpace.abi) and call CreateContent which creates new content on the blockchain
    ///  2) With new content address instantiate a BaseContentService (baseContent.abi) and make an UpdateRequest
    ///  3) Using the TransactionId from step 2 form an Eluvio Token
    ///  4) Using the new token from Step 3, call Edit on the new content
    ///  5) Using write token from step 4, form some test metadata and set it on the write token calling update meta
    ///  6) Finalize the content
    ///  7) With the has acquired from finalization, call commit on the blockchain
    static async Task<bool> DoSampleAsync(BlockchainPrimitives bcp)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var ct = bcp.contentTypeAddress;
        var libAddress = bcp.libraryAddress;
        var libid = BlockchainUtils.LibFromBlockchainAddress(libAddress);
        // Instantiate abi for space using user provided values for the qfab http endpoint and the base contract address 
        var spaceService = new BaseContentSpaceService(bcp.web3, bcp.baseContract);

        var content = await BlockchainUtils.CreateContent(spaceService, ct, libAddress);
        var qid = BlockchainUtils.QIDFromBlockchainAddress(content);
        Console.WriteLine("new content = {0} fabricID(QID) = {1}", content, qid);
        // Instantiate content using the new content address 
        var newContentService = new BaseContentService(bcp.web3, content);
        // Initiate the UpdateRequest
        var res = await newContentService.UpdateRequestRequestAndWaitForReceiptAsync();
        Console.WriteLine(String.Format("transaction hash for UpdateRequest= {0}", res.TransactionHash));
        // get byte rep of transaction hash
        byte[] txhBytes = BlockchainUtils.DecodeString(res.TransactionHash);
        Dictionary<string, object> updateJson = new()
                {
                    { "spc", BlockchainUtils.SpaceFromBlockchainAddress("0x9b29360efb1169c801bbcbe8e50d0664dcbc78d3") },
                    { "txh", Convert.ToBase64String(txhBytes) }
                };
        // Make token accepts a dictionary to add to the json token also create a signed json transaction token atxsj_
        var token = bcp.MakeToken("atxsj_", updateJson);
        Console.WriteLine(" Token = {0}", token);
        // Call qfab's http handler and request edit on the new qid with the newly constructed token
        var ec = await bcp.CallEditContent(token, libid, qid);
        // parse json result and extract the write_token
        JObject ecValues = JObject.Parse(ec);
        var qwt = ecValues["write_token"].ToString();
        Console.WriteLine("write_token = {0}", qwt);
        // new meta to add to our content object
        string newMeta = "{\"key1\":{\"subkey1\":[\"value1\", \"value2\", \"value3\"]}}";
        // Call qfabs http handler for update meta using the newMeta to add
        await bcp.UpdateMetadata(token, libid, qwt, JObject.Parse(newMeta));
        // Call qfabs http handler to Finalize our new content
        var fin = await bcp.FinalizeContent(token, libid, qwt);
        Console.WriteLine("finalized output = {0}", fin);
        JObject finVals = JObject.Parse(fin);
        var hash = finVals["hash"].ToString();

        var decHash = BlockchainUtils.BlockchainFromFabric(hash);
        // decHash == content
        Console.WriteLine("hash = {0} dec = {1}", hash, decHash);
        // Instantiate a new Content service using the blockchain address from the hash from the return of FinalizeContent
        var commitService = new BaseContentService(bcp.web3, decHash);
        // Call Commit on the new qfab hash
        var commitReceipt = BlockchainUtils.Commit(commitService, hash);
        var cpe = commitReceipt.Logs.DecodeAllEvents<CommitPendingEventDTO>();
        if (cpe.Count > 0)
        {
            Console.WriteLine("commitReceipt tx hash = {0}, tx idx {1}, hash pending {2}", commitReceipt.TransactionHash, commitReceipt.TransactionIndex, cpe[0].Event.ObjectHash);
        }
        else
        {
            Console.WriteLine("commitReceipt status = {0}, No Events", commitReceipt.Status);
        }
        return true;
    }
    static int DoSample(string pwd, string ep, string contractAddr, string contentTypeAddress, string libraryAddress)
    {
        try
        {
            BlockchainPrimitives bcp = new(pwd, ep, contractAddr, contentTypeAddress, libraryAddress);
            var f = DoSampleAsync(bcp);
            f.Wait();
        }
        catch (Exception e)
        {
            Console.WriteLine("exception {0}", e);
            return -1;
        }
        return 0;
    }
    static void Main(string[] args)
    {
        var app = new CommandLineApplication();

        app.HelpOption();

        /// Reasonable sample values are provided in the usage message
        /// The password will need to be provided at runtime to avoid leaking in code.
        var passwordOption = app.Option("-p|--pwd <PASSWORD>", "The password", CommandOptionType.SingleValue);
        var endpoint = app.Option("-e|--ep <EndPoint>", "eth endpoint eg -e https://demov3.net955210.contentfabric.io/", CommandOptionType.SingleValue);
        var contractAdress = app.Option("-c|--contract <Contract>", "Contract address eg -c \"0x9b29360efb1169c801bbcbe8e50d0664dcbc78d3\"", CommandOptionType.SingleValue);
        var contentTypeAddress = app.Option("-t|--type <Type>", "Content Type address eg -t \"0x0a5bc8d97be691970df876534a3433901fafe5d9\"", CommandOptionType.SingleValue);
        var LibraryAddress = app.Option("-l|--library <Library>", "Library address eg -l \"0x76d5287501f6d8e3b72AA34545C9cbf951702C74\"", CommandOptionType.SingleValue);


        app.OnExecute(() =>
        {
            List<CommandOption> optionsList = new() { passwordOption, endpoint, contractAdress, contentTypeAddress, LibraryAddress };
            List<string> optionVals = new() { "", "", "", "", "" };
            var iOption = 0;
            // Access the elements in the list
            foreach (var option in optionsList)
            {
                string? v = option.Value();
                if (v == null || v == "")
                {
                    Console.WriteLine("Option \"{0}\" missing", optionsList[iOption].Description);
                    return -1;
                }
                optionVals[iOption] = v;
                iOption++;
            }

            return DoSample(optionVals[0], optionVals[1], optionVals[2], optionVals[3], optionVals[4]);
        });

        app.Execute(args);

    }
}
