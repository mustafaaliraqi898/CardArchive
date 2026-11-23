namespace CardArchive;
public sealed class Person
{
    public long Id { get; set; }
    public string FullName { get; set; } = "";
    public string CardNumber { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}