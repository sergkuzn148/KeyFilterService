using System.Threading.Tasks;
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net;


namespace Gmx.OAuth {

    public class HexString
    {
        public static string XOR(string hex, string key)
        {
            byte[] a1 = HexString.ToByteArray(hex);
            //byte[] a2 = HexString.ToByteArray(key);
            for (int c = 0; c < a1.Length; c++)
                a1[c] = (byte)(a1[c] ^ (byte)key[c % key.Length]);
            return HexString.FromByteArray(a1);
        }

        public static byte[] ToByteArray(string hex)
        {
            if (hex == null || hex.Length % 2 != 0)
            {
                // input string length is not evenly divisible by 2
                return null;
            }

            byte[] binary = new byte[hex.Length / 2];

            for (int i = 0; i < binary.Length; i++)
            {
                int highNibble = HexToInt(hex[2 * i]);
                int lowNibble = HexToInt(hex[2 * i + 1]);

                if (highNibble == -1 || lowNibble == -1)
                {
                    return null; // bad hex hex
                }
                binary[i] = (byte)((highNibble << 4) | lowNibble);
            }

            return binary;
        }
        public static string FromByteArray(byte[] arr)
        {
            if (arr == null)
            {
                return null;
            }
            char[] hex = new char[checked(arr.Length * 2)];
            for (int i = 0; i < arr.Length; i++)
            {
                byte thisByte = arr[i];
                hex[2 * i] = NibbleToHex((byte)(thisByte >> 4)); // high nibble
                hex[2 * i + 1] = NibbleToHex((byte)(thisByte & 0xf)); // low nibble
            }
            return new string(hex);
        }
        private static int HexToInt(char h)
        {
            return (h >= '0' && h <= '9') ? h - '0' :
            (h >= 'a' && h <= 'f') ? h - 'a' + 10 :
            (h >= 'A' && h <= 'F') ? h - 'A' + 10 :
            -1;
        }
        // converts a nibble (4 bits) to its uppercase hexadecimal character representation [0-9, A-F]
        private static char NibbleToHex(byte nibble)
        {
            return (char)((nibble < 10) ? (nibble + '0') : (nibble - 10 + 'A'));
        }
    }

    public class GmxOAuth {
        private readonly RequestDelegate _next;
        private readonly GmxOAuthOptions _options;
        private static readonly HttpClient _httpClient  = new HttpClient();
        private OAuthUserInfo _userInfo;
        private readonly IMemoryCache _cache;               
        
        public GmxOAuth(RequestDelegate next, IMemoryCache cache, IOptions<GmxOAuthOptions> options)
        {
            this._next = next;
            this._cache = cache;
            this._options = options.Value;            
        }

        OAuthUserInfo GetLoginStatus(HttpContext context) {
            if (AcceptBearer(context) ||     // Токен в хедере Authorization
                AcceptTokenParam(context) || // Токен в параметре запроса
                AcceptTicket(context)        // Авторизационные данные в куке
            )
            {
                return _userInfo;
            }
            else {
                return null;
            }                
        }

        #region Handlers
        
        async Task Login(HttpContext context)
        {
            // Continue Session (Part 2.)             
            if (AcceptToken(context))
            {
                var result = JObject.FromObject(
                    new
                    {
                        Status = "ok",
                        Service = new { ServerId = "Catalog" },
                        Result = new { Login = (string)null }
                    }
                );

                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(result.ToString());
            }
            var ui = GetLoginStatus(context);
            if (ui != null)
            {
                var result = JObject.FromObject(
                    new
                    {
                        Status = "ok",
                        Service = new { ServerId = "Catalog" },
                        Result = new { Login = ui.Email }
                    }
                );
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(result.ToString());
            }
            else
            {
                var result = JObject.FromObject(
                    new
                    {
                        Status = "ok",
                        Service = new { ServerId = "Catalog" },
                        Result = new { Login = (string)null }
                    }
                );
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(result.ToString());
            }
        }
        async Task LoginDialog(HttpContext context)
        {
            var antiscrf = context.Request.Query["sync"].ToString();
            if (context.Request.Cookies.ContainsKey(antiscrf))
            {
                var v = context.Request.Cookies[antiscrf];
                context.Response.Cookies.Append(antiscrf, v, new CookieOptions { Expires = DateTime.Now.AddDays(-1) });  // prev auth cookie
            }

            string state = Guid.NewGuid().ToString("N");
            string sid;
            if (context.Request.Cookies["sid"] == null)
            {
                sid = Guid.NewGuid().ToString("N");
                context.Response.Cookies.Append("sid", sid, new CookieOptions { HttpOnly = true, Expires = DateTime.Now.AddDays(1) });
            }
            else
            {
                sid = context.Request.Cookies["sid"];
            }

            context.Response.Headers["Cache-Control"] = "public";
            context.Response.Headers["Expires"] = DateTime.Now.AddMinutes(15).ToString("r", System.Globalization.CultureInfo.InvariantCulture);

            var result = JObject.FromObject(
                new
                {
                    Status = "ok",
                    Service = new { ServerId = "Catalog" },
                    Result = new { State = state }
                }
            );
            var cb = context.Request.Query["CallbackName"].ToString();
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(string.Format("{0}({1})", cb, result.ToString()));
        }
        async Task OAuthCallback(HttpContext context)
        {
            string returnUrl = context.Request.Query["return_url"];
            string code = context.Request.Query["code"];
            string old = context.Request.Cookies["sync"];
            string state = context.Request.Query["state"];
            string host = string.Format("{0}://{1}", context.Request.Scheme, context.Request.Host);
            string path = context.Request.Path.ToString();
            string cb = context.Request.Query["CallbackName"].ToString();
            if (!String.IsNullOrEmpty(returnUrl))
            {
                await ReturnTo(context, returnUrl);
            }
            else
            {
                string anticsrf = Guid.NewGuid().ToString("N");

                if (old == null)
                {
                    old = "";
                }

                try
                {
                    var tempToken = GetToken(this._options, code);
                    string[] scopes = tempToken.ClientData.Split(',');
                    for (int i = 0; i < scopes.Length; ++i)
                    {
                        string resourceServer = this._options.Scopes[scopes[i].ToLower()];
                        if (!resourceServer.StartsWith(host) && !path.StartsWith(resourceServer))
                        {
                            // Кросcдоменное обращение
                            var tokenEndpoint = new Uri(string.Format("{0}/oAuth2/Login.ashx", resourceServer));
                            var request = (HttpWebRequest)HttpWebRequest.Create(string.Format("{0}?wrapstyle=none&sync={1}&state={2}&old={3}", tokenEndpoint.OriginalString, anticsrf, state, old));
                            request.Headers.Add("Authorization", string.Format("Bearer {0}", tempToken.TokenString));
                            using (var resp = request.GetResponse())
                            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                            {
                                sr.ReadToEnd();
                            }
                        }
                        else
                        {
                            // Один домен - переход на 1-й этап авторизации                         
                            AcceptToken(tempToken.TokenString, state, anticsrf, old);
                        }
                    }
                    if (tempToken != null)
                    {
                        context.Response.Cookies.Append("sync", anticsrf, new CookieOptions { Expires = tempToken.Expires, Path = "/" });
                    }

                    var ui = GetUserInfo(this._options.AuthenticationEndpoint, tempToken.TokenString);

                    //await PostAuthorize (context, ui.ID);

                    var claims = new List<Claim> {
                        new Claim(ClaimsIdentity.DefaultNameClaimType, ui.ID)
                    };
                    // создаем объект ClaimsIdentity
                    var id = new ClaimsIdentity(claims, "ApplicationCookie", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
                    // установка аутентификационных куки
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));

                    var result = JObject.FromObject(new
                    {
                        Status = "ok",
                        Result = new
                        {
                            Login = ui.Email,
                            FullName = ui.FullName,
                            Email = ui.Email,
                            Organization = ui.Organization,
                            Position = ui.Position,
                            Nickname = ui.Login,
                            Phone = ui.Phone,
                            Server = ui.Server
                        }
                    });

                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(string.Format("{0}({1})", cb, result.ToString()));
                }
                catch (Exception e)
                {
                    if (!String.IsNullOrEmpty(returnUrl))
                    {
                        await ReturnTo(context, returnUrl);
                    }
                    else
                    {
                        var error = new
                        {
                            Status = "error",
                            ErrorInfo = new
                            {
                                ErrorMessage = e.Message,
                                ExceptionType = e.GetType().ToString(),
                                StackTrace = e.StackTrace
                            }
                        };
                        await context.Response.WriteAsync(JObject.FromObject(error).ToString());
                    }
                }
            }
        }
        async Task OAuthCallbackHtml(HttpContext context)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(@"<html>
                <head>
                    <script>
                        var target = opener || parent;
                        try {
                            target.authorizationGrant(window.location.search);
                        }
                        catch(e) {
                            console.log(e);
                        }
                        if (opener) {
                            window.close();
                        }                            
                    </script>
                </head>
                <body>
                </body>
                </html>");
        }

        async Task Logout(HttpContext context) {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); //здесь () было пуcто
            RevokePermission(context);
            var result = JObject.FromObject(
                new
                {
                    Status = "ok",                    
                    Result = new { Login = (string)null }
                }
            );
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(result.ToString());
        }

        #endregion

        #region Helpers

        static byte[] GetBytesFromUrl(string url)
        {
            var buff = new byte[4096];
            var rq = (HttpWebRequest)HttpWebRequest.Create(url);
            using (var rs = rq.GetResponse())
            using (var str = rs.GetResponseStream())
            using (var ms = new MemoryStream())
            {
                int count = 0;
                do
                {
                    count = str.Read(buff, 0, buff.Length);
                    ms.Write(buff, 0, count);
                } while (count != 0);
                return ms.ToArray();
            }
        }

        #endregion

        void RevokePermission (HttpContext context)
        {
            string antiCsrfParam = GetAntiCsrfParam(context);
            if (!String.IsNullOrEmpty(antiCsrfParam) && context.Request.Cookies[antiCsrfParam + "abc123"] != null)
            {
                if (this._cache.Get(antiCsrfParam) != null)
                {
                    this._cache.Remove(this._cache.Get(antiCsrfParam));
                    this._cache.Remove(antiCsrfParam);
                }
            }
            foreach (var k in context.Request.Cookies.Keys.Where(k => k.EndsWith("abc123"))) {                
                context.Response.Cookies.Append(k, context.Request.Cookies[k] , new CookieOptions { Expires = DateTime.Now.AddDays(-1) });
            }
        }
    
        static Token GetToken(GmxOAuthOptions options, string code)
        {
            var url = string.Format ("{0}/oAuth/AccessToken?client_id={1}&client_secret={2}&code={3}", options.AuthenticationEndpoint, options.ClientId, options.ClientSecret, code);
            var tokenStream = GetBytesFromUrl(url);
            var oResult = ParseResponse(tokenStream);
            var dtExpires = oResult["expires"].Value<DateTime>();
            return new Token(oResult["access_token"].Value<string>(), dtExpires) { 
                ClientData = oResult["scope"].Value<string>()
            };
        }
        static JToken ParseResponse(byte[] responseStream)
        {
            string json = Encoding.UTF8.GetString(responseStream);
            var response = JObject.Parse(json);
            if (response["Status"].Value<string>() == "Error") {
                throw new Exception(response["Result"]["Message"].Value<string>());
            }
            return response["Result"];
        }
        static string GetStateParam(HttpContext context)
        {
            return context.Request.Query["state"];
        }
        public bool AcceptToken(HttpContext context)
        {
            string tokenString = GetBearer (context);
            string state = GetStateParam(context);
            string anticsrf = GetAntiCsrfParam(context);
            if (!String.IsNullOrEmpty(tokenString) && state != string.Empty && !String.IsNullOrEmpty(anticsrf))
            {
                AcceptToken(tokenString, state, anticsrf, context.Request.Query["old"]);
                return true;
            }
            return false;
        }
        public bool AcceptToken(string token, string states, string anticsrf, string old)
        {
            string[] a = states.Split(',');
            foreach (string s in a)
            {
                string state = s.Trim();

                string sid = this._cache.Get(state) as string;
                if (!String.IsNullOrEmpty(sid) && this._cache.Get(sid) is ConcurrentDictionary<string, string>)
                {
                    var d = this._cache.Get(sid) as ConcurrentDictionary<string, string>;
                    d[anticsrf] = token;
                    if (!String.IsNullOrEmpty(old))
                    {
                        d[old] = "old";
                    }
                    this._cache.Remove(state);
                }
            }
            return false;
        }
        static OAuthUserInfo ParseUserInfo(byte[] userInfoStream)
        {
            var oResult = ParseResponse(userInfoStream);

            string sID = oResult["ID"].Value<string>();
            string EMail = oResult["Email"].Value<string>();
            string Login = oResult["Login"].Value<string>();
            string FullName = oResult["FullName"].Value<string>();
            string Phone = oResult["Phone"].Value<string>();
            string Organization = oResult["Organization"].Value<string>();
            string Position = oResult["Position"].Value<string>();
            string Role = oResult["Role"].Value<string>();
            string clientId = oResult["ClientId"].Value<string>();
            DateTime? tokenExpires = oResult["TokenExpires"].Value<DateTime>();
            string scope = oResult["Scope"].Value<string>();
            var authServer = new StringBuilder(string.IsNullOrEmpty (oResult["Server"].Value<string>()) ? "MyKosmosnimki" : oResult["Server"].Value<string>());
            authServer[0] = char.ToUpper(authServer[0]);
            return new OAuthUserInfo 
            {
                ID = sID,
                Email = EMail,
                Login = Login,
                Server = (OAuthServers)Enum.Parse(typeof(OAuthServers), authServer.ToString()),
                FullName = FullName,
                Phone = Phone,
                Organization = Organization,
                Position = Position,
                Role = Role,
                ClientId = clientId,
                TokenExpires = tokenExpires,
                Scope = scope
            };
        }
        static string GetSessionIdParam(HttpContext context) {
            return context.Request.Cookies["sid"] != null ? context.Request.Cookies["sid"] : "";
        }
        static OAuthUserInfo GetUserInfo(string authenticationEndpoint, string access_token)
        {            
            string url = string.Format("{0}/Handler/Me?token={1}", authenticationEndpoint, access_token);
            var tokenStream = GetBytesFromUrl (url);
            return ParseUserInfo(tokenStream);
        }
        static async Task ReturnTo (HttpContext context, string returnUrl) {
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(string.Format ("<script>location.href='{0}'</script>", returnUrl));
        }                       
        string GetBearer(HttpContext context) {
            string authheader = context.Request.Headers["Authorization"];
            if (!String.IsNullOrEmpty(authheader) && authheader.ToLower().StartsWith("bearer "))
                return authheader.Substring("Bearer ".Length);
            else
                return String.Empty;
        }
        bool AcceptBearer(HttpContext context) {
            string accessToken = GetBearer (context);
            if (accessToken != String.Empty && !accessToken.StartsWith("temp"))
            {
                // Информация о владельце
                _userInfo = this._cache.Get<OAuthUserInfo>(accessToken);
                if (_userInfo == null)
                {
                    _userInfo = GetUserInfo(this._options.AuthenticationEndpoint, accessToken);
                    this._cache.Set<OAuthUserInfo>(accessToken, _userInfo, new TimeSpan(1, 0, 0));
                }
                return true;
            }
            return false;
        }
        bool AcceptTokenParam(HttpContext context) {
            string accessToken = context.Request.Query["access_token"];
            if (!String.IsNullOrEmpty(accessToken))
            {
                // Информация о владельце
                _userInfo = this._cache.Get<OAuthUserInfo>(accessToken);
                if (_userInfo == null)
                {
                    _userInfo = GetUserInfo(this._options.AuthenticationEndpoint, accessToken);
                    this._cache.Set<OAuthUserInfo>(accessToken, _userInfo, new TimeSpan(1, 0, 0));
                }
                return true;
            }
            return false;
        }
        string GetAntiCsrfParam (HttpContext context) {
            return context.Request.Query["sync"].ToString() != null ?
                context.Request.Query["sync"].ToString() : context.Request.Form["sync"].ToString();
        }
        bool AcceptTicket(HttpContext context) {
            string antiCsrfParam = GetAntiCsrfParam(context);
            string sid = context.Request.Cookies[string.Concat(antiCsrfParam, "abc123")] != null ? context.Request.Cookies[string.Concat(antiCsrfParam, "abc123")] : "";
            if (!String.IsNullOrEmpty(antiCsrfParam) && sid.Length > antiCsrfParam.Length)
            {
                string add = this._options.ResourceServerSecret;
                if (String.IsNullOrEmpty(add)) {
                    add = context.Request.Host.Value;
                }                    
                string accessToken = HexString.XOR(sid, add + antiCsrfParam);
                // Информация о владельце
                _userInfo = this._cache.Get<OAuthUserInfo>(accessToken);
                if (_userInfo == null)
                {
                    _userInfo = GetUserInfo(this._options.AuthenticationEndpoint, accessToken);
                    this._cache.Set<OAuthUserInfo>(accessToken, _userInfo, new TimeSpan(1, 0, 0));
                }
                return true;
            }
            return false;
        }
        void CreateAuthTicket(HttpContext context, string accessToken)
        {

            //foreach (var prev in Request.Cookies.AllKeys)
            //    if (prev.EndsWith("abc123"))
            //        Response.Cookies[prev].Expires = DateTime.Now.AddDays(-1);
            string sessionIdParam = GetSessionIdParam(context);
            var d = this._cache.Get<ConcurrentDictionary<string,string>>(sessionIdParam);
            if (d != null)
            {
                foreach (var prev in context.Request.Cookies.Keys) {
                    if (d.ContainsKey(prev.Replace("abc123", "")))
                    {
                        context.Response.Cookies.Append(prev, context.Request.Cookies[prev], new CookieOptions { Expires = DateTime.Now.AddDays(-1) });
                    }
                }                                            
                d.Where(kv => kv.Value == "old").Select(kv => kv.Key).ToList().ForEach(k => {
                    string i;
                    d.TryRemove(k, out i);
                });
            }

            string add = this._options.ResourceServerSecret;
            if (String.IsNullOrEmpty(add)) {
                add = context.Request.Host.Value;
            }
            string antiCsrfParam = GetAntiCsrfParam(context);
            context.Response.Cookies.Append(string.Concat(antiCsrfParam, "abc123"), HexString.XOR(accessToken, string.Concat(add, antiCsrfParam)), new CookieOptions { Expires = (DateTime)_userInfo.TokenExpires, HttpOnly = true });
        }
        bool AuthorizeSession(HttpContext context) {
            string sessionIdParam = GetSessionIdParam(context);
            string antiCsrfParam = GetAntiCsrfParam(context);
            if (!String.IsNullOrEmpty(antiCsrfParam) && !String.IsNullOrEmpty(sessionIdParam))
            {
                var d = this._cache.Get<ConcurrentDictionary<string,string>> (sessionIdParam);
                if (d != null)
                {
                    string accessToken = "";
                    if (d.ContainsKey(antiCsrfParam))
                    {
                        accessToken = d[antiCsrfParam];
                    }
                    if (accessToken != "")
                    {
                        this._cache.Set<string>(antiCsrfParam, accessToken);

                        // Информация о владельце
                        _userInfo = this._cache.Get<OAuthUserInfo>(accessToken);
                        if (_userInfo == null)
                        {
                            _userInfo = GetUserInfo(this._options.AuthenticationEndpoint, accessToken);
                            this._cache.Set<OAuthUserInfo>(accessToken, _userInfo, new TimeSpan (1, 0, 0));
                        }
                        CreateAuthTicket(context, accessToken);

                        string i;
                        d.TryRemove(antiCsrfParam, out i);
                        return true;
                    }
                }
            }
            return false;
        }
        async Task PostAuthorize (HttpContext context, string userId) {
            try
            {                
               if (
                   AcceptBearer(context) ||  // Токен в хедере Authorization
                   AcceptTokenParam(context) || // Токен в параметре запроса
                   AcceptTicket(context) ||  // Авторизационные данные в куке
                   AuthorizeSession(context) // Авторизационные данные храняться на сервере
               )
               {                   
                    var claims = new List<Claim> {
                        new Claim(ClaimsIdentity.DefaultNameClaimType, userId)
                    };
                    // создаем объект ClaimsIdentity
                    var id = new ClaimsIdentity(claims, "ApplicationCookie", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
                    // установка аутентификационных куки
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id));
               }
            }
            catch (Exception)
            {
            }
        }        
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(new PathString("/oAuth2/Login.ashx")))
            {
                await Login(context);
            }
            else if (context.Request.Path.StartsWithSegments(new PathString("/oAuth2/LoginDialog.ashx")))
            {
                await LoginDialog(context);
            }
            else if (context.Request.Path.StartsWithSegments(new PathString("/oAuth2/oAuthCallback.htm")))
            {
                await OAuthCallbackHtml(context);
            }
            else if (context.Request.Path.StartsWithSegments(new PathString("/oAuth2/oAuthCallback.ashx")))
            {
                await OAuthCallback(context);
            }
            else if (context.Request.Path.StartsWithSegments(new PathString("/oAuth2/Logout.ashx")))
            {
                await Logout(context);
            }            
            else
            {
                await _next(context);
            }                
        }
    }    
}