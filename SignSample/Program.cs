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
    static async Task<bool> DoSampleAsync(ContentFabricClient cfc, string contentTypeAddress, string libraryAddress)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        // Instantiate abi for space using user provided values for the qfab http endpoint and the base contract address 
        var qid = await cfc.CreateContent(contentTypeAddress, libraryAddress);
        Console.WriteLine("fabricID(QID) = {0}", qid);
        // Instantiate content using the new content address 
        var newContentService = new BaseContentService(cfc.web3, ContentFabricClient.BlockchainFromFabric(qid));
        // Initiate the UpdateRequest
        var res = await newContentService.UpdateRequestRequestAndWaitForReceiptAsync();
        Console.WriteLine(String.Format("transaction hash for UpdateRequest= {0}", res.TransactionHash));
        // get byte rep of transaction hash
        byte[] txhBytes = ContentFabricClient.DecodeString(res.TransactionHash);
        Dictionary<string, object> updateJson = new()
                {
                    { "spc", cfc.QspaceID },
                    { "txh", Convert.ToBase64String(txhBytes) }
                };
        // Make token accepts a dictionary to add to the json token also create a signed json transaction token atxsj_
        var token = cfc.MakeToken("atxsj_", updateJson);
        Console.WriteLine(" Token = {0}", token);
        // Call qfab's http handler and request edit on the new qid with the newly constructed token
        var ec = await cfc.CallEditContent(token, libraryAddress, qid);
        // parse json result and extract the write_token
        JObject ecValues = JObject.Parse(ec);
        var qwt = ecValues["write_token"].ToString();
        Console.WriteLine("write_token = {0}", qwt);
        // new meta to add to our content object
        string newMeta = "{\"key1\":{\"subkey1\":[\"value1\", \"value2\", \"value3\"]}}";
        // Call qfabs http handler for update meta using the newMeta to add
        await cfc.UpdateMetadata(token, libraryAddress, qwt, JObject.Parse(newMeta));
        // Call qfabs http handler to Finalize our new content
        var fin = await cfc.FinalizeContent(token, libraryAddress, qwt);
        Console.WriteLine("finalized output = {0}", fin);
        JObject finVals = JObject.Parse(fin);
        var hash = finVals["hash"].ToString();

        var decHash = ContentFabricClient.BlockchainFromFabric(hash);
        // decHash == content
        Console.WriteLine("hash = {0} dec = {1}", hash, decHash);
        // Instantiate a new Content service using the blockchain address from the hash from the return of FinalizeContent
        // Call Commit on the new qfab hash
        var commitReceipt = ContentFabricClient.Commit(newContentService, hash);
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
    static int DoSample(string pwd, string net, string contentTypeAddress, string libraryAddress)
    {
        try
        {
            ContentFabricClient cfc = new(pwd, net);
            var f = DoSampleAsync(cfc, contentTypeAddress, libraryAddress);
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
        var privateKey = app.Option("-p|--private <PRIVATE_KEY>", "The private key", CommandOptionType.SingleValue);
        var network = app.Option("-n|--network <network>", "eg -n demov3", CommandOptionType.SingleValue);
        var contentTypeAddress = app.Option("-t|--type <Type>", "Content Type address eg -t \"iq__9NTxhagnVXo3spsfBJkw3Y2dc2c\"", CommandOptionType.SingleValue);
        var LibraryAddress = app.Option("-l|--library <Library>", "Library address eg -l \"ilib2f2ES7AB6rZVvLQqBkLNqAj7GTMD\"", CommandOptionType.SingleValue);


        app.OnExecute(() =>
        {
            List<CommandOption> optionsList = new() { privateKey, network, contentTypeAddress, LibraryAddress };
            List<string> optionVals = new() { "", "", "", "" };
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

            return DoSample(optionVals[0], optionVals[1], optionVals[2], optionVals[3]);
        });

        app.Execute(args);

    }
}
