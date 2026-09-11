namespace API.Infrastructure.Entities;

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Password { get; set; }
    public List<Address> Addresses { get; set; } = [];
}
