namespace Financial.Api.Models;

public class UserBalance
{
    public Guid Id { get; set; }
    public Guid UserIdentifier { get; set; }
    public int InvoiceCount { get; set; }
    public decimal Balance { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
