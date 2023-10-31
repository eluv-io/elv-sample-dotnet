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
    /// This example performs a number of operations in order to illustrate the bascis of creating and
    /// updating a 'content object' in the Content Fabric.
    ///
    /// Input:
    ///   - pivate key for a user in the tenancy
    ///   - library ID
    ///   - content type ID
    ///
    /// Steps performed:
    ///   1) Create a new content object
    ///   2) Perform an UpdateRequest transaction - this is required to create an 'edit' access token
    ///   3) Create an 'edit' access token
    ///   4) Edit the content object - this returns a 'write token'
    ///   5) Set content object metadata
    ///   6) Finalize the content - this returns a new content hash
    ///   7) Commit the new content hash
    static async Task<bool> DoSampleAsync(ContentFabricClient cfc, string contentTypeAddress, string libraryAddress)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Create a new Content Object in the specified Content Library
        var qid = await cfc.CreateContent(contentTypeAddress, libraryAddress);
        Console.WriteLine("content object ID = {0}", qid);

        // Perform an 'update request' transaction, needed for creating a content fabric 'edit' acccess token
        var newContentService = new BaseContentService(cfc.web3, ContentFabricClient.BlockchainFromFabric(qid));
        var res = await newContentService.UpdateRequestRequestAndWaitForReceiptAsync();
        Console.WriteLine(String.Format("transaction hash for UpdateRequest= {0}", res.TransactionHash));

        // Make the content fabric 'edit' access token using the transaction hash obtained above
        byte[] txhBytes = ContentFabricClient.DecodeString(res.TransactionHash);
        Dictionary<string, object> updateJson = new()
                {
                    { "spc", cfc.QspaceID },
                    { "txh", Convert.ToBase64String(txhBytes) }
                };
        var token = cfc.MakeToken("atxsj_", updateJson);
        Console.WriteLine("content fabric 'edit' access token = {0}", token);

        // Open the Content Object for writing - this returns a 'content write token'
        var ec = await cfc.CallEditContent(token, libraryAddress, qid);
        JObject ecValues = JObject.Parse(ec);
        var qwt = ecValues["write_token"].ToString();
        Console.WriteLine("content write token = {0}", qwt);

        // Prepare sample JSON metadata to write to the Content Object
        string newMeta = "{\"key1\":{\"subkey1\":[\"value1\", \"value2\", \"value3\"]}}";

        // Write JSON metadata to the Content Object
        await cfc.UpdateMetadata(token, libraryAddress, qwt, JObject.Parse(newMeta));

        // Finalize the content write token - this creates a new content hash (a new 'version' of the content)
        var fin = await cfc.FinalizeContent(token, libraryAddress, qwt);
        JObject finVals = JObject.Parse(fin);
        var hash = finVals["hash"].ToString();
        Console.WriteLine("new content hash = {0}", hash);

        // Commit the new content hash
        var commitReceipt = ContentFabricClient.Commit(newContentService, hash);
        var cpe = commitReceipt.Logs.DecodeAllEvents<CommitPendingEventDTO>();
        if (cpe.Count > 0)
        {
            Console.WriteLine("commit tx hash = {0}, hash pending {1}", commitReceipt.TransactionHash, cpe[0].Event.ObjectHash);
        }
        else
        {
            Console.WriteLine("commit failed - no events, status = {0}", commitReceipt.Status);
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

        var privateKey = app.Option("-p|--private <PRIVATE_KEY>", "The private key", CommandOptionType.SingleValue);
        var network = app.Option("-n|--network <network>", "Content Fabric network: main or demov3", CommandOptionType.SingleValue);
        var contentTypeAddress = app.Option("-t|--type <Type>", "Content Type address eg \"iq__9NTxhagnVXo3spsfBJkw3Y2dc2c\"", CommandOptionType.SingleValue);
        var LibraryAddress = app.Option("-l|--library <Library>", "Content Library address eg \"ilib2f2ES7AB6rZVvLQqBkLNqAj7GTMD\"", CommandOptionType.SingleValue);


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
