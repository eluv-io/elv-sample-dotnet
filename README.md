# elv-sample-dotnet


Eluvio Content Fabric .NET Client SDK

## Install .NET

https://learn.microsoft.com/en-us/dotnet/core/install/

## Build

```
dotnet build elv-sample-dotnet.sln  -c Debug
```

### Clean

```
dotnet clean elv-sample-dotnet.sln
```

## Run Sample

Information needed:

- A tenancy on the Conent Fabric network 'main' or 'demov3'
- A private key for a user within the tenancy
- A Content Library (for example `ilib2f2ES7AB6rZVvLQqBkLNqAj7GTMD`)
- A Content Type (for example the tenant 'Title' content type)

```
./SampleContentOps/bin/Debug/net6.0/SampleContentOps -n "demov3" -p "private_key" -t "iq__9NTxhagnVXo3spsfBJkw3Y2dc2c" -l "ilib2f2ES7AB6rZVvLQqBkLNqAj7GTMD"
``````

Output

```
content object ID = iq__2ygmMWogfVPasM1SS6bYTCwL3MPQ

transaction hash for UpdateRequest= 0xbed5dbbd5581cbc23d959a3a94979d9b8692246f3e6ca82d0fe2b1e59ef76037

content fabric 'edit' access token = atxsj_p4KoaRUXxn8ueSTCgv9mBEMSzSzb5MVEUtTmpo7eTWdiqaGM9okaxcrTY5LLym7JrqAbzNuUGXWubYodu91s3ZHxK9EohAHZy6M2EzUjM5C8M5hzNf47oHWGWrJURfpZdgBNz9HsmxohXcvPu5VK6suvMEoYVzMCPHUSXGm2XemrDU68HgCpm4TuW9YxES5ex8nEzyEFrQtBNX81jitMMKrXGfCj8M7QiWXZioGLiMwW7hvDHBoJe7HuBtKLiyiG7z6WUQhCtVnjZWMbZvy9bdfFcg

content write token = tqw__HSVbQbmSxKV3QLUimLePhg8E8q6w4cq7cWSPgcbvHDCzWj5GDVZJAVSe8XzNwfoghLDu2FdxDMfbX4H3duv

new content hash = hq__6wFJdbSm4DdM5hotjudxhYgJ2NQLnXrCacfcRNaYzLvxBhXkdQEXizVcd2Z1ochYzNwfAxnoX8

commit tx hash = 0x54c095e909b50f477a0783cc12314cef44a2dd534627b3499b903195c558fecd, hash pending hq__6wFJdbSm4DdM5hotjudxhYgJ2NQLnXrCacfcRNaYzLvxBhXkdQEXizVcd2Z1ochYzNwfAxnoX8
```

A new content is created in the specified content library. The new content ID and hash are printed out by the sample.
