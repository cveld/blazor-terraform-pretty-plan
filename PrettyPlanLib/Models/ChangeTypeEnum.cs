namespace BlazorApp1.Models
{
    public enum ChangeType
    {
        Unknown,
        NoOperation,
        Read,
        Update,
        Create,
        Destroy,
        Recreate
    }
}
