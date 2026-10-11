using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Domus.Ai;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

public static class TransferChecks
{
    public static async Task Run()
    {
        var root = Directory.CreateTempSubdirectory("domus-transfer-tests-");
        try
        {
            var sourceConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["AI_DATA_DIR"] = Path.Combine(root.FullName,"source"), ["PORTAL_PUBLIC_URL"] = "http://127.0.0.1:8088", ["AI_OPENAI_OAUTH_MODE"] = "opensource" }).Build();
            var targetConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["AI_DATA_DIR"] = Path.Combine(root.FullName,"target"), ["PORTAL_PUBLIC_URL"] = "https://domus.example.test" }).Build();
            using var source = new AiStore(DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(root.FullName,"source-keys"))),sourceConfig);
            using var target = new AiStore(DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(root.FullName,"target-keys"))),targetConfig);
            using var rsa = RSA.Create(2048); var signing = new RsaSecurityKey(rsa) { KeyId = "transfer-test" };
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Issuer="https://auth.openai.com",Audience="oaiapp_transfer",Expires=DateTime.UtcNow.AddHours(1),Claims=new Dictionary<string,object>{{"sub","selected-account"},{"email","selected@example.test"}},SigningCredentials=new(signing,SecurityAlgorithms.RsaSha256) });
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(signing); jwk.D=null;jwk.DP=null;jwk.DQ=null;jwk.P=null;jwk.Q=null;jwk.QI=null;
            var handler = new TransferProvider(JsonSerializer.Serialize(jwk)); using var http = new HttpClient(handler);
            var sourceOauth = new ChatGptOAuth(source,sourceConfig,http); var targetOauth = new ChatGptOAuth(target,targetConfig,http);
            var actor=Guid.NewGuid(); var recipient=Guid.NewGuid();
            var connection = new ChatGptConnection("oaiapp_transfer","selected-account","selected@example.test","secret-access","secret-refresh",token,DateTimeOffset.UtcNow.AddHours(1),["resource.invoke","chatgpt.tokens.use.direct"],"model");
            await source.SaveConnection(actor,connection,Guid.Empty);
            var targetHost=await target.HostId();
            var exported=JsonSerializer.SerializeToElement(await sourceOauth.Export(actor,"https://domus.example.test",default));
            var file=exported.GetProperty("file").Deserialize<ProtectedTransfer>()!;var code=exported.GetProperty("code").GetString()!;
            Check(!JsonSerializer.Serialize(file).Contains("secret-refresh"),"Credential file was not encrypted");
            Check((await source.Connection(actor)).Connection is null,"Source retained token refresh ownership");
            await Reject(()=>targetOauth.Import(recipient,new(file,new string('x',43)),default),400);
            var payload=ConnectionTransfer.Unprotect(new(file,code),"https://domus.example.test");
            await Reject(()=>Task.FromResult(ConnectionTransfer.Unprotect(new(file,code),"https://other.example.test")),403);
            var expired=ConnectionTransfer.Protect(payload with { CreatedAt=DateTimeOffset.UtcNow.AddHours(-1),ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(-30) });
            await Reject(()=>targetOauth.Import(recipient,new(expired.File,expired.Code),default),409);
            var corrupt=file with { Ciphertext=Convert.ToBase64String(new byte[20]) };
            await Reject(()=>targetOauth.Import(recipient,new(corrupt,code),default),400);
            await targetOauth.Import(recipient,new(file,code),default);
            Check((await target.Connection(recipient)).Connection!.RefreshToken=="rotated-refresh","Target did not verify and own the refreshed session");
            Check((await target.Connection(actor)).Connection is null,"Imported credentials leaked to another Domus member");
            Check(await target.HostId()==targetHost && targetHost!=await source.HostId(),"Source host ID overwrote target host identity");
            Check(!File.ReadAllText(Path.Combine(root.FullName,"target","settings.json")).Contains("rotated-refresh"),"Imported token stored unencrypted");
            await target.SaveWorkspace(Guid.NewGuid(),new(true,"openai"));
            await Reject(()=>targetOauth.Import(Guid.NewGuid(),new(file,code),default),409);
            await targetOauth.Disconnect(recipient);
            await Reject(()=>targetOauth.Import(recipient,new(file,code),default),409);
            Check(handler.Refreshes==1,"Replay rotated provider tokens again");
            Console.WriteLine("PASS: protected OAuth transfer, destination/expiry checks, source disconnect, validated refresh, retained target host ID, encrypted per-user storage and replay rejection.");
        }
        finally { root.Delete(true); }
    }
    private static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
    private static async Task Reject(Func<Task> action,int status) { try {await action();throw new Exception("Expected transfer rejection");}catch(AiError error){Check(error.Status==status,"Wrong transfer rejection status");} }
    private sealed class TransferProvider(string publicJwk) : HttpMessageHandler
    {
        public int Refreshes;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            string body;
            if(request.RequestUri!.AbsolutePath.Contains("openid-configuration")) body="""{"issuer":"https://auth.openai.com","authorization_endpoint":"https://auth.openai.com/api/accounts/authorize","token_endpoint":"https://auth.openai.com/api/accounts/oauth/token","jwks_uri":"https://auth.openai.com/.well-known/jwks.json"}""";
            else if(request.RequestUri.AbsolutePath.Contains("jwks")) body="{\"keys\":["+publicJwk+"]}";
            else {var form=await request.Content!.ReadAsStringAsync(ct);Check(form.Contains("refresh_token=secret-refresh")&&!form.Contains("client_secret"),"Wrong imported token grant");Refreshes++;body="""{"access_token":"verified-access","refresh_token":"rotated-refresh","token_type":"Bearer","expires_in":3600,"scope":"openid resource.invoke chatgpt.tokens.use.direct"}""";}
            return new(HttpStatusCode.OK){Content=new StringContent(body,System.Text.Encoding.UTF8,"application/json")};
        }
    }
}
