namespace BlazorApp1.Models
{
    public class TerraformAction
    {
        public required string Address { get; init; }
        public List<TerraformChange> Changes { get; init; } = new List<TerraformChange>();
        public ChangeType ChangeType { get; set; }
        public required string ActionReason {  get; init; }
        public required Dictionary<string, bool> ReplacePaths { get; init; }
        public bool IsOpen { get; set; }
        public required TerraformResourceId TerraformResourceId { get; init; }
    }
}
