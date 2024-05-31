#nullable enable

namespace IRWA.Resources
{
    using BR = WebResources.BinaryResources;

    internal static class WebResourceProvider
    {
        public const string DefaultFileName = "index.html";

        public static bool TryResolveResource(string localPath, out BR resource)
        {
            resource = default;
            localPath = localPath.ToLower().TrimStart('/').TrimStart();

            switch (localPath)
            {
                case "" or DefaultFileName:
                    resource = BR.index_html;
                    break;
                case "manifest.webmanifest":
                    resource = BR.manifest_webmanifest;
                    break;
                case "sw.js":
                    resource = BR.sw_js;
                    break;
                case "logo.svg":
                    resource = BR.logo_svg;
                    break;
                case "appicon.svg":
                    resource = BR.appicon_svg;
                    break;
                default:
                    return false;
            }

            return true;
        }
    }
}
