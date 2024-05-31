using IRWA.Resources;
using nanoFramework.Runtime.Native;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

#nullable enable

namespace IRWA
{
    public class AppServer : HttpServiceBase
    {       
        private const string ApiRequestPathPrefix = "api/";
        private const string ApiRequestSendRawSuffix = "sendraw";

        public const string ContentTypeText = "text/plain";
        public const string ContentTypeHtml = "text/html";
        public const string ContentTypeJson = "application/json";

        private readonly IrSenderNec irSender;

        public AppServer(int port, IrSenderNec irSender) : base(port)
        {
            this.irSender = irSender;
        }

        public AppServer(int port, X509Certificate httpsCertificate, IrSenderNec irSender) 
            : base(port, httpsCertificate)
        {
            this.irSender = irSender;
        }

        protected override void HandleClient(HttpListenerContext context)
        {
            var path = context.Request.RawUrl.ToLower().TrimStart('/').TrimStart() ?? "";
            if (path.Length == 0)
            {
                path = WebResourceProvider.DefaultFileName;
            }

            if (path.StartsWith(ApiRequestPathPrefix))
            {
                if (path == (ApiRequestPathPrefix + ApiRequestSendRawSuffix))
                {
                    HandleRequestAsApiSendRaw(context);
                }
                else
                {
                    SendErrorResponse(context.Response, 404, "API endpoint not found.");
                }
            }
            else if (!TrySendResourceResponse(context.Response, path))
            {
                SendErrorResponse(context.Response, 404, "File not found.");
            }
        }

        public bool TrySendResourceResponse(HttpListenerResponse response, 
            string path)
        {
            if (WebResourceProvider.TryResolveResource(path, out var resource))
            {
                response.StatusCode = 200;
                response.Headers.Add("Cache-Control", "max-age=86400");
                response.ContentType = GetContentType(path);
                CopyResourceToStream(resource, response.OutputStream);
                response.Close();
                return true;
            }
            else
            {
                return false;
            }            
        }

        private void HandleRequestAsApiSendRaw(HttpListenerContext context)
        {
            if (context.Request.HttpMethod != "POST")
            {
                SendErrorResponse(context.Response, 405, "Only POST mode allowed.");
                return;
            }

            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            byte[] requestContent = new byte[Math.Min((int)context.Request.ContentLength64, 1024)];
            context.Request.InputStream.Read(requestContent, 0, requestContent.Length);
            string requestContentString = Encoding.UTF8.GetString(requestContent, 0, requestContent.Length);
            uint code;
            if (requestContentString.Contains("x"))
            {
                code = Convert.ToUInt32(requestContentString, 16);
            }
            else if (!uint.TryParse(requestContentString, out code))
            {
                code = default;
            }

            irSender.SendRaw(code, 2);

            SendEmptyResponse(context.Response);
        }

        public static void SendEmptyResponse(HttpListenerResponse targetResponse,
            int statusCode = 204)
        {
            targetResponse.StatusCode = statusCode;
            targetResponse.Close();
        }

        public static void SendErrorResponse(HttpListenerResponse targetResponse, int statusCode,
            string description)
        {
            SendStringResponse(targetResponse, statusCode,
                BuildErrorPage("Error", "&#xFF1E;&#xFE4F;&#xFF1C;", description));
        }

        public static void SendStringResponse(HttpListenerResponse targetResponse, int statusCode,
            string content, string contentType = ContentTypeHtml, bool closeAfterwards = true)
        {
            targetResponse.ContentType = contentType;
            targetResponse.StatusCode = statusCode;
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            targetResponse.ContentLength64 = contentBytes.Length;
            targetResponse.OutputStream.Write(contentBytes, 0, contentBytes.Length);

            if (closeAfterwards)
            {
                targetResponse.Close();
            }
        }

        public static string GetContentType(string uri)
        {
            int fileExtensionSeparatorIndex = uri.LastIndexOf('.') + 1;
            if (fileExtensionSeparatorIndex > 0)
            {
                string fileExtension = uri.Substring(fileExtensionSeparatorIndex).ToLower();
                return fileExtension switch
                {
                    "txt" => "text/plain",
                    "css" => "text/css",
                    "js" => "text/javascript",
                    "htm" => ContentTypeHtml,
                    "html" => ContentTypeHtml,
                    "ico" => "image/vnd.microsoft.icon",
                    "svg" => "image/svg+xml",
                    "png" => "image/png",
                    _ => "application/octet-stream",
                };
            }
            else return "application/octet-stream";
        }

        public static string BuildErrorPage(string title, string heading, string subtext)
        {
            return $"<!DOCTYPE html><html><head><title>{title}</title><meta name='viewport' " +
                "content='width=device-width,initial-scale=1,maximum-scale=1," +
                "user-scalable=no'></head><body style=\"background-color:#11191F; " +
                "color: #EDF0F3; margin: 0; height: 100vh; display: flex; flex-direction: " +
                "column; justify-content: center; text-align: center; font-family: system-ui," +
                "-apple-system,'Segoe UI','Roboto','Ubuntu','Cantarell','Noto Sans'," +
                $"sans-serif\"><h1 style='font-size: 5em'>{heading}</h1>" +
                $"<sub>{subtext}</sub></body></html>";
        }

        internal static void CopyResourceToStream(WebResources.BinaryResources resourceName,
            Stream targetStream)
        {
            byte[] buffer;
            int offset = 0;
            const int count = 1024;
            try
            {
                do
                {
                    buffer = (byte[])ResourceUtility.GetObject(
                        WebResources.ResourceManager, resourceName, offset, count);
                    targetStream.Write(buffer, 0, buffer.Length);
                    offset += buffer.Length;
                } while (buffer.Length == count);
            }
            catch (Exception)
            {
                // If the size of the requested resource is divisible by the specified count,
                // an exception will be thrown after getting the last chunk (as no way to retrieve
                // the resource file size was found while writing this).
                // So, only if the offset is 0 (no data has been transferred), treat an exception
                // as an error.
                if (offset == 0) throw;
            }
        }
    }
}
