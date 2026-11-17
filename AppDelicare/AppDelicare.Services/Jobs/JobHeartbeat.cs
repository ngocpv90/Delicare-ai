namespace AppDelicare.Services.Jobs
{
    /// <summary>
    /// T48.4 (D-60): file nhịp của AppDelicare.Job – <see cref="AppDelicare.Services.Jobs"/> vòng hẹn giờ ghi giờ UTC mỗi lần quét; Docker HEALTHCHECK
    /// (<c>dotnet AppDelicare.Job.dll --healthcheck</c>) coi job "khỏe" khi file được ghi trong <c>JobHost:HeartbeatMaxAgeSeconds</c> giây gần nhất.
    /// </summary>
    public static class JobHeartbeat
    {
        public const string FileKey = "JobHost:HeartbeatFile";
        public const string MaxAgeKey = "JobHost:HeartbeatMaxAgeSeconds";
        public const int DefaultMaxAgeSeconds = 120;

        /// <summary>Mặc định: thư mục tạm của hệ điều hành (Linux container: /tmp/delicare-job-heartbeat).</summary>
        public static string DefaultPath => Path.Combine(Path.GetTempPath(), "delicare-job-heartbeat");

        public static void Write(string path, DateTime utcNow)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string temp = path + ".tmp";
            File.WriteAllText(temp, utcNow.ToString("O"));
            File.Move(temp, path, overwrite: true);
        }

        /// <summary>true khi file tồn tại, đọc được giờ và không cũ hơn <paramref name="maxAge"/>.</summary>
        public static bool IsFresh(string path, TimeSpan maxAge, DateTime utcNow)
        {
            try
            {
                if (!File.Exists(path)
                    || !DateTime.TryParse(File.ReadAllText(path).Trim(), System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out DateTime beat))
                {
                    return false;
                }
                TimeSpan age = utcNow - beat.ToUniversalTime();
                return age <= maxAge && age >= TimeSpan.FromMinutes(-5);
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
