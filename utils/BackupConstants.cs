namespace MsixTool.Utils
{
    public static class BackupConstants
    {
        // 统一的备份时间戳格式 (精确到分钟)
        public const string BackupTimestampFormat = "yyyyMMddHHmm";

        public static string GenerateTimestamp() => DateTime.Now.ToString(BackupTimestampFormat);
    }
}
