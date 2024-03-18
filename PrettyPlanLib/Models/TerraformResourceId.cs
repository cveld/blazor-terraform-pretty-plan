namespace BlazorApp1.Models
{
    public class TerraformResourceId
    {
        public required string Name { get; init; }
        public required string Type { get; set; }
        public required List<string> Prefixes { get; init; }
        public required string? Index { get; init; }
        public required string Address { get; init; }
    }
}
