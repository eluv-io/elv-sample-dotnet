﻿using Nethereum.Web3;
using NethereumSample.BaseContent;
using Nethereum.Signer;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Net.Http.Headers;
using System.Text;
using SimpleBase;
using Nethereum.Util;
using Nethereum.Model;
using ADRaffy.ENSNormalize;
using Elv.NET.Contracts.BaseContentSpace;
using Elv.NET.Contracts.BaseContentSpace.ContractDefinition;
using Nethereum.Contracts;
using Newtonsoft.Json.Linq;


namespace Eluvio
{
    /// HttpHelper is a utility class that will provide a seamless wrapper around a Node config call selecting the first entry to use
    /// There are static methods to GET POST and PUT various urls as the qfab api expects
    public class HttpHelper
    {
        private static readonly Dictionary<string, string> networks = new()
        {
            {"main", "https://main.net955305.contentfabric.io/config"},
            {"demov3", "https://demov3.net955210.contentfabric.io/config"}
        };
        delegate Task<HttpResponseMessage> PostCallDelegate(HttpClient client, string url, string token, HttpContent content);
        public HttpHelper(string network)
        {
            client = new();
            if (network != "main" && network != "demov3")
            {
                network = "demov3";
            }
            var mainNet = HttpHelper.networks[network];
            //
            var restResult = CallGet(client, mainNet, "");
            restResult.Wait();
            JObject jsonObject = JObject.Parse(restResult.Result);
            currentNode = jsonObject["network"]["seed_nodes"]["fabric_api"][0].ToString();
            EthURL = jsonObject["network"]["seed_nodes"]["ethereum_api"][0].ToString();
            QspaceID = jsonObject["qspace"]["id"].ToString();
        }
        private static async Task<string> CallHttp(HttpClient client, string url, string token, HttpContent content, JObject metadata, Delegate delCall)
        {
            // Set the authentication token in the request headers
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);


            // Create the request content with the metadata
            if (metadata != null)
            {
                content = new StringContent(metadata.ToString());
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            }

            // Send the POST request to the specified URL

            HttpResponseMessage response = await (Task<HttpResponseMessage>)delCall.DynamicInvoke(client, url, token, content);

            // Handle the response
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                return "";
            }
        }
        public static async Task<string> CallPost(HttpClient client, string url, string token, HttpContent content, JObject metadata)
        {
            PostCallDelegate postDelegate = async (HttpClient client, string url, string token, HttpContent content) =>
            {
                HttpResponseMessage response = await client.PostAsync(url, content);
                return response;
            };
            return await CallHttp(client, url, token, content, metadata, postDelegate);

        }

        public static async Task<string> CallPut(HttpClient client, string url, string token, HttpContent content, JObject metadata)
        {
            PostCallDelegate putDelegate = async (HttpClient client, string url, string token, HttpContent content) =>
            {
                HttpResponseMessage response = await client.PutAsync(url, content);
                return response;
            };
            return await CallHttp(client, url, token, content, metadata, putDelegate);
        }

        public static async Task<string> CallGet(HttpClient client, string url, string token)
        {
            PostCallDelegate getDelegate = async (HttpClient client, string url, string token, HttpContent content) =>
            {
                HttpResponseMessage response = await client.GetAsync(url);
                return response;
            };
            return await CallHttp(client, url, token, null, null, getDelegate);
        }

        private string GetBaseURL(string token, string libid, string qid, string format)
        {
            var url = currentNode + String.Format(format, libid, qid);
            return url;
        }
        public async Task<string> CallEditContent(string token, string libid, string qid)
        {
            string url = GetBaseURL(token, libid, qid, "/qlibs/{0}/q/{1}");
            return await CallPut(client, url, token, null, new());
        }

        public async Task<string> CallGetMetadata(string token, string libid, string qid)
        {
            string url = GetBaseURL(token, libid, qid, "/qlibs/{0}/q/{1}/meta");
            return await CallGet(client, url, token);
        }
        public async Task<string> UpdateMetadata(string token, string libid, string qid, JObject jsonUpdate)
        {
            string url = GetBaseURL(token, libid, qid, "/qlibs/{0}/q/{1}/meta");
            return await CallPut(client, url, token, null, jsonUpdate);
        }
        public async Task<string> FinalizeContent(string token, string libid, string qwt)
        {
            string url = GetBaseURL(token, libid, qwt, "/qlibs/{0}/q/{1}");
            return await CallPost(client, url, token, null, new());
        }

        private readonly string currentNode;
        public string EthURL { get; set; }
        private readonly HttpClient client;

        public string QspaceID { get; set; }


    }

    public class ContentFabricClient : HttpHelper
    {
        private void CommonConstruct()
        {
            account = new Nethereum.Web3.Accounts.Account(this.Key);
            web3 = new Web3(this.account, EthURL);
            baseContract = ContentFabricClient.BlockchainFromFabric(QspaceID);
            // this.contentTypeAddress = ContentFabricClient.BlockchainFromFabric(contentTypeAddress);
            // this.libraryAddress = ContentFabricClient.BlockchainFromFabric(libraryAddress);
            this.spaceService = new BaseContentSpaceService(web3, baseContract);

        }
        public ContentFabricClient(string mainNet) : base(mainNet)
        {
            Key = EthECKey.GenerateKey().GetPrivateKeyAsBytes().ToHex();
            CommonConstruct();
        }
        public ContentFabricClient(string key, string mainNet) : base(mainNet)
        {
            Key = key;
            CommonConstruct();
        }



        public string MakeToken(string prefix, Dictionary<string, object> jsonToken)
        {
            var ethECKey = new EthECKey(Key);
            jsonToken.Add("adr", ethECKey.GetPublicAddressAsBytes());
            var tok = System.Text.Json.JsonSerializer.Serialize(jsonToken);
            var strToken = tok;
            byte[] hashedBytes = ContentFabricClient.DecodeString(new Sha3Keccack().CalculateHash(strToken));

            byte[] signature = Array.Empty<byte>();
            if (prefix[3] == 's')
            {
                signature = ContentFabricClient.SignMessage(ethECKey, hashedBytes);
            }
            // // Signing
            byte[] concat = signature.Concat(Encoding.UTF8.GetBytes(strToken)).ToArray();

            string signatureString = prefix + Base58.Bitcoin.Encode(concat);

            return signatureString;

        }

        public string Key { get; private set; }
        public Nethereum.Web3.Accounts.Account account;
        public Web3 web3;
        public string baseContract;

        public BaseContentSpaceService spaceService;


        public static string FabricIdFromBlckchainAdress(string prefix, string bcAdress)
        {
            if (bcAdress[..2] == "0x")
            {
                bcAdress = bcAdress[2..];
            }
            return prefix + Base58.Bitcoin.Encode(DecodeString(bcAdress));
        }

        public static ulong DecodeUvarint(byte[] data, out int bytesRead)
        {
            ulong value = 0;
            int shift = 0;
            bytesRead = 0;

            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                value |= (ulong)(b & 0x7F) << shift;
                shift += 7;
                bytesRead++;

                if ((b & 0x80) == 0)
                {
                    return value;
                }
            }

            throw new ArgumentException("Invalid variable-length encoded integer.");
        }
        public static string BlockchainFromFabric(string fabAdress)
        {
            fabAdress = fabAdress[4..];
            var hashBytes = Base58.Bitcoin.Decode(fabAdress);
            if (hashBytes.Length == 20)
            {
                return EncodeBytes(hashBytes);
            }
            DecodeUvarint(hashBytes[32..], out int bytesRead);
            var idOffset = 32 + bytesRead;
            var id = hashBytes[idOffset..];
            return EncodeBytes(id);
        }


        public static string QIDFromBlockchainAddress(string bcAdress)
        {
            return FabricIdFromBlckchainAdress("iq__", bcAdress);
        }
        public static string SpaceFromBlockchainAddress(string bcAdress)
        {
            return FabricIdFromBlckchainAdress("ispc", bcAdress);
        }
        public static string LibFromBlockchainAddress(string bcAdress)
        {
            return FabricIdFromBlckchainAdress("ilib", bcAdress);
        }


        public static byte[] DecodeString(string val)
        {
            if (val[0] == '0' && val[1] == 'x')
            {
                val = val[2..];
            }
            return Enumerable.Range(0, val.Length)
                     .Where(x => x % 2 == 0)
                     .Select(x => Convert.ToByte(val.Substring(x, 2), 16))
                     .ToArray();
        }

        public static string EncodeBytes(byte[] bytes)
        {
            string hexString = BitConverter.ToString(bytes).Replace("-", "");
            return "0x" + hexString;
        }

        public static byte[] SignMessage(EthECKey key, byte[] hashedBytes)
        {
            var signer = new EthereumMessageSigner();
            var ethECDSASignature = signer.SignAndCalculateV(hashedBytes, key);
            var signed = ethECDSASignature.CreateStringSignature();
            var decoded = DecodeString(signed);
            return decoded;
        }

        public async Task<string> CreateContentType()
        {
            var ct = await spaceService.CreateContentTypeRequestAndWaitForReceiptAsync();
            var cctReceipt = ct.DecodeAllEvents<CreateContentTypeEventDTO>();
            return cctReceipt[0].Event.ContentTypeAddress;
        }

        public async Task<string> CreateLibrary(string space)
        {
            // should be using space "0x501382E5f15501427D1Fc3d93e949C96b25A2224"
            var lib = await spaceService.CreateLibraryRequestAndWaitForReceiptAsync(space);
            var libReceipt = lib.DecodeAllEvents<CreateLibraryEventDTO>();
            return libReceipt[0].Event.LibraryAddress;
        }

        public async Task<string> CreateContent(string contentTypeAddress, string libraryAddress)
        {
            var content = await spaceService.CreateContentRequestAndWaitForReceiptAsync(ContentFabricClient.BlockchainFromFabric(libraryAddress), ContentFabricClient.BlockchainFromFabric(contentTypeAddress));
            var contentReceipt = content.DecodeAllEvents<CreateContentEventDTO>();
            return ContentFabricClient.QIDFromBlockchainAddress(contentReceipt[0].Event.ContentAddress);
        }
        public static Nethereum.RPC.Eth.DTOs.TransactionReceipt UpdateRequest(BaseContentService contentService)
        {
            var res = contentService.UpdateRequestRequestAndWaitForReceiptAsync();
            res.Wait();
            return res.Result;
        }

        public static Nethereum.RPC.Eth.DTOs.TransactionReceipt AccessRequest(BaseContentService contentService)
        {
            var res = contentService.AccessRequestRequestAndWaitForReceiptAsync(0, "", "", new List<Byte[]>(), new List<string>());
            res.Wait();
            return res.Result;
        }
        public static Nethereum.RPC.Eth.DTOs.TransactionReceipt Commit(BaseContentService commitServ, string hash)
        {
            var res = commitServ.CommitRequestAndWaitForReceiptAsync(hash);
            res.Wait();
            return res.Result;
        }

    }

}
