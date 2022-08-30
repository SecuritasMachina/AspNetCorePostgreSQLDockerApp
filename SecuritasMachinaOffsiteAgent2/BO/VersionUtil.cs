namespace SecuritasMachinaOffsiteAgent.BO
{
    public class VersionUtil
    {
        public static string getAppVersion()
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            DateTime buildDate = new DateTime(2000, 1, 1)
                                    .AddDays(version.Build).AddSeconds(version.Revision * 2);
            string displayableVersion = $"{version} ({buildDate.ToString("yyyy-MM-dd HH:mm:ss")})";
            return displayableVersion;
        }
    }
}
