namespace EMSModelLibrary.Models
{
    public class Venue
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int TotalCapacity { get; set; }
        public string LayoutConfig { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public Venue()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public Venue(int id, string name, string address, string city, int totalCapacity, string layoutConfig)
        {
            Id = id;
            Name = name;
            Address = address;
            City = city;
            TotalCapacity = totalCapacity;
            LayoutConfig = layoutConfig;
            CreatedAt = DateTime.UtcNow;
        }
    }
}