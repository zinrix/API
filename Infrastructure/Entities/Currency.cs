namespace API.Infrastructure.Entities;

public class Currency
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public decimal RateToBase { get; set; }
}
