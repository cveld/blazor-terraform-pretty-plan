using System.Text.Json.Nodes;

namespace BlazorApp1.Models
{
    public class MergeJsonNode
    {
        public JsonNode? Left { get; set; }
        public JsonNode? Right { get; set; }
        public bool LeftHasKey { get; set; }
    }
}
