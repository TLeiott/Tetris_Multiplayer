using System.Text.Json;
using Xunit;

namespace TetrisMultiplayer.Tests
{
    public class MessageSerializationTests
    {
        [Fact]
        public void Can_Serialize_And_Deserialize_NextPiece()
        {
            var msg = new { type = "NextPiece", pieceId = 3 };
            string json = JsonSerializer.Serialize(msg);
            var doc = JsonDocument.Parse(json);
            Assert.Equal("NextPiece", doc.RootElement.GetProperty("type").GetString());
            Assert.Equal(3, doc.RootElement.GetProperty("pieceId").GetInt32());
        }

        [Fact]
        public void Can_Serialize_And_Deserialize_RoundResults()
        {
            var msg = new { type = "RoundResults", newScores = new { p1 = 100, p2 = 200 } };
            string json = JsonSerializer.Serialize(msg);
            var doc = JsonDocument.Parse(json);
            Assert.Equal("RoundResults", doc.RootElement.GetProperty("type").GetString());
            Assert.Equal(100, doc.RootElement.GetProperty("newScores").GetProperty("p1").GetInt32());
        }
    }
}
