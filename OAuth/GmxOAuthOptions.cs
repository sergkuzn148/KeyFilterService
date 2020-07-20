using System.Collections.Generic;
public class GmxOAuthOptions
{
    public string AuthenticationEndpoint { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string ResourceServerSecret { get; set; }
    public Dictionary<string,string> Scopes { get; set; }
}