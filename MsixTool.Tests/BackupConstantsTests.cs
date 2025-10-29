using MsixTool.Utils;
using Xunit;

namespace MsixTool.Tests
{
    public class BackupConstantsTests
    {
        [Fact]
        public void GenerateTimestamp_ReturnsCorrectLength()
        {
            var ts = BackupConstants.GenerateTimestamp();
            Assert.Equal(12, ts.Length); // yyyyMMddHHmm
        }

        [Fact]
        public void GenerateTimestamp_FormatIsDigits()
        {
            var ts = BackupConstants.GenerateTimestamp();
            Assert.Matches("^[0-9]{12}$", ts);
        }
    }
}
