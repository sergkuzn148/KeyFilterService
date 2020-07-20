using System;

namespace Gmx.OAuth {
    public class Token
    {
        public int Version { get; set; }
        public string EMail { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime Expiration { get; set; }
        public bool IsPersistent { get; set; }
        public string ClientData { get; set; }
        //public string TokenString { get; private set; }
        //public DateTime Expires { get; private set; }
        public readonly string TokenString;
        public readonly DateTime Expires;

        public Token(){}

        public Token(string token, DateTime expires)
        {
            TokenString = token;
            Expires = expires;
        }    

        public static string Validate(Token token)
        {
            if (token == null) return "INVALID_TOKEN";
            if (token.Expiration < DateTime.Now) return "TOKEN_EXPIRES";
            return "";
        }    
    }
}