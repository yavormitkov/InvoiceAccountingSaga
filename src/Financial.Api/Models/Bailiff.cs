using System;

namespace Financial.Api.Models;

public class Bailiff
{
    public Guid Id { get; set; }
    public string LicenseNumber { get; set; } = null!;
    public string Name { get; set; } = null!;
}
