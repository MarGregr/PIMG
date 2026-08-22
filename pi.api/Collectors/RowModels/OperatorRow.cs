namespace CollectData.Collectors.RowModels;

public record OperatorRow
{
    public long Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string ShortName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Website { get; set; }
    public int Type { get; set; }
    public string Country { get; set; }
}
