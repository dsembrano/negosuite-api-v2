namespace negosuite_api.Models
{
    public partial class AppVersion
    {
        public AppVersion()
        {
        }

        public int Id { get; set; }
        public string VersionCode { get; set; }
        public string AndroidUpdateUrl { get; set; }
        public string APKFilename { get; set; }
    }
}