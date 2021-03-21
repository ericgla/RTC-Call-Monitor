using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CallMonitor
{
    public static class Webhook
    {
        private static readonly HttpClient _client = new();

        public static Task Invoke(Uri uri, object data)
        {
            var body = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

            return _client.PostAsync(uri.AbsoluteUri, body);
        }
    }
}
