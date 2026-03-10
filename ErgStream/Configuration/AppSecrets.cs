namespace ErgStream.Configuration
{
    internal static class AppSecrets
    {
        private const string placeholder = "SYNCFUSION" + "_WILL_BE_REPLACED_BY_BUILD";
        private const string syncfusionLicenseKey = "SYNCFUSION_WILL_BE_REPLACED_BY_BUILD";

        internal static string? SyncfusionLicenseKey => syncfusionLicenseKey != placeholder ? syncfusionLicenseKey : null;
    }
}