using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;

namespace Controllers
{
    public static partial class HttpRequestExtensions {
        public static string GetBody(this HttpRequest request) {
            string body;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
            {
                body = reader.ReadToEndAsync().Result;
            }
            
            return body;
        }

        public static dynamic ParseContentType(this HttpRequest request)
        {
            var contentType = request.ContentType.Split(";");
            dynamic obj = new ExpandoObject();
            obj.ContentType = contentType[0].Trim();
            for (var c = 1; c < contentType.Length; c++)
            {
                var kv = contentType[c].Trim().Split("=");
                if (kv.Length == 2) (obj as IDictionary<string, object>).Add(kv[0], kv[1]);
            }

            return obj;
        }

    }    
}