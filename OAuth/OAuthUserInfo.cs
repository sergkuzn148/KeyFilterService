using System;

namespace Gmx.OAuth {
    public enum OAuthServers
    {
        MyKosmosnimki = 1,
        Google,
        Facebook 
    }    
    /// <summary>
    /// Интерфейс данных о пользователе
    /// </summary>
    public class OAuthUserInfo
    {
        public string ID {get; set; }
        public string Email { get; set; }
        public string Login { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Organization { get; set; }
        public string Position { get; set; }
        public string Role { get; set; }
        public OAuthServers Server { get; set; }
        public string ClientId { get; set; }
        public DateTime? TokenExpires { get; set; }
        public string Scope { get; set; }

        public OAuthUserInfo() {}

        public OAuthUserInfo(string id, string email, string login, OAuthServers server, string fullname, string phone, string organization)
        {
            ID = id;
            Email = email;
            Login = login;
            Server = server;
            FullName = fullname;
            Phone = phone;
            Organization = organization;
        }
    }
}