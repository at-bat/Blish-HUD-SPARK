using System;

namespace rp.spark.Services
{
    public static class SparkServiceConfig
    {
        public const string ServerURL = "https://spark.a-bat.com";
        public const string IconIndexPath = "/gw2-icons/";
        public const string IconIndexFilename = "icon_index.json.gz";
        public const string IconIndexManifest = "manifest.json";

        public const string IconIndexManifestUrl = ServerURL + IconIndexPath + IconIndexManifest;
        public const string DefaultIconIndexUrl = ServerURL + IconIndexPath + IconIndexFilename;

        public static readonly Uri ServerUri = new Uri(ServerURL);

        public static string ServerHost => ServerUri.Host;
    }
}
