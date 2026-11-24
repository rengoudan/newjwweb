using Jw.Domain.Auditing;

namespace Jw.Domain.Customers;

/// <summary>顧客。对应旧版 JwCustomerData。</summary>
public class Customer : AuditedEntity, IAuditable
{
    public string CompanyName { get; set; } = "";
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public string? Telephone { get; set; }
    public string? Note { get; set; }

    public string AuditDisplay => CompanyName;
}
