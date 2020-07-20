using Microsoft.AspNetCore.Builder;
using Gmx.OAuth;
using Microsoft.Extensions.Options;

namespace Server {

    public static class GmxOAuthExtensions
    {
        public static IApplicationBuilder UseGmxOAuth(this IApplicationBuilder builder, GmxOAuthOptions options)
        {            
            return builder.UseMiddleware<GmxOAuth>(new OptionsWrapper<GmxOAuthOptions>(options));
        }
    }

}