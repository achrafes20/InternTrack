using StageFlow.Domain.Common;

namespace StageFlow.Domain.Entities;

public class Company : BaseAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Industry { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Country { get; private set; } = "Maroc";
    public string Address { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string? ContactPhone { get; private set; }
    public string? Website { get; private set; }

    // Relations
    public ICollection<InternshipOffer> Offers { get; private set; } = new List<InternshipOffer>();
    public ICollection<Internship> Internships { get; private set; } = new List<Internship>();

    private Company() { }

    public Company(string name, string industry, string city, string address, string contactEmail, string? contactPhone = null, string? website = null, string country = "Maroc")
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Industry = industry ?? string.Empty;
        City = city ?? throw new ArgumentNullException(nameof(city));
        Address = address ?? string.Empty;
        ContactEmail = contactEmail ?? throw new ArgumentNullException(nameof(contactEmail));
        ContactPhone = contactPhone;
        Website = website;
        Country = country;
    }

    public void Update(string name, string industry, string city, string address, string contactEmail, string? contactPhone, string? website)
    {
        Name = name;
        Industry = industry;
        City = city;
        Address = address;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Website = website;
    }
}

