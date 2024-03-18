using System.Text.Json.Nodes;

namespace BlazorApp1.Models
{
    public class TerraformChange
    {
        public required string Property { get; init; }
        public JsonNode? Old { get; set; }
        public JsonNode? New { get; set; }
        public bool OldHasValue { get; set; } = false;
        public bool NewHasValue { get; set; } = false;
        public bool NewComputed { get; set; } = false;
        public bool CausesReplacement { get; set; } = false;
        public string? NewSerialized { get; set; }
    }
}
